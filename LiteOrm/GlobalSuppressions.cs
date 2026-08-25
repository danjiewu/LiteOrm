// Global trimming/AOT warning suppressions.
//
// LiteOrm core accesses framework properties such as PropertyInfo.PropertyType,
// SqlColumn.PropertyType, and ParameterExpression.Type. The Type returned by these
// properties has no [DynamicallyAccessedMembers] annotation, so the trimmer
// cannot statically track them. At runtime, however, entity properties are
// preserved via [Table] metadata registered with CommonTableInfoProvider by the
// TableInfoGenerator source generator.
//
// Under AOT, users must annotate all entity classes with [Table]; the source
// generator then emits property accessors and DataReader mappers at compile
// time, avoiding runtime reflection. This file only suppresses warnings that
// cannot be statically resolved through annotation chains; it does not mask
// genuine AOT incompatibilities (JIT-only paths are guarded by
// RuntimeFeature.IsDynamicCodeSupported).

using System.Diagnostics.CodeAnalysis;

[assembly: UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "PropertyInfo.PropertyType / SqlColumn.PropertyType / ParameterExpression.Type framework properties carry no annotation; entity properties are preserved via [Table] + source generator under AOT.")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "PropertyInfo.PropertyType / SqlColumn.PropertyType framework properties carry no annotation; entity properties are preserved via [Table] + source generator under AOT.")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "GetType() returns a Type without annotation; entity properties are preserved via [Table] + source generator under AOT.")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "Type.GetMethods / GetFields / GetInterfaces calls operate on a runtime Type; related types are preserved via [Table] + source generator under AOT.")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2091", Justification = "Generic parameter T annotations between derived types and base interfaces are not fully matched; entity properties are preserved via [Table] + source generator under AOT.")]
//[assembly: UnconditionalSuppressMessage("Trimming", "IL2087", Justification = "Generic parameter T annotations between derived types and base interfaces are not fully matched; entity properties are preserved via [Table] + source generator under AOT.")]
//[assembly: UnconditionalSuppressMessage("Trimming", "IL2092", Justification = "Override parameter annotations do not fully match the base class; related types are preserved via [Table] + source generator under AOT.")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2098", Justification = "DynamicallyAccessedMembers annotation on IEnumerable<Type> parameters; related types are preserved via [Table] + source generator under AOT.")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "MakeGenericMethod operates on a runtime MethodInfo; this path is not taken under AOT (guarded by RuntimeFeature.IsDynamicCodeSupported).")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Remote module RequiresUnreferencedCode attribute; LiteOrm.Remote / LiteOrm.Remote.Server should not be used under AOT.")]
