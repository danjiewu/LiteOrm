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
        public async Task ObjectViewDAO_Search_WithExprString_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            
            var users = new List<TestUser>
            {
                new TestUser { Name = "User1", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "User2", Age = 30, CreateTime = DateTime.Now },
                new TestUser { Name = "User3", Age = 25, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users);

            var ageThreshold = 20;
            var ageExpr = Expr.Prop("Age") > ageThreshold;
            var results = objectViewDAO.Search($"WHERE {ageExpr}");

            Assert.NotNull(results);
            var resultList = results.ToList();
            Assert.Equal(2, resultList.Count);
            Assert.All(resultList, user => Assert.True(user.Age > ageThreshold));
        }

        [Fact]
        public async Task ObjectViewDAO_Search_WithExprString_ComplexExpr_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            
            var users = new List<TestUser>
            {
                new TestUser { Name = "User1", Age = 20, CreateTime = DateTime.Now.AddDays(-10) },
                new TestUser { Name = "User2", Age = 30, CreateTime = DateTime.Now.AddDays(-5) },
                new TestUser { Name = "User3", Age = 25, CreateTime = DateTime.Now.AddDays(-1) }
            };
            await service.BatchInsertAsync(users);

            var minAge = 20;
            var startDate = DateTime.Now.AddDays(-7);
            var complexExpr = (Expr.Prop("Age") > minAge) & (Expr.Prop("CreateTime") > startDate);
            var results = objectViewDAO.Search($"WHERE {complexExpr}");

            Assert.NotNull(results);
            Assert.All(results, user => {
                Assert.True(user.Age > minAge);
                Assert.True(user.CreateTime > startDate);
            });
        }

        [Fact]
        public async Task ObjectViewDAO_Search_WithExprString_EmptyWhere_ShouldReturnAll()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            
            var users = new List<TestUser>
            {
                new TestUser { Name = "UserA", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "UserB", Age = 30, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users);

            var results = objectViewDAO.Search($"");

            Assert.NotNull(results);
            var resultList = results.ToList();
            Assert.True(resultList.Count >= 2);
        }

        [Fact]
        public async Task ObjectViewDAO_Search_WithExprString_MixedSqlAndExpr_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            
            var users = new List<TestUser>
            {
                new TestUser { Name = "User1", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "User2", Age = 30, CreateTime = DateTime.Now },
                new TestUser { Name = "User3", Age = 25, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users);

            var ageThreshold = 20;
            var ageExpr = Expr.Prop("Age") > ageThreshold;
            var results = objectViewDAO.Search($"WHERE {ageExpr} AND Name LIKE 'User%'");

            Assert.NotNull(results);
            var resultList = results.ToList();
            Assert.True(resultList.Count > 0);
            Assert.All(resultList, user => {
                Assert.True(user.Age > ageThreshold);
                Assert.StartsWith("User", user.Name);
            });
        }


    }
}
