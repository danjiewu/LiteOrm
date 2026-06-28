using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace LiteOrm.Service
{
    /// <summary>
    /// <see cref="RemoteInvocationRequest"/> 的自定义 JSON 转换器。
    /// 仅负责序列化（Write），将 <see cref="RemoteInvocationRequest.Method"/>（<see cref="MethodInfo"/>）
    /// 按名称序列化为 JSON 字符串属性 <c>"Method"</c>，并使用方法参数类型对 <see cref="RemoteInvocationRequest.Arguments"/> 进行类型感知序列化。
    /// <para>
    /// 反序列化（Read）不在此处完成 <see cref="MethodInfo"/> 的解析——转换器仅读取
    /// <see cref="RemoteInvocationRequest.ServiceName"/> 与 <see cref="RemoteInvocationRequest.Arguments"/>（元素为 <see cref="JsonElement"/>）。
    /// 完整的反序列化由 <see cref="RemoteServiceDispatcher.ParseRequest"/> 完成：
    /// 先根据 <see cref="RemoteInvocationRequest.ServiceName"/> 匹配服务类型，再按方法名查找 <see cref="MethodInfo"/>，
    /// 最后按方法参数类型反序列化 <see cref="RemoteInvocationRequest.Arguments"/>。
    /// </para>
    /// <para>
    /// 序列化规则（<see cref="RemoteInvocationRequest.Arguments"/>）：
    /// 1. 实参运行时类型与参数声明类型相同，或参数声明类型为 <see cref="Common.Expr"/> 派生类 → 直接使用实参类型序列化，无额外类型信息；
    /// 2. 类型不一致 → 以 <c>{"$type":"实际类型名","$value":<值>}</c> 结构包装。
    /// </para>
    /// </summary>
    public sealed class RemoteInvocationRequestConverter : JsonConverter<RemoteInvocationRequest>
    {
        private static readonly Type ExprType = typeof(Common.Expr);

        /// <inheritdoc />
        public override RemoteInvocationRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject for RemoteInvocationRequest");

            var request = new RemoteInvocationRequest();
            var argumentsRaw = default(JsonElement?);

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    continue;

                var propName = reader.GetString();
                reader.Read();

                switch (propName)
                {
                    case "ServiceName":
                        request.ServiceName = reader.GetString();
                        break;
                    case "Method":
                        // Method (MethodInfo) 按名称序列化，反序列化时由 dispatcher.ParseRequest 解析
                        // 此处仅跳过值（不赋值给 Method，因为无法在此解析 MethodInfo）
                        JsonDocument.ParseValue(ref reader).Dispose();
                        break;
                    case "Arguments":
                        argumentsRaw = JsonDocument.ParseValue(ref reader).RootElement.Clone();
                        break;
                }
            }

            // Arguments 元素以 JsonElement 形式保存，
            // 由服务端 dispatcher.ParseRequest 在查找方法后按参数类型二次反序列化
            if (argumentsRaw.HasValue && argumentsRaw.Value.ValueKind == JsonValueKind.Array)
            {
                var argList = new System.Collections.Generic.List<object>();
                foreach (var element in argumentsRaw.Value.EnumerateArray())
                    argList.Add(DeserializeArgument(element, null, options));
                request.Arguments = argList.ToArray();
            }

            return request;
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, RemoteInvocationRequest value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString("ServiceName", value.ServiceName);

            // Method (MethodInfo) 按名称序列化
            writer.WriteString("Method", value.Method?.Name);

            writer.WritePropertyName("Arguments");
            writer.WriteStartArray();

            // 参数声明类型从 Method（MethodInfo）提取（过滤 CancellationToken）
            var paramTypes = ResolveParameterTypes(value.Method);
            var arguments = value.Arguments ?? Array.Empty<object>();
            for (int i = 0; i < arguments.Length; i++)
            {
                var arg = arguments[i];
                Type declaredType = i < paramTypes.Length ? paramTypes[i] : null;
                WriteArgument(writer, arg, declaredType, options);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        /// <summary>
        /// 从 <see cref="MethodInfo"/> 提取参数类型（不含 <see cref="CancellationToken"/>）。
        /// </summary>
        private static Type[] ResolveParameterTypes(MethodInfo method)
        {
            if (method is null) return Array.Empty<Type>();
            return method.GetParameters()
                .Where(p => p.ParameterType != typeof(CancellationToken))
                .Select(p => p.ParameterType)
                .ToArray();
        }

        /// <summary>
        /// 序列化单个参数。类型一致或 Expr 参数 → 直接序列化；类型不一致 → 包装为 $type/$value。
        /// </summary>
        private static void WriteArgument(Utf8JsonWriter writer, object arg, Type declaredType, JsonSerializerOptions options)
        {
            if (arg is null)
            {
                writer.WriteNullValue();
                return;
            }

            var actualType = arg.GetType();

            // 类型一致，或声明类型为 Expr 派生类 → 直接序列化，无额外类型信息
            if (declaredType != null && (declaredType == actualType || ExprType.IsAssignableFrom(declaredType)))
            {
                JsonSerializer.Serialize(writer, arg, actualType, options);
                return;
            }

            // 类型不一致 → 包装
            writer.WriteStartObject();
            writer.WriteString("$type", actualType.AssemblyQualifiedName);
            writer.WritePropertyName("$value");
            JsonSerializer.Serialize(writer, arg, actualType, options);
            writer.WriteEndObject();
        }

        /// <summary>
        /// 反序列化单个参数。含 $type → 按实际类型反序列化；否则返回原始 <see cref="JsonElement"/>，
        /// 由服务端 dispatcher 按方法参数声明类型二次反序列化。
        /// </summary>
        private static object DeserializeArgument(JsonElement element, Type declaredType, JsonSerializerOptions options)
        {
            if (element.ValueKind == JsonValueKind.Null)
                return declaredType != null && declaredType.IsValueType
                    ? Activator.CreateInstance(declaredType)
                    : null;

            // 检查是否为 $type 包装
            if (element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty("$type", out var typeProp))
            {
                var typeName = typeProp.GetString();
                var actualType = Type.GetType(typeName);
                if (actualType != null && element.TryGetProperty("$value", out var valueProp))
                    return JsonSerializer.Deserialize(valueProp.GetRawText(), actualType, options);
            }

            // 按声明类型反序列化（若有）；否则返回原始 JsonElement
            if (declaredType != null)
                return JsonSerializer.Deserialize(element.GetRawText(), declaredType, options);

            return element.Clone();
        }
    }
}
