using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    [Collection("Database")]
    public class ServiceSearchAsIntegrationTests : TestBase
    {
        public ServiceSearchAsIntegrationTests(DatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public async Task ViewService_SearchAsLambda_ShouldReturnProjectedList()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewService<TestUser>>();
            await userService.InsertAsync(new TestUser { Name = "AsLambda 1", Age = 20, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "AsLambda 2", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "AsLambda 3", Age = 40, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var result = viewService.SearchAs(q => q.Where(u => u.Age >= 30).Select(u => new UserNameAge { Name = u.Name, Age = u.Age }));

            Assert.Equal(2, result.Count);
            Assert.All(result, r => Assert.True(r.Age >= 30));
            Assert.Contains(result, r => r.Name == "AsLambda 2");
        }

        [Fact]
        public async Task ViewService_SearchOneAsLambda_ShouldReturnSingle()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewService<TestUser>>();
            await userService.InsertAsync(new TestUser { Name = "AsOne 1", Age = 20, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "AsOne 2", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var result = viewService.SearchOneAs(q => q.Where(u => u.Name == "AsOne 2").Select(u => new UserNameAge { Name = u.Name, Age = u.Age }));

            Assert.NotNull(result);
            Assert.Equal("AsOne 2", result.Name);
            Assert.Equal(30, result.Age);
        }

        [Fact]
        public async Task ViewService_SearchAsLambda_WithoutProjection_ShouldReturnEntities()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewService<TestUser>>();
            await userService.InsertAsync(new TestUser { Name = "AsNoProj 1", Age = 20, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "AsNoProj 2", Age = 35, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            // TResult 推断为 TestUser（Where 后无 Select，走 ToSelectExpr 包装默认列）
            var result = viewService.SearchAs(q => q.Where(u => u.Age >= 30));

            Assert.Single(result);
            Assert.Equal("AsNoProj 2", result[0].Name);
        }

        [Fact]
        public async Task ViewService_SearchAsAsyncLambda_ShouldReturnProjectedList()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            await userService.InsertAsync(new TestUser { Name = "AsAsync 1", Age = 20, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "AsAsync 2", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "AsAsync 3", Age = 40, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var result = await viewService.SearchAsAsync(q => q.Where(u => u.Age >= 30).Select(u => new UserNameAge { Name = u.Name, Age = u.Age }), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(2, result.Count);
            Assert.All(result, r => Assert.True(r.Age >= 30));
        }

        [Fact]
        public async Task ViewService_SearchOneAsAsyncLambda_ShouldReturnSingle()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            await userService.InsertAsync(new TestUser { Name = "AsOneAsync 1", Age = 20, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "AsOneAsync 2", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var result = await viewService.SearchOneAsAsync(q => q.Where(u => u.Name == "AsOneAsync 2").Select(u => new UserNameAge { Name = u.Name, Age = u.Age }), cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal("AsOneAsync 2", result.Name);
            Assert.Equal(30, result.Age);
        }

        [Fact]
        public async Task ViewService_SearchAs_SelectExpr_ShouldReturnProjectedList()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewService<TestUser>>();
            await userService.InsertAsync(new TestUser { Name = "AsExpr 1", Age = 20, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "AsExpr 2", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var selectExpr = new SelectExpr(
                Expr.From<TestUser>().Where(Expr.Prop("Name").StartsWith("AsExpr")),
                Expr.Prop("Name").As("Name"),
                Expr.Prop("Age").As("Age"));

            var result = viewService.SearchAs<UserNameAge>(selectExpr);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, r => r.Name == "AsExpr 2" && r.Age == 30);
        }

        [Fact]
        public async Task ViewService_SearchOneAs_SelectExpr_ShouldReturnSingle()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewService<TestUser>>();
            await userService.InsertAsync(new TestUser { Name = "AsOneExpr 1", Age = 20, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "AsOneExpr 2", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var selectExpr = new SelectExpr(
                Expr.From<TestUser>().Where(Expr.Prop("Name") == "AsOneExpr 2"),
                Expr.Prop("Name").As("Name"),
                Expr.Prop("Age").As("Age"));

            var result = viewService.SearchOneAs<UserNameAge>(selectExpr);

            Assert.NotNull(result);
            Assert.Equal("AsOneExpr 2", result.Name);
            Assert.Equal(30, result.Age);
        }

        [Fact]
        public async Task ViewService_SearchAsLambda_AnonymousProjection_ShouldReturnList()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewService<TestUser>>();
            await userService.InsertAsync(new TestUser { Name = "Anon 1", Age = 20, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "Anon 2", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "Anon 3", Age = 40, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var result = viewService.SearchAs(q => q.Where(u => u.Age >= 30).Select(u => new { u.Name, u.Age }));

            Assert.Equal(2, result.Count);
            Assert.All(result, r => Assert.True(r.Age >= 30));
            Assert.Contains(result, r => r.Name == "Anon 2");
        }

        [Fact]
        public async Task ViewService_SearchOneAsLambda_AnonymousProjection_ShouldReturnSingle()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewService<TestUser>>();
            await userService.InsertAsync(new TestUser { Name = "AnonOne 1", Age = 20, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "AnonOne 2", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var result = viewService.SearchOneAs(q => q.Where(u => u.Name == "AnonOne 2").Select(u => new { u.Name, u.Age }));

            Assert.NotNull(result);
            Assert.Equal("AnonOne 2", result.Name);
            Assert.Equal(30, result.Age);
        }

        [Fact]
        public async Task ViewService_SearchAsAsyncLambda_AnonymousProjection_ShouldReturnList()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            await userService.InsertAsync(new TestUser { Name = "AnonAsync 1", Age = 20, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "AnonAsync 2", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "AnonAsync 3", Age = 40, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var result = await viewService.SearchAsAsync(q => q.Where(u => u.Age >= 30).Select(u => new { u.Name, u.Age }), cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(2, result.Count);
            Assert.All(result, r => Assert.True(r.Age >= 30));
            Assert.Contains(result, r => r.Name == "AnonAsync 3");
        }

        [Fact]
        public async Task ViewService_SearchOneAsAsyncLambda_AnonymousProjection_ShouldReturnSingle()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();
            await userService.InsertAsync(new TestUser { Name = "AnonOneAsync 1", Age = 20, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "AnonOneAsync 2", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var result = await viewService.SearchOneAsAsync(q => q.Where(u => u.Name == "AnonOneAsync 2").Select(u => new { u.Name, u.Age }), cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal("AnonOneAsync 2", result.Name);
            Assert.Equal(30, result.Age);
        }

        private class UserNameAge
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }
    }
}
