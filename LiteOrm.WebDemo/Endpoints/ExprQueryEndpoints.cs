using System.Text.Json;
using LiteOrm.Common;
using LiteOrm.WebDemo.Contracts;
using LiteOrm.WebDemo.Infrastructure;
using LiteOrm.WebDemo.Services;

namespace LiteOrm.WebDemo.Endpoints;

public static class ExprQueryEndpoints
{
    public static void MapExprQueryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/orders/query/expr", QueryByExprAsync)
            .ExcludeFromDescription();
    }

    private static async Task<IResult> QueryByExprAsync(
        JsonElement requestBody,
        IDemoOrderService orderService,
        CancellationToken cancellationToken)
    {
        try
        {
            var expr = requestBody.Deserialize<Expr>();
            var exprJson = requestBody.GetRawText();

            var result = await orderService.QueryByExprAsync(expr, cancellationToken);

            return Results.Ok(new
            {
                exprJson,
                sql = result.Sql,
                total = result.Total,
                items = result.Items
            });
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
