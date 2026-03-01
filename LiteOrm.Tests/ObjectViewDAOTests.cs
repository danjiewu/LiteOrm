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
            var results = objectViewDAO.Search($"{ageExpr}");

            // Assert
            Assert.NotNull(results);
            Assert.Equal(2, results.Count); // Should return User2 (30) and User3 (25)
            Assert.All(results, user => Assert.True(user.Age > ageThreshold));
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
            var results = objectViewDAO.Search($"{complexExpr}");

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
            Assert.True(results.Count >= 2); // Should return all users
        }

        [Fact]
        public async Task ObjectViewDAO_SearchOne_WithExprString_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            
            // Insert test data
            var user = new TestUser { Name = "TestUser", Age = 25, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            // Act
            // Test with ExprString syntax using Expr as formatted fragment
            var userName = "TestUser";
            var nameExpr = Expr.Prop("Name") == userName;
            var result = objectViewDAO.SearchOne($"{nameExpr}");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(userName, result.Name);
            Assert.Equal(25, result.Age);
        }

        [Fact]
        public async Task ObjectViewDAO_SearchOne_WithExprString_ComplexExpr_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            
            // Insert test data
            var user = new TestUser { Name = "ComplexUser", Age = 30, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            // Act
            // Test with complex Expr in ExprString
            var complexExpr = (Expr.Prop("Name") == "ComplexUser") & (Expr.Prop("Age") == 30);
            var result = objectViewDAO.SearchOne($"{complexExpr}");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("ComplexUser", result.Name);
            Assert.Equal(30, result.Age);
        }

        [Fact]
        public async Task ObjectViewDAO_Search_WithExprString_OrderBy_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            
            // Insert test data
            var users = new List<TestUser>
            {
                new TestUser { Name = "User1", Age = 30, CreateTime = DateTime.Now.AddDays(-3) },
                new TestUser { Name = "User2", Age = 20, CreateTime = DateTime.Now.AddDays(-1) },
                new TestUser { Name = "User3", Age = 25, CreateTime = DateTime.Now.AddDays(-2) }
            };
            await service.BatchInsertAsync(users);

            // Act
            // Test with OrderBy in ExprString
            var orderByExpr = Expr.Where<TestUser>(u => true).OrderBy("Age");
            var results = objectViewDAO.Search($"{orderByExpr}");

            // Assert
            Assert.NotNull(results);
            Assert.Equal(3, results.Count);
            // 验证按年龄升序排序
            Assert.True(results[0].Age <= results[1].Age && results[1].Age <= results[2].Age);
        }

        [Fact]
        public async Task ObjectViewDAO_Search_WithExprString_Section_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            
            // Insert test data
            var users = new List<TestUser>
            {
                new TestUser { Name = "User1", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "User2", Age = 21, CreateTime = DateTime.Now },
                new TestUser { Name = "User3", Age = 22, CreateTime = DateTime.Now },
                new TestUser { Name = "User4", Age = 23, CreateTime = DateTime.Now },
                new TestUser { Name = "User5", Age = 24, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users);

            // Act
            // Test with Section in ExprString
            var sectionExpr = Expr.Where<TestUser>(u => true).Section(1, 2); // 跳过1条，取2条
            var results = objectViewDAO.Search($"{sectionExpr}");

            // Assert
            Assert.NotNull(results);
            Assert.Equal(2, results.Count);
        }


    }
}
