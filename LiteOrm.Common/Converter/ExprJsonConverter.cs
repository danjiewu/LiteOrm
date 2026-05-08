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
                typeof(ExprJsonConverter<>).MakeGenericType(typeToConvert))!;
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
                { ValueOperator.Modulo, "%" },
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

                // 非对象直接视为常量值
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    return (T)(Expr)new ValueExpr(ReadNative(ref reader, options));
                }

                Expr result = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string propName = reader.GetString() ?? string.Empty;

                    if (propName == "@")
                    {
                        reader.Read();
                        result = new ValueExpr(ReadNative(ref reader, options)) { IsConst = false };
                    }
                    else if (propName == "#")
                    {
                        reader.Read();
                        string stringValue = reader.GetString() ?? string.Empty;
                        string[] parts = stringValue.Split(['.'], 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2)
                            result = Expr.Prop(parts[0], parts[1]);
                        else
                            result = Expr.Prop(stringValue);
                    }
                    else if (propName == "!")
                    {
                        reader.Read();
                        result = new NotExpr(JsonSerializer.Deserialize<Expr>(ref reader, options) as LogicExpr);
                    }
                    else if (propName.StartsWith("$"))
                    {
                        // 读取标识（可能是 "$": "like" 或 "$like": {...}）
                        string mark = propName == "$" ? null : propName.Substring(1);
                        if (mark == null)
                        {
                            reader.Read();
                            mark = reader.GetString()?.ToLower();
                        }

                        if (mark is not null && _jsonToLogicOperator.TryGetValue(mark, out var lbop))
                        {
                            result = ReadLogicBinary(ref reader, options, lbop);
                            break;
                        }
                        else if (mark is not null && _jsonToValueOperator.TryGetValue(mark, out var vbop))
                        {
                            result = ReadValueBinary(ref reader, options, vbop);
                            break;
                        }
                        else if (mark is not null && Enum.TryParse<LogicOperator>(mark, true, out var lbop2))
                        {
                            result = ReadLogicBinary(ref reader, options, lbop2);
                            break;
                        }
                        else if (mark is not null && Enum.TryParse<ValueOperator>(mark, true, out var vbop2))
                        {
                            result = ReadValueBinary(ref reader, options, vbop2);
                            break;
                        }
                        else
                        {
                            // 特殊标识映射
                            if (mark == "bin") { result = ReadValueBinary(ref reader, options); break; }
                            if (mark == "logic") { result = ReadLogicBinary(ref reader, options); break; }
                            if (mark == "and") { result = ReadAndExpr(ref reader, options); break; }
                            if (mark == "or") { result = ReadOrExpr(ref reader, options); break; }
                            if (mark == "set") { result = ReadValueSet(ref reader, options); break; }
                            if (mark == "func") { result = ReadFunction(ref reader, options); break; }
                            if (mark == "prop") { result = ReadProperty(ref reader, options); break; }
                            if (mark == "not") { result = ReadNot(ref reader, options); break; }
                            if (mark == "unary") { result = ReadValueUnary(ref reader, options); break; }
                            if (mark == "sql") { result = ReadSql(ref reader, options); break; }
                            if (mark == "value") { result = ReadValueBody(ref reader, options); break; }
                            if (mark == "const") { result = ReadValueBody(ref reader, options, true); break; }
                            if (mark == "foreign") { result = ReadForeign(ref reader, options); break; }
                            if (mark == "delete") { result = propName != "$" ? new DeleteExpr() { Table = JsonSerializer.Deserialize<TableExpr>(ref reader, options) } : new DeleteExpr(); }
                            if (mark == "update") { result = propName != "$" ? new UpdateExpr() { Table = JsonSerializer.Deserialize<TableExpr>(ref reader, options) } : new UpdateExpr(); }
                            if (mark == "from") { result = new FromExpr(); }
                            if (mark == "table") { result = new TableExpr(); }
                            if (mark == "join") { result = new TableJoinExpr(); }
                            if (mark == "where") { result = new WhereExpr(); }
                            if (mark == "orderby") { result = new OrderByExpr(); }
                            if (mark == "orderbyitem") { result = new OrderByItemExpr(); }
                            if (mark == "group") { result = new GroupByExpr(); }
                            if (mark == "having") { result = new HavingExpr(); }
                            if (mark == "section") { result = new SectionExpr(); }
                            if (mark == "select") { result = new SelectExpr(); }
                            if (mark == "selectitem") { result = new SelectItemExpr(); }
                            // 对于 SQL 片段类型且是通过简写形式传值（例如 "$table": "Full.Type.Name" 或 "$from": {...}）
                            if (result is SqlSegment segment && propName != "$")
                            {
                                // 需要先读取属性值的位置
                                reader.Read();
                                ReadSqlSegmentProperty(ref reader, segment, propName, options);
                            }
                        }
                    }
                    else if (result is not null)
                    {
                        ReadResultProperty(ref reader, result, propName, options);
                    }
                    else
                    {
                        // 未识别的属性，跳过
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
                    case SqlSegment ss when propName == "Source":
                        ss.Source = JsonSerializer.Deserialize<SqlSegment>(ref reader, options);
                        break;
                    case WhereExpr we when propName == "Where":
                        we.Where = JsonSerializer.Deserialize<LogicExpr>(ref reader, options);
                        break;
                    case OrderByExpr obe when propName == "OrderBys":
                        obe.OrderBys.Clear();
                        if (reader.TokenType == JsonTokenType.StartArray)
                        {
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                if (reader.TokenType != JsonTokenType.StartObject) { reader.Skip(); continue; }
                                ValueTypeExpr expr = null; bool asc = true;
                                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                                {
                                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                                    if (reader.ValueTextEquals("Field")) { reader.Read(); expr = JsonSerializer.Deserialize<ValueTypeExpr>(ref reader, options); }
                                    else if (reader.ValueTextEquals("Asc")) { reader.Read(); asc = reader.GetBoolean(); }
                                    else { reader.Read(); reader.Skip(); }
                                }
                                if (expr is not null) obe.OrderBys.Add(new OrderByItemExpr(expr, asc));
                            }
                        }
                        break;
                    case OrderByItemExpr obi when propName == "Field":
                        obi.Field = JsonSerializer.Deserialize<ValueTypeExpr>(ref reader, options);
                        break;
                    case OrderByItemExpr obi when propName == "Asc":
                        obi.Ascending = reader.GetBoolean();
                        break;
                    case GroupByExpr gbe when propName == "GroupBys":
                        gbe.GroupBys.Clear();
                        var gList = JsonSerializer.Deserialize<List<ValueTypeExpr>>(ref reader, options);
                        if (gList != null) gbe.GroupBys.AddRange(gList);
                        break;
                    case HavingExpr he when propName == "Having":
                        he.Having = JsonSerializer.Deserialize<LogicExpr>(ref reader, options);
                        break;
                    case SectionExpr se when propName == "Skip":
                        se.Skip = reader.GetInt32();
                        break;
                    case SectionExpr se when propName == "Take":
                        se.Take = reader.GetInt32();
                        break;
                    case SelectExpr sele when propName == "Selects":
                        ReadSelectItems(ref reader, sele, options);
                        break;
                    case SelectItemExpr sie when propName == "Value":
                        sie.Value = JsonSerializer.Deserialize<ValueTypeExpr>(ref reader, options);
                        break;
                    case SelectItemExpr sie when propName == "Alias":
                        sie.Alias = reader.GetString();
                        break;
                    case UpdateExpr ue when propName == "Sets":
                        ReadUpdateSets(ref reader, ue, options);
                        break;
                    case UpdateExpr ue when propName == "Where":
                        ue.Where = JsonSerializer.Deserialize<LogicExpr>(ref reader, options);
                        break;
                    case DeleteExpr de when propName == "Where":
                        de.Where = JsonSerializer.Deserialize<LogicExpr>(ref reader, options);
                        break;
                    case FromExpr fe when propName == "Joins":
                        if (reader.TokenType == JsonTokenType.StartArray)
                        {
                            fe.Joins.Clear();
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                if (reader.TokenType != JsonTokenType.StartObject) { reader.Skip(); continue; }
                                var join = JsonSerializer.Deserialize<Expr>(ref reader, options) as TableJoinExpr;
                                if (join != null) fe.Joins.Add(join);
                            }
                        }
                        break;
                    case TableExpr te when propName == "Type":
                        te.Type = GetObjectType(reader.GetString());
                        break;
                    case TableExpr te when propName == "Alias":
                        te.Alias = reader.GetString();
                        break;
                    case TableExpr te when propName == "TableArgs":
                        te.TableArgs = JsonSerializer.Deserialize<string[]>(ref reader, options);
                        break;
                    case TableJoinExpr tje when propName == "Source":
                        tje.Source = JsonSerializer.Deserialize<SourceExpr>(ref reader, options);
                        break;
                    case TableJoinExpr tje when propName == "JoinType":
                        if (reader.TokenType == JsonTokenType.String && Enum.TryParse<TableJoinType>(reader.GetString(), true, out var jt))
                            tje.JoinType = jt;
                        break;
                    case TableJoinExpr tje when propName == "On":
                        tje.On = JsonSerializer.Deserialize<LogicExpr>(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            /// <summary>
            /// 读取 SelectExpr 的 Selects 属性
            /// </summary>
            private void ReadSelectItems(ref Utf8JsonReader reader, SelectExpr sele, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            // 按属性方式反序列化：读取 Name 和 Value 属性
                            string name = null;
                            ValueTypeExpr value = null;
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                            {
                                if (reader.TokenType != JsonTokenType.PropertyName) continue;
                                string prop = reader.GetString() ?? string.Empty;
                                reader.Read();
                                if (prop == "Alias")
                                {
                                    name = reader.GetString();
                                }
                                else if (prop == "Value")
                                {
                                    value = JsonSerializer.Deserialize<Expr>(ref reader, options).AsValue();
                                }
                                else
                                {
                                    reader.Skip();
                                }
                            }
                            if (value is not null) sele.Selects.Add(new SelectItemExpr(value) { Alias = name });
                        }
                        else
                        {
                            var v = JsonSerializer.Deserialize<Expr>(ref reader, options).AsValue();
                            if (v is not null) sele.Selects.Add(new SelectItemExpr(v));
                        }
                    }
                }
            }

            /// <summary>
            /// 读取 UpdateExpr 的 Sets 属性
            /// </summary>
            private void ReadUpdateSets(ref Utf8JsonReader reader, UpdateExpr ue, JsonSerializerOptions options)
            {
                ue.Sets.Clear();
                if (reader.TokenType == JsonTokenType.StartArray)
                {
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType != JsonTokenType.StartObject) { reader.Skip(); continue; }
                        string prop = null;
                        ValueTypeExpr val = null;
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                        {
                            if (reader.TokenType != JsonTokenType.PropertyName) continue;
                            prop = reader.GetString();
                            reader.Read();
                            val = JsonSerializer.Deserialize<Expr>(ref reader, options).AsValue();
                        }
                        if (prop is not null && val is not null) ue.Sets.Add(new(Expr.Prop(prop), val));
                    }
                }
            }

            /// <summary>
            /// 读取 SQL 片段属性（如 FromExpr 的类型和别名）
            /// </summary>
            private void ReadSqlSegmentProperty(ref Utf8JsonReader reader, SqlSegment ss, string propName, JsonSerializerOptions options)
            {
                // reader 已经在属性值的位置，直接处理
                if (ss is TableExpr te && propName == "$table")
                {
                    string typeName = reader.GetString();
                    if (!string.IsNullOrEmpty(typeName))
                    {
                        Type type = GetObjectType(typeName);
                        if (type != null) te.Type = type;
                    }
                }
                else if (ss is SqlSegment segment)
                {
                    segment.Source = JsonSerializer.Deserialize<SqlSegment>(ref reader, options);
                }
            }

            private static Type GetObjectType(string typeName)
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

                return type;
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                WriteExpr(writer, value, options);
            }

            private void WriteExpr(Utf8JsonWriter writer, Expr value, JsonSerializerOptions options)
            {
                if (value is LambdaExpr lambda)
                {
                    value = lambda.InnerExpr;
                }

                // 优化序列化格式，使用简洁表示法
                switch (value)
                {
                    case null:
                        writer.WriteNullValue();
                        return;
                    case SqlSegment segment:
                        WriteSqlSegment(writer, segment, options);
                        return;
                    case AndExpr andExpr:
                        writer.WriteStartObject();
                        writer.WritePropertyName("$and");
                        JsonSerializer.Serialize(writer, andExpr.Items, _compactOptions);
                        writer.WriteEndObject();
                        return;
                    case OrExpr orExpr:
                        writer.WriteStartObject();
                        writer.WritePropertyName("$or");
                        JsonSerializer.Serialize(writer, orExpr.Items, _compactOptions);
                        writer.WriteEndObject();
                        return;
                    case ValueExpr ve:
                        if (ve.Value is Expr innerExpr)
                        {
                            while (innerExpr is ValueExpr veInner && veInner.Value is Expr nestedExpr)
                            {
                                innerExpr = nestedExpr;
                            }
                            JsonSerializer.Serialize(writer, innerExpr, options);
                        }
                        else if (ve.IsConst)
                        {
                            // 常量值直接序列化                        
                            JsonSerializer.Serialize(writer, ve.Value, _compactOptions);
                        }
                        else
                        {
                            // 变量值使用快捷方式 {"@": value}                        
                            writer.WriteStartObject();
                            writer.WritePropertyName("@");
                            JsonSerializer.Serialize(writer, ve.Value, _compactOptions);
                            writer.WriteEndObject();
                        }
                        return;
                    case PropertyExpr pe:
                        writer.WriteStartObject();
                        writer.WriteString("#", String.IsNullOrEmpty(pe.TableAlias) ? pe.PropertyName : pe.TableAlias + "." + pe.PropertyName);
                        writer.WriteEndObject();
                        return;
                    case NotExpr ne:
                        writer.WriteStartObject();
                        writer.WritePropertyName("!");
                        WriteExpr(writer, ne.Operand, options);
                        writer.WriteEndObject();
                        return;
                    case UpdateExpr ue:
                        writer.WriteStartObject();
                        writer.WritePropertyName("$update");
                        WriteExpr(writer, ue.Table, options);
                        if (ue.Sets?.Count > 0)
                        {
                            writer.WritePropertyName("Sets");
                            writer.WriteStartArray();
                            foreach (var set in ue.Sets)
                            {
                                writer.WriteStartObject();
                                writer.WritePropertyName(set.Property.PropertyName);
                                JsonSerializer.Serialize(writer, set.Value, options);
                                writer.WriteEndObject();
                            }
                            writer.WriteEndArray();
                        }
                        if (ue.Where is not null) { writer.WritePropertyName("Where"); WriteExpr(writer, ue.Where, options); }
                        writer.WriteEndObject();
                        return;
                    case DeleteExpr de:
                        writer.WriteStartObject();
                        writer.WritePropertyName("$delete");
                        WriteExpr(writer, de.Table, options);
                        if (de.Where is not null) { writer.WritePropertyName("Where"); WriteExpr(writer, de.Where, options); }
                        writer.WriteEndObject();
                        return;
                }

                // 复杂表达式使用 $标识符
                writer.WriteStartObject();
                string mark = value switch
                {
                    LogicBinaryExpr be => _logicOperatorToJson.TryGetValue(be.Operator, out var symbol) ? symbol : "logic",
                    ValueBinaryExpr be => _valueOperatorToJson.TryGetValue(be.Operator, out var symbol) ? symbol : "bin",
                    AndExpr => "and",
                    OrExpr => "or",
                    ValueSet => "set",
                    FunctionExpr => "func",
                    NotExpr => "not",
                    UnaryExpr => "unary",
                    GenericSqlExpr => "sql",
                    ValueExpr vve => vve.IsConst ? "const" : "value",
                    ForeignExpr => "foreign",
                    TableExpr => "table",
                    TableJoinExpr => "join",
                    FromExpr => "from",
                    WhereExpr => "where",
                    OrderByExpr => "orderby",
                    OrderByItemExpr => "orderbyitem",
                    GroupByExpr => "group",
                    HavingExpr => "having",
                    SectionExpr => "section",
                    SelectExpr => "select",
                    SelectItemExpr => "selectitem",
                    DeleteExpr => "delete",
                    UpdateExpr => "update",
                    _ => value.GetType().Name.Replace("Expr", "").ToLower()
                };
                writer.WritePropertyName("$");
                writer.WriteStringValue(mark);

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
                    case AndExpr and:
                        writer.WritePropertyName("Items");
                        writer.WriteStartArray();
                        foreach (var item in and.Items) WriteExpr(writer, item, options);
                        writer.WriteEndArray();
                        break;
                    case OrExpr or:
                        writer.WritePropertyName("Items");
                        writer.WriteStartArray();
                        foreach (var item in or.Items) WriteExpr(writer, item, options);
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
                        foreach (var param in fe.Args) WriteExpr(writer, param, options);
                        writer.WriteEndArray();
                        if (fe.IsAggregate) writer.WriteBoolean("IsAggregate", true);
                        break;
                    case NotExpr ne2:
                        if (ne2.Operand is not null)
                        {
                            writer.WritePropertyName("Operand");
                            WriteExpr(writer, ne2.Operand, options);
                        }
                        break;
                    case UnaryExpr ue:
                        writer.WritePropertyName(ue.Operator.ToString());
                        WriteExpr(writer, ue.Operand, options);
                        break;
                    case GenericSqlExpr ge:
                        if (ge.Key is not null) writer.WriteString("Key", ge.Key);
                        if (ge.Arg is not null)
                        {
                            writer.WritePropertyName("Arg");
                            JsonSerializer.Serialize(writer, ge.Arg, options);
                        }
                        break;
                    case ValueExpr vve:
                        if (vve.Value is not null)
                        {
                            writer.WritePropertyName("Value");
                            JsonSerializer.Serialize(writer, vve.Value, options);
                        }
                        break;
                    case ForeignExpr fe:
                        if (fe.Foreign is not null)
                        {
                            writer.WritePropertyName("$");
                            writer.WriteStringValue(fe.Foreign.FullName);
                        }
                        if (!string.IsNullOrEmpty(fe.Alias))
                        {
                            writer.WritePropertyName("Alias");
                            writer.WriteStringValue(fe.Alias);
                        }
                        if (fe.AutoRelated)
                        {
                            writer.WriteBoolean("AutoRelated", true);
                        }
                        if (fe.InnerExpr is not null)
                        {
                            writer.WritePropertyName("InnerExpr");
                            WriteExpr(writer, fe.InnerExpr, options);
                        }
                        if (fe.TableArgs != null && fe.TableArgs.Length > 0)
                        {
                            writer.WritePropertyName("TableArgs");
                            JsonSerializer.Serialize(writer, fe.TableArgs, options);
                        }
                        break;
                    case OrderByItemExpr oie:
                        writer.WritePropertyName("Field");
                        JsonSerializer.Serialize(writer, oie.Field, options);
                        writer.WriteBoolean("Asc", oie.Ascending);
                        break;
                    case SelectItemExpr sie:
                        if (!string.IsNullOrEmpty(sie.Alias))
                        {
                            writer.WritePropertyName("Alias");
                            writer.WriteStringValue(sie.Alias);
                        }
                        writer.WritePropertyName("Value");
                        WriteExpr(writer, sie.Value, options);
                        break;
                    case TableJoinExpr tje:
                        if (tje.Source != null)
                        {
                            writer.WritePropertyName("Source");
                            WriteExpr(writer, tje.Source, options);
                        }
                        if (tje.On != null)
                        {
                            writer.WritePropertyName("On");
                            WriteExpr(writer, tje.On, options);
                        }
                        if (tje.JoinType != TableJoinType.Left)
                        {
                            writer.WritePropertyName("JoinType");
                            writer.WriteStringValue(tje.JoinType.ToString());
                        }
                        break;
                }
                writer.WriteEndObject();
            }

            private static readonly Dictionary<Type, string> _typeToMark = new Dictionary<Type, string>
            {
                { typeof(FromExpr), "from" },
                { typeof(TableExpr), "table" },
                { typeof(TableJoinExpr), "join" },
                { typeof(WhereExpr), "where" },
                { typeof(OrderByExpr), "orderby" },
                { typeof(GroupByExpr), "group" },
                { typeof(HavingExpr), "having" },
                { typeof(SectionExpr), "section" },
                { typeof(SelectExpr), "select" },
                { typeof(DeleteExpr), "delete" },
                { typeof(UpdateExpr), "update" }
            };

            /// <summary>
            /// 写入 SQL 片段
            /// </summary>
            private void WriteSqlSegment(Utf8JsonWriter writer, SqlSegment value, JsonSerializerOptions options)
            {
                if (value == null) { writer.WriteNullValue(); return; }
                writer.WriteStartObject();

                string mark = _typeToMark.TryGetValue(value.GetType(), out string m) ? m : value.GetType().Name.Replace("Expr", "").ToLower();
                writer.WritePropertyName("$" + mark);
                if (value is TableExpr te)
                {
                    writer.WriteStringValue(te.Type?.FullName);
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
                            foreach (var item in sele.Selects)
                            {
                                // 优化序列化格式：SelectItemExpr 使用属性方式序列化 Alias 和 Value
                                writer.WriteStartObject();
                                if (!string.IsNullOrEmpty(item.Alias))
                                {
                                    writer.WritePropertyName("Alias");
                                    writer.WriteStringValue(item.Alias);
                                }
                                writer.WritePropertyName("Value");
                                WriteExpr(writer, item.Value, options);
                                writer.WriteEndObject();
                            }
                            writer.WriteEndArray();
                        }
                        break;
                    case TableExpr te2:
                        if (te2.TableArgs != null && te2.TableArgs.Length > 0)
                        {
                            writer.WritePropertyName("TableArgs");
                            JsonSerializer.Serialize(writer, te2.TableArgs, options);
                        }
                        if (!String.IsNullOrEmpty(te2.Alias))
                        {
                            writer.WritePropertyName("Alias");
                            writer.WriteStringValue(te2.Alias);
                        }
                        break;
                    case TableJoinExpr tje:
                        if (tje.On != null)
                        {
                            writer.WritePropertyName("On");
                            WriteExpr(writer, tje.On, options);
                        }
                        if (tje.JoinType != TableJoinType.Left)
                        {
                            writer.WritePropertyName("JoinType");
                            writer.WriteStringValue(tje.JoinType.ToString());
                        }
                        break;
                    case WhereExpr we:
                        if (we.Where is not null) { writer.WritePropertyName("Where"); WriteExpr(writer, we.Where, options); }
                        break;
                    case OrderByExpr obe:
                        if (obe.OrderBys?.Count > 0)
                        {
                            writer.WritePropertyName("OrderBys");
                            writer.WriteStartArray();
                            foreach (var ob in obe.OrderBys)
                            {
                                writer.WriteStartObject();
                                writer.WritePropertyName("Field");
                                WriteExpr(writer, ob.Field, options);
                                if (ob.Ascending != true)
                                {
                                    writer.WritePropertyName("Asc");
                                    writer.WriteBooleanValue(ob.Ascending);
                                }
                                writer.WriteEndObject();
                            }
                            writer.WriteEndArray();
                        }
                        break;
                    case GroupByExpr gbe:
                        if (gbe.GroupBys?.Count > 0)
                        {
                            writer.WritePropertyName("GroupBys");
                            writer.WriteStartArray();
                            foreach (var gb in gbe.GroupBys)
                            {
                                WriteExpr(writer, gb, options);
                            }
                            writer.WriteEndArray();
                        }
                        break;
                    case HavingExpr he:
                        if (he.Having is not null) { writer.WritePropertyName("Having"); WriteExpr(writer, he.Having, options); }
                        break;
                    case SectionExpr se:
                        if (se.Skip != 0) writer.WriteNumber("Skip", se.Skip);
                        if (se.Take != 0) writer.WriteNumber("Take", se.Take);
                        break;
                    case FromExpr fe:
                        if (fe.Joins?.Count > 0)
                        {
                            writer.WritePropertyName("Joins");
                            writer.WriteStartArray();
                            foreach (var join in fe.Joins) JsonSerializer.Serialize(writer, join, options);
                            writer.WriteEndArray();
                        }
                        break;
                }
                writer.WriteEndObject();
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
                            if (reader.TokenType != JsonTokenType.PropertyName) continue;
                            string prop = reader.GetString();
                            reader.Read();
                            dict[prop ?? string.Empty] = ReadNative(ref reader, options);
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

            private LogicBinaryExpr ReadLogicBinary(ref Utf8JsonReader reader, JsonSerializerOptions options, LogicOperator op = default)
            {
                LogicOperator finalOp = op == default ? LogicOperator.Equal : op;
                ValueTypeExpr left = null;
                ValueTypeExpr right = null;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    if (reader.ValueTextEquals("Left"))
                    {
                        reader.Read();
                        left = JsonSerializer.Deserialize<Expr>(ref reader, options).AsValue();
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
                        right = JsonSerializer.Deserialize<Expr>(ref reader, options).AsValue();
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }

                return new LogicBinaryExpr(left, finalOp, right);
            }

            private ValueBinaryExpr ReadValueBinary(ref Utf8JsonReader reader, JsonSerializerOptions options, ValueOperator op = default)
            {
                ValueOperator finalOp = op == default ? ValueOperator.Add : op;
                ValueTypeExpr left = null;
                ValueTypeExpr right = null;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    if (reader.ValueTextEquals("Left"))
                    {
                        reader.Read();
                        left = JsonSerializer.Deserialize<Expr>(ref reader, options).AsValue();
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
                        right = JsonSerializer.Deserialize<Expr>(ref reader, options).AsValue();
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }

                return new ValueBinaryExpr(left, finalOp, right);
            }

            private AndExpr ReadAndExpr(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                List<LogicExpr> items = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        items = JsonSerializer.Deserialize<List<LogicExpr>>(ref reader, options);
                    }
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    string prop = reader.GetString() ?? string.Empty;
                    if (prop == "Items")
                    {
                        reader.Read();
                        items = JsonSerializer.Deserialize<List<LogicExpr>>(ref reader, options);
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                return new AndExpr(items);
            }

            private OrExpr ReadOrExpr(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                List<LogicExpr> items = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        items = JsonSerializer.Deserialize<List<LogicExpr>>(ref reader, options);
                    }
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    string prop = reader.GetString() ?? string.Empty;
                    if (prop == "Items")
                    {
                        reader.Read();
                        items = JsonSerializer.Deserialize<List<LogicExpr>>(ref reader, options);
                    }
                    else
                    {
                        reader.Skip();
                    }
                }

                return new OrExpr(items);
            }

            private ValueSet ReadValueSet(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                ValueJoinType joinType = ValueJoinType.List;
                List<ValueTypeExpr> items = null;

                bool success = false;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;

                    string prop = reader.GetString() ?? string.Empty;
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
                    string prop = reader.GetString() ?? string.Empty;
                    if (prop == "FunctionName")
                    {
                        reader.Read();
                        fe.FunctionName = reader.GetString();
                        continue;
                    }
                    else if (prop == "Args")
                    {
                        reader.Read();
                        var parameters = JsonSerializer.Deserialize<List<Expr>>(ref reader, options);
                        if (parameters != null) fe.Args.AddRange(parameters.Cast<ValueTypeExpr>());
                    }
                    else if (prop == "IsAggregate")
                    {
                        reader.Read();
                        fe.IsAggregate = reader.GetBoolean();
                    }
                    else
                    {
                        if (fe.FunctionName == null)
                        {
                            fe.FunctionName = prop;
                            reader.Read();
                            var parameters = JsonSerializer.Deserialize<List<Expr>>(ref reader, options);
                            if (parameters != null) fe.Args.AddRange(parameters.Cast<ValueTypeExpr>());
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
                    string prop = reader.GetString() ?? string.Empty;
                    if (prop == "$")
                    {
                        reader.Read();
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
                            foreignExpr.Foreign = type;
                        }
                    }
                    else if (prop == "Alias")
                    {
                        reader.Read();
                        foreignExpr.Alias = reader.GetString();
                    }
                    else if (prop == "AutoRelated")
                    {
                        reader.Read();
                        foreignExpr.AutoRelated = reader.GetBoolean();
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
                        reader.Read();
                        reader.Skip();
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
                    string prop = reader.GetString() ?? string.Empty;
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
                    string prop = reader.GetString() ?? string.Empty;
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
                        operand = JsonSerializer.Deserialize<Expr>(ref reader, options).AsValue();
                    }
                    else
                    {
                        if (!success && Enum.TryParse<UnaryOperator>(prop, out var parsedOp))
                        {
                            success = true;
                            op = parsedOp;
                            reader.Read();
                            operand = JsonSerializer.Deserialize<Expr>(ref reader, options).AsValue();
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
