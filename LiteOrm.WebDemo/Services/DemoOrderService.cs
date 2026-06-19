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
    Task<OrderExprQueryResponse> QueryByExprAsync(Expr? expr, CancellationToken cancellationToken = default);
}

public class DemoOrderService : EntityService<DemoOrder, DemoOrderView>, IDemoOrderService
{
    private static readonly TimeSpan CountCacheDuration = TimeSpan.FromSeconds(30);
    private static long _countCacheVersion = 1;
    private readonly IMemoryCache _memoryCache;

    public DemoOrderService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public async Task<OrderExprQueryResponse> QueryByExprAsync(Expr? expr, CancellationToken cancellationToken = default)
    {
        var parts = ParseNativeExpr(expr);
        var filter = parts.Filter ?? (Expr.Prop(nameof(DemoOrder.Id)) > 0);
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

    public override async Task<bool> InsertAsync(DemoOrder entity, CancellationToken cancellationToken = default)
    {
        var inserted = await base.InsertAsync(entity, cancellationToken);
        if (inserted) InvalidateCountCache();
        return inserted;
    }

    public override async Task<bool> UpdateAsync(DemoOrder entity, CancellationToken cancellationToken = default)
    {
        var updated = await base.UpdateAsync(entity, cancellationToken);
        if (updated) InvalidateCountCache();
        return updated;
    }

    public override async Task<bool> DeleteAsync(DemoOrder entity, CancellationToken cancellationToken = default)
    {
        var deleted = await base.DeleteAsync(entity, cancellationToken);
        if (deleted) InvalidateCountCache();
        return deleted;
    }

    private static OrderByExpr ApplyNativeOrderBy(IOrderByAnchor query, IReadOnlyList<OrderByItemExpr>? orderByItems)
    {
        return orderByItems is { Count: > 0 }
            ? query.OrderBy(orderByItems.Select(item => (OrderByItemExpr)item.Clone()).ToArray())
            : query.OrderBy(Expr.Prop(nameof(DemoOrder.CreatedTime)).Desc());
    }

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
