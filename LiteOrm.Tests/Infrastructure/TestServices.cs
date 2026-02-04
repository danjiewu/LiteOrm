using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.Tests.Infrastructure
{
    /// <summary>
    /// �Զ����û�����ӿڣ��̳��Է��ͷ���ӿڡ�
    /// </summary>
    public interface ITestUserService : IEntityServiceAsync<TestUser>, IEntityViewServiceAsync<TestUser>
    {
        // �����ڴ˴������Զ���ҵ�񷽷�
        Task<TestUser?> GetLatestUserAsync();
    }

    /// <summary>
    /// �Զ��岿�ŷ���ӿڡ�
    /// </summary>
    public interface ITestDepartmentService : IEntityServiceAsync<TestDepartment>, IEntityViewServiceAsync<TestDepartment>
    {
    }

    /// <summary>
    /// �����û�����ʵ�֡�
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Scoped)]
    public class TestUserService : EntityService<TestUser>, ITestUserService
    {
        public async Task<TestUser?> GetLatestUserAsync()
        {
            var results = await SearchAsync(null);
            return results.OrderByDescending(u => u.Id).FirstOrDefault();
        }
    }

    /// <summary>
    /// ���Բ��ŷ���ʵ�֡�
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Scoped)]
    public class TestDepartmentService : EntityService<TestDepartment>, ITestDepartmentService
    {
    }
}
