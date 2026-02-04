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
    internal class SqlSegmentJsonConverterFactory : JsonConverterFactory
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
                typeof(SqlSegmentJsonConverter<>).MakeGenericType(typeToConvert));
        }

        /// <summary>
        /// 针对 Expr 及其子类的自定义 JSON 序列化器。
        /// 采用紧凑型设计，支持字面量直接序列化（ValueExpr）和带类型标识符（$）的复杂转换。
        /// </summary>
        private class SqlSegmentJsonConverter<T> : JsonConverter<T> where T : SqlSegment
        {
            // 使用非转义编码器，避免 HTML 字符被转义，提高 SQL 表达式的可读性
            private static readonly JsonSerializerOptions _compactOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            private static readonly Dictionary<string, Type> _markToType = new(StringComparer.OrdinalIgnoreCase)
            {
                { "table", typeof(TableExpr) },
                { "where", typeof(WhereExpr) },
                { "order", typeof(OrderByExpr) },
                { "group", typeof(GroupByExpr) },
                { "having", typeof(HavingExpr) },
                { "section", typeof(SectionExpr) },
                { "select", typeof(SelectExpr) },
                { "delete", typeof(DeleteExpr) },
                { "update", typeof(UpdateExpr) }
            };

            private static readonly Dictionary<Type, string> _typeToMark = _markToType.ToDictionary(kv => kv.Value, kv => kv.Key);

            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null) return null;
                if (reader.TokenType != JsonTokenType.StartObject) return null;

                SqlSegment result = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string propName = reader.GetString();

                    if (propName.StartsWith("$"))
                    {
                        string mark = propName == "$" ? null : propName.Substring(1);
                        if (mark == null)
                        {
                            reader.Read();
                            mark = reader.GetString().ToLower();
                        }

                        result = mark switch
                        {
                            "table" => new TableExpr(),
                            "where" => new WhereExpr(),
                            "order" => new OrderByExpr(),
                            "group" => new GroupByExpr(),
                            "having" => new HavingExpr(),
                            "section" => new SectionExpr(),
                            "select" => new SelectExpr(),
                            "delete" => new DeleteExpr(),
                            "update" => new UpdateExpr(),
                            _ => null
                        };

                        if (result != null)
                        {
                            if (propName != "$")
                            {
                                reader.Read();
                                if (result is TableExpr te)
                                {
                                    string typeName = reader.GetString();
                                    if (!string.IsNullOrEmpty(typeName))
                                        te.Table = TableInfoProvider.Default.GetTableView(Type.GetType(typeName));
                                }
                                else
                                {
                                    result.Source = JsonSerializer.Deserialize<Expr>(ref reader, options) as SqlSegment;
                                }
                            }
                        }
                    }
                    else if (result != null)
                    {
                        ReadProperty(ref reader, result, propName, options);
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }
                return (T)result;
            }

            private void ReadProperty(ref Utf8JsonReader reader, SqlSegment result, string propName, JsonSerializerOptions options)
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
                            ValueTypeExpr expr = null;
                            bool asc = true;
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
                        sele.Selects.AddRange(JsonSerializer.Deserialize<List<Expr>>(ref reader, options).Cast<ValueTypeExpr>());
                        break;
                    case UpdateExpr ue when propName == "Sets":
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            string prop = null;
                            ValueTypeExpr val = null;
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                            {
                                if (reader.TokenType != JsonTokenType.PropertyName) continue;
                                if (reader.ValueTextEquals("Prop")) { reader.Read(); prop = reader.GetString(); }
                                else if (reader.ValueTextEquals("Value")) { reader.Read(); val = JsonSerializer.Deserialize<Expr>(ref reader, options) as ValueTypeExpr; }
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
                if (value == null) { writer.WriteNullValue(); return; }
                writer.WriteStartObject();

                string mark = _typeToMark.TryGetValue(value.GetType(), out string m) ? m : value.GetType().Name.Replace("Expr", "").ToLower();
                writer.WritePropertyName("$" + mark);
                if (value is TableExpr te)
                {
                    writer.WriteStringValue(te.Table?.DefinitionType.AssemblyQualifiedName);
                }
                else
                {
                    JsonSerializer.Serialize(writer, value.Source, options);
                }

                switch (value)
                {
                    case SelectExpr sele:
                        if (sele.Selects?.Count > 0) { writer.WritePropertyName("Selects"); JsonSerializer.Serialize(writer, sele.Selects, options); }
                        break;
                    case WhereExpr we:
                        if (we.Where != null) { writer.WritePropertyName("Where"); JsonSerializer.Serialize(writer, we.Where, options); }
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
                        if (he.Having != null) { writer.WritePropertyName("Having"); JsonSerializer.Serialize(writer, he.Having, options); }
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
                                writer.WriteString("Prop", set.Item1);
                                writer.WritePropertyName("Value");
                                JsonSerializer.Serialize(writer, set.Item2, options);
                                writer.WriteEndObject();
                            }
                            writer.WriteEndArray();
                        }
                        if (ue.Where != null) { writer.WritePropertyName("Where"); JsonSerializer.Serialize(writer, ue.Where, options); }
                        break;                    
                    case DeleteExpr de:
                        if (de.Where != null) { writer.WritePropertyName("Where"); JsonSerializer.Serialize(writer, de.Where, options); }
                        break;
                }
                writer.WriteEndObject();
            }
        }
    }
}
