namespace LiteOrm.CodeGen;

public sealed class SelectGenerationOptions
{
    public string Namespace { get; set; } = "Generated.Models";

    public string ViewName { get; set; } = "GeneratedView";
}
