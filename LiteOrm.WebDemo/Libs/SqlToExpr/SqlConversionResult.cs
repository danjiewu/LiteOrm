using System.Collections.Generic;
using System.Linq;

namespace LiteOrm.SqlToExpr;

public sealed class SqlConversionResult
{
    public bool Succeeded => Diagnostics.All(d => d.Severity != CodeGenDiagnosticSeverity.Error);

    public List<CodeGenDiagnostic> Diagnostics { get; } = new();

    public string? AstJson { get; set; }

    public string? HelperCode { get; set; }

    public string? ViewCode { get; set; }

    public string? ModelCode =>
        string.IsNullOrWhiteSpace(HelperCode) ? ViewCode
        : string.IsNullOrWhiteSpace(ViewCode) ? HelperCode
        : HelperCode + "\r\n\r\n" + ViewCode;

    public string? ExprCode { get; set; }

    public string? RegeneratedSql { get; set; }
}