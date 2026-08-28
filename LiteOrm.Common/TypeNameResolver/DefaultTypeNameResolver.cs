using System;
using System.Diagnostics.CodeAnalysis;

namespace LiteOrm.Common
{
    /// <summary>
    /// <see cref="ITypeNameResolver"/> 的默认实现。
    /// <para>
    /// 正向（<see cref="GetName"/>）返回 <see cref="Type.FullName"/>（回退到 <see cref="System.Reflection.MemberInfo.Name"/>），
    /// 与原有序列化行为保持一致。
    /// </para>
    /// <para>
    /// 反向（<see cref="GetType"/>）委托 <see cref="TypeResolverHelper.FindType(string)"/>，
    /// 支持自定义注册、全名匹配、短名扫描，并自动缓存结果。
    /// </para>
    /// </summary>
    public class DefaultTypeNameResolver : ITypeNameResolver
    {
        /// <summary>
        /// 默认单例实例。
        /// </summary>
        public static readonly DefaultTypeNameResolver Instance = new();

        /// <inheritdoc />
        public string GetName(Type type)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));
            return type.FullName ?? type.Name;
        }

        /// <inheritdoc />
        [return: DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
        public Type? GetType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return TypeResolverHelper.FindType(name);
        }
    }
}
