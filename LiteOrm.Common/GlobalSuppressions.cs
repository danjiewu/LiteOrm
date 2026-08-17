// Global trimming/AOT warning suppressions (LiteOrm.Common).
//
// LiteOrm.Common accesses framework properties such as PropertyInfo.PropertyType
// and SqlColumn.PropertyType. The Type returned by these properties has no
// [DynamicallyAccessedMembers] annotation, so the trimmer cannot statically
// track them. At runtime, however, entity properties are preserved via [Table]
// metadata registered with CommonTableInfoProvider by the TableInfoGenerator
// source generator.
//
// Under AOT, users must annotate all entity classes with [Table]; the source
// generator then emits property accessors and DataReader mappers at compile
// time, avoiding runtime reflection. This file only suppresses warnings that
// cannot be statically resolved through annotation chains; it does not mask
// genuine AOT incompatibilities (JIT-only paths are guarded by
// RuntimeFeature.IsDynamicCodeSupported).

using System.Diagnostics.CodeAnalysis;

[assembly: UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "PropertyInfo.PropertyType / SqlColumn.PropertyType framework properties carry no annotation; entity properties are preserved via [Table] + source generator under AOT.")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "PropertyInfo.PropertyType / SqlColumn.PropertyType framework properties carry no annotation; entity properties are preserved via [Table] + source generator under AOT.")]
[assembly: UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "GetType() returns a Type without annotation; entity properties are preserved via [Table] + source generator under AOT.")]
