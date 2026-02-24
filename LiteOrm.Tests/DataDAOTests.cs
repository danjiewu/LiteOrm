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
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateAllValues", Age = 10, CreateTime = System.DateTime.Now };
            await service.InsertAsync(user);

            // Act
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = dataDao.UpdateAllValues(updateValues, Expr.Exp<TestUser>(u => u.Name == "UpdateAllValues"));
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.Equal(1, affected);
            Assert.Equal(99, retrieved?.Age);
        }

        [Fact]
        public async Task DataDAO_UpdateAllValues_WithNonExistentProperty_ShouldThrowException()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var user = new TestUser { Name = "UpdateAllValues", Age = 10, CreateTime = System.DateTime.Now };
            await service.InsertAsync(user);

            // Act & Assert
            var updateValues = new Dictionary<string, object> { { "NonExistentProperty", 99 } };
            await Assert.ThrowsAsync<System.Exception>(() => Task.Run(() =>
                dataDao.UpdateAllValues(updateValues, Expr.Exp<TestUser>(u => u.Name == "UpdateAllValues"))
            ));
        }

        [Fact]
        public async Task DataDAO_UpdateValues_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateValues", Age = 10, CreateTime = System.DateTime.Now };
            await service.InsertAsync(user);

            // Act
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            bool updated = dataDao.UpdateValues(updateValues, user.Id);
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.True(updated);
            Assert.Equal(99, retrieved?.Age);
        }

        [Fact]
        public async Task DataDAO_UpdateValues_WithNonExistentId_ShouldReturnFalse()
        {
            // Arrange
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();

            // Act
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            bool updated = dataDao.UpdateValues(updateValues, -1);

            // Assert
            Assert.False(updated);
        }

        [Fact]
        public async Task DataDAO_UpdateAllValuesAsync_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateAllValuesAsync", Age = 10, CreateTime = System.DateTime.Now };
            await service.InsertAsync(user);

            // Act
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = await dataDao.UpdateAllValuesAsync(updateValues, Expr.Exp<TestUser>(u => u.Name == "UpdateAllValuesAsync"));
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.Equal(1, affected);
            Assert.Equal(99, retrieved?.Age);
        }

        [Fact]
        public async Task DataDAO_UpdateValuesAsync_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateValuesAsync", Age = 10, CreateTime = System.DateTime.Now };
            await service.InsertAsync(user);

            // Act
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            bool updated = await dataDao.UpdateValuesAsync(updateValues, new object[] { user.Id });
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.True(updated);
            Assert.Equal(99, retrieved?.Age);
        }

        [Fact]
        public async Task DataDAO_UpdateValuesAsync_WithNonExistentId_ShouldReturnFalse()
        {
            // Arrange
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();

            // Act
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            bool updated = await dataDao.UpdateValuesAsync(updateValues, new object[] { -1 });

            // Assert
            Assert.False(updated);
        }

        [Fact]
        public async Task DataDAO_BatchUpdateValues_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            
            // Insert multiple users
            var users = new List<TestUser>
            {
                new TestUser { Name = "BatchUpdate1", Age = 10, CreateTime = System.DateTime.Now },
                new TestUser { Name = "BatchUpdate2", Age = 20, CreateTime = System.DateTime.Now },
                new TestUser { Name = "BatchUpdate3", Age = 30, CreateTime = System.DateTime.Now }
            };
            await service.BatchInsertAsync(users);

            // Act
            var updateValues = new Dictionary<string, object> { { "Age", 99 } };
            int affected = dataDao.UpdateAllValues(updateValues, Expr.Exp<TestUser>(u => u.Name.StartsWith("BatchUpdate")));
            var retrievedUsers = await viewService.SearchAsync(Expr.Exp<TestUser>(u => u.Name.StartsWith("BatchUpdate")));

            // Assert
            Assert.Equal(3, affected);
            Assert.All(retrievedUsers, u => Assert.Equal(99, u.Age));
        }

        [Fact]
        public async Task DataDAO_UpdateMultipleProperties_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UpdateMultiple", Age = 10, CreateTime = System.DateTime.Now };
            await service.InsertAsync(user);

            // Act
            var updateValues = new Dictionary<string, object>
            {
                { "Age", 99 },
                { "Name", "UpdatedName" }
            };
            int affected = dataDao.UpdateAllValues(updateValues, Expr.Exp<TestUser>(u => u.Id == user.Id));
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.Equal(1, affected);
            Assert.Equal(99, retrieved?.Age);
            Assert.Equal("UpdatedName", retrieved?.Name);
        }
    }
}