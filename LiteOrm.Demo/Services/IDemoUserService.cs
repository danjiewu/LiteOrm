using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Demo.Models;

namespace LiteOrm.Demo.Services;

public interface IDemoUserService :
    IEntityServiceAsync<DemoUser>,
    IEntityViewServiceAsync<DemoUserView>
{
    Task<DemoUserView?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);
    Task<DemoUserView?> GetProfileAsync(int userId, CancellationToken cancellationToken = default);
}