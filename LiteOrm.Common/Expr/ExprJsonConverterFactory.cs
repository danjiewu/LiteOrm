using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{

    /// <summary>
    /// 表达式 JSON 转换器工厂
    /// </summary>
    internal class ExprJsonConverterFactory : JsonConverterFactory
    {
        // 检查类型是否为 Expr 或其子类
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(Expr).IsAssignableFrom(typeToConvert);
        }

        // 创建针对特定 Expr 子类的泛型转换器
        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            return (JsonConverter)Activator.CreateInstance(
                typeof(ExprJsonConverter<>).MakeGenericType(typeToConvert));
        }

        /// <summary>
        /// 针对 Expr 及其子类的自定义 JSON 序列化器。
        /// 采用紧凑型设计，支持字面量直接序列化（ValueExpr）和带类型标识符（$）的复杂转换。
        /// </summary>
        private class ExprJsonConverter<T> : JsonConverter<T> where T : Expr
        {
            // 使用非转义编码器，避免 HTML 字符被转义，提高 SQL 表达式的可读性
            private static readonly JsonSerializerOptions _compactOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            // 维护 C# 操作符与 JSON 短标识的映射
            private static readonly Dictionary<LogicBinaryOperator, string> _logicOperatorToJson = new()
            {
                { LogicBinaryOperator.Equal, "==" },
                { LogicBinaryOperator.NotEqual, "!=" },
                { LogicBinaryOperator.GreaterThan, ">" },
                { LogicBinaryOperator.GreaterThanOrEqual, ">=" },
                { LogicBinaryOperator.LessThan, "<" },
                { LogicBinaryOperator.LessThanOrEqual, "<=" },
                { LogicBinaryOperator.In, "in" },
                { LogicBinaryOperator.NotIn, "notin" },
                { LogicBinaryOperator.Like, "like" },
                { LogicBinaryOperator.NotLike, "notlike" },
                { LogicBinaryOperator.Contains, "contains" },
                { LogicBinaryOperator.NotContains, "notcontains" },
                { LogicBinaryOperator.StartsWith, "startswith" },
                { LogicBinaryOperator.NotStartsWith, "notstartswith" },
                { LogicBinaryOperator.EndsWith, "endswith" },
                { LogicBinaryOperator.NotEndsWith, "notendswith" },
                { LogicBinaryOperator.RegexpLike, "regexp" },
                { LogicBinaryOperator.NotRegexpLike, "notregexp" }
            };

            private static readonly Dictionary<ValueBinaryOperator, string> _valueOperatorToJson = new()
            {
                { ValueBinaryOperator.Add, "+" },
                { ValueBinaryOperator.Subtract, "-" },
                { ValueBinaryOperator.Multiply, "*" },
                { ValueBinaryOperator.Divide, "/" },
                { ValueBinaryOperator.Concat, "||" }
            };

            private static readonly Dictionary<string, LogicBinaryOperator> _jsonToLogicOperator = new(StringComparer.OrdinalIgnoreCase);
            private static readonly Dictionary<string, ValueBinaryOperator> _jsonToValueOperator = new(StringComparer.OrdinalIgnoreCase);

            static ExprJsonConverter()
            {
                foreach (var kvp in _logicOperatorToJson)
                {
                    _jsonToLogicOperator[kvp.Value] = kvp.Key;
                }
                foreach (var kvp in _valueOperatorToJson)
                {
                    _jsonToValueOperator[kvp.Value] = kvp.Key;
                }
                // 兼容性符号
                _jsonToLogicOperator["="] = LogicBinaryOperator.Equal;
                _jsonToLogicOperator["<>"] = LogicBinaryOperator.NotEqual;
            }

            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null) return null;

                Expr result;
                // 若 JSON 为字面量（字符串、数字、布尔），直接映射为 ValueExpr
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    result = new ValueExpr(ReadNative(ref reader, options));
                }
                else
                {
                    // 探查对象属性以确定 Expr 类型
                    Utf8JsonReader tempReader = reader;
                    tempReader.Read();
                    if (tempReader.TokenType != JsonTokenType.PropertyName)
                    {
                        result = new ValueExpr(ReadNative(ref reader, options));
                    }
                    // 快捷方式：{"@": "PropName"} 映射为非 Const 类型 ValueExpr
                    else if (tempReader.ValueTextEquals("@"))
                    {
                        reader.Read(); // 跳过 :
                        reader.Read(); // 读取属性值
                        result = new ValueExpr(ReadNative(ref reader, options)) { IsConst = false };
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject) ;
                        ;
                    }
                    // 快捷方式：{"#": "PropName"} 显示为 PropertyExpr
                    else if (tempReader.ValueTextEquals("#"))
                    {
                        reader.Read(); // 跳过 #
                        reader.Read(); // 读取属性值
                        string propName = reader.GetString();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject) ;
                        result = new PropertyExpr(propName);
                    }
                    // 快捷方式：{"!": { ... }} 显示为 NotExpr
                    else if (tempReader.ValueTextEquals("!"))
                    {
                        reader.Read(); // 跳过 !
                        reader.Read();
                        var operand = JsonSerializer.Deserialize<LogicExpr>(ref reader, options);
                        result = new NotExpr(operand);
                    }
                    // 标准方式：{"$": "typeMark", ...}
                    else if (tempReader.ValueTextEquals("$"))
                    {
                        reader.Read(); // 跳过 $
                        reader.Read(); // 读取 typeMark
                        string mark = reader.GetString().ToLower();

                        // 优先按操作符短标识识别（如 ==, in, contains）
                        if (mark == "not")
                        {
                            result = ReadNot(ref reader, options);
                        }
                        else if (_jsonToLogicOperator.TryGetValue(mark, out var lbopSymbol))
                        {
                            result = ReadLogicBinary(ref reader, options, lbopSymbol);
                        }
                        else if (_jsonToValueOperator.TryGetValue(mark, out var vbopSymbol))
                        {
                            result = ReadValueBinary(ref reader, options, vbopSymbol);
                        }
                        // 其次按枚举名识别
                        else if (Enum.TryParse<LogicBinaryOperator>(mark, true, out var lbop))
                        {
                            result = ReadLogicBinary(ref reader, options, lbop);
                        }
                        else if (Enum.TryParse<ValueBinaryOperator>(mark, true, out var vbop))
                        {
                            result = ReadValueBinary(ref reader, options, vbop);
                        }
                        else
                        {
                            // 最后按特殊类型标识识别
                            result = mark switch
                            {
                                "bin" => ReadValueBinary(ref reader, options, null),
                                "logic" => ReadLogicBinary(ref reader, options, null),
                                "set" => ReadLogicSet(ref reader, options),
                                "vset" => ReadValueSet(ref reader, options),
                                "func" => ReadFunction(ref reader, options),
                                "prop" => ReadProperty(ref reader, options),
                                "not" => ReadNot(ref reader, options),
                                "unary" => ReadValueUnary(ref reader, options),
                                "sql" => ReadSql(ref reader, options),
                                "value" => ReadValueBody(ref reader, options),
                                "const" => ReadValueBody(ref reader, options, true),
                                "for" => ReadForeign(ref reader, options),
                                _ => new ValueExpr(ReadNative(ref reader, options)) { IsConst = true }
                            };
                        }
                    }
                    else
                    {
                        // 兜底：作为普通值对象
                        result = new ValueExpr(ReadNative(ref reader, options)) { IsConst = true };
                    }
                }

                return (T)result;
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                WriteExpr(writer, value, options);
            }

            private void WriteExpr(Utf8JsonWriter writer, Expr value, JsonSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNullValue();
                    return;
                }

                // 优化序列化格式：基本值类型直接写入
                if (value is ValueExpr ve)
                {
                    if (ve.IsConst)
                    {
                        // 常量值直接序列化
                        writer.WriteRawValue(JsonSerializer.Serialize(ve.Value, _compactOptions));
                    }
                    else
                    {
                        // 变量值使用快捷方式 {"@": value}
                        writer.WriteRawValue(JsonSerializer.Serialize(new Dictionary<string, object> { { "@", ve.Value } }, _compactOptions));
                    }
                    return;
                }

                // 优化序列化格式：PropertyExpr 使用简写的 # 标识
                if (value is PropertyExpr pe)
                {
                    writer.WriteRawValue(JsonSerializer.Serialize(new Dictionary<string, string> { { "#", pe.PropertyName } }, _compactOptions));
                    return;
                }

                // 优化序列化格式：NotExpr 使用简写的 ! 标识
                if (value is NotExpr ne)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("!");
                    JsonSerializer.Serialize(writer, ne.Operand, options);
                    writer.WriteStartObject();
                    return;
                }

                if (value is LambdaExpr lambda)
                {
                    WriteExpr(writer, lambda.InnerExpr, options);
                    return;
                }

                // 复杂表达式使用 $标识符
                writer.WriteStartObject();
                string mark = value switch
                {
                    LogicBinaryExpr be => _logicOperatorToJson.TryGetValue(be.Operator, out var symbol) ? symbol : "logic",
                    ValueBinaryExpr be => _valueOperatorToJson.TryGetValue(be.Operator, out var symbol) ? symbol : "bin",
                    LogicExprSet => "set",
                    ValueExprSet => "vset",
                    FunctionExpr => "func",
                    NotExpr => "not",
                    ValueUnaryExpr => "unary",
                    GenericSqlExpr => "sql",
                    ValueExpr vve => vve.IsConst ? "const" : "value",
                    ForeignExpr => "for",
                    _ => value.GetType().Name.Replace("Expr", "").ToLower()
                };
                writer.WritePropertyName("$");
                WriteStringUnescaped(writer, mark);

                switch (value)
                {
                    case LogicBinaryExpr be:
                        writer.WritePropertyName("Left");
                        JsonSerializer.Serialize(writer, be.Left, options);
                        writer.WritePropertyName("Right");
                        JsonSerializer.Serialize(writer, be.Right, options);
                        break;
                    case ValueBinaryExpr be:
                        writer.WritePropertyName("Left");
                        JsonSerializer.Serialize(writer, be.Left, options);
                        writer.WritePropertyName("Right");
                        JsonSerializer.Serialize(writer, be.Right, options);
                        break;
                    case LogicExprSet set:
                        writer.WritePropertyName(set.JoinType.ToString());
                        JsonSerializer.Serialize(writer, set.Items, options);
                        break;
                    case ValueExprSet set:
                        writer.WritePropertyName(set.JoinType.ToString());
                        JsonSerializer.Serialize(writer, set.Items, options);
                        break;
                    case FunctionExpr fe:
                        writer.WritePropertyName(fe.FunctionName);
                        JsonSerializer.Serialize(writer, fe.Parameters, options);
                        break;
                    case NotExpr ue:
                        writer.WritePropertyName("Operand");
                        JsonSerializer.Serialize(writer, ue.Operand, options);
                        break;
                    case ValueUnaryExpr ue:
                        writer.WritePropertyName(ue.Operator.ToString());
                        JsonSerializer.Serialize(writer, ue.Operand, options);
                        break;
                    case GenericSqlExpr ge:
                        writer.WriteString("Key", ge.Key);
                        writer.WritePropertyName("Arg");
                        JsonSerializer.Serialize(writer, ge.Arg, options);
                        break;
                    case ValueExpr vve:
                        writer.WritePropertyName("Value");
                        JsonSerializer.Serialize(writer, vve.Value, options);
                        break;
                    case ForeignExpr fe:
                        writer.WritePropertyName(fe.Foreign);
                        JsonSerializer.Serialize(writer, fe.InnerExpr, options);
                        break;
                }
                writer.WriteEndObject();
            }

            /// <summary>
            /// 手不转义 HTML 敏感字符的 JSON 字符串值
            /// </summary>
            private void WriteStringUnescaped(Utf8JsonWriter writer, string value)
            {
                // 使用 UnsafeRelaxedJsonEscaping 编码并包裹引号后直接写入原文
                string encoded = JavaScriptEncoder.UnsafeRelaxedJsonEscaping.Encode(value);
                writer.WriteRawValue($"\"{encoded}\"");
            }

            private object ReadNative(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.String: return reader.GetString();
                    case JsonTokenType.Number:
                        if (reader.TryGetInt32(out int i)) return i;
                        else if (reader.TryGetInt64(out long l)) return l;
                        else if (reader.TryGetDecimal(out decimal d)) return d;
                        else return reader.GetDouble();
                    case JsonTokenType.True: return true;
                    case JsonTokenType.False: return false;
                    case JsonTokenType.Null: return null;
                    case JsonTokenType.StartObject:
                        var dict = new Dictionary<string, object>();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                        {
                            string prop = reader.GetString();
                            reader.Read();
                            dict[prop] = ReadNative(ref reader, options);
                        }
                        return dict;
                    case JsonTokenType.StartArray:
                        var list = new List<object>();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            list.Add(ReadNative(ref reader, options));
                        }
                        return list;
                    default:
                        return JsonSerializer.Deserialize<object>(ref reader, options);
                }
            }

            private LogicBinaryExpr ReadLogicBinary(ref Utf8JsonReader reader, JsonSerializerOptions options, LogicBinaryOperator? op = null)
            {
                LogicBinaryOperator finalOp = op ?? LogicBinaryOperator.Equal;
                ValueTypeExpr left = null;
                ValueTypeExpr right = null;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    if (reader.ValueTextEquals("Left"))
                    {
                        reader.Read();
                        left = JsonSerializer.Deserialize<ValueTypeExpr>(ref reader, options);
                    }
                    else if (reader.ValueTextEquals("Operator"))
                    {
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            string opStr = reader.GetString();
                            if (Enum.TryParse<LogicBinaryOperator>(opStr, true, out var lbop)) finalOp = lbop;
                        }
                        else
                        {
                            finalOp = (LogicBinaryOperator)reader.GetInt32();
                        }
                    }
                    else if (reader.ValueTextEquals("Right"))
                    {
                        reader.Read();
                        right = JsonSerializer.Deserialize<ValueTypeExpr>(ref reader, options);
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }

                return new LogicBinaryExpr(left, finalOp, right);
            }

            private ValueBinaryExpr ReadValueBinary(ref Utf8JsonReader reader, JsonSerializerOptions options, ValueBinaryOperator? op = null)
            {
                ValueBinaryOperator finalOp = op ?? ValueBinaryOperator.Add;
                ValueTypeExpr left = null;
                ValueTypeExpr right = null;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    if (reader.ValueTextEquals("Left"))
                    {
                        reader.Read();
                        left = JsonSerializer.Deserialize<ValueTypeExpr>(ref reader, options);
                    }
                    else if (reader.ValueTextEquals("Operator"))
                    {
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            string opStr = reader.GetString();
                            if (Enum.TryParse<ValueBinaryOperator>(opStr, true, out var vbop)) finalOp = vbop;
                        }
                        else
                        {
                            finalOp = (ValueBinaryOperator)reader.GetInt32();
                        }
                    }
                    else if (reader.ValueTextEquals("Right"))
                    {
                        reader.Read();
                        right = JsonSerializer.Deserialize<ValueTypeExpr>(ref reader, options);
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }

                return new ValueBinaryExpr(left, finalOp, right);
            }

            private LogicExprSet ReadLogicSet(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                LogicJoinType joinType = LogicJoinType.And;
                List<LogicExpr> items = null;

                bool success = false;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    string prop = reader.GetString();
                    if (prop == "JoinType")
                    {
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            string jtStr = reader.GetString();
                            if (Enum.TryParse<LogicJoinType>(jtStr, true, out var ljt)) joinType = ljt;
                        }
                        else
                        {
                            joinType = (LogicJoinType)reader.GetInt32();
                        }
                        success = true;
                    }
                    else if (prop == "Items")
                    {
                        reader.Read();
                        items = JsonSerializer.Deserialize<List<LogicExpr>>(ref reader, options);
                    }
                    else
                    {
                        if (!success && Enum.TryParse<LogicJoinType>(prop, out joinType))
                        {
                            success = true;
                            reader.Read();
                            items = JsonSerializer.Deserialize<List<LogicExpr>>(ref reader, options);
                        }
                        else
                        {
                            reader.Read();
                            reader.Skip();
                        }
                    }
                }

                return new LogicExprSet(joinType, items);
            }

            private ValueExprSet ReadValueSet(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                ValueJoinType joinType = ValueJoinType.List;
                List<ValueTypeExpr> items = null;

                bool success = false;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    string prop = reader.GetString();
                    if (prop == "JoinType")
                    {
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            string jtStr = reader.GetString();
                            if (Enum.TryParse<ValueJoinType>(jtStr, true, out var vjt)) joinType = vjt;
                        }
                        else
                        {
                            joinType = (ValueJoinType)reader.GetInt32();
                        }
                        success = true;
                    }
                    else if (prop == "Items")
                    {
                        reader.Read();
                        items = JsonSerializer.Deserialize<List<ValueTypeExpr>>(ref reader, options);
                    }
                    else
                    {
                        if (!success && Enum.TryParse<ValueJoinType>(prop, out var vjt))
                        {
                            success = true;
                            joinType = vjt;
                            reader.Read();
                            items = JsonSerializer.Deserialize<List<ValueTypeExpr>>(ref reader, options);
                        }
                        else
                        {
                            reader.Read();
                            reader.Skip();
                        }
                    }
                }

                return new ValueExprSet(joinType, items);
            }

            private FunctionExpr ReadFunction(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                var fe = new FunctionExpr();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string prop = reader.GetString();
                    if (prop == "FunctionName")
                    {
                        reader.Read();
                        fe.FunctionName = reader.GetString();
                        continue;
                    }
                    else if (prop == "Parameters")
                    {
                        reader.Read();
                        var parameters = JsonSerializer.Deserialize<List<ValueTypeExpr>>(ref reader, options);
                        if (parameters != null) fe.Parameters.AddRange(parameters);
                    }
                    else
                    {
                        if (fe.FunctionName == null)
                        {
                            fe.FunctionName = prop;
                            reader.Read();
                            var parameters = JsonSerializer.Deserialize<List<ValueTypeExpr>>(ref reader, options);
                            if (parameters != null) fe.Parameters.AddRange(parameters);
                        }
                        else
                        {
                            reader.Read();
                            reader.Skip();
                        }
                    }
                }
                return fe;
            }

            private PropertyExpr ReadProperty(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                string name = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    if (reader.ValueTextEquals("PropertyName"))
                    {
                        reader.Read();
                        name = reader.GetString();
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }
                return new PropertyExpr(name);
            }

            private ForeignExpr ReadForeign(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                ForeignExpr foreignExpr = new ForeignExpr();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string prop = reader.GetString();
                    if (prop == "Foreign")
                    {
                        reader.Read();
                        foreignExpr.Foreign = reader.GetString();
                    }
                    else if (prop == "InnerExpr")
                    {
                        reader.Read();
                        foreignExpr.InnerExpr = JsonSerializer.Deserialize<LogicExpr>(ref reader, options);
                    }
                    else
                    {
                        if (foreignExpr.Foreign == null)
                        {
                            foreignExpr.Foreign = prop;
                            reader.Read();
                            foreignExpr.InnerExpr = JsonSerializer.Deserialize<LogicExpr>(ref reader, options);
                        }
                        else
                        {
                            reader.Read();
                            reader.Skip();
                        }
                    }
                }
                return foreignExpr;
            }

            private NotExpr ReadNot(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                LogicExpr operand = null;
                bool success = false;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string prop = reader.GetString();
                    if (prop == "Operand")
                    {
                        reader.Read();
                        operand = JsonSerializer.Deserialize<LogicExpr>(ref reader, options);
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }
                return new NotExpr(operand);
            }

            private ValueUnaryExpr ReadValueUnary(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                ValueUnaryOperator op = ValueUnaryOperator.Nagive;
                ValueTypeExpr operand = null;
                bool success = false;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string prop = reader.GetString();
                    if (prop == "Operator")
                    {
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            string opStr = reader.GetString();
                            if (Enum.TryParse<ValueUnaryOperator>(opStr, true, out var vop)) op = vop;
                        }
                        else
                        {
                            op = (ValueUnaryOperator)reader.GetInt32();
                        }
                        success = true;
                    }
                    else if (prop == "Operand")
                    {
                        reader.Read();
                        operand = JsonSerializer.Deserialize<ValueTypeExpr>(ref reader, options);
                    }
                    else
                    {
                        if (!success && Enum.TryParse<ValueUnaryOperator>(prop, out var parsedOp))
                        {
                            success = true;
                            op = parsedOp;
                            reader.Read();
                            operand = JsonSerializer.Deserialize<ValueTypeExpr>(ref reader, options);
                        }
                        else
                        {
                            reader.Read();
                            reader.Skip();
                        }
                    }
                }
                return new ValueUnaryExpr(op, operand);
            }

            private GenericSqlExpr ReadSql(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                var ge = new GenericSqlExpr();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    if (reader.ValueTextEquals("Key"))
                    {
                        reader.Read();
                        ge.Key = reader.GetString();
                    }
                    else if (reader.ValueTextEquals("Arg"))
                    {
                        reader.Read();
                        ge.Arg = ReadNative(ref reader, options);
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }
                return ge;
            }

            private ValueExpr ReadValueBody(ref Utf8JsonReader reader, JsonSerializerOptions options, bool isConst = false)
            {
                object val = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    if (reader.ValueTextEquals("Value"))
                    {
                        reader.Read();
                        val = ReadNative(ref reader, options);
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }
                return new ValueExpr(val) { IsConst = isConst };
            }
        }
    }
}
