namespace LiteOrm.CodeGen;

public sealed class ForeignKeySchema
{
    public string? Name { get; set; }

    public string SourceColumn { get; set; } = string.Empty;

    public string TargetTable { get; set; } = string.Empty;

    public string TargetColumn { get; set; } = string.Empty;
}
