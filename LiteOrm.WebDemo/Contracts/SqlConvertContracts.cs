using LiteOrm.SqlToExpr;

namespace LiteOrm.WebDemo.Contracts;

public sealed class SqlConvertRequest
{
    public string Sql { get; set; } = string.Empty;

    public SqlDialect Dialect { get; set; } = SqlDialect.SQLite;

    public string? Namespace { get; set; }

    public string? ViewName { get; set; }

    public bool UseStaticExpr { get; set; } = true;

    public bool UseNameof { get; set; }

    public bool GenerateFullSelect { get; set; }

    public JoinMetadataMode JoinMetadataMode { get; set; } = JoinMetadataMode.ForeignType;
}

public sealed class SqlConvertDiagnostic
{
    public string Severity { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Hint { get; set; }
}

public sealed class SqlConvertResponse
{
    public bool Succeeded { get; set; }

    public List<SqlConvertDiagnostic> Diagnostics { get; } = new();

    public string? HelperCode { get; set; }

    public string? ViewCode { get; set; }

    public string? ExprCode { get; set; }

    public string? RegeneratedSql { get; set; }
}
