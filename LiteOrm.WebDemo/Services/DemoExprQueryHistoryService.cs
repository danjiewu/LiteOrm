using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.WebDemo.Contracts;
using LiteOrm.WebDemo.Models;

namespace LiteOrm.WebDemo.Services;

public interface IDemoExprQueryHistoryService :
    IEntityServiceAsync<DemoExprQueryHistory>,
    IEntityViewServiceAsync<DemoExprQueryHistory>
{
    Task SaveAsync(AuthSessionUser currentUser, string exprJson, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExprQueryHistoryDto>> ListAsync(AuthSessionUser currentUser, int take = 20, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(AuthSessionUser currentUser, int id, CancellationToken cancellationToken = default);
}

public class DemoExprQueryHistoryService : EntityService<DemoExprQueryHistory>, IDemoExprQueryHistoryService
{
    public async Task SaveAsync(AuthSessionUser currentUser, string exprJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exprJson))
        {
            throw new ArgumentException("Expr history cannot be empty.", nameof(exprJson));
        }

        var history = new DemoExprQueryHistory
        {
            UserId = currentUser.Id,
            ExprJson = exprJson.Trim(),
            CreatedTime = DateTime.UtcNow
        };

        var inserted = await InsertAsync(history, cancellationToken);
        if (!inserted)
        {
            throw new InvalidOperationException("Failed to save expr query history.");
        }
    }

    public async Task<IReadOnlyList<ExprQueryHistoryDto>> ListAsync(AuthSessionUser currentUser, int take = 20, CancellationToken cancellationToken = default)
    {
        var query = Expr.From<DemoExprQueryHistory>()
            .Where(Expr.Prop(nameof(DemoExprQueryHistory.UserId)) == currentUser.Id)
            .OrderBy(Expr.Prop(nameof(DemoExprQueryHistory.CreatedTime)).Desc())
            .Section(0, NormalizeTake(take));

        var items = await SearchAsync(query, cancellationToken: cancellationToken);
        return items
            .Select(item => new ExprQueryHistoryDto(item.Id, item.ExprJson, item.CreatedTime))
            .ToArray();
    }

    public async Task<bool> DeleteAsync(AuthSessionUser currentUser, int id, CancellationToken cancellationToken = default)
    {
        var history = await SearchOneAsync(
            (Expr.Prop(nameof(DemoExprQueryHistory.Id)) == id)
            & (Expr.Prop(nameof(DemoExprQueryHistory.UserId)) == currentUser.Id),
            cancellationToken: cancellationToken);

        if (history is null)
        {
            return false;
        }

        return await DeleteIDAsync(history.Id, cancellationToken: cancellationToken);
    }

    private static int NormalizeTake(int take) => take switch
    {
        < 1 => 10,
        > 50 => 50,
        _ => take
    };
}
