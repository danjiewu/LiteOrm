using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.WebDemo.Contracts;
using LiteOrm.WebDemo.Infrastructure;
using LiteOrm.WebDemo.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Threading;

namespace LiteOrm.WebDemo.Services;

public interface IDemoOrderService :
    IEntityServiceAsync<DemoOrder>,
    IEntityViewServiceAsync<DemoOrderView>
{
    Task<OrderQueryResult> QueryAsync(OrderQueryRequest request, AuthSessionUser currentUser, CancellationToken cancellationToken = default);
    Task<OrderExprQueryResponse> QueryByExprAsync(Expr? expr, AuthSessionUser currentUser, CancellationToken cancellationToken = default);
    Task<OrderStatsResponse> GetStatsAsync(OrderQueryRequest request, AuthSessionUser currentUser, CancellationToken cancellationToken = default);
}

public class DemoOrderService : EntityService<DemoOrder, DemoOrderView>, IDemoOrderService
{
    private static readonly TimeSpan CountCacheDuration = TimeSpan.FromSeconds(30);
    private static long _countCacheVersion = 1;
    private static readonly IReadOnlyDictionary<string, string> SortFieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["OrderNo"] = nameof(DemoOrder.OrderNo),
        ["CustomerName"] = nameof(DemoOrder.CustomerName),
        ["ProductName"] = nameof(DemoOrder.ProductName),
        ["Status"] = nameof(DemoOrder.Status),
        ["TotalAmount"] = nameof(DemoOrder.TotalAmount),
        ["CreatedTime"] = nameof(DemoOrder.CreatedTime),
        ["UpdatedTime"] = nameof(DemoOrder.UpdatedTime),
        ["CreatedByUserName"] = nameof(DemoOrderView.CreatedByUserName),
        ["DepartmentName"] = nameof(DemoOrderView.DepartmentName)
    };
    private readonly IMemoryCache _memoryCache;

    public DemoOrderService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public async Task<OrderQueryResult> QueryAsync(OrderQueryRequest request, AuthSessionUser currentUser, CancellationToken cancellationToken = default)
    {
        request.Normalize();
        var filter = BuildFilter(request, currentUser);
        var sortField = ResolveSortField(request.SortBy);
        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? 10;
        var skip = (page - 1) * pageSize;

        SqlTraceHelper.Reset();

        var total = await CountCachedAsync(filter, cancellationToken);
        var query = Expr.From<DemoOrderView>()
            .Where(filter)
            .OrderBy(request.Desc == true ? Expr.Prop(sortField).Desc() : Expr.Prop(sortField).Asc())
            .Section(skip, pageSize);

        var items = await SearchAsync(query, cancellationToken: cancellationToken);

        return new OrderQueryResult(
            page,
            pageSize,
            total,
            SqlTraceHelper.GetLatestSql(),
            items.Select(item => item.ToDto()).ToArray());
    }

    public async Task<OrderStatsResponse> GetStatsAsync(OrderQueryRequest request, AuthSessionUser currentUser, CancellationToken cancellationToken = default)
    {
        request.Normalize();
        var filter = BuildFilter(request, currentUser);

        SqlTraceHelper.Reset();

        var items = await SearchAsync(Expr.From<DemoOrderView>().Where(filter), cancellationToken: cancellationToken);

        return new OrderStatsResponse(
            items.Count,
            items.Sum(item => item.TotalAmount),
            items.Count(item => string.Equals(item.Status, DemoOrderStatuses.Pending, StringComparison.OrdinalIgnoreCase)),
            items.Count(item => string.Equals(item.Status, DemoOrderStatuses.Paid, StringComparison.OrdinalIgnoreCase)),
            items.Count(item => string.Equals(item.Status, DemoOrderStatuses.Shipped, StringComparison.OrdinalIgnoreCase)),
            items.Count(item => string.Equals(item.Status, DemoOrderStatuses.Completed, StringComparison.OrdinalIgnoreCase)),
            items.Count(item => string.Equals(item.Status, DemoOrderStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)),
            SqlTraceHelper.GetLatestSql());
    }

    public async Task<OrderExprQueryResponse> QueryByExprAsync(Expr? expr, AuthSessionUser currentUser, CancellationToken cancellationToken = default)
    {
        var parts = ParseNativeExpr(expr);
        var filter = BuildExprFilter(parts.Filter, currentUser);
        var skip = parts.Skip ?? 0;
        var take = parts.Take ?? 10;

        SqlTraceHelper.Reset();

        var total = await CountCachedAsync(filter, cancellationToken);
        var query = ApplyNativeOrderBy(Expr.From<DemoOrderView>().Where(filter), parts.OrderBys).Section(skip, take);
        var items = await SearchAsync(query, cancellationToken: cancellationToken);

        return new OrderExprQueryResponse(
            skip,
            parts.Take ?? items.Count,
            total,
            SqlTraceHelper.GetLatestSql(),
            items.Select(item => item.ToDto()).ToArray());
    }

    private static LogicExpr BuildFilter(OrderQueryRequest request, AuthSessionUser currentUser)
    {
        LogicExpr filter = Expr.Prop(nameof(DemoOrder.Id)) > 0;

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            filter &= Expr.Prop(nameof(DemoOrder.OrderNo)).Contains(keyword)
                   | Expr.Prop(nameof(DemoOrder.CustomerName)).Contains(keyword)
                   | Expr.Prop(nameof(DemoOrder.ProductName)).Contains(keyword);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            filter &= Expr.Prop(nameof(DemoOrder.Status)) == request.Status;
        }

        if (!string.IsNullOrWhiteSpace(request.DepartmentName))
        {
            filter &= Expr.Prop(nameof(DemoOrderView.DepartmentName)).Contains(request.DepartmentName);
        }

        if (!string.IsNullOrWhiteSpace(request.CreatedByUserName))
        {
            filter &= Expr.Prop(nameof(DemoOrderView.CreatedByUserName)).Contains(request.CreatedByUserName);
        }

        if (request.MinTotalAmount.HasValue)
        {
            filter &= Expr.Prop(nameof(DemoOrder.TotalAmount)) >= request.MinTotalAmount.Value;
        }

        if (request.MaxTotalAmount.HasValue)
        {
            filter &= Expr.Prop(nameof(DemoOrder.TotalAmount)) <= request.MaxTotalAmount.Value;
        }

        if (request.CreatedFrom.HasValue)
        {
            filter &= Expr.Prop(nameof(DemoOrder.CreatedTime)) >= request.CreatedFrom.Value;
        }

        if (request.CreatedTo.HasValue)
        {
            filter &= Expr.Prop(nameof(DemoOrder.CreatedTime)) <= request.CreatedTo.Value;
        }

        if (request.OnlyMine == true || !IsAdmin(currentUser))
        {
            filter &= Expr.Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id;
        }

        return filter;
    }

    public override async Task<bool> InsertAsync(DemoOrder entity, CancellationToken cancellationToken = default)
    {
        var inserted = await base.InsertAsync(entity, cancellationToken);
        if (inserted)
        {
            InvalidateCountCache();
        }

        return inserted;
    }

    public override async Task<bool> UpdateAsync(DemoOrder entity, CancellationToken cancellationToken = default)
    {
        var updated = await base.UpdateAsync(entity, cancellationToken);
        if (updated)
        {
            InvalidateCountCache();
        }

        return updated;
    }

    public override async Task<bool> DeleteAsync(DemoOrder entity, CancellationToken cancellationToken = default)
    {
        var deleted = await base.DeleteAsync(entity, cancellationToken);
        if (deleted)
        {
            InvalidateCountCache();
        }

        return deleted;
    }

    private static LogicExpr BuildExprFilter(LogicExpr? filter, AuthSessionUser currentUser)
    {
        filter ??= Expr.Prop(nameof(DemoOrder.Id)) > 0;

        if (!IsAdmin(currentUser))
        {
            filter &= Expr.Prop(nameof(DemoOrder.CreatedByUserId)) == currentUser.Id;
        }

        return filter;
    }

    private static OrderByExpr ApplyOrderBy(IOrderByAnchor query, IReadOnlyList<OrderByItemExpr>? orderByItems)
    {
        return orderByItems is { Count: > 0 }
            ? query.OrderBy(orderByItems.Select(item => (OrderByItemExpr)item.Clone()).ToArray())
            : query.OrderBy(Expr.Prop(nameof(DemoOrder.CreatedTime)).Desc());
    }

    private static OrderByExpr ApplyNativeOrderBy(IOrderByAnchor query, IReadOnlyList<OrderByItemExpr>? orderByItems) =>
        ApplyOrderBy(query, orderByItems);

    private static NativeExprParts ParseNativeExpr(Expr? expr)
    {
        var parts = new NativeExprParts();
        ParseNativeExprInternal(expr, parts);
        return parts;
    }

    private static void ParseNativeExprInternal(Expr? expr, NativeExprParts parts)
    {
        switch (expr)
        {
            case null:
                return;
            case LogicExpr logicExpr:
                parts.Filter = logicExpr;
                return;
            case WhereExpr whereExpr:
                ParseNativeExprInternal(whereExpr.Source, parts);
                parts.Filter = whereExpr.Where ?? parts.Filter;
                return;
            case OrderByExpr orderByExpr:
                ParseNativeExprInternal(orderByExpr.Source, parts);
                if (orderByExpr.OrderBys?.Count > 0)
                {
                    parts.OrderBys = orderByExpr.OrderBys.Select(item => (OrderByItemExpr)item.Clone()).ToList();
                }
                return;
            case SectionExpr sectionExpr:
                ParseNativeExprInternal(sectionExpr.Source, parts);
                parts.Skip = Math.Max(0, sectionExpr.Skip);
                parts.Take = NormalizeTake(sectionExpr.Take);
                return;
            case SqlSegment sqlSegment when sqlSegment.Source is not null:
                ParseNativeExprInternal(sqlSegment.Source, parts);
                return;
            case SqlSegment:
                return;
            default:
                throw new ArgumentException("Expr 查询只支持原生 LogicExpr、WhereExpr、OrderByExpr 或 SectionExpr。", nameof(expr));
        }
    }

    private static int NormalizeTake(int take) => take switch
    {
        < 1 => 10,
        > 100 => 100,
        _ => take
    };

    private static string ResolveSortField(string? sortBy) =>
        sortBy is not null && SortFieldMap.TryGetValue(sortBy, out var field) ? field : nameof(DemoOrder.CreatedTime);

    private static bool IsAdmin(AuthSessionUser currentUser) =>
        string.Equals(currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);

    private async Task<int> CountCachedAsync(Expr? filter, CancellationToken cancellationToken)
    {
        var cacheKey = filter ?? Expr.Null;
        if (_memoryCache.TryGetValue<CountCacheEntry>(cacheKey, out var cached) && cached is not null
            && cached.Version == Interlocked.Read(ref _countCacheVersion))
        {
            return cached.Total;
        }

        var total = await CountAsync(filter, cancellationToken: cancellationToken);
        _memoryCache.Set(filter is null ? Expr.Null : (Expr)filter.Clone(), new CountCacheEntry(Interlocked.Read(ref _countCacheVersion), total), CountCacheDuration);

        return total;
    }

    private static void InvalidateCountCache()
    {
        Interlocked.Increment(ref _countCacheVersion);
    }

    private sealed class NativeExprParts
    {
        public LogicExpr? Filter { get; set; }
        public List<OrderByItemExpr>? OrderBys { get; set; }
        public int? Skip { get; set; }
        public int? Take { get; set; }
    }

    private sealed record CountCacheEntry(long Version, int Total);
}
