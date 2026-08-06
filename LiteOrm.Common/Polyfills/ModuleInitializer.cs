// Polyfill for ModuleInitializerAttribute on older target frameworks.
// This attribute exists natively on net5.0+; this file provides a no-op stub for netstandard2.0/2.1.

#if !NET5_0_OR_GREATER

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill for .NET 5+ ModuleInitializerAttribute.
    /// On netstandard2.0/2.1 this is a no-op attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute
    {
    }
}

#endif
