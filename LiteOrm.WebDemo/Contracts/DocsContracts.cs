namespace LiteOrm.WebDemo.Contracts;

public sealed class DocsArticle
{
    public string Title { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public bool HasChinese { get; set; }

    public bool HasEnglish { get; set; }
}

public sealed class DocsChapter
{
    public string Title { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public List<DocsArticle> Articles { get; } = new();
}

public sealed class DocsIndex
{
    public List<DocsChapter> Chapters { get; } = new();

    public string? ReadmeHtml { get; set; }
}

public sealed class DocsPage
{
    public string Title { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string Lang { get; set; } = "zh";

    public string Html { get; set; } = string.Empty;
}
