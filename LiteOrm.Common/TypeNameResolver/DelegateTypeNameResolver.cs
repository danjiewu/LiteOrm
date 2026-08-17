using System;
using System.Diagnostics.CodeAnalysis;

namespace LiteOrm.Common
{
    /// <summary>
    /// 通过委托构造的类型名称解析器。允许用户提供任意自定义的正反向解析逻辑。
    /// <para>
    /// 适用于测试或需要精确控制类型映射的场景（如多个同名类型存在于不同命名空间时）。
    /// </para>
    /// </summary>
    public class DelegateTypeNameResolver : ITypeNameResolver
    {
        private readonly Func<Type, string> _getName;
        private readonly Func<string, Type?> _getType;

        /// <summary>
        /// 初始化 <see cref="DelegateTypeNameResolver"/> 类的新实例。
        /// </summary>
        /// <param name="getName">类型 → 名称 转换委托。</param>
        /// <param name="getType">名称 → 类型 转换委托（未找到返回 null）。</param>
        public DelegateTypeNameResolver(Func<Type, string> getName, Func<string, Type?> getType)
        {
            _getName = getName ?? throw new ArgumentNullException(nameof(getName));
            _getType = getType ?? throw new ArgumentNullException(nameof(getType));
        }

        /// <inheritdoc />
        public string GetName(Type type)
            => _getName(type);

        /// <inheritdoc />
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2073", Justification = "The Type returned by the delegate is user-supplied; callers must ensure annotation requirements. Under AOT, users should use the pre-registration path.")]
#endif
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)]
        public Type? GetType(string name)
            => _getType(name);
    }
}
