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
    public class DataDAOComplexValueTests : TestBase
    {
        public DataDAOComplexValueTests(DatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public async Task DataDAO_UpdateWithReplaceFunction_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "TestUser123", Age = 10, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            // Act - 使用字符串函数replace更新Name字段，将"123"替换为"456"
            var updateExpr = new UpdateExpr
            {
                Source = Expr.Table<TestUser>(),
                Sets = new List<(string, ValueTypeExpr)> 
                {
                    ("Name", Expr.Func("REPLACE", Expr.Property("Name"), Expr.Const("123"), Expr.Const("456")))
                },
                Where = Expr.Exp<TestUser>(u => u.Id == user.Id)
            };
            int affected = dataDao.Update(updateExpr);
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.Equal(1, affected);
            Assert.Equal("TestUser456", retrieved?.Name);
        }

        [Fact]
        public async Task DataDAO_UpdateWithMultipleFunctions_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "testuser", Age = 10, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            // Act - 使用多个字符串函数，将Name字段转换为大写
            var updateExpr = new UpdateExpr
            {
                Source = Expr.Table<TestUser>(),
                Sets = new List<(string, ValueTypeExpr)> 
                {
                    ("Name", Expr.Func("UPPER", Expr.Property("Name")))
                },
                Where = Expr.Exp<TestUser>(u => u.Id == user.Id)
            };
            int affected = dataDao.Update(updateExpr);
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.Equal(1, affected);
            Assert.Equal("TESTUSER", retrieved?.Name);
        }

        [Fact]
        public async Task DataDAO_UpdateAsyncWithFunction_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "UserWithSpaces", Age = 10, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            // Act - 使用函数去除字符串两端的空格（假设数据库支持TRIM函数）
            var updateExpr = new UpdateExpr
            {
                Source = Expr.Table<TestUser>(),
                Sets = new List<(string, ValueTypeExpr)> 
                {
                    ("Name", Expr.Func("TRIM", Expr.Property("Name")))
                },
                Where = Expr.Exp<TestUser>(u => u.Id == user.Id)
            };
            int affected = await dataDao.UpdateAsync(updateExpr);
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.Equal(1, affected);
            Assert.Equal("UserWithSpaces", retrieved?.Name); // 这里应该还是原样，因为没有空格
        }

        [Fact]
        public async Task DataDAO_UpdateWithFunctionAndParameters_ShouldWork()
        {
            // Arrange
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataDao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            var user = new TestUser { Name = "OldName", Age = 10, CreateTime = DateTime.Now };
            await service.InsertAsync(user);

            // Act - 使用函数并传入参数
            string oldValue = "Old";
            string newValue = "New";
            var updateExpr = new UpdateExpr
            {
                Source = Expr.Table<TestUser>(),
                Sets = new List<(string, ValueTypeExpr)> 
                {
                    ("Name", Expr.Func("REPLACE", Expr.Property("Name"), Expr.Const(oldValue), Expr.Const(newValue)))
                },
                Where = Expr.Exp<TestUser>(u => u.Id == user.Id)
            };
            int affected = dataDao.Update(updateExpr);
            var retrieved = await viewService.GetObjectAsync(user.Id);

            // Assert
            Assert.Equal(1, affected);
            Assert.Equal("NewName", retrieved?.Name);
        }
    }
}