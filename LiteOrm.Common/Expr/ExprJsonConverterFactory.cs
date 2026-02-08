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
            private static readonly Dictionary<LogicOperator, string> _logicOperatorToJson = new()
            {
                { LogicOperator.Equal, "==" },
                { LogicOperator.NotEqual, "!=" },
                { LogicOperator.GreaterThan, ">" },
                { LogicOperator.GreaterThanOrEqual, ">=" },
                { LogicOperator.LessThan, "<" },
                { LogicOperator.LessThanOrEqual, "<=" },
                { LogicOperator.In, "in" },
                { LogicOperator.NotIn, "notin" },
                { LogicOperator.Like, "like" },
                { LogicOperator.NotLike, "notlike" },
                { LogicOperator.Contains, "contains" },
                { LogicOperator.NotContains, "notcontains" },
                { LogicOperator.StartsWith, "startswith" },
                { LogicOperator.NotStartsWith, "notstartswith" },
                { LogicOperator.EndsWith, "endswith" },
                { LogicOperator.NotEndsWith, "notendswith" },
                { LogicOperator.RegexpLike, "regexp" },
                { LogicOperator.NotRegexpLike, "notregexp" }
            };

            private static readonly Dictionary<ValueOperator, string> _valueOperatorToJson = new()
            {
                { ValueOperator.Add, "+" },
                { ValueOperator.Subtract, "-" },
                { ValueOperator.Multiply, "*" },
                { ValueOperator.Divide, "/" },
                { ValueOperator.Concat, "||" }
            };

            private static readonly Dictionary<string, LogicOperator> _jsonToLogicOperator = new(StringComparer.OrdinalIgnoreCase);
            private static readonly Dictionary<string, ValueOperator> _jsonToValueOperator = new(StringComparer.OrdinalIgnoreCase);

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
                _jsonToLogicOperator["="] = LogicOperator.Equal;
                _jsonToLogicOperator["<>"] = LogicOperator.NotEqual;
            }

            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null) return null;

                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    return (T)(Expr)new ValueExpr(ReadNative(ref reader, options));
                }

                Expr result = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string propName = reader.GetString();

                    if (propName == "@")
                    {
                        reader.Read();
                        result = new ValueExpr(ReadNative(ref reader, options)) { IsConst = false };
                    }
                    else if (propName == "#")
                    {
                        reader.Read();
                        result = new PropertyExpr(reader.GetString());
                    }
                    else if (propName == "!")
                    {
                        reader.Read();
                        result = new NotExpr(JsonSerializer.Deserialize<Expr>(ref reader, options) as LogicExpr);
                    }
                    else if (propName.StartsWith("$"))
                    {
                        string mark = propName == "$" ? null : propName.Substring(1);
                        if (mark == null)
                        {
                            reader.Read();
                            mark = reader.GetString().ToLower();
                        }

                        // 按操作符识别
                        if (_jsonToLogicOperator.TryGetValue(mark, out var lbop)) { result = ReadLogicBinary(ref reader, options, lbop); break; }
                        else if (_jsonToValueOperator.TryGetValue(mark, out var vbop)) { result = ReadValueBinary(ref reader, options, vbop); break; }
                        else if (Enum.TryParse<LogicOperator>(mark, true, out var lbop2)) { result = ReadLogicBinary(ref reader, options, lbop2); break; }
                        else if (Enum.TryParse<ValueOperator>(mark, true, out var vbop2)) { result = ReadValueBinary(ref reader, options, vbop2); break; }
                        // 按特殊标识识别
                        else
                        {
                            result = mark switch
                            {
                                "bin" => ReadValueBinary(ref reader, options),
                                "logic" => ReadLogicBinary(ref reader, options),
                                "set" => ReadLogicSet(ref reader, options),
                                "vset" => ReadValueSet(ref reader, options),
                                "func" => ReadFunction(ref reader, options),
                                "agg" => ReadAggregate(ref reader, options),
                                "prop" => ReadProperty(ref reader, options),
                                "not" => ReadNot(ref reader, options),
                                "unary" => ReadValueUnary(ref reader, options),
                                "sql" => ReadSql(ref reader, options),
                                "value" => ReadValueBody(ref reader, options),
                                "const" => ReadValueBody(ref reader, options, true),
                                "for" => ReadForeign(ref reader, options),
                                "table" => new TableExpr(),
                                "where" => new WhereExpr(),
                                "order" => new OrderByExpr(),
                                "group" => new GroupByExpr(),
                                "having" => new HavingExpr(),
                                "section" => new SectionExpr(),
                                "select" => new SelectExpr(),
                                "delete" => new DeleteExpr(),
                                "update" => new UpdateExpr(),
                                _ => new ValueExpr(mark) { IsConst = true }
                            };

                            // 如果使用了特殊的读取器，则意味着该读取器已经消耗了 EndObject
                            if (result is not null && mark != "table" && mark != "where" && mark != "order" &&
                                mark != "group" && mark != "having" && mark != "section" &&
                                mark != "select" && mark != "delete" && mark != "update" &&
                                mark != "value" && mark != "const" && mark != "prop")
                            {
                                break;
                            }
                            
                            // 处理快捷片段的数据 (如 "$table": "Name" or "$update": {Source})
                            if (propName != "$" && result is SqlSegment ss)
                            {
                                reader.Read(); // 移动到属性值
                                if (ss is TableExpr te)
                                {
                                    string typeName = reader.GetString();
                                    if (!string.IsNullOrEmpty(typeName))
                                    {
                                        Type type = Type.GetType(typeName);
                                        if (type == null)
                                        {
                                            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                                            {
                                                type = assembly.GetType(typeName);
                                                if (type != null) break;
                                            }
                                        }
                                        if (type != null) te.Table = TableInfoProvider.Default.GetTableView(type);
                                    }
                                }
                                else
                                {
                                    ss.Source = JsonSerializer.Deserialize<Expr>(ref reader, options) as SqlSegment;
                                }
                            }
                        }
                    }
                    else if (result is not null)
                    {
                        ReadResultProperty(ref reader, result, propName, options);
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }

                return (T)result;
            }

            private void ReadResultProperty(ref Utf8JsonReader reader, Expr result, string propName, JsonSerializerOptions options)
            {
                reader.Read();
                switch (result)
                {
                    case WhereExpr we when propName == "Where":
                        we.Where = JsonSerializer.Deserialize<Expr>(ref reader, options) as LogicExpr;
                        break;
                    case OrderByExpr obe when propName == "OrderBys":
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            ValueTypeExpr expr = null; bool asc = true;
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                            {
                                if (reader.TokenType != JsonTokenType.PropertyName) continue;
                                if (reader.ValueTextEquals("Expr")) { reader.Read(); expr = JsonSerializer.Deserialize<Expr>(ref reader, options) as ValueTypeExpr; }
                                else if (reader.ValueTextEquals("Asc")) { reader.Read(); asc = reader.GetBoolean(); }
                            }
                            obe.OrderBys.Add((expr, asc));
                        }
                        break;
                    case GroupByExpr gbe when propName == "GroupBys":
                        gbe.GroupBys.AddRange(JsonSerializer.Deserialize<List<Expr>>(ref reader, options).Cast<ValueTypeExpr>());
                        break;
                    case HavingExpr he when propName == "Having":
                        he.Having = JsonSerializer.Deserialize<Expr>(ref reader, options) as LogicExpr;
                        break;
                    case SectionExpr se when propName == "Skip":
                        se.Skip = reader.GetInt32();
                        break;
                    case SectionExpr se when propName == "Take":
                        se.Take = reader.GetInt32();
                        break;
                    case SelectExpr sele when propName == "Selects":
                        if (reader.TokenType == JsonTokenType.StartArray)
                        {
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                if (reader.TokenType == JsonTokenType.StartObject)
                                {
                                    using var doc = JsonDocument.ParseValue(ref reader);
                                    var root = doc.RootElement;
                                    var firstProp = root.EnumerateObject().FirstOrDefault();
                                    string n = firstProp.Name;
                                    if (n is not null && !n.StartsWith("$") && n != "@" && n != "#" && n != "!")
                                    {
                                        var v = JsonSerializer.Deserialize<Expr>(firstProp.Value.GetRawText(), options) as ValueTypeExpr;
                                        sele.Selects.Add(new SelectItemExpr(v) { Name = n });
                                    }
                                    else
                                    {
                                        var v = JsonSerializer.Deserialize<Expr>(root.GetRawText(), options) as ValueTypeExpr;
                                        sele.Selects.Add(new SelectItemExpr(v as ValueTypeExpr));
                                    }
                                }
                                else
                                {
                                    var v = JsonSerializer.Deserialize<Expr>(ref reader, options) as ValueTypeExpr;
                                    if (v is not null) sele.Selects.Add(new SelectItemExpr(v));
                                }
                            }
                        }
                        break;
                    case UpdateExpr ue when propName == "Sets":
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            string prop = null;
                            ValueTypeExpr val = null;
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                            {
                                if (reader.TokenType != JsonTokenType.PropertyName) continue;
                                prop = reader.GetString();
                                reader.Read();
                                val = JsonSerializer.Deserialize<Expr>(ref reader, options) as ValueTypeExpr;
                            }
                            if (prop != null) ue.Sets.Add((prop, val));
                        }
                        break;
                    case UpdateExpr ue when propName == "Where":
                        ue.Where = JsonSerializer.Deserialize<Expr>(ref reader, options) as LogicExpr;
                        break;
                    case DeleteExpr de when propName == "Where":
                        de.Where = JsonSerializer.Deserialize<Expr>(ref reader, options) as LogicExpr;
                        break;
                    case SqlSegment ss when propName == "Source":
                        ss.Source = JsonSerializer.Deserialize<Expr>(ref reader, options) as SqlSegment;
                        break;
                    default:
                        reader.Skip();
                        break;
                }
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

                // 结构化查询片段直接处理
                if (value is SqlSegment segment)
                {
                    WriteSqlSegment(writer, segment, options);
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
                    WriteExpr(writer, ne.Operand, options);
                    writer.WriteEndObject();
                    return;
                }

                // 优化序列化格式：SelectItemExpr 使用 { Name: Value } 格式
                if (value is SelectItemExpr sie)
                {
                    if (string.IsNullOrEmpty(sie.Name))
                    {
                        WriteExpr(writer, sie.Value, options);
                    }
                    else
                    {
                        writer.WriteStartObject();
                        writer.WritePropertyName(sie.Name);
                        WriteExpr(writer, sie.Value, options);
                        writer.WriteEndObject();
                    }
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
                    LogicSet => "set",
                    ValueSet => "vset",
                    AggregateFunctionExpr => "agg",
                    FunctionExpr => "func",
                    NotExpr => "not",
                    UnaryExpr => "unary",
                    GenericSqlExpr => "sql",
                    ValueExpr vve => vve.IsConst ? "const" : "value",
                    ForeignExpr => "for",
                    TableExpr => "table",
                    WhereExpr => "where",
                    OrderByExpr => "order",
                    GroupByExpr => "group",
                    HavingExpr => "having",
                    SectionExpr => "section",
                    SelectExpr => "select",
                    DeleteExpr => "delete",
                    UpdateExpr => "update",
                    _ => value.GetType().Name.Replace("Expr", "").ToLower()
                };
                writer.WritePropertyName("$");
                WriteStringUnescaped(writer, mark);

                switch (value)
                {
                    case LogicBinaryExpr be:
                        writer.WritePropertyName("Left");
                        WriteExpr(writer, be.Left, options);
                        writer.WritePropertyName("Right");
                        WriteExpr(writer, be.Right, options);
                        break;
                    case ValueBinaryExpr be:
                        writer.WritePropertyName("Left");
                        WriteExpr(writer, be.Left, options);
                        writer.WritePropertyName("Right");
                        WriteExpr(writer, be.Right, options);
                        break;
                    case LogicSet set:
                        writer.WritePropertyName(set.JoinType.ToString());
                        writer.WriteStartArray();
                        foreach (var item in set.Items) WriteExpr(writer, item, options);
                        writer.WriteEndArray();
                        break;
                    case ValueSet set:
                        writer.WritePropertyName(set.JoinType.ToString());
                        writer.WriteStartArray();
                        foreach (var item in set.Items) WriteExpr(writer, item, options);
                        writer.WriteEndArray();
                        break;
                    case FunctionExpr fe:
                        writer.WritePropertyName(fe.FunctionName);
                        writer.WriteStartArray();
                        foreach (var param in fe.Parameters) WriteExpr(writer, param, options);
                        writer.WriteEndArray();
                        break;
                    case AggregateFunctionExpr afe:
                        writer.WriteString("Name", afe.FunctionName);
                        writer.WritePropertyName("Expr");
                        WriteExpr(writer, afe.Expression, options);
                        writer.WriteBoolean("Distinct", afe.IsDistinct);
                        break;
                    case NotExpr ne2:
                        writer.WritePropertyName("Operand");
                        WriteExpr(writer, ne2.Operand, options);
                        break;
                    case UnaryExpr ue:
                        writer.WritePropertyName(ue.Operator.ToString());
                        WriteExpr(writer, ue.Operand, options);
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
                        if (fe.TableArgs != null && fe.TableArgs.Length > 0)
                        {
                            writer.WriteString("Foreign", fe.Foreign);
                            writer.WritePropertyName("InnerExpr");
                            WriteExpr(writer, fe.InnerExpr, options);
                            writer.WritePropertyName("TableArgs");
                            JsonSerializer.Serialize(writer, fe.TableArgs, options);
                        }
                        else
                        {
                            writer.WritePropertyName(fe.Foreign);
                            WriteExpr(writer, fe.InnerExpr, options);
                        }
                        break;
                }
                writer.WriteEndObject();
            }

            /// <summary>
            /// 写入 SQL 片段
            /// </summary>
            private void WriteSqlSegment(Utf8JsonWriter writer, SqlSegment value, JsonSerializerOptions options)
            {
                if (value == null) { writer.WriteNullValue(); return; }
                writer.WriteStartObject();

                // 类型到类型标识符的映射
                var typeToMark = new Dictionary<Type, string>
                {
                    { typeof(TableExpr), "table" },
                    { typeof(WhereExpr), "where" },
                    { typeof(OrderByExpr), "order" },
                    { typeof(GroupByExpr), "group" },
                    { typeof(HavingExpr), "having" },
                    { typeof(SectionExpr), "section" },
                    { typeof(SelectExpr), "select" },
                    { typeof(DeleteExpr), "delete" },
                    { typeof(UpdateExpr), "update" }
                };

                string mark = typeToMark.TryGetValue(value.GetType(), out string m) ? m : value.GetType().Name.Replace("Expr", "").ToLower();
                writer.WritePropertyName("$" + mark);
                if (value is TableExpr te)
                {
                    writer.WriteStringValue(te.Table?.DefinitionType.FullName);
                }
                else
                {
                    JsonSerializer.Serialize(writer, value.Source, options);
                }

                switch (value)
                {
                    case SelectExpr sele:
                        if (sele.Selects?.Count > 0)
                        {
                            writer.WritePropertyName("Selects");
                            writer.WriteStartArray();
                            foreach (var item in sele.Selects) WriteExpr(writer, item, options);
                            writer.WriteEndArray();
                        }
                        break;
                    case WhereExpr we:
                        if (we.Where is not null) { writer.WritePropertyName("Where"); JsonSerializer.Serialize(writer, we.Where, options); }
                        break;
                    case OrderByExpr obe:
                        if (obe.OrderBys?.Count > 0)
                        {
                            writer.WritePropertyName("OrderBys");
                            writer.WriteStartArray();
                            foreach (var ob in obe.OrderBys)
                            {
                                writer.WriteStartObject();
                                writer.WritePropertyName("Expr");
                                JsonSerializer.Serialize(writer, ob.Item1, options);
                                writer.WriteBoolean("Asc", ob.Item2);
                                writer.WriteEndObject();
                            }
                            writer.WriteEndArray();
                        }
                        break;
                    case GroupByExpr gbe:
                        if (gbe.GroupBys?.Count > 0) { writer.WritePropertyName("GroupBys"); JsonSerializer.Serialize(writer, gbe.GroupBys, options); }
                        break;
                    case HavingExpr he:
                        if (he.Having is not null) { writer.WritePropertyName("Having"); JsonSerializer.Serialize(writer, he.Having, options); }
                        break;
                    case SectionExpr se:
                        writer.WriteNumber("Skip", se.Skip);
                        writer.WriteNumber("Take", se.Take);
                        break;
                    case UpdateExpr ue:
                        if (ue.Sets?.Count > 0)
                        {
                            writer.WritePropertyName("Sets");
                            writer.WriteStartArray();
                            foreach (var set in ue.Sets)
                            {
                                writer.WriteStartObject();
                                writer.WritePropertyName(set.Item1);
                                JsonSerializer.Serialize(writer, set.Item2, options);
                                writer.WriteEndObject();
                            }
                            writer.WriteEndArray();
                        }
                        if (ue.Where is not null) { writer.WritePropertyName("Where"); JsonSerializer.Serialize(writer, ue.Where, options); }
                        break;
                    case DeleteExpr de:
                        if (de.Where is not null) { writer.WritePropertyName("Where"); JsonSerializer.Serialize(writer, de.Where, options); }
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

            private LogicBinaryExpr ReadLogicBinary(ref Utf8JsonReader reader, JsonSerializerOptions options, LogicOperator? op = null)
            {
                LogicOperator finalOp = op ?? LogicOperator.Equal;
                ValueTypeExpr left = null;
                ValueTypeExpr right = null;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    if (reader.ValueTextEquals("Left"))
                    {
                        reader.Read();
                        left = JsonSerializer.Deserialize<Expr>(ref reader, options) as ValueTypeExpr;
                    }
                    else if (reader.ValueTextEquals("Operator"))
                    {
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            string opStr = reader.GetString();
                            if (Enum.TryParse<LogicOperator>(opStr, true, out var lbop)) finalOp = lbop;
                        }
                        else
                        {
                            finalOp = (LogicOperator)reader.GetInt32();
                        }
                    }
                    else if (reader.ValueTextEquals("Right"))
                    {
                        reader.Read();
                        right = JsonSerializer.Deserialize<Expr>(ref reader, options) as ValueTypeExpr;
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }

                return new LogicBinaryExpr(left, finalOp, right);
            }

            private ValueBinaryExpr ReadValueBinary(ref Utf8JsonReader reader, JsonSerializerOptions options, ValueOperator? op = null)
            {
                ValueOperator finalOp = op ?? ValueOperator.Add;
                ValueTypeExpr left = null;
                ValueTypeExpr right = null;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    if (reader.ValueTextEquals("Left"))
                    {
                        reader.Read();
                        left = JsonSerializer.Deserialize<Expr>(ref reader, options) as ValueTypeExpr;
                    }
                    else if (reader.ValueTextEquals("Operator"))
                    {
                        reader.Read();
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            string opStr = reader.GetString();
                            if (Enum.TryParse<ValueOperator>(opStr, true, out var vbop)) finalOp = vbop;
                        }
                        else
                        {
                            finalOp = (ValueOperator)reader.GetInt32();
                        }
                    }
                    else if (reader.ValueTextEquals("Right"))
                    {
                        reader.Read();
                        right = JsonSerializer.Deserialize<Expr>(ref reader, options) as ValueTypeExpr;
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }

                return new ValueBinaryExpr(left, finalOp, right);
            }

            private LogicSet ReadLogicSet(ref Utf8JsonReader reader, JsonSerializerOptions options)
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
                        items = JsonSerializer.Deserialize<List<Expr>>(ref reader, options)?.Cast<LogicExpr>().ToList();
                    }
                    else
                    {
                        if (!success && Enum.TryParse<LogicJoinType>(prop, out joinType))
                        {
                            success = true;
                            reader.Read();
                            items = JsonSerializer.Deserialize<List<Expr>>(ref reader, options)?.Cast<LogicExpr>().ToList();
                        }
                        else
                        {
                            reader.Read();
                            reader.Skip();
                        }
                    }
                }

                return new LogicSet(joinType, items);
            }

            private ValueSet ReadValueSet(ref Utf8JsonReader reader, JsonSerializerOptions options)
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
                        items = JsonSerializer.Deserialize<List<Expr>>(ref reader, options)?.Cast<ValueTypeExpr>().ToList();
                    }
                    else
                    {
                        if (!success && Enum.TryParse<ValueJoinType>(prop, out var vjt))
                        {
                            success = true;
                            joinType = vjt;
                            reader.Read();
                            items = JsonSerializer.Deserialize<List<Expr>>(ref reader, options)?.Cast<ValueTypeExpr>().ToList();
                        }
                        else
                        {
                            reader.Read();
                            reader.Skip();
                        }
                    }
                }

                return new ValueSet(joinType, items);
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
                        if (parameters != null) fe.Parameters.AddRange(parameters.Cast<ValueTypeExpr>());
                    }
                    else
                    {
                        if (fe.FunctionName == null)
                        {
                            fe.FunctionName = prop;
                            reader.Read();
                            var parameters = JsonSerializer.Deserialize<List<Expr>>(ref reader, options);
                            if (parameters != null) fe.Parameters.AddRange(parameters.Cast<ValueTypeExpr>());
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

            private AggregateFunctionExpr ReadAggregate(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                var afe = new AggregateFunctionExpr();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    if (reader.ValueTextEquals("Name"))
                    {
                        reader.Read();
                        afe.FunctionName = reader.GetString();
                    }
                    else if (reader.ValueTextEquals("Expr"))
                    {
                        reader.Read();
                        afe.Expression = JsonSerializer.Deserialize<Expr>(ref reader, options) as ValueTypeExpr;
                    }
                    else if (reader.ValueTextEquals("Distinct"))
                    {
                        reader.Read();
                        afe.IsDistinct = reader.GetBoolean();
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }
                return afe;
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
                        foreignExpr.InnerExpr = JsonSerializer.Deserialize<Expr>(ref reader, options) as LogicExpr;
                    }
                    else if (prop == "TableArgs")
                    {
                        reader.Read();
                        foreignExpr.TableArgs = JsonSerializer.Deserialize<string[]>(ref reader, options);
                    }
                    else
                    {
                        if (foreignExpr.Foreign == null)
                        {
                            foreignExpr.Foreign = prop;
                            reader.Read();
                            foreignExpr.InnerExpr = JsonSerializer.Deserialize<Expr>(ref reader, options) as LogicExpr;
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
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string prop = reader.GetString();
                    if (prop == "Operand")
                    {
                        reader.Read();
                        operand = JsonSerializer.Deserialize<Expr>(ref reader, options) as LogicExpr;
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }
                return new NotExpr(operand);
            }

            private UnaryExpr ReadValueUnary(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                UnaryOperator op = UnaryOperator.Nagive;
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
                            if (Enum.TryParse<UnaryOperator>(opStr, true, out var vop)) op = vop;
                        }
                        else
                        {
                            op = (UnaryOperator)reader.GetInt32();
                        }
                        success = true;
                    }
                    else if (prop == "Operand")
                    {
                        reader.Read();
                        operand = JsonSerializer.Deserialize<Expr>(ref reader, options) as ValueTypeExpr;
                    }
                    else
                    {
                        if (!success && Enum.TryParse<UnaryOperator>(prop, out var parsedOp))
                        {
                            success = true;
                            op = parsedOp;
                            reader.Read();
                            operand = JsonSerializer.Deserialize<Expr>(ref reader, options) as ValueTypeExpr;
                        }
                        else
                        {
                            reader.Read();
                            reader.Skip();
                        }
                    }
                }
                return new UnaryExpr(op, operand);
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
