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
    public sealed class DynamicallyAccessedMembersAttribute : Attribute
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
        None = 0,
        PublicParameterlessConstructor = 0x0001,
        PublicConstructors = 0x0002,
        NonPublicConstructors = 0x0004,
        PublicMethods = 0x0008,
        NonPublicMethods = 0x0010,
        PublicFields = 0x0020,
        NonPublicFields = 0x0040,
        PublicProperties = 0x0080,
        NonPublicProperties = 0x0100,
        PublicEvents = 0x0200,
        NonPublicEvents = 0x0400,
        NestedTypes = 0x0800,
        All = ~None,
    }
}
#endif
