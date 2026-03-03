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
            await service.InsertAsync(user);

            // 执行
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = dataDao.UpdateAllValues(updateValues, Expr.Lambda<TestUser>(u => u.Name == "UpdateAllValues")).GetResult();
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // 断言
            Assert.Equal(1, affected);
            Assert.Equal(99, retrieved?.Age);
        }

        [Fact]
        public async Task DataDAO_UpdateAllValues_WithNonExistentProperty_ShouldThrowException()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var user = new TestUser { Name = "UpdateAllValues", Age = 10, CreateTime = System.DateTime.Now };
            await service.InsertAsync(user);
            
            var updateValues = new Dictionary<string, object> { { "NonExistentProperty", 99 } };
            await Assert.ThrowsAsync<System.Exception>(() => Task.Run(() =>
                dataDao.UpdateAllValues(updateValues, Expr.Lambda<TestUser>(u => u.Name == "UpdateAllValues"))
            ));
        }

        [Fact]
        public async Task DataDAO_UpdateValues_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateValues", Age = 10, CreateTime = System.DateTime.Now };
            await service.InsertAsync(user);

            // 执行
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = dataDao.UpdateValues(updateValues, user.Id).GetResult();
            bool updated = affected > 0;
            var retrieved = await viewService.GetObjectAsync(user.Id);

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
            int affected = await dataDao.UpdateValues(updateValues, -1).GetResultAsync();
            bool updated = affected > 0;

            // 断言
            Assert.False(updated);
        }

        [Fact]
        public async Task DataDAO_UpdateAllValuesAsync_ShouldWork()
        {
            // 准备
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateAllValuesAsync", Age = 10, CreateTime = System.DateTime.Now };
            await service.InsertAsync(user);

            // 执行
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = await dataDao.UpdateAllValues(updateValues, Expr.Lambda<TestUser>(u => u.Name == "UpdateAllValuesAsync")).GetResultAsync();
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // 断言
            Assert.Equal(1, affected);
            Assert.Equal(99, retrieved?.Age);
        }

        [Fact]
        public async Task DataDAO_UpdateValuesAsync_ShouldWork()
        {
            // 准备
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateValuesAsync", Age = 10, CreateTime = System.DateTime.Now };
            await service.InsertAsync(user);

            // 执行
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = await dataDao.UpdateValues(updateValues, user.Id).GetResultAsync();
            bool updated = affected > 0;
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // 断言
            Assert.True(updated);
            Assert.Equal(99, retrieved?.Age);
        }

        [Fact]
        public async Task DataDAO_UpdateValuesAsync_WithNonExistentId_ShouldReturnFalse()
        {
            // 准备
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();

            // 执行
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = await dataDao.UpdateValues(updateValues, -1).GetResultAsync();
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
            await service.BatchInsertAsync(users);

            // 执行
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = await dataDao.UpdateAllValues(updateValues, Expr.Lambda<TestUser>(u => u.Name.StartsWith("BatchUpdate"))).GetResultAsync();
            var retrievedUsers = await viewService.SearchAsync(Expr.Lambda<TestUser>(u => u.Name.StartsWith("BatchUpdate")));

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
            await service.InsertAsync(user);

            // 执行
            var updateValues = new Dictionary<string, object>
            {
                { "Age", 99 },
                { "Name", "UpdatedName" }
            };
            int affected = await dataDao.UpdateAllValues(updateValues, Expr.Lambda<TestUser>(u => u.Id == user.Id)).GetResultAsync();
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // 断言   
            Assert.Equal(1, affected);
            Assert.Equal(99, retrieved?.Age);
            Assert.Equal("UpdatedName", retrieved?.Name);
        }
    }
}