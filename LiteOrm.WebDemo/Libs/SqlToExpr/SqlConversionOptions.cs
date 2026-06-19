namespace LiteOrm.SqlToExpr;

public sealed class SqlConversionOptions
{
    public SqlDialect Dialect { get; set; } = SqlDialect.SQLite;

    public string Namespace { get; set; } = "SampleProject.Models";

    public string ViewName { get; set; } = string.Empty;

    public bool UseStaticExpr { get; set; } = true;

    public bool UseNameof { get; set; }

    public bool GenerateFullSelect { get; set; }

    public JoinMetadataMode JoinMetadataMode { get; set; } = JoinMetadataMode.ForeignType;
}