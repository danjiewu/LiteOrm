// Polyfill for RuntimeFeature on older target frameworks.
// These attributes exist natively on net5.0+; this file provides no-op stubs for netstandard2.0/2.1.

#if !NET5_0_OR_GREATER && !NETSTANDARD2_1_OR_GREATER

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill for .NET 5+ RuntimeFeature.IsDynamicCodeSupported.
    /// On frameworks that lack this property, dynamic code is always supported (no AOT).
    /// </summary>
    internal static class RuntimeFeature
    {
        public static bool IsDynamicCodeSupported => true;
    }
}

#endif
