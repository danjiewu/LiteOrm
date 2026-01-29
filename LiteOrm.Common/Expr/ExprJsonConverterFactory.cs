using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

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
            private static readonly Dictionary<BinaryOperator, string> _operatorToJson = new Dictionary<BinaryOperator, string>
        {
            { BinaryOperator.Equal, "==" },
            { BinaryOperator.NotEqual, "!=" },
            { BinaryOperator.GreaterThan, ">" },
            { BinaryOperator.GreaterThanOrEqual, ">=" },
            { BinaryOperator.LessThan, "<" },
            { BinaryOperator.LessThanOrEqual, "<=" },
            { BinaryOperator.Add, "+" },
            { BinaryOperator.Subtract, "-" },
            { BinaryOperator.Multiply, "*" },
            { BinaryOperator.Divide, "/" },
            { BinaryOperator.Concat, "||" },
            { BinaryOperator.In, "in" },
            { BinaryOperator.NotIn, "notin" },
            { BinaryOperator.Like, "like" },
            { BinaryOperator.NotLike, "notlike" },
            { BinaryOperator.Contains, "contains" },
            { BinaryOperator.NotContains, "notcontains" },
            { BinaryOperator.StartsWith, "startswith" },
            { BinaryOperator.NotStartsWith, "notstartswith" },
            { BinaryOperator.EndsWith, "endswith" },
            { BinaryOperator.NotEndsWith, "notendswith" },
            { BinaryOperator.RegexpLike, "regexp" },
            { BinaryOperator.NotRegexpLike, "notregexp" }
        };

            private static readonly Dictionary<string, BinaryOperator> _jsonToOperator = new Dictionary<string, BinaryOperator>(StringComparer.OrdinalIgnoreCase);

            static ExprJsonConverter()
            {
                foreach (var kvp in _operatorToJson)
                {
                    _jsonToOperator[kvp.Value] = kvp.Key;
                }
                // 兼容性符号
                _jsonToOperator["="] = BinaryOperator.Equal;
                _jsonToOperator["<>"] = BinaryOperator.NotEqual;
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
                    // 快捷方式：{"#": "PropName"} 映射为 PropertyExpr
                    else if (tempReader.ValueTextEquals("#"))
                    {
                        reader.Read(); // 跳过 @
                        reader.Read(); // 读取属性值
                        string propName = reader.GetString();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject) ;
                        result = new PropertyExpr(propName);
                    }
                    // 标准方式：{"$": "typeMark", ...}
                    else if (tempReader.ValueTextEquals("$"))
                    {
                        reader.Read(); // 跳过 $
                        reader.Read(); // 读取 typeMark
                        string mark = reader.GetString();

                        // 优先按操作符短标识识别（如 ==, in, contains）
                        if (_jsonToOperator.TryGetValue(mark, out var bopSymbol))
                        {
                            result = ReadBinary(ref reader, options, bopSymbol);
                        }
                        // 其次按枚举名识别
                        else if (Enum.TryParse<BinaryOperator>(mark, true, out var bop))
                        {
                            result = ReadBinary(ref reader, options, bop);
                        }
                        else
                        {
                            // 最后按特殊类型标识识别
                            result = mark switch
                            {
                                "bin" => ReadBinary(ref reader, options, null),
                                "set" => ReadSet(ref reader, options),
                                "func" => ReadFunction(ref reader, options),
                                "prop" => ReadProperty(ref reader, options),
                                "unary" => ReadUnary(ref reader, options),
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

                if (value is LambdaExpr lambda)
                {
                    WriteExpr(writer, lambda.InnerExpr, options);
                    return;
                }

                // 复杂表达式使用 $ 标识符
                writer.WriteStartObject();
                string mark = value switch
                {
                    BinaryExpr be => _operatorToJson.TryGetValue(be.Operator, out var symbol) ? symbol : be.Operator.ToString().ToLower(),
                    ExprSet => "set",
                    FunctionExpr => "func",
                    UnaryExpr => "unary",
                    GenericSqlExpr => "sql",
                    ValueExpr vve => vve.IsConst ? "const" : "value",
                    ForeignExpr => "for",
                    _ => value.GetType().Name.Replace("Expr", "").ToLower()
                };
                writer.WritePropertyName("$");
                WriteStringUnescaped(writer, mark);

                switch (value)
                {
                    case BinaryExpr be:
                        writer.WritePropertyName("Left");
                        JsonSerializer.Serialize(writer, be.Left, options);
                        writer.WritePropertyName("Right");
                        JsonSerializer.Serialize(writer, be.Right, options);
                        break;
                    case ExprSet set:
                        writer.WritePropertyName(set.JoinType.ToString());
                        JsonSerializer.Serialize(writer, set.Items, options);
                        break;
                    case FunctionExpr fe:
                        writer.WritePropertyName(fe.FunctionName);
                        JsonSerializer.Serialize(writer, fe.Parameters, options);
                        break;
                    case UnaryExpr ue:
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

            private BinaryExpr ReadBinary(ref Utf8JsonReader reader, JsonSerializerOptions options, BinaryOperator? op = null)
            {
                var be = new BinaryExpr();
                if (op.HasValue) be.Operator = op.Value;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    if (reader.ValueTextEquals("Left"))
                    {
                        reader.Read();
                        be.Left = JsonSerializer.Deserialize<Expr>(ref reader, options);
                    }
                    else if (reader.ValueTextEquals("Operator"))
                    {
                        reader.Read();
                        be.Operator = JsonSerializer.Deserialize<BinaryOperator>(ref reader, options);
                    }
                    else if (reader.ValueTextEquals("Right"))
                    {
                        reader.Read();
                        be.Right = JsonSerializer.Deserialize<Expr>(ref reader, options);
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }
                return be;
            }

            private ExprSet ReadSet(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                var set = new ExprSet();
                bool success = false;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    string prop = reader.GetString();
                    if (prop == "JoinType")
                    {
                        reader.Read();
                        set.JoinType = JsonSerializer.Deserialize<ExprJoinType>(ref reader, options);
                        success = true;
                    }
                    else if (prop == "Items")
                    {
                        reader.Read();
                        var items = JsonSerializer.Deserialize<List<Expr>>(ref reader, options);
                        if (items != null) set.AddRange(items);
                    }
                    else
                    {
                        if (!success && Enum.TryParse<ExprJoinType>(prop, out var jt))
                        {
                            success = true;
                            set.JoinType = jt;
                            reader.Read();
                            var items = JsonSerializer.Deserialize<List<Expr>>(ref reader, options);
                            if (items != null) set.AddRange(items);
                        }
                        else
                        {
                            reader.Read();
                            reader.Skip();
                        }
                    }
                }
                return set;
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
                        var parameters = JsonSerializer.Deserialize<List<Expr>>(ref reader, options);
                        if (parameters != null) fe.Parameters.AddRange(parameters);
                    }
                    else
                    {
                        if (fe.FunctionName == null)
                        {
                            fe.FunctionName = prop;
                            reader.Read();
                            var parameters = JsonSerializer.Deserialize<List<Expr>>(ref reader, options);
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
                        foreignExpr.InnerExpr = JsonSerializer.Deserialize<Expr>(ref reader, options);
                    }
                    else
                    {
                        if (foreignExpr.Foreign == null)
                        {
                            foreignExpr.Foreign = prop;
                            reader.Read();
                            foreignExpr.InnerExpr = JsonSerializer.Deserialize<Expr>(ref reader, options);
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

            private UnaryExpr ReadUnary(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                var ue = new UnaryExpr();
                bool success = false;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string prop = reader.GetString();
                    if (prop == "Operator")
                    {
                        reader.Read();
                        ue.Operator = JsonSerializer.Deserialize<UnaryOperator>(ref reader, options);
                        success = true;
                    }
                    else if (prop == "Operand")
                    {
                        reader.Read();
                        ue.Operand = JsonSerializer.Deserialize<Expr>(ref reader, options);
                    }
                    else
                    {
                        if (!success && Enum.TryParse<UnaryOperator>(prop, out var parsedOp))
                        {
                            success = true;
                            ue.Operator = parsedOp;
                            reader.Read();
                            ue.Operand = JsonSerializer.Deserialize<Expr>(ref reader, options);
                        }
                        else
                        {
                            reader.Read();
                            reader.Skip();
                        }
                    }
                }
                return ue;
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
