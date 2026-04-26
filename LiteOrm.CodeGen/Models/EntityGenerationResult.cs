using System.Collections.Generic;

namespace LiteOrm.CodeGen;

public sealed class GeneratedCodeFile
{
    public string FileName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

public sealed class EntityGenerationResult
{
    public List<GeneratedCodeFile> Files { get; } = new();

    public string CombinedCode => string.Join("\r\n\r\n", Files.ConvertAll(f => f.Content));
}
