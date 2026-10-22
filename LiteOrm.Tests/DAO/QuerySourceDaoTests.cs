using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 覆盖 <see cref="QuerySource{T1}"/> 从容器直接解析后的执行能力，以及
    /// <see cref="ObjectViewDAO{T}"/> 接受 <see cref="IQueryable{T}"/> 的重载。
    /// </summary>
    [Collection("Database")]
    public class QuerySourceDaoTests : TestBase
    {
        public QuerySourceDaoTests(DatabaseFixture fixture) : base(fixture) { }

        /// <summary>容器中直接可用的查询源，已绑定执行器。</summary>
        private QuerySource<TestUser> Query => ServiceProvider.GetRequiredService<QuerySource<TestUser>>();

        /// <summary>仅在测试 SearchAs / Search 这类 DAO 自身 API 时需要。</summary>
        private ObjectViewDAO<TestUser> Dao => ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

        private async Task SeedAsync(string prefix, int count = 3)
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            var users = new List<TestUser>();
            for (int i = 0; i < count; i++)
            {
                users.Add(new TestUser
                {
                    Name = $"{prefix}{i}",
                    Age = 20 + i,
                    CreateTime = DateTime.Now
                });
            }
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task QuerySource_FromContainer_ForEach_ReturnsRows()
        {
            await SeedAsync("QsForEach");

            int count = 0;
            foreach (TestUser _ in Query.Where(u => u.Name!.StartsWith("QsForEach")))
                count++;

            Assert.Equal(3, count);
        }

        [Fact]
        public async Task QuerySource_FromContainer_ToList_ReturnsRows()
        {
            await SeedAsync("QsToList");

            List<TestUser> users = Query.Where(u => u.Name!.StartsWith("QsToList")).ToList();

            Assert.Equal(3, users.Count);
        }

        [Fact]
        public async Task QuerySource_FromContainer_FirstOrDefault_ReturnsRow()
        {
            await SeedAsync("QsFirst");

            TestUser? user = Query.Where(u => u.Name == "QsFirst1").FirstOrDefault();

            Assert.NotNull(user);
            Assert.Equal(21, user!.Age);
        }

        [Fact]
        public async Task QuerySource_FromContainer_ToListAsync_ReturnsRows()
        {
            await SeedAsync("QsAsync", 2);

            // 每个测试开始前都会清表，这里直接用注入的查询源取全量
            List<TestUser> users = await Query.ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, users.Count);
        }

        [Fact]
        public async Task QuerySource_FromContainer_AwaitForEach_ReturnsRows()
        {
            await SeedAsync("QsAwait", 2);

            int count = 0;
            await foreach (TestUser _ in Query.ToAsyncEnumerable(TestContext.Current.CancellationToken))
                count++;

            Assert.Equal(2, count);
        }

        [Fact]
        public async Task QuerySource_FromContainer_CountAsync_ReturnsCount()
        {
            await SeedAsync("QsCount", 2);

            int count = await Query.CountAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, count);
        }

        [Fact]
        public async Task QuerySource_AsSource_ToListAsync_ReturnsRows()
        {
            await SeedAsync("QsAsSource", 2);

            // Where 之后是 IQueryable，用 AsSource 收窄后可直接调用 QuerySource 的异步方法
            List<TestUser> users = await Query
                .Where(u => u.Name!.StartsWith("QsAsSource"))
                .AsSource()
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, users.Count);
        }

        [Fact]
        public async Task QuerySource_AsSource_AfterProjection_ToListAsync()
        {
            await SeedAsync("QsAsProj", 2);

            List<string> names = await Query
                .Where(u => u.Name!.StartsWith("QsAsProj"))
                .OrderBy(u => u.Name)
                .Select(u => u.Name!)
                .AsSource()
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, names.Count);
            Assert.Contains("QsAsProj0", names);
        }

        [Fact]
        public async Task QuerySource_AsSource_CountAsync_ReturnsCount()
        {
            await SeedAsync("QsAsCount", 2);

            int count = await Query
                .Where(u => u.Name!.StartsWith("QsAsCount"))
                .AsSource()
                .CountAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, count);
        }

        [Fact]
        public async Task QuerySource_AsSource_WithInjectedExecutor_KeepsBinding()
        {
            await SeedAsync("QsAsBind", 1);

            IQueryable<TestUser> chain = Query.Where(u => u.Name!.StartsWith("QsAsBind"));
            var executor = ServiceProvider.GetRequiredService<IQuerySourceExecutor<TestUser>>();

            // 链上已绑定执行器，传入执行器也不重建
            Assert.Same(chain, chain.AsSource(executor));
        }

        [Fact]
        public async Task QuerySource_FromContainer_DaoSearchAs_ReturnsProjection()
        {
            await SeedAsync("QsProj", 2);

            List<string> names = await Dao
                .SearchAs(Query
                    .Where(u => u.Name!.StartsWith("QsProj"))
                    .OrderBy(u => u.Name)
                    .Select(u => u.Name!))
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, names.Count);
            Assert.Contains("QsProj0", names);
        }

        [Fact]
        public async Task SearchAs_IQueryable_ReturnsProjection()
        {
            await SeedAsync("QsSearchAs", 2);

            List<TestUser> users = await Dao
                .SearchAs(Query.Where(u => u.Name!.StartsWith("QsSearchAs")))
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, users.Count);
        }

        [Fact]
        public async Task SearchAs_ArbitraryIQueryable_Works()
        {
            await SeedAsync("QsAnySource", 2);

            // 不限制源类型：普通集合的 AsQueryable() 也能转成 SQL 执行
            List<TestUser> users = await Dao
                .SearchAs(new List<TestUser>().AsQueryable().Where(u => u.Name!.StartsWith("QsAnySource")))
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, users.Count);
        }

        [Fact]
        public async Task Search_IQueryable_ReturnsEntities()
        {
            await SeedAsync("QsSearch", 2);

            List<TestUser> users = await Dao
                .Search(Query.Where(u => u.Name!.StartsWith("QsSearch")))
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, users.Count);
        }

        [Fact]
        public async Task SearchAs_IQueryable_WrongRootType_Throws()
        {
            await SeedAsync("QsWrong", 1);

            Assert.Throws<ArgumentException>(() =>
                Dao.SearchAs(Expr.From<TestDepartment>().Where(d => d.Name != null)));
        }

        [Fact]
        public void QuerySource_FromContainer_JoinAtRuntime_BuildsJoinExpression()
        {
            // 实例方法在 Lambda 外直接拼装真实 JOIN 表达式，不再抛异常
            SqlSegment expr = Query
                .Join<TestDepartment>((u, d) => u.DeptId == d.Id)
                .GetQueryExpr();

            var from = Assert.IsType<FromExpr>(expr);
            var join = Assert.Single(from.Joins);
            Assert.Equal(TableJoinType.Left, join.JoinType);
            Assert.IsType<TableExpr>(join.Source);
            Assert.Equal(typeof(TestDepartment), ((TableExpr)join.Source!).Type);
            Assert.NotNull(join.On);
        }
    }
}