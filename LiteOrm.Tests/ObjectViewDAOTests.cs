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
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            
            // Insert test data
            var users = new List<TestUser>
            {
                new TestUser { Name = "User1", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "User2", Age = 30, CreateTime = DateTime.Now },
                new TestUser { Name = "User3", Age = 25, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users);

            // Act
            // Test with ExprString syntax using Expr as formatted fragment
            var ageThreshold = 20;
            var ageExpr = Expr.Prop("Age") > ageThreshold;
            var results = objectViewDAO.Search($"WHERE {ageExpr}");

            // Assert
            Assert.NotNull(results);
            var resultList = results.ToList();
            Assert.Equal(2, resultList.Count); // Should return User2 (30) and User3 (25)
            Assert.All(resultList, user => Assert.True(user.Age > ageThreshold));
        }

        [Fact]
        public async Task ObjectViewDAO_Search_WithExprString_ComplexExpr_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            
            // Insert test data
            var users = new List<TestUser>
            {
                new TestUser { Name = "User1", Age = 20, CreateTime = DateTime.Now.AddDays(-10) },
                new TestUser { Name = "User2", Age = 30, CreateTime = DateTime.Now.AddDays(-5) },
                new TestUser { Name = "User3", Age = 25, CreateTime = DateTime.Now.AddDays(-1) }
            };
            await service.BatchInsertAsync(users);

            // Act
            // Test with complex Expr in ExprString
            var minAge = 20;
            var startDate = DateTime.Now.AddDays(-7);
            var complexExpr = (Expr.Prop("Age") > minAge) & (Expr.Prop("CreateTime") > startDate);
            var results = objectViewDAO.Search($"WHERE {complexExpr}");

            // Assert
            Assert.NotNull(results);
            // Should return User2 and User3 (both > 20 and created within last 7 days)
            Assert.All(results, user => {
                Assert.True(user.Age > minAge);
                Assert.True(user.CreateTime > startDate);
            });
        }

        [Fact]
        public async Task ObjectViewDAO_Search_WithExprString_EmptyWhere_ShouldReturnAll()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            
            // Insert test data
            var users = new List<TestUser>
            {
                new TestUser { Name = "UserA", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "UserB", Age = 30, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users);

            // Act
            // Test with empty ExprString
            var results = objectViewDAO.Search($"");

            // Assert
            Assert.NotNull(results);
            var resultList = results.ToList();
            Assert.True(resultList.Count >= 2); // Should return all users
        }





        [Fact]
        public async Task ObjectViewDAO_Search_WithExprString_MixedSqlAndExpr_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            
            // Insert test data
            var users = new List<TestUser>
            {
                new TestUser { Name = "User1", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "User2", Age = 30, CreateTime = DateTime.Now },
                new TestUser { Name = "User3", Age = 25, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users);

            // Act
            // Test with mixed SQL and Expr in ExprString
            var ageThreshold = 20;
            var ageExpr = Expr.Prop("Age") > ageThreshold;
            var results = objectViewDAO.Search($"WHERE {ageExpr} AND Name LIKE 'User%'");

            // Assert
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
