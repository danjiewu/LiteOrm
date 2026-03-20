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
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            var results = await objectViewDAO.Search(Expr.Prop("Name").StartsWith("SearchTest")).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.True(results.Count >= 2);
        }

        [Fact]
        public async Task ObjectViewDAO_GetObject_ByKey_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user = new TestUser { Name = "GetObjectTest", Age = 25, CreateTime = DateTime.Now };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            var result = await objectViewDAO.GetObject(user.Id).FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal(user.Name, result.Name);
        }

        [Fact]
        public async Task ObjectViewDAO_Exists_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user = new TestUser { Name = "ExistsTest", Age = 35, CreateTime = DateTime.Now };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            var exists = await objectViewDAO.Exists(Expr.Prop("Name") == "ExistsTest").GetResultAsync(TestContext.Current.CancellationToken);

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
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            var count = await objectViewDAO.Count(Expr.Prop("Name").StartsWith("CountTest")).GetResultAsync(TestContext.Current.CancellationToken);

            Assert.True(count >= 2);
        }

        // ── Query<TResult> tests ──────────────────────────────────────────────

        /// <summary>
        /// Query&lt;T&gt; with WHERE only; TResult == T; readerFunc is null.
        /// GetConverter&lt;T&gt;(reader) routes to GetConverter&lt;T&gt;() via TableInfoProvider.
        /// </summary>
        [Fact]
        public async Task Query_Filter_ReturnsSameType()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var users = new List<TestUser>
            {
                new TestUser { Name = "QueryFilterTest1", Age = 25, CreateTime = DateTime.Now },
                new TestUser { Name = "QueryFilterTest2", Age = 35, CreateTime = DateTime.Now },
                new TestUser { Name = "QueryFilterTest3", Age = 15, CreateTime = DateTime.Now },
            };
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            var results = await dao.SearchAs(
                q => q.Where(u => u.Age > 20)
                      .Where(u => u.Name.StartsWith("QueryFilterTest")))
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.True(r.Age > 20));
        }

        /// <summary>
        /// Anonymous type projection with no readerFunc.
        /// EnumerableResult calls GetConverter&lt;TResult&gt;(reader) which routes to
        /// CompileAnonymousConverter — matches ctor parameter names to reader column names.
        /// </summary>
        [Fact]
        public async Task Query_AnonymousProjection_ReadsColumnsByName()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user = new TestUser { Name = "AnonProjTest", Age = 42, CreateTime = DateTime.Now };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            var results = await dao.SearchAs(
                q => q.Where(u => u.Name == "AnonProjTest")
                      .Select(u => new { u.Name, u.Age }))
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Single(results);
            Assert.Equal("AnonProjTest", results[0].Name);
            Assert.Equal(42, results[0].Age);
        }

        /// <summary>
        /// Same as above but uses the async path (IAsyncEnumerable / FirstOrDefaultAsync),
        /// which goes through AsyncEnumerator.MoveNextAsync → GetConverter&lt;TResult&gt;(reader).
        /// </summary>
        [Fact]
        public async Task Query_AnonymousProjection_Async()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user = new TestUser { Name = "AsyncAnonTest", Age = 28, CreateTime = DateTime.Now };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            var result = await dao.SearchAs(
                q => q.Where(u => u.Name == "AsyncAnonTest")
                      .Select(u => new { u.Name, u.Age }))
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal("AsyncAnonTest", result.Name);
            Assert.Equal(28, result.Age);
        }

        /// <summary>
        /// Chained Where + OrderBy + Take; verifies ordering and result count.
        /// </summary>
        [Fact]
        public async Task Query_OrderByTake_ReturnsTopNOrdered()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var users = new List<TestUser>
            {
                new TestUser { Name = "QueryTopTest1", Age = 10, CreateTime = DateTime.Now },
                new TestUser { Name = "QueryTopTest2", Age = 30, CreateTime = DateTime.Now },
                new TestUser { Name = "QueryTopTest3", Age = 20, CreateTime = DateTime.Now },
            };
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            var results = await dao.SearchAs<TestUser>(
                q => q.Where(u => u.Name.StartsWith("QueryTopTest"))
                      .OrderBy(u => u.Age)
                      .Take(2))
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, results.Count);
            Assert.Equal(10, results[0].Age);
            Assert.Equal(20, results[1].Age);
        }

        /// <summary>
        /// Scalar string projection; TResult inferred as string from Select(u => u.Name).
        /// readerFunc omitted: string is a scalar type so CompileScalarConverter&lt;string&gt;
        /// is generated automatically (reads column 0 via GetString).
        /// </summary>
        [Fact]
        public async Task Query_ScalarProjection_AutoConverter()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var users = new List<TestUser>
            {
                new TestUser { Name = "ScalarProjTest1", Age = 10, CreateTime = DateTime.Now },
                new TestUser { Name = "ScalarProjTest2", Age = 20, CreateTime = DateTime.Now },
            };
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            var names = await dao.SearchAs(
                q => q.Where(u => u.Name.StartsWith("ScalarProjTest"))
                      .OrderBy(u => u.Name)
                      .Select(u => u.Name))
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, names.Count);
            Assert.Equal("ScalarProjTest1", names[0]);
            Assert.Equal("ScalarProjTest2", names[1]);
        }

        // ── Anonymous type projection tests ──────────────────────────────────

        /// <summary>
        /// Projects int + string + DateTime columns.
        /// Verifies that CompileAnonymousConverter uses the typed reader methods
        /// (GetInt32, GetString, GetDateTime) for each constructor parameter.
        /// </summary>
        [Fact]
        public async Task Query_AnonymousProjection_MixedColumnTypes()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var createTime = new DateTime(2024, 6, 1, 12, 0, 0);
            var user = new TestUser { Name = "MixedTypeTest", Age = 33, CreateTime = createTime };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            var results = await dao.SearchAs(
                q => q.Where(u => u.Name == "MixedTypeTest")
                      .Select(u => new { u.Id, u.Name, u.Age, u.CreateTime }))
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Single(results);
            Assert.True(results[0].Id > 0);
            Assert.Equal("MixedTypeTest", results[0].Name);
            Assert.Equal(33, results[0].Age);
            Assert.Equal(createTime, results[0].CreateTime);
        }

        /// <summary>
        /// Projects columns in reversed order (Age before Name).
        /// Because CompileAnonymousConverter maps ctor parameter names to reader column
        /// names (not ordinals), the result must still be correct regardless of order.
        /// </summary>
        [Fact]
        public async Task Query_AnonymousProjection_ColumnOrderIndependent()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user = new TestUser { Name = "OrderIndepTest", Age = 77, CreateTime = DateTime.Now };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            var results = await dao.SearchAs(
                q => q.Where(u => u.Name == "OrderIndepTest")
                      .Select(u => new { u.Age, u.Name }))   // Age first, then Name
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Single(results);
            Assert.Equal(77, results[0].Age);
            Assert.Equal("OrderIndepTest", results[0].Name);
        }

        /// <summary>
        /// Uses ObjectViewDAO&lt;TestUserView&gt; (LEFT JOIN TestDepartments) and projects an
        /// anonymous type that spans both the base table (Name) and the joined table (DeptName).
        /// Verifies that CompileAnonymousConverter resolves cross-join columns by name.
        /// </summary>
        [Fact]
        public async Task Query_AnonymousProjection_FromJoinedView()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUserView>>();

            var dept = new TestDepartment { Name = "Engineering" };
            await deptService.InsertAsync(dept, TestContext.Current.CancellationToken);

            var user = new TestUser { Name = "JoinViewTest", Age = 30, CreateTime = DateTime.Now, DeptId = dept.Id };
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            var results = await dao.SearchAs(
                q => q.Where(u => u.Name == "JoinViewTest")
                      .Select(u => new { u.Name, u.DeptName }))
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Single(results);
            Assert.Equal("JoinViewTest", results[0].Name);
            Assert.Equal("Engineering", results[0].DeptName);
        }

        // ── byte[] column tests ───────────────────────────────────────────────

        /// <summary>
        /// Inserts a user with a non-null <see cref="byte[]"/> Avatar and reads it back via
        /// GetObject, exercising the GetFieldValue&lt;byte[]&gt; branch in
        /// <see cref="DataReaderConverter"/>.
        /// </summary>
        [Fact]
        public async Task Query_ByteArray_RoundTrip()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            byte[] avatar = [1, 2, 3, 4, 5, 255, 128, 0];
            var user = new TestUser { Name = "ByteArrayRoundTripTest", Age = 20, CreateTime = DateTime.Now, Avatar = avatar };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            var result = await dao.GetObject(user.Id).FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal(avatar, result.Avatar);
        }

        /// <summary>
        /// Inserts a user with a null Avatar and reads it back, verifying that the
        /// IsDBNull guard in <see cref="DataReaderConverter"/> returns null correctly.
        /// </summary>
        [Fact]
        public async Task Query_ByteArray_NullValue()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user = new TestUser { Name = "ByteArrayNullTest", Age = 21, CreateTime = DateTime.Now, Avatar = null };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            var result = await dao.GetObject(user.Id).FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Null(result.Avatar);
        }

        /// <summary>
        /// Projects a single binary column via Select(u =&gt; u.Avatar), which routes through
        /// <see cref="DataReaderConverter.CompileScalarConverter{TResult}"/> with TResult = byte[].
        /// </summary>
        [Fact]
        public async Task Query_ScalarProjection_ByteArray()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            byte[] avatar = [10, 20, 30];
            var user = new TestUser { Name = "ByteArrayScalarTest", Age = 22, CreateTime = DateTime.Now, Avatar = avatar };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            var result = await dao.SearchAs(
                q => q.Where(u => u.Name == "ByteArrayScalarTest")
                      .Select(u => u.Avatar))
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.Equal(avatar, result);
        }

        /// <summary>
        /// Projects Name + Avatar into an anonymous type, exercising the byte[] path inside
        /// <see cref="DataReaderConverter.CompileAnonymousConverter{TResult}"/>.
        /// </summary>
        [Fact]
        public async Task Query_AnonymousProjection_ByteArray()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dao = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            byte[] avatar = [0xDE, 0xAD, 0xBE, 0xEF];
            var user = new TestUser { Name = "ByteArrayAnonTest", Age = 23, CreateTime = DateTime.Now, Avatar = avatar };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            var result = await dao.SearchAs(
                q => q.Where(u => u.Name == "ByteArrayAnonTest")
                      .Select(u => new { u.Name, u.Avatar }))
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal("ByteArrayAnonTest", result.Name);
            Assert.Equal(avatar, result.Avatar);
        }
    }
}
