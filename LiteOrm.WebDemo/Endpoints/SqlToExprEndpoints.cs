using LiteOrm.SqlToExpr;
using LiteOrm.WebDemo.Contracts;

namespace LiteOrm.WebDemo.Endpoints;

public static class SqlToExprEndpoints
{
    public static void MapSqlToExprEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/convert", ConvertAsync)
            .ExcludeFromDescription();

        endpoints.MapGet("/api/sql-to-expr/options", GetOptions)
            .ExcludeFromDescription();
    }

    private static IResult ConvertAsync(SqlConvertRequest request, SqlConversionService service)
    {
        if (string.IsNullOrWhiteSpace(request.Sql))
            return Results.BadRequest(new { error = "sql is required." });

        var options = new SqlConversionOptions
        {
            Dialect = request.Dialect,
            Namespace = string.IsNullOrWhiteSpace(request.Namespace) ? "Generated.SqlModels" : request.Namespace,
            ViewName = request.ViewName ?? string.Empty,
            UseStaticExpr = request.UseStaticExpr,
            UseNameof = request.UseNameof,
            GenerateFullSelect = request.GenerateFullSelect,
            JoinMetadataMode = request.JoinMetadataMode
        };

        var result = service.Convert(request.Sql, options);
        var response = new SqlConvertResponse
        {
            Succeeded = result.Succeeded,
            HelperCode = result.HelperCode,
            ViewCode = result.ViewCode,
            ExprCode = result.ExprCode,
            RegeneratedSql = result.RegeneratedSql
        };

        foreach (var d in result.Diagnostics)
        {
            response.Diagnostics.Add(new SqlConvertDiagnostic
            {
                Severity = d.Severity.ToString(),
                Message = d.Message,
                Hint = d.Hint
            });
        }

        return Results.Ok(response);
    }

    private static IResult GetOptions()
    {
        return Results.Ok(new
        {
            dialects = Enum.GetNames<SqlDialect>(),
            joinModes = Enum.GetNames<JoinMetadataMode>()
        });
    }
}
