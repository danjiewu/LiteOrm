using LiteOrm.Common;
using LiteOrm.Service;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace LiteOrm.Remote
{
    /// <summary>
    /// <see cref="RemoteInvocationRequest"/> 的自定义 JSON 转换器。
    /// 仅负责序列化（Write），将 <see cref="RemoteInvocationRequest.Method"/>（<see cref="MethodInfo"/>）
    /// 按名称序列化为 JSON 字符串属性 <c>"Method"</c>，并使用方法参数类型对 <see cref="RemoteInvocationRequest.Arguments"/> 进行类型感知序列化。
    /// <para>
    /// 反序列化（Read）不在此处完成 <see cref="MethodInfo"/> 的解析——转换器仅读取
    /// <see cref="RemoteInvocationRequest.ServiceName"/> 与 <see cref="RemoteInvocationRequest.Arguments"/>（元素为 <see cref="JsonElement"/>）。
    /// 完整的反序列化由 RemoteServiceDispatcher.ParseRequest 完成：
    /// 先根据 <see cref="RemoteInvocationRequest.ServiceName"/> 匹配服务类型，再按方法名查找 <see cref="MethodInfo"/>，
    /// 最后按方法参数类型反序列化 <see cref="RemoteInvocationRequest.Arguments"/>。
    /// </para>
    /// <para>
    /// 序列化规则（<see cref="RemoteInvocationRequest.Arguments"/>）：
    /// 1. 实参运行时类型与参数声明类型相同，或参数声明类型为 <see cref="Common.Expr"/> 派生类 → 直接使用实参类型序列化，无额外类型信息；
    /// 2. 类型不一致 → 以 <c>{"$type":"实际类型名","$value":&lt;值&gt;}</c> 结构包装。
    /// </para>
    /// </summary>
    public sealed class RemoteInvocationRequestConverter : JsonConverter<RemoteInvocationRequest>
    {
        private static readonly Type ExprType = typeof(Common.Expr);

        /// <summary>
        /// 静态默认命名空间。序列化时 <c>$type</c> 使用 <see cref="TypeResolverHelper.GetName(Type)"/> 生成短名，
        /// 反序列化时通过 <see cref="TypeResolverHelper.FindType(string, string?)"/> 解析，
        /// 当短名无法精确匹配时以此命名空间组合 <c>命名空间.类型名</c> 进行匹配。
        /// <para>
        /// 为 null 时 <see cref="TypeResolverHelper"/> 回退到全程序集短名扫描。
        /// </para>
        /// </summary>
        public static string? DefaultNamespace { get; set; }

        /// <summary>
        /// 自定义类型 → 名称转换委托。序列化 <c>$type</c> 时优先调用，返回非 null 且非空字符串则采用，
        /// 否则回退到 <see cref="TypeResolverHelper.GetName(Type)"/>。
        /// </summary>
        public static Func<Type, string?>? TypeNameResolver { get; set; }

        /// <summary>
        /// 自定义名称 → 类型转换委托。反序列化 <c>$type</c> 时优先调用，返回非 null 类型则采用，
        /// 否则回退到 <see cref="TypeResolverHelper.FindType(string, string?)"/>。
        /// </summary>
        public static Func<string, Type?>? TypeResolver { get; set; }

        /// <summary>
        /// 解析类型名称：委托优先，否则用 <see cref="TypeResolverHelper.GetName"/>。
        /// </summary>
        private static string ResolveTypeName(Type type)
            => TypeNameResolver?.Invoke(type) is string name && name.Length > 0
                ? name
                : TypeResolverHelper.GetName(type);

        /// <summary>
        /// 解析类型：委托优先；其次解析 typeMark 缩写（datetime/guid 等）和 enum: 前缀；
        /// 否则用 <see cref="TypeResolverHelper.FindType"/>（带 <see cref="DefaultNamespace"/>）。
        /// </summary>
        private static Type? ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            // 委托优先
            if (TypeResolver?.Invoke(typeName) is Type delegated)
                return delegated;

            // typeMark 缩写（datetime/datetimeoffset/timespan/guid/bytes）
            if (_markToType.TryGetValue(typeName, out var marked))
                return marked;

            // enum: 前缀 → 解析枚举类型名
            if (typeName.StartsWith(EnumMarkPrefix, StringComparison.Ordinal))
            {
                var enumTypeName = typeName.Substring(EnumMarkPrefix.Length);
                var enumType = TypeResolverHelper.FindType(enumTypeName, DefaultNamespace);
                return enumType?.IsEnum == true ? enumType : null;
            }

            return TypeResolverHelper.FindType(typeName, DefaultNamespace);
        }

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
                    argList.Add(DeserializeTypedValue(element, null, options));
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
                WriteTypedValue(writer, arg, declaredType, options);
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
        /// 序列化单个对象。类型一致或 Expr 参数 → 直接序列化；类型不一致 → 包装为 $type/$value。
        /// </summary>
        public static void WriteTypedValue(Utf8JsonWriter writer, object arg, Type declaredType, JsonSerializerOptions options)
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

            // 基础类型且声明类型也是基础类型 → 直接序列化。
            // 反序列化端 TryReadPrimitive 按声明类型（基础类型）兼容转换，无需 $type。
            if (declaredType != null && IsPrimitiveLike(actualType))
            {
                JsonSerializer.Serialize(writer, arg, actualType, options);
                return;
            }

            // 声明类型为泛型集合接口且实参为 List/Array → 直接序列化。
            // 反序列化端 ResolveCollectionConcreteType 会将接口还原为 List<T>，无需额外类型名称
            if (declaredType != null && IsCollectionInterface(declaredType) && IsListOrArray(actualType))
            {
                JsonSerializer.Serialize(writer, arg, actualType, options);
                return;
            }

            // 需要类型标记的基础类型（DateTime/DateTimeOffset/TimeSpan/Guid/byte[]/枚举）：
            // 声明类型无法推断实际类型（如 object）时，用缩写 typeMark 输出 $type，避免冗长完整类型名。
            if (TryGetTypeMark(actualType, out var typeMark))
            {
                writer.WriteStartObject();
                writer.WriteString("$type", typeMark);
                writer.WritePropertyName("$value");
                JsonSerializer.Serialize(writer, arg, actualType, options);
                writer.WriteEndObject();
                return;
            }

            // 类型不一致 → 包装。使用 ResolveTypeName 生成名称（委托优先），反序列化端通过 ResolveType 解析
            writer.WriteStartObject();
            writer.WriteString("$type", ResolveTypeName(actualType));
            writer.WritePropertyName("$value");
            JsonSerializer.Serialize(writer, arg, actualType, options);
            writer.WriteEndObject();
        }

        /// <summary>
        /// 类型 → 名称缩写映射。参考 <c>ExprJsonConverter._nativeTypeToJson</c>，
        /// 用于声明类型无法推断时（如 object）以简短标记携带类型信息。
        /// </summary>
        private static readonly Dictionary<Type, string> _typeToMark = new()
        {
            { typeof(DateTime), "datetime" },
            { typeof(DateTimeOffset), "datetimeoffset" },
            { typeof(TimeSpan), "timespan" },
            { typeof(Guid), "guid" },
            { typeof(byte[]), "bytes" }
        };

        /// <summary>
        /// 名称缩写 → 类型映射（反向查找）。
        /// </summary>
        private static readonly Dictionary<string, Type> _markToType = new(StringComparer.OrdinalIgnoreCase)
        {
            { "datetime", typeof(DateTime) },
            { "datetimeoffset", typeof(DateTimeOffset) },
            { "timespan", typeof(TimeSpan) },
            { "guid", typeof(Guid) },
            { "bytes", typeof(byte[]) }
        };

        /// <summary>
        /// 枚举类型标记前缀，后接枚举类型名（通过 <see cref="ResolveTypeName"/> 生成）。
        /// </summary>
        private const string EnumMarkPrefix = "enum:";

        /// <summary>
        /// 尝试获取类型的名称缩写标记。DateTime/DateTimeOffset/TimeSpan/Guid/byte[] 返回固定缩写；
        /// 枚举返回 <c>"enum:" + 类型名</c>；其他类型返回 false。
        /// </summary>
        private static bool TryGetTypeMark(Type type, out string mark)
        {
            if (_typeToMark.TryGetValue(type, out mark))
                return true;
            if (type.IsEnum)
            {
                mark = EnumMarkPrefix + ResolveTypeName(type);
                return true;
            }
            mark = null;
            return false;
        }

        /// <summary>
        /// 判断类型是否为泛型集合接口（<c>IEnumerable&lt;&gt;</c>/<c>ICollection&lt;&gt;</c>/<c>IList&lt;&gt;</c>/
        /// <c>IReadOnlyList&lt;&gt;</c>/<c>IReadOnlyCollection&lt;&gt;</c>）。
        /// </summary>
        public static bool IsCollectionInterface(Type type)
        {
            if (!type.IsGenericType) return false;
            var def = type.GetGenericTypeDefinition();
            return def == typeof(IEnumerable<>) ||
                   def == typeof(ICollection<>) ||
                   def == typeof(IList<>) ||
                   def == typeof(IReadOnlyList<>) ||
                   def == typeof(IReadOnlyCollection<>) ||
                   def == typeof(ISet<>) ||
#if NET5_0_OR_GREATER
                   def == typeof(IReadOnlySet<>) ||
#endif
                   def == typeof(IDictionary<,>) ||
                   def == typeof(IReadOnlyDictionary<,>);
        }

        /// <summary>
        /// 判断类型是否为 <see cref="List{T}"/> 或数组。
        /// </summary>
        public static bool IsListOrArray(Type type)
            => type.IsArray || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));

        /// <summary>
        /// 判断类型是否为"基础类型"——可由 JSON 直接无损表示、无需 <c>$type</c> 包装的类型。
        /// <para>
        /// 包含：基元类型（int/bool/double 等）、<see cref="string"/>、<see cref="decimal"/>、
        /// <see cref="DateTime"/>/<see cref="DateTimeOffset"/>/<see cref="TimeSpan"/>/<see cref="Guid"/>、
        /// 枚举，以及上述类型的可空（<see cref="Nullable{T}"/>）版本。
        /// </para>
        /// </summary>
        public static bool IsPrimitiveLike(Type type)
        {
            if (type.IsPrimitive || type.IsEnum) return true;
            if (type == typeof(string) || type == typeof(decimal)) return true;

            // 可空基础类型：Nullable<T>，T 为基础类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return IsPrimitiveLike(type.GetGenericArguments()[0]);

            return false;
        }

        /// <summary>
        /// 反序列化单个参数。含 $type → 按实际类型反序列化；否则返回原始 <see cref="JsonElement"/>，
        /// 由服务端 dispatcher 按方法参数声明类型二次反序列化。
        /// </summary>
        public static object DeserializeTypedValue(JsonElement element, Type declaredType, JsonSerializerOptions options)
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
                // 使用 ResolveType 解析类型名（委托优先，否则 TypeResolverHelper.FindType）
                var actualType = ResolveType(typeName);
                if (actualType != null && element.TryGetProperty("$value", out var valueProp))
                    return JsonSerializer.Deserialize(valueProp.GetRawText(), actualType, options);
            }

            // 按声明类型反序列化（若有）；否则返回原始 JsonElement
            if (declaredType != null)
            {
                // 声明类型为基础类型时，灵活读取 JSON 值并按声明类型转换，
                // 兼容值与目标类型不完全匹配的场景（如 int 参数收到 long 数值、double 参数收到 int）
                var underlying = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
                if ((underlying == typeof(object) || IsPrimitiveLike(underlying)) && TryReadPrimitive(element, underlying, out var primitiveValue))
                    return primitiveValue;

                // 声明类型为 IEnumerable/ICollection/IList（含泛型）时改用 List<T> 反序列化，
                // 避免直接反序列化为接口或抽象集合类型失败
                var concreteType = ResolveCollectionConcreteType(declaredType);
                return JsonSerializer.Deserialize(element.GetRawText(), concreteType, options);
            }

            return element.Clone();
        }

        /// <summary>
        /// 尝试从 <see cref="JsonElement"/> 读取基础类型值，并通过 <see cref="Convert.ChangeType"/> 转换为目标类型。
        /// <para>
        /// 参考 <c>ExprJsonConverter.ReadNative</c> 的逐级数值尝试策略，兼容 JSON 数值与目标数值类型不完全匹配的场景。
        /// 支持基元、string、decimal、DateTime、DateTimeOffset、TimeSpan、Guid、枚举。
        /// </para>
        /// </summary>
        /// <param name="element">JSON 元素。</param>
        /// <param name="targetType">目标基础类型（已去除 Nullable 包装）。</param>
        /// <param name="value">转换后的值。</param>
        /// <returns>是否成功读取并转换。</returns>
        private static bool TryReadPrimitive(JsonElement element, Type targetType, out object value)
        {
            value = null;
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    // 逐级尝试：int → long → decimal → double，取最贴近的值再 ChangeType 到目标类型
                    object number;
                    if (element.TryGetInt32(out int i)) number = i;
                    else if (element.TryGetInt64(out long l)) number = l;
                    else if (element.TryGetDecimal(out decimal d)) number = d;
                    else if (element.TryGetDouble(out double dbl)) number = dbl;
                    else return false;
                    value = number;
                    return true;
                case JsonValueKind.String:
                    var s = element.GetString();
                    if (s is null) return false;
                    // string 目标直接返回
                    if (targetType == typeof(string)) { value = s; return true; }
                    // char 目标
                    if (targetType == typeof(char))
                    {
                        if (s.Length == 1) { value = s[0]; return true; }
                        return false;
                    }
                    // 枚举：按名称或数值解析
                    if (targetType.IsEnum)
                    {
#if NETSTANDARD2_0
                        try
                        {
                            value = Enum.Parse(targetType, s, true);
                            return true;
                        }
                        catch { }
#else
                        if (Enum.TryParse(targetType, s, true, out var enumValue)) { value = enumValue; return true; }
#endif
                        // 数值字符串 → 枚举
                        if (long.TryParse(s, out var enumNum))
                        {
                            try { value = Enum.ToObject(targetType, enumNum); return true; } catch { return false; }
                        }
                        return false;
                    }
                    // Guid / TimeSpan / DateTime / DateTimeOffset：JSON 字符串 → 对应类型
                    if (targetType == typeof(Guid) && Guid.TryParse(s, out var guid)) { value = guid; return true; }
                    if (targetType == typeof(TimeSpan) && TimeSpan.TryParse(s, out var ts)) { value = ts; return true; }
                    if (targetType == typeof(DateTime) && DateTime.TryParse(s, out var dt)) { value = dt; return true; }
                    if (targetType == typeof(DateTimeOffset) && DateTimeOffset.TryParse(s, out var dto)) { value = dto; return true; }
                    value = s;
                    return true;

                case JsonValueKind.True:
                    value = true;
                    return true;

                case JsonValueKind.False:
                    value = false;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 解析集合声明类型对应的可实例化具体类型。
        /// <para>
        /// 声明类型为 <see cref="IEnumerable"/>/<see cref="ICollection"/>/<see cref="IList"/>
        /// 或其泛型版本（<c>IEnumerable&lt;T&gt;</c>/<c>ICollection&lt;T&gt;</c>/<c>IList&lt;T&gt;</c>）时，
        /// 返回 <see cref="List{T}"/>；其他类型原样返回。
        /// </para>
        /// </summary>
        /// <param name="declaredType">参数声明类型。</param>
        /// <returns>可用于反序列化的具体类型。</returns>
        private static Type ResolveCollectionConcreteType(Type declaredType)
        {
            if (declaredType.IsInterface)
            {
                if (declaredType.IsGenericType)
                {
                    var def = declaredType.GetGenericTypeDefinition();
                    var elementType = declaredType.GetGenericArguments();
                    if (def == typeof(IEnumerable<>) ||
                        def == typeof(ICollection<>) ||
                        def == typeof(IList<>) ||
                        def == typeof(IReadOnlyList<>) ||
                        def == typeof(IReadOnlyCollection<>))
                    {
                        return typeof(List<>).MakeGenericType(elementType);
                    }
                    else if (def == typeof(ISet<>)
#if NET5_0_OR_GREATER
                        || def == typeof(IReadOnlySet<>)
#endif
                        )
                    {
                        return typeof(HashSet<>).MakeGenericType(elementType);
                    }
                    else if (def == typeof(IDictionary<,>) || def == typeof(IReadOnlyDictionary<,>))
                    {
                        return typeof(Dictionary<,>).MakeGenericType(elementType);
                    }
                }
                else if (declaredType == typeof(IList) || declaredType == typeof(ICollection) || declaredType == typeof(IEnumerable))
                {
                    return typeof(List<object>);
                }
            }
            return declaredType;
        }
    }
}
