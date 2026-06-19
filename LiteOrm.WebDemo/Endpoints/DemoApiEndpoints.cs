using System.Text.Json;
using LiteOrm.Common;
using LiteOrm.WebDemo.Contracts;
using LiteOrm.WebDemo.Infrastructure;
using LiteOrm.WebDemo.Services;

namespace LiteOrm.WebDemo.Endpoints;

public static class DemoApiEndpoints
{
    public static void MapDemoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api", () => Results.Ok(new
        {
            project = "LiteOrm.WebDemo",
            message = "LiteOrm Web 演示：SQL 转 Expr 与 Expr 查询体验。",
            endpoints = new[]
            {
                "POST /api/orders/query/expr",
                "POST /api/convert",
                "GET  /api/sql-to-expr/options"
            }
        }))
        .ExcludeFromDescription();
    }
}
