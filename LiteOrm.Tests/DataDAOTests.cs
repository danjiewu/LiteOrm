using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    [Collection("Database")]
    public class DataDAOTests : TestBase
    {
        public DataDAOTests(DatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public async Task DataDAO_UpdateAllValues_ShouldWork()
        {
            // 准备
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateAllValues", Age = 10, CreateTime = System.DateTime.Now };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            // 执行
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = await dataDao.UpdateAllValues(updateValues, Expr.Lambda<TestUser>(u => u.Name == "UpdateAllValues")).GetResultAsync(TestContext.Current.CancellationToken);
            var retrieved = await viewService.GetObjectAsync(user.Id, cancellationToken: TestContext.Current.CancellationToken);

            // 断言
            Assert.Equal(1, affected);
            Assert.Equal(99, retrieved?.Age);
        }

        [Fact]
        public async Task DataDAO_UpdateValues_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateValues", Age = 10, CreateTime = System.DateTime.Now };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            // 执行
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = await dataDao.UpdateValues(updateValues, user.Id).GetResultAsync(TestContext.Current.CancellationToken);
            bool updated = affected > 0;
            var retrieved = await viewService.GetObjectAsync(user.Id, cancellationToken: TestContext.Current.CancellationToken);

            // 断言
            Assert.True(updated);
            Assert.Equal(99, retrieved?.Age);
        }

        [Fact]
        public async Task DataDAO_UpdateValues_WithNonExistentId_ShouldReturnFalse()
        {
            // 准备
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();

            // 执行
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = await dataDao.UpdateValues(updateValues, -1).GetResultAsync(TestContext.Current.CancellationToken);
            bool updated = affected > 0;

            // 断言
            Assert.False(updated);
        }

        [Fact]
        public async Task DataDAO_BatchUpdateValues_ShouldWork()
        {
            // 准备
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();

            // 插入多个用户
            var users = new List<TestUser>
            {
                new TestUser { Name = "BatchUpdate1", Age = 10, CreateTime = System.DateTime.Now },
                new TestUser { Name = "BatchUpdate2", Age = 20, CreateTime = System.DateTime.Now },
                new TestUser { Name = "BatchUpdate3", Age = 30, CreateTime = System.DateTime.Now }
            };
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            // 执行
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = await dataDao.UpdateAllValues(updateValues, Expr.Lambda<TestUser>(u => u.Name.StartsWith("BatchUpdate"))).GetResultAsync(TestContext.Current.CancellationToken);
            var retrievedUsers = await viewService.SearchAsync(Expr.Lambda<TestUser>(u => u.Name.StartsWith("BatchUpdate")), cancellationToken: TestContext.Current.CancellationToken);

            // 断言
            Assert.Equal(3, affected);
            Assert.All(retrievedUsers, u => Assert.Equal(99, u.Age));
        }

        [Fact]
        public async Task DataDAO_UpdateMultipleProperties_ShouldWork()
        {
            // 准备
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateMultiple", Age = 10, CreateTime = System.DateTime.Now };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            // 执行
            var updateValues = new Dictionary<string, object>
            {
                { "Age", 99 },
                { "Name", "UpdatedName" }
            };
            int affected = await dataDao.UpdateAllValues(updateValues, Expr.Lambda<TestUser>(u => u.Id == user.Id)).GetResultAsync(TestContext.Current.CancellationToken);
            var retrieved = await viewService.GetObjectAsync(user.Id, cancellationToken: TestContext.Current.CancellationToken);

            // 断言   
            Assert.Equal(1, affected);
            Assert.Equal(99, retrieved?.Age);
            Assert.Equal("UpdatedName", retrieved?.Name);
        }
    }
}