using System.Text.Json;
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.WebDemo.Infrastructure;
using LiteOrm.WebDemo.Services;
using Microsoft.AspNetCore.Mvc;

namespace LiteOrm.WebDemo.Controllers;

[ApiController]
[ServiceFilter(typeof(DemoControllerAuthFilter))]
[Route("api/[controller]/[action]")]
public abstract class EntityControllerBase<T, TView> : ControllerBase
    where T : class
    where TView : class
{
    private const int MaxPageSize = 100;

    protected readonly IEntityServiceAsync<T> EntityService;
    protected readonly IEntityViewServiceAsync<TView> ViewService;

    protected EntityControllerBase(
        IEntityServiceAsync<T> entityService,
        IEntityViewServiceAsync<TView> viewService)
    {
        EntityService = entityService;
        ViewService = viewService;
    }

    [HttpGet("{id}")]
    public virtual async Task<TView?> GetById(object id)
    {
        return await ViewService.GetObjectAsync(id);
    }

    [HttpGet]
    public virtual async Task<IActionResult> Query()
    {
        string? query = null;
        string? sort = null;
        bool desc = true;
        int page = 1;
        int rows = 20;

        var queryFields = new List<KeyValuePair<string, string>>();
        foreach (var p in Request.Query)
        {
            switch (p.Key.ToLower())
            {
                case "query":
                    query = p.Value;
                    break;
                case "sort":
                case "order":
                    sort = p.Value;
                    break;
                case "desc":
                    desc = p.Value != "false";
                    break;
                case "rows":
                case "limit":
                    rows = Math.Min(MaxPageSize, Math.Max(1, int.TryParse(p.Value, out var r) ? r : 20));
                    break;
                case "page":
                case "offset":
                    page = Math.Max(1, int.TryParse(p.Value, out var pg) ? pg : 1);
                    break;
                default:
                    queryFields.Add(new KeyValuePair<string, string>(p.Key, p.Value.ToString()));
                    break;
            }
        }

        LogicExpr? filter = null;

        if (!string.IsNullOrEmpty(query))
            filter &= Expr.Prop("Id") > 0;

        foreach (var field in queryFields)
        {
            if (string.IsNullOrEmpty(field.Value)) continue;
            filter &= Expr.Prop(field.Key).Contains(field.Value);
        }

        var skip = (page - 1) * rows;

        ISourceAnchor queryExpr = Expr.From<TView>();
        if (filter != null)
            queryExpr = queryExpr.Where(filter);

        ISectionAnchor sectionSource = queryExpr;
        if (!string.IsNullOrEmpty(sort))
        {
            var orderByItem = desc ? Expr.Prop(sort).Desc() : Expr.Prop(sort).Asc();
            sectionSource = ((IOrderByAnchor)queryExpr).OrderBy(orderByItem);
        }

        var sectionExpr = sectionSource.Section(skip, rows);

        var total = await ViewService.CountAsync(filter);
        var items = await ViewService.SearchAsync(sectionExpr);

        return Ok(new { skip, take = rows, total, page, items });
    }

    [HttpPost]
    public virtual async Task<bool> Create(T entity)
    {
        return await EntityService.InsertAsync(entity);
    }

    [HttpPut]
    public virtual async Task<bool> Update(T entity)
    {
        return await EntityService.UpdateAsync(entity);
    }

    [HttpDelete("{id}")]
    public virtual async Task<bool> Delete(object id)
    {
        return await EntityService.DeleteIDAsync(id);
    }

    [HttpPost]
    public virtual async Task<IActionResult> PageQuery([FromBody] JsonElement exprJson, IDemoExprQueryHistoryService exprQueryHistoryService, CancellationToken cancellationToken)
    {
        var currentUser = HttpContext.GetCurrentDemoUser();
        Expr? expr;
        try
        {
            expr = exprJson.Deserialize<Expr>();
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException)
        {
            return BadRequest(new { error = "Invalid Expr JSON.", detail = ex.Message });
        }

        if (expr is null)
            return BadRequest(new { error = "Request body must be a valid Expr JSON." });

        var validation = ExprValidator.CreateQueryOnly();
        if (!validation.VisitAll(expr))
            return BadRequest(new { error = "Expr contains disallowed node types.", failedType = validation.FailedExpr?.GetType().Name });

        int skip = 0, take = 20;

        if (expr is SectionExpr section)
        {
            skip = Math.Max(0, section.Skip);
            take = section.Take;
            if (take < 1) take = 20;
            if (take > MaxPageSize) take = MaxPageSize;
            expr = RebuildSection(section, skip, take);
        }
        else if (expr is LogicExpr logicExpr)
        {
            expr = Expr.From<TView>().Where(logicExpr).Section(0, take);
        }
        else
        {
            expr = Expr.From<TView>().Section(0, take);
        }

        var countExpr = ExtractFilter(expr);
        var total = await ViewService.CountAsync(countExpr);
        var items = await ViewService.SearchAsync(expr);

        await exprQueryHistoryService.SaveAsync(currentUser, exprJson.GetRawText(), cancellationToken);

        return Ok(new { skip, take, total, items });
    }

    private static SectionExpr RebuildSection(SectionExpr section, int skip, int take)
    {
        var source = section.Source;
        var rebuilt = (source as ISectionAnchor)?.Section(skip, take)
            ?? Expr.From<TView>().Section(skip, take);
        return (rebuilt as SectionExpr)!;
    }

    private static Expr? ExtractFilter(Expr? expr)
    {
        return expr switch
        {
            SectionExpr s => ExtractFilter(s.Source),
            OrderByExpr o => ExtractFilter(o.Source),
            WhereExpr w => w.Where,
            LogicExpr l => l,
            _ => null
        };
    }
}
