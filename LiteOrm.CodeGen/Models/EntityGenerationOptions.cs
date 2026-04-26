using System.Collections.Generic;

namespace LiteOrm.CodeGen;

public sealed class EntityGenerationOptions
{
    public string Namespace { get; set; } = "Generated.Models";

    public bool IncludeObjectBase { get; set; } = true;

    public bool EmitForeignTypes { get; set; } = true;

    public List<string>? Tables { get; set; }
}
