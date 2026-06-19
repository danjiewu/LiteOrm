using LiteOrm.WebDemo.Contracts;
using Markdig;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace LiteOrm.WebDemo.Services;

public sealed class DocsService
{
    private readonly string _docsRoot;
    private readonly MarkdownPipeline _pipeline;
    private readonly ConcurrentDictionary<(string Path, string Lang), string> _htmlCache = new();

    private static readonly Dictionary<string, (string ZhTitle, string EnTitle, string? Summary)> ChapterTitles = new()
    {
        ["01-getting-started"] = ("入门", "Getting Started", "环境准备与第一个 LiteOrm 示例"),
        ["02-core-usage"] = ("核心使用", "Core Usage", "实体映射、视图模型、Expr 与查询基础"),
        ["03-advanced-topics"] = ("高级特性", "Advanced Topics", "事务、分表、性能、窗口函数、权限与日志"),
        ["04-extensibility"] = ("扩展开发", "Extensibility", "自定义表达式、函数验证、SqlBuilder 与动态 Controller"),
        ["05-reference"] = ("参考文档", "Reference", "API 索引、术语表、SQL 示例与兼容性"),
    };

    public DocsService(string docsPath)
    {
        _docsRoot = docsPath;
        _pipeline = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseGridTables()
            .UseEmphasisExtras()
            .UseAutoLinks()
            .UseTaskLists()
            .Build();
    }

    public DocsIndex BuildIndex(string lang = "zh")
    {
        var index = new DocsIndex();
        if (!Directory.Exists(_docsRoot)) return index;

        bool useEnglish = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);

        var readmePath = Path.Combine(_docsRoot, useEnglish ? "README.en.md" : "README.md");
        if (!File.Exists(readmePath)) readmePath = Path.Combine(_docsRoot, "README.md");
        if (File.Exists(readmePath))
            index.ReadmeHtml = RenderMarkdownToHtml(File.ReadAllText(readmePath), string.Empty);

        var directories = Directory.GetDirectories(_docsRoot)
            .OrderBy(d => Path.GetFileName(d), StringComparer.Ordinal)
            .ToList();

        foreach (var directory in directories)
        {
            var dirName = Path.GetFileName(directory);
            var (zhTitle, enTitle, summary) = GetChapterInfo(dirName);
            var chapter = new DocsChapter { Title = useEnglish ? enTitle : zhTitle, Summary = summary };

            var files = Directory.GetFiles(directory, "*.md")
                .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
                .ToList();

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                bool isEnglish = fileName.EndsWith(".en.md", StringComparison.Ordinal);
                string baseName = isEnglish ? fileName[..^6] : fileName[..^3];
                string relativePath = dirName + "/" + baseName;

                var article = chapter.Articles.FirstOrDefault(a =>
                    string.Equals(a.Path, relativePath, StringComparison.Ordinal));

                if (article is null)
                {
                    // 根据语言选择标题来源文件
                    string titleFile = file;
                    if (useEnglish && !isEnglish)
                    {
                        var enFile = Path.Combine(directory, baseName + ".en.md");
                        if (File.Exists(enFile)) titleFile = enFile;
                    }
                    else if (!useEnglish && isEnglish)
                    {
                        var zhFile = Path.Combine(directory, baseName + ".md");
                        if (File.Exists(zhFile)) titleFile = zhFile;
                    }

                    article = new DocsArticle
                    {
                        Title = ExtractTitleFromMarkdown(titleFile) ?? baseName,
                        Path = relativePath,
                    };
                    chapter.Articles.Add(article);
                }

                if (isEnglish) article.HasEnglish = true;
                else article.HasChinese = true;
            }

