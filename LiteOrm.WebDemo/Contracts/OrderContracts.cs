using LiteOrm.Common;
using LiteOrm.WebDemo.Models;

namespace LiteOrm.WebDemo.Contracts;

public sealed class OrderQueryRequest
{
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public string? DepartmentName { get; set; }
    public string? CreatedByUserName { get; set; }
    public decimal? MinTotalAmount { get; set; }
    public decimal? MaxTotalAmount { get; set; }
    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public bool? OnlyMine { get; set; }
    public string? SortBy { get; set; }
    public bool? Desc { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }

    public OrderQueryRequest Normalize()
    {
        Page = Page is null || Page < 1 ? 1 : Page;
        PageSize = PageSize switch
        {
            null => 10,
            < 1 => 10,
            > 100 => 100,
            _ => PageSize
        };
        OnlyMine ??= false;
        Desc ??= true;
        SortBy = string.IsNullOrWhiteSpace(SortBy) ? "CreatedTime" : SortBy.Trim();
        Status = string.IsNullOrWhiteSpace(Status) ? null : Status.Trim();
        DepartmentName = string.IsNullOrWhiteSpace(DepartmentName) ? null : DepartmentName.Trim();
        CreatedByUserName = string.IsNullOrWhiteSpace(CreatedByUserName) ? null : CreatedByUserName.Trim();
        Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
        return this;
    }
}

public sealed record CreateOrderRequest(
    string CustomerName,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    string Status,
    string? Note);

public sealed record UpdateOrderRequest(
    string CustomerName,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    string Status,
    string? Note);

public sealed record OrderDto(
    int Id,
    string OrderNo,
    string CustomerName,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    string Status,
    string? Note,
    DateTime CreatedTime,
    DateTime UpdatedTime,
    int CreatedByUserId,
    string? CreatedByUserName,
    string? CreatedByLoginName,
    string? DepartmentName);

public sealed record OrderQueryResponse(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<string> Sql,
    IReadOnlyList<OrderDto> Items);

public sealed record OrderStatsResponse(
    int Total,
    decimal TotalAmount,
    int PendingCount,
    int PaidCount,
    int ShippedCount,
    int CompletedCount,
    int CancelledCount,
    IReadOnlyList<string> Sql);

public sealed record OrderQueryResult(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<string> Sql,
    IReadOnlyList<OrderDto> Items);

public sealed record OrderExprQueryResponse(
    int Skip,
    int Take,
    int Total,
    IReadOnlyList<string> Sql,
    IReadOnlyList<OrderDto> Items);

public static class OrderMappings
{
    public static OrderDto ToDto(this DemoOrderView order) =>
        new(
            order.Id,
            order.OrderNo,
            order.CustomerName,
            order.ProductName,
            order.Quantity,
            order.UnitPrice,
            order.TotalAmount,
            order.Status,
            order.Note,
            order.CreatedTime,
            order.UpdatedTime,
            order.CreatedByUserId,
            order.CreatedByUserName,
            order.CreatedByLoginName,
            order.DepartmentName);
}
