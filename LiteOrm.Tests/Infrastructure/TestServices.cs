using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Linq;

namespace LiteOrm.Tests.Infrastructure
{
    /// <summary>
    /// 自定义用户服务接口，继承自泛型服务接口。
    /// </summary>
    public interface ITestUserService : IEntityServiceAsync<TestUser>, IEntityViewServiceAsync<TestUser>
    {
        // 可以在此处添加自定义业务方法
        Task<TestUser?> GetLatestUserAsync();
    }

    /// <summary>
    /// 自定义部门服务接口。
    /// </summary>
    public interface ITestDepartmentService : IEntityServiceAsync<TestDepartment>, IEntityViewServiceAsync<TestDepartment>
    {
    }

    /// <summary>
    /// 测试用户服务实现。
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Scoped)]
    public class TestUserService : EntityService<TestUser>, ITestUserService
    {
        public async Task<TestUser?> GetLatestUserAsync()
        {
            var results = await SearchSectionAsync(null, new PageSection(0, 1, new Sorting("Id", System.ComponentModel.ListSortDirection.Descending)));
            return results.FirstOrDefault();
        }
    }

    /// <summary>
    /// 测试部门服务实现。
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Scoped)]
    public class TestDepartmentService : EntityService<TestDepartment>, ITestDepartmentService
    {
    }
}