            if (chapter.Articles.Count > 0)
                index.Chapters.Add(chapter);
        }

        return index;
    }

    public DocsPage? GetPage(string relativePath, string lang = "zh")
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var sanitized = relativePath.Trim('/', '\\');
        if (sanitized.Length == 0 || sanitized.Contains("..", StringComparison.Ordinal)) return null;

        var fileBase = Path.Combine(_docsRoot, sanitized);
        bool useEnglish = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);
        var targetFile = useEnglish ? fileBase + ".en.md" : fileBase + ".md";

        if (!File.Exists(targetFile))
        {
            if (useEnglish && File.Exists(fileBase + ".md")) targetFile = fileBase + ".md";
            else if (!useEnglish && File.Exists(fileBase + ".en.md")) targetFile = fileBase + ".en.md";
            else return null;
        }

        var raw = File.ReadAllText(targetFile);
        var docDir = Path.GetDirectoryName(targetFile) ?? _docsRoot;
        var relativeDir = _docsRoot.Length > 0 && docDir.StartsWith(_docsRoot, StringComparison.Ordinal)
            ? docDir.Substring(_docsRoot.Length).Replace('\\', '/').Trim('/')
            : string.Empty;

        var cacheKey = (sanitized, useEnglish ? "en" : "zh");
        var html = _htmlCache.GetOrAdd(cacheKey, _ => RenderMarkdownToHtml(raw, relativeDir));
        var firstHeading = ExtractTitleFromMarkdown(targetFile) ?? sanitized;

        return new DocsPage
        {
            Title = firstHeading,
            Path = sanitized,
            Lang = useEnglish ? "en" : "zh",
            Html = html,
        };
    }

    private static (string ZhTitle, string EnTitle, string? Summary) GetChapterInfo(string dirName)
    {
        if (ChapterTitles.TryGetValue(dirName, out var info)) return info;
        var trimmed = dirName.AsSpan();
        var firstDash = trimmed.IndexOf('-');
        var title = firstDash >= 0 && firstDash + 1 < trimmed.Length
            ? trimmed[(firstDash + 1)..].TrimStart('-').ToString()
            : dirName;
        return (title, title, null);
    }

    private static string? ExtractTitleFromMarkdown(string filePath)
    {
        try
        {
            using var reader = new StreamReader(filePath);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line.StartsWith("# ", StringComparison.Ordinal))
                    return line.Substring(2).Trim();
            }
        }
        catch { return null; }
        return null;
    }

    private string RenderMarkdownToHtml(string markdown, string baseDir)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;
        var html = Markdig.Markdown.ToHtml(markdown, _pipeline);
        return RewriteRelativeLinks(html, baseDir);
    }

    private static string RewriteRelativeLinks(string html, string baseDir)
    {
        if (string.IsNullOrWhiteSpace(html)) return html;
        return Regex.Replace(
            html,
            @"<a\s+([^>]*?)href\s*=\s*""([^""]+)""([^>]*)>",
            m =>
            {
                var before = m.Groups[1].Value;
                var href = m.Groups[2].Value;
                var after = m.Groups[3].Value;
                var candidate = href.Trim();
                if (candidate.StartsWith("http://", StringComparison.Ordinal) ||
                    candidate.StartsWith("https://", StringComparison.Ordinal) ||
                    candidate.StartsWith("#", StringComparison.Ordinal) ||
                    candidate.StartsWith("/", StringComparison.Ordinal))
                    return m.Value;
                if (!candidate.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    return m.Value;

                var hashIdx = candidate.IndexOf('#');
                var anchor = string.Empty;
                var pathPart = candidate;
                if (hashIdx >= 0)
                {
                    anchor = candidate.Substring(hashIdx);
                    pathPart = candidate.Substring(0, hashIdx);
                }

                pathPart = pathPart.TrimEnd('.').Replace('\\', '/');
                if (pathPart.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    pathPart = pathPart[..^3];

                string resolved;
                if (pathPart.StartsWith("./", StringComparison.Ordinal))
                    pathPart = pathPart[2..];

                if (string.IsNullOrEmpty(baseDir))
                    resolved = pathPart;
                else
                    resolved = baseDir.TrimEnd('/') + "/" + pathPart;

                resolved = NormalizeDocPath(resolved);
                var finalHref = "#/docs?path=" + Uri.EscapeDataString(resolved);
                if (!string.IsNullOrEmpty(anchor))
                    finalHref += "&fragment=" + Uri.EscapeDataString(anchor.TrimStart('#'));
                return "<a " + before + "href=\"" + finalHref + "\"" + after + " data-doc-link=\"1\">";
            },
            RegexOptions.Singleline);
    }

    private static string NormalizeDocPath(string path)
    {
        var parts = path.Split('/', StringSplitOptions.None);
        var stack = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (part == "." || part == string.Empty) continue;
            if (part == ".." && stack.Count > 0) { stack.RemoveAt(stack.Count - 1); continue; }
            if (part == "..") continue;
            stack.Add(part);
        }
        return string.Join("/", stack);
    }
}
