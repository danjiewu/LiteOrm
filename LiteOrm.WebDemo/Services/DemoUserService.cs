using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.WebDemo.Models;

namespace LiteOrm.WebDemo.Services;

public interface IDemoUserService :
    IEntityServiceAsync<DemoUser>,
    IEntityViewServiceAsync<DemoUserView>
{
    Task<DemoUserView?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<DemoUserView?> GetProfileAsync(int userId, CancellationToken cancellationToken = default);
}

public class DemoUserService : EntityService<DemoUser, DemoUserView>, IDemoUserService
{
    public DemoUserService(ObjectDAO<DemoUser> objectDAO, ObjectViewDAO<DemoUserView> objectViewDAO)
        : base(objectDAO, objectViewDAO)
    {
    }

    public async Task<DemoUserView?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default) =>
        await SearchOneAsync(Expr.Prop(nameof(DemoUser.UserName)) == userName, cancellationToken: cancellationToken);

    public async Task<DemoUserView?> GetProfileAsync(int userId, CancellationToken cancellationToken = default) =>
        await SearchOneAsync(Expr.Prop(nameof(DemoUser.Id)) == userId, cancellationToken: cancellationToken);
}
