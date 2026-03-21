using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.Tests.Infrastructure
{
    /// <summary>
    /// 提供测试用户相关数据访问和业务逻辑的服务接口，继承自 LiteOrm 的实体服务接口。
    /// </summary>
    public interface ITestUserService : IEntityServiceAsync<TestUser>, IEntityViewServiceAsync<TestUser>
    {
        Task<TestUser?> GetLatestUserAsync();
    }

    /// <summary>
    /// 定义测试部门相关数据访问和业务逻辑的服务接口，继承自 LiteOrm 的实体服务接口。
    /// </summary>
    public interface ITestDepartmentService : IEntityServiceAsync<TestDepartment>, IEntityViewServiceAsync<TestDepartment>
    {
    }

    /// <summary>
    /// 测试用户服务的具体实现类，提供了获取最新用户的业务逻辑实现。
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Scoped)]
    public class TestUserService : EntityService<TestUser>, ITestUserService
    {
        public async Task<TestUser?> GetLatestUserAsync()
        {
            return await SearchOneAsync(
                Expr.Lambda<TestUser>(u => u.Id > 0).OrderBy(("Id", false)).Section(0, 1)
            );
        }
    }

    /// <summary>
    /// 提供测试部门服务的具体实现类，目前没有额外的业务逻辑，仅继承自 EntityService 来提供基本的数据访问功能。
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Scoped)]
    public class TestDepartmentService : EntityService<TestDepartment>, ITestDepartmentService
    {
    }
}
