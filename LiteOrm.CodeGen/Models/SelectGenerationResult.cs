using System.Collections.Generic;
using System.Linq;

namespace LiteOrm.CodeGen;

public sealed class SelectGenerationResult
{
    public bool Succeeded => Diagnostics.All(d => d.Severity != CodeGenDiagnosticSeverity.Error);

    public List<CodeGenDiagnostic> Diagnostics { get; } = new();

    public string? RelatedEntityCode { get; set; }

    public string? ViewCode { get; set; }

    public string? QueryCode { get; set; }
}
