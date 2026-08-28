// Polyfill for DynamicallyAccessedMembersAttribute on older target frameworks.
// These attributes exist natively on net5.0+; this file provides no-op stubs for netstandard2.0/2.1.

#if !NET5_0_OR_GREATER

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Polyfill for .NET 5+ DynamicallyAccessedMembersAttribute.
    /// On netstandard2.0/2.1 this is a no-op attribute that does not affect trimming.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct | AttributeTargets.GenericParameter, Inherited = false)]
    internal sealed class DynamicallyAccessedMembersAttribute : Attribute
    {
        public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes)
        {
            MemberTypes = memberTypes;
        }

        public DynamicallyAccessedMemberTypes MemberTypes { get; }
    }

    /// <summary>
    /// Polyfill for .NET 5+ DynamicallyAccessedMemberTypes enum.
    /// </summary>
    [Flags]
    public enum DynamicallyAccessedMemberTypes
    {
        /// <summary>不指定任何成员。</summary>
        None = 0,
        /// <summary>公开的无参构造函数。</summary>
        PublicParameterlessConstructor = 0x0001,
        /// <summary>全部公开构造函数。</summary>
        PublicConstructors = 0x0002,
        /// <summary>全部非公开构造函数。</summary>
        NonPublicConstructors = 0x0004,
        /// <summary>公开方法。</summary>
        PublicMethods = 0x0008,
        /// <summary>非公开方法。</summary>
        NonPublicMethods = 0x0010,
        /// <summary>公开字段。</summary>
        PublicFields = 0x0020,
        /// <summary>非公开字段。</summary>
        NonPublicFields = 0x0040,
        /// <summary>公开属性。</summary>
        PublicProperties = 0x0080,
        /// <summary>非公开属性。</summary>
        NonPublicProperties = 0x0100,
        /// <summary>公开事件。</summary>
        PublicEvents = 0x0200,
        /// <summary>非公开事件。</summary>
        NonPublicEvents = 0x0400,
        /// <summary>嵌套类型。</summary>
        NestedTypes = 0x0800,
        /// <summary>接口及其默认实现。</summary>
        Interfaces = 0x1000,
        /// <summary>所有成员。</summary>
        All = ~None,
    }
}

#if !NET7_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Polyfill for .NET 7+ RequiresDynamicCodeAttribute.
    /// On netstandard2.0/2.1 this is a no-op attribute that does not affect AOT analysis.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    internal sealed class RequiresDynamicCodeAttribute : Attribute
    {
        public RequiresDynamicCodeAttribute(string message) { Message = message; }
        public string Message { get; }
        public string? Url { get; set; }
    }
}
#endif

#if !NETCOREAPP3_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Polyfill for .NET Core 3.0+ RequiresUnreferencedCodeAttribute.
    /// On netstandard2.0/2.1 this is a no-op attribute that does not affect trimming.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    internal sealed class RequiresUnreferencedCodeAttribute : Attribute
    {
        public RequiresUnreferencedCodeAttribute(string message) { Message = message; }
        public string Message { get; }
        public string? Url { get; set; }
    }
}
#endif

#if !NET5_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Polyfill for .NET 5+ UnconditionalSuppressMessageAttribute.
    /// On netstandard2.0/2.1 this is a no-op attribute that does not affect trimming.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    internal sealed class UnconditionalSuppressMessageAttribute : Attribute
    {
        public UnconditionalSuppressMessageAttribute(string category, string checkId) { Category = category; CheckId = checkId; }
        public string Category { get; }
        public string CheckId { get; }
        public string Scope { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public string Justification { get; set; } = string.Empty;
    }
}
#endif
#endif