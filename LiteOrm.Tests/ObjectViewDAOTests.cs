using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    [Collection("Database")]
    public class ObjectViewDAOTests : TestBase
    {
        public ObjectViewDAOTests(DatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public async Task ObjectViewDAO_BasicSearch_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var users = new List<TestUser>
            {
                new TestUser { Name = "SearchTest1", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "SearchTest2", Age = 30, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users);

            var results = await objectViewDAO.Search(Expr.Prop("Name").StartsWith("SearchTest")).ToListAsync();

            Assert.NotNull(results);
            Assert.True(results.Count >= 2);
        }

        [Fact]
        public async Task ObjectViewDAO_GetObject_ByKey_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user = new TestUser { Name = "GetObjectTest", Age = 25, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            var result = await objectViewDAO.GetObject(user.Id).FirstOrDefaultAsync();

            Assert.NotNull(result);
            Assert.Equal(user.Name, result.Name);
        }

        [Fact]
        public async Task ObjectViewDAO_Exists_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user = new TestUser { Name = "ExistsTest", Age = 35, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            var exists = await objectViewDAO.Exists(Expr.Prop("Name") == "ExistsTest").GetResultAsync();

            Assert.True(exists);
        }

        [Fact]
        public async Task ObjectViewDAO_Count_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var users = new List<TestUser>
            {
                new TestUser { Name = "CountTest1", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "CountTest2", Age = 25, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users);

            var count = await objectViewDAO.Count(Expr.Prop("Name").StartsWith("CountTest")).GetResultAsync();

            Assert.True(count >= 2);
        }
    }
}
