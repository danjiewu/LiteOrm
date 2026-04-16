using LiteOrm.Common;
using LiteOrm.WebDemo.Contracts;
using LiteOrm.WebDemo.Infrastructure;
using LiteOrm.WebDemo.Models;
using System.Text.Json;
using LiteOrm.WebDemo.Services;

namespace LiteOrm.WebDemo.Endpoints;

public static class DemoApiEndpoints
{
    public static void MapDemoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api", () => Results.Ok(new
        {
            project = "LiteOrm.WebDemo",
            message = "LiteOrm web demo API is running.",
            sampleAccounts = new[]
            {
                new { userName = "admin", password = "admin123" },
                new { userName = "alice", password = "demo123" }
            },
            orderStatuses = DemoOrderStatuses.All,
            endpoints = new[]
            {
                "POST /api/auth/login",
                "POST /api/auth/logout",
                "GET /api/auth/me",
                "GET /api/orders/query (query string)",
                "POST /api/orders/query/expr (Expr JSON)",
                "GET /api/orders/query/expr/history",
                "DELETE /api/orders/query/expr/history/{id}",
                "GET /api/orders/stats",
                "GET /api/orders/{id}",
                "POST /api/orders",
                "PUT /api/orders/{id}",
                "DELETE /api/orders/{id}"
            }
        }))
        .ExcludeFromDescription();

        var auth = endpoints.MapGroup("/api/auth");
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/logout", LogoutAsync).AddEndpointFilter<DemoAuthFilter>();
        auth.MapGet("/me", MeAsync).AddEndpointFilter<DemoAuthFilter>();

        var orders = endpoints.MapGroup("/api/orders").AddEndpointFilter<DemoAuthFilter>();
        orders.MapGet("/query", QueryOrdersAsync);
        orders.MapPost("/query/expr", QueryOrdersByExprAsync);
        orders.MapGet("/query/expr/history", GetExprQueryHistoryAsync);
        orders.MapDelete("/query/expr/history/{id:int}", DeleteExprQueryHistoryAsync);
        orders.MapGet("/stats", GetOrderStatsAsync);
        orders.MapGet("/{id:int}", GetOrderAsync);
        orders.MapPost("/", CreateOrderAsync);
        orders.MapPut("/{id:int}", UpdateOrderAsync);
        orders.MapDelete("/{id:int}", DeleteOrderAsync);
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, IAuthService authService, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { error = "userName and password are required." });
        }

        var result = await authService.LoginAsync(request, cancellationToken);
        return result is null
            ? Results.Unauthorized()
            : Results.Ok(result);
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, IAuthService authService, CancellationToken cancellationToken)
    {
        var user = context.GetCurrentDemoUser();
        await authService.LogoutAsync(user.Token, cancellationToken);
        return Results.Ok(new { success = true });
    }

    private static IResult MeAsync(HttpContext context)
    {
        var user = context.GetCurrentDemoUser();
        return Results.Ok(user);
    }

    private static async Task<IResult> QueryOrdersAsync(HttpContext context, [AsParameters] OrderQueryRequest request, IDemoOrderService orderService, CancellationToken cancellationToken)
    {
        var currentUser = context.GetCurrentDemoUser();
        var result = await orderService.QueryAsync(request, currentUser, cancellationToken);
        return Results.Ok(new OrderQueryResponse(result.Page, result.PageSize, result.Total, result.Sql, result.Items));
    }

    private static async Task<IResult> QueryOrdersByExprAsync(HttpContext context, JsonElement requestBody, IDemoOrderService orderService, IDemoExprQueryHistoryService exprQueryHistoryService, CancellationToken cancellationToken)
    {
        var currentUser = context.GetCurrentDemoUser();
        try
        {
            var expr = requestBody.Deserialize<Expr>();
            var validation = ExprValidator.CreateMinimum();
            if (!validation.VisitAll(expr)) return Results.BadRequest(new { error = "非法 Expr 内容.", hint = "请确保提交的 JSON 结构符合 LiteOrm Expr 的要求，且仅包含基本值、逻辑表达式、集合表达式和基础 SQL 片段。" });
            
            var result = await orderService.QueryByExprAsync(expr, currentUser, cancellationToken);
            await exprQueryHistoryService.SaveAsync(currentUser, requestBody.GetRawText(), cancellationToken);
            return Results.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException)
        {
            return Results.BadRequest(new
            {
                error = ex.Message,
                hint = "请提交 LiteOrm Expr 当前实际序列化后的 JSON 形状，例如使用 $section / $orderby / $where 表达链式片段。"
            });
        }
    }

    private static async Task<IResult> GetExprQueryHistoryAsync(HttpContext context, IDemoExprQueryHistoryService exprQueryHistoryService, int? take, CancellationToken cancellationToken)
    {
        var currentUser = context.GetCurrentDemoUser();
        var items = await exprQueryHistoryService.ListAsync(currentUser, take ?? 20, cancellationToken);
        return Results.Ok(new ExprQueryHistoryResponse(items));
    }

    private static async Task<IResult> DeleteExprQueryHistoryAsync(HttpContext context, int id, IDemoExprQueryHistoryService exprQueryHistoryService, CancellationToken cancellationToken)
    {
        var currentUser = context.GetCurrentDemoUser();
        var deleted = await exprQueryHistoryService.DeleteAsync(currentUser, id, cancellationToken);
        return deleted
            ? Results.Ok(new { success = true })
            : Results.NotFound(new { error = "历史记录不存在或无权删除。" });
    }

    private static async Task<IResult> GetOrderStatsAsync(HttpContext context, [AsParameters] OrderQueryRequest request, IDemoOrderService orderService, CancellationToken cancellationToken)
    {
        var currentUser = context.GetCurrentDemoUser();
        var result = await orderService.GetStatsAsync(request, currentUser, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetOrderAsync(HttpContext context, int id, IDemoOrderService orderService, CancellationToken cancellationToken)
    {
        var currentUser = context.GetCurrentDemoUser();
        SqlTraceHelper.Reset();
        var order = await orderService.GetObjectAsync(id, cancellationToken: cancellationToken);

        if (order is null)
        {
            return Results.NotFound();
        }

        if (!CanAccessOrder(currentUser, order.CreatedByUserId))
        {
            return OrderAccessDenied();
        }

        return Results.Ok(new
        {
            item = order.ToDto(),
            sql = SqlTraceHelper.GetLatestSql()
        });
    }

    private static async Task<IResult> CreateOrderAsync(HttpContext context, CreateOrderRequest request, IDemoOrderService orderService, CancellationToken cancellationToken)
    {
        var validation = ValidateOrderRequest(request.CustomerName, request.ProductName, request.Quantity, request.UnitPrice, request.Status);
        if (validation is not null)
        {
            return validation;
        }

        var currentUser = context.GetCurrentDemoUser();
        var now = DateTime.UtcNow;
        var order = new DemoOrder
        {
            OrderNo = $"ORD-{now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
            CustomerName = request.CustomerName.Trim(),
            ProductName = request.ProductName.Trim(),
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            TotalAmount = request.Quantity * request.UnitPrice,
            Status = request.Status.Trim(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedTime = now,
            UpdatedTime = now,
            CreatedByUserId = currentUser.Id
        };

        SqlTraceHelper.Reset();
        var inserted = await orderService.InsertAsync(order, cancellationToken);
        if (!inserted)
        {
            return Results.BadRequest(new { error = "Failed to create order." });
        }

        var detail = await orderService.GetObjectAsync(order.Id, cancellationToken: cancellationToken);
        return Results.Created($"/api/orders/{order.Id}", new
        {
            item = detail?.ToDto(),
            sql = SqlTraceHelper.GetLatestSql()
        });
    }

    private static async Task<IResult> UpdateOrderAsync(HttpContext context, int id, UpdateOrderRequest request, IDemoOrderService orderService, CancellationToken cancellationToken)
    {
        var validation = ValidateOrderRequest(request.CustomerName, request.ProductName, request.Quantity, request.UnitPrice, request.Status);
        if (validation is not null)
        {
            return validation;
        }

        var currentUser = context.GetCurrentDemoUser();
        var existing = await orderService.GetObjectAsync(id, cancellationToken: cancellationToken);
        if (existing is null)
        {
            return Results.NotFound();
        }

        if (!CanAccessOrder(currentUser, existing.CreatedByUserId))
        {
            return OrderAccessDenied();
        }

        existing.CustomerName = request.CustomerName.Trim();
        existing.ProductName = request.ProductName.Trim();
        existing.Quantity = request.Quantity;
        existing.UnitPrice = request.UnitPrice;
        existing.TotalAmount = request.Quantity * request.UnitPrice;
        existing.Status = request.Status.Trim();
        existing.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        existing.UpdatedTime = DateTime.UtcNow;

        SqlTraceHelper.Reset();
        var updated = await orderService.UpdateAsync(existing, cancellationToken);
        if (!updated)
        {
            return Results.BadRequest(new { error = "Failed to update order." });
        }

        var detail = await orderService.GetObjectAsync(existing.Id, cancellationToken: cancellationToken);
        return Results.Ok(new
        {
            item = detail?.ToDto(),
            sql = SqlTraceHelper.GetLatestSql()
        });
    }

    private static async Task<IResult> DeleteOrderAsync(HttpContext context, int id, IDemoOrderService orderService, CancellationToken cancellationToken)
    {
        var currentUser = context.GetCurrentDemoUser();
        var existing = await orderService.GetObjectAsync(id, cancellationToken: cancellationToken);
        if (existing is null)
        {
            return Results.NotFound();
        }

        if (!CanAccessOrder(currentUser, existing.CreatedByUserId))
        {
            return OrderAccessDenied();
        }

        SqlTraceHelper.Reset();
        var deleted = await orderService.DeleteAsync(existing, cancellationToken);
        return deleted
            ? Results.Ok(new { success = true, sql = SqlTraceHelper.GetLatestSql() })
            : Results.NotFound();
    }

    private static IResult? ValidateOrderRequest(string customerName, string productName, int quantity, decimal unitPrice, string status)
    {
        if (string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(productName))
        {
            return Results.BadRequest(new { error = "customerName and productName are required." });
        }

        if (quantity <= 0 || unitPrice <= 0)
        {
            return Results.BadRequest(new { error = "quantity and unitPrice must be greater than zero." });
        }

        if (!DemoOrderStatuses.IsValid(status))
        {
            return Results.BadRequest(new { error = $"status must be one of: {string.Join(", ", DemoOrderStatuses.All)}" });
        }

        return null;
    }

    private static bool CanAccessOrder(AuthSessionUser currentUser, int createdByUserId) =>
        string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase) || currentUser.Id == createdByUserId;

    private static IResult OrderAccessDenied() =>
        Results.Json(new { error = "当前用户无权访问这条订单。" }, statusCode: StatusCodes.Status403Forbidden);
}
