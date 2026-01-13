using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{

    /// <summary>
    /// 表达式 JSON 转换器工厂
    /// </summary>
    internal class ExprJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(Expr);
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            return new ExprJsonConverter();
        }

        private class ExprJsonConverter : JsonConverter<Expr>
        {
            private static readonly JsonSerializerOptions _compactOptions = new JsonSerializerOptions { WriteIndented = false };

            public override Expr Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null) return null;

                if (reader.TokenType != JsonTokenType.StartObject)
                    return new ValueExpr(ReadNative(ref reader, options));

                // 嗅探对象类型
                Utf8JsonReader tempReader = reader;
                tempReader.Read(); // 移动到第一个属性名
                if (tempReader.TokenType != JsonTokenType.PropertyName)
                    return new ValueExpr(ReadNative(ref reader, options));

                string firstProp = tempReader.GetString();
                if (firstProp == "@")
                {
                    reader.Read(); // 跳过 @
                    reader.Read(); // 读取属性值
                    string propName = reader.GetString();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject) ;
                    return new PropertyExpr(propName);
                }

                if (firstProp == "$")
                {
                    reader.Read(); // 跳过 $
                    reader.Read(); // 读取 typeMark
                    string mark = reader.GetString();
                    return mark switch
                    {
                        "bin" => ReadBinary(ref reader, options),
                        "set" => ReadSet(ref reader, options),
                        "func" => ReadFunction(ref reader, options),
                        "prop" => ReadProperty(ref reader, options),
                        "unary" => ReadUnary(ref reader, options),
                        "sql" => ReadSql(ref reader, options),
                        "value" => ReadValueBody(ref reader, options),
                        _ => new ValueExpr(ReadNative(ref reader, options))
                    };
                }

                // 回退逻辑
                return new ValueExpr(ReadNative(ref reader, options));
            }

            private object ReadNative(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                var val = JsonSerializer.Deserialize<object>(ref reader, options);
                return ToNative(val);
            }

            private static object ToNative(object obj)
            {
                if (obj is JsonElement ele)
                {
                    switch (ele.ValueKind)
                    {
                        case JsonValueKind.Object:
                            var dict = new Dictionary<string, object>();
                            foreach (var p in ele.EnumerateObject()) dict[p.Name] = ToNative(p.Value);
                            return dict;
                        case JsonValueKind.Array:
                            var list = new List<object>();
                            foreach (var item in ele.EnumerateArray()) list.Add(ToNative(item));
                            return list;
                        case JsonValueKind.String: return ele.GetString();
                        case JsonValueKind.Number:
                            if (ele.TryGetInt64(out long l)) return l;
                            return ele.GetDouble();
                        case JsonValueKind.True: return true;
                        case JsonValueKind.False: return false;
                        case JsonValueKind.Null: return null;
                    }
                }
                return obj;
            }

            private BinaryExpr ReadBinary(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                var be = new BinaryExpr();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string prop = reader.GetString();
                    reader.Read();
                    if (prop == "Left") be.Left = JsonSerializer.Deserialize<Expr>(ref reader, options);
                    else if (prop == "Operator") be.Operator = JsonSerializer.Deserialize<BinaryOperator>(ref reader, options);
                    else if (prop == "Right") be.Right = JsonSerializer.Deserialize<Expr>(ref reader, options);
                }
                return be;
            }

            private ExprSet ReadSet(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                var set = new ExprSet();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string prop = reader.GetString();
                    if (Enum.TryParse<ExprJoinType>(prop, out var jt))
                    {
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
                return set;
            }

            private FunctionExpr ReadFunction(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                var fe = new FunctionExpr();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    fe.FunctionName = reader.GetString();
                    reader.Read();        
                    fe.Parameters = JsonSerializer.Deserialize<List<Expr>>(ref reader, options);
                }
                return fe;
            }

            private PropertyExpr ReadProperty(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                string name = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string prop = reader.GetString();
                    reader.Read();
                    if (prop == "PropertyName") name = reader.GetString();
                }
                return new PropertyExpr(name);
            }

            private UnaryExpr ReadUnary(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                var ue = new UnaryExpr();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string prop = reader.GetString();
                    reader.Read();
                    if (prop == "Operator") ue.Operator = JsonSerializer.Deserialize<UnaryOperator>(ref reader, options);
                    else if (prop == "Operand") ue.Operand = JsonSerializer.Deserialize<Expr>(ref reader, options);
                }
                return ue;
            }

            private GenericSqlExpr ReadSql(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                var ge = new GenericSqlExpr();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string prop = reader.GetString();
                    reader.Read();
                    if (prop == "Key") ge.Key = reader.GetString();
                }
                return ge;
            }

            private ValueExpr ReadValueBody(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                object val = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    string prop = reader.GetString();
                    reader.Read();
                    if (prop == "Value") val = ReadNative(ref reader, options);
                }
                return new ValueExpr(val);
            }

            public override void Write(Utf8JsonWriter writer, Expr value, JsonSerializerOptions options)
            {
                if (value is null)
                {
                    writer.WriteNullValue();
                    return;
                }

                if (value is ValueExpr ve)
                {
                    // 对纯值表达式强制单行，优化 IN [1,2,3] 的显示
                    writer.WriteRawValue(JsonSerializer.Serialize(ve.Value, _compactOptions));
                    return;
                }

                if (value is PropertyExpr pe)
                {
                    writer.WriteStartObject();
                    writer.WriteString("@", pe.PropertyName);
                    writer.WriteEndObject();
                    return;
                }

                writer.WriteStartObject();
                string mark = value switch
                {
                    BinaryExpr => "bin",
                    ExprSet => "set",
                    FunctionExpr => "func",
                    UnaryExpr => "unary",
                    GenericSqlExpr => "sql",
                    _ => value.GetType().Name.Replace("Expr", "").ToLower()
                };
                writer.WriteString("$", mark);

                switch (value)
                {
                    case BinaryExpr be:
                        writer.WritePropertyName("Left");
                        JsonSerializer.Serialize(writer, be.Left, options);
                        writer.WritePropertyName("Operator");
                        JsonSerializer.Serialize(writer, be.Operator, options);
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
                        writer.WritePropertyName("Operator");
                        JsonSerializer.Serialize(writer, ue.Operator, options);
                        writer.WritePropertyName("Operand");
                        JsonSerializer.Serialize(writer, ue.Operand, options);
                        break;
                    case GenericSqlExpr ge:
                        writer.WriteString("Key", ge.Key);
                        break;
                }
                writer.WriteEndObject();
            }
        }
    }
}
