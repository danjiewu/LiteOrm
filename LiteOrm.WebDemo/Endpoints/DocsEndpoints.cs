using LiteOrm.WebDemo.Services;

namespace LiteOrm.WebDemo.Endpoints;

public static class DocsEndpoints
{
    public static void MapDocsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/docs/index", GetIndex)
            .ExcludeFromDescription();

        endpoints.MapGet("/api/docs/page", GetPage)
            .ExcludeFromDescription();
    }

    private static IResult GetIndex(DocsService docsService, string lang = "zh")
    {
        var index = docsService.BuildIndex(lang);
        return Results.Ok(index);
    }

    private static IResult GetPage(DocsService docsService, string? path, string lang = "zh")
    {
        if (string.IsNullOrWhiteSpace(path))
            return Results.BadRequest(new { error = "path is required." });

        var page = docsService.GetPage(path, lang);
        if (page is null)
            return Results.NotFound(new { error = "Page not found." });

        return Results.Ok(page);
    }
}
