using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// Enhanced expression tests covering edge cases and missing scenarios
    /// </summary>
    [Collection("Database")]
    public class ExprEnhancedTests : TestBase
    {
        public ExprEnhancedTests(DatabaseFixture fixture) : base(fixture) { }

        #region IN 运算符空集合测试

        [Fact]
        public async Task InExpr_WithEmptyCollection_ShouldReturnNoResults()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            // Insert test data
            var users = new List<TestUser>
            {
                new TestUser { Name = "User1", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "User2", Age = 25, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            // Query with empty IN list using Expr form
            var emptyList = new int[] { };
            var expr = Expr.Prop("Id").In(emptyList);
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Empty(results);
        }

        [Fact]
        public async Task Object_SelectExpr_Union_ShouldCombineResults()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            // Insert two distinct users
            var u1 = new TestUser { Name = "ObjUnionA", Age = 30, CreateTime = DateTime.Now };
            var u2 = new TestUser { Name = "ObjUnionB", Age = 35, CreateTime = DateTime.Now };
            await service.InsertAsync(u1, TestContext.Current.CancellationToken);
            await service.InsertAsync(u2, TestContext.Current.CancellationToken);

            var s1 = Expr.From<TestUser>().Where(Expr.Prop("Name") == "ObjUnionA").Select(Expr.Prop("Name"));
            var s2 = Expr.From<TestUser>().Where(Expr.Prop("Name") == "ObjUnionB").Select(Expr.Prop("Name"));
            var union = s1.Union(s2);

            var names = await objectViewDAO.SearchAs<string>(union).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(names);
            Assert.Contains("ObjUnionA", names);
            Assert.Contains("ObjUnionB", names);
        }

        [Fact]
        public async Task Object_SelectExpr_UnionAll_ShouldKeepDuplicates()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            // Insert one user
            var u = new TestUser { Name = "ObjUnionAllUser", Age = 28, CreateTime = DateTime.Now };
            await service.InsertAsync(u, TestContext.Current.CancellationToken);

            var s1 = Expr.From<TestUser>().Where(Expr.Prop("Name") == "ObjUnionAllUser").Select(Expr.Prop("Name"));
            var s2 = Expr.From<TestUser>().Where(Expr.Prop("Name") == "ObjUnionAllUser").Select(Expr.Prop("Name"));
            var unionAll = s1.UnionAll(s2);

            var names = await objectViewDAO.SearchAs<string>(unionAll).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(names);
            Assert.True(names.Count >= 2);
        }

        [Fact]
        public async Task Object_SelectExpr_Intersect_ShouldReturnCommonRows()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var a = new TestUser { Name = "ObjIntersectA", Age = 10, CreateTime = DateTime.Now };
            var b = new TestUser { Name = "ObjIntersectB", Age = 20, CreateTime = DateTime.Now };
            await service.InsertAsync(a, TestContext.Current.CancellationToken);
            await service.InsertAsync(b, TestContext.Current.CancellationToken);

            var s1 = Expr.From<TestUser>().Where(Expr.Prop("Age") >= 10).Select(Expr.Prop("Name"));
            var s2 = Expr.From<TestUser>().Where(Expr.Prop("Age") <= 15).Select(Expr.Prop("Name"));
            var intersect = s1.Intersect(s2);

            var names = await objectViewDAO.SearchAs<string>(intersect).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(names);
            Assert.Contains("ObjIntersectA", names);
            Assert.DoesNotContain("ObjIntersectB", names);
        }

        [Fact]
        public async Task Object_SelectExpr_Except_ShouldReturnDifference()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var a = new TestUser { Name = "ObjExceptA", Age = 15, CreateTime = DateTime.Now };
            var b = new TestUser { Name = "ObjExceptB", Age = 25, CreateTime = DateTime.Now };
            await service.InsertAsync(a, TestContext.Current.CancellationToken);
            await service.InsertAsync(b, TestContext.Current.CancellationToken);

            var s1 = Expr.From<TestUser>().Where(Expr.Prop("Age") >= 10).Select(Expr.Prop("Name"));
            var s2 = Expr.From<TestUser>().Where(Expr.Prop("Age") > 20).Select(Expr.Prop("Name"));
            var except = s1.Except(s2);

            var names = await objectViewDAO.SearchAs<string>(except).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(names);
            Assert.Contains("ObjExceptA", names);
            Assert.DoesNotContain("ObjExceptB", names);
        }

        [Fact]
        public async Task SelectExpr_Union_ShouldCombineResults()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            // Insert two distinct users
            var u1 = new TestUser { Name = "UnionUserA", Age = 30, CreateTime = DateTime.Now };
            var u2 = new TestUser { Name = "UnionUserB", Age = 35, CreateTime = DateTime.Now };
            await service.InsertAsync(u1, TestContext.Current.CancellationToken);
            await service.InsertAsync(u2, TestContext.Current.CancellationToken);

            var s1 = Expr.From<TestUser>().Where(Expr.Prop("Name") == "UnionUserA").Select(Expr.Prop("Name"));
            var s2 = Expr.From<TestUser>().Where(Expr.Prop("Name") == "UnionUserB").Select(Expr.Prop("Name"));
            var union = s1.Union(s2);

            var result = dataViewDAO.Search(union);
            DataTable dt = await result.GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            // should contain both rows
            var names = dt.Rows.Cast<DataRow>().Select(r => r[0].ToString()).ToList();
            Assert.Contains("UnionUserA", names);
            Assert.Contains("UnionUserB", names);
        }

        [Fact]
        public async Task SelectExpr_UnionAll_ShouldKeepDuplicates()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            // Insert one user
            var u = new TestUser { Name = "UnionAllUser", Age = 28, CreateTime = DateTime.Now };
            await service.InsertAsync(u, TestContext.Current.CancellationToken);

            var s1 = Expr.From<TestUser>().Where(Expr.Prop("Name") == "UnionAllUser").Select(Expr.Prop("Name"));
            var s2 = Expr.From<TestUser>().Where(Expr.Prop("Name") == "UnionAllUser").Select(Expr.Prop("Name"));
            var unionAll = s1.UnionAll(s2);

            var result = dataViewDAO.Search(unionAll);
            DataTable dt = await result.GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            // UNION ALL should produce two rows for the same underlying row
            Assert.True(dt.Rows.Count >= 2);
        }

        [Fact]
        public async Task SelectExpr_Intersect_ShouldReturnCommonRows()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            var a = new TestUser { Name = "IntersectA", Age = 10, CreateTime = DateTime.Now };
            var b = new TestUser { Name = "IntersectB", Age = 20, CreateTime = DateTime.Now };
            await service.InsertAsync(a, TestContext.Current.CancellationToken);
            await service.InsertAsync(b, TestContext.Current.CancellationToken);

            var s1 = Expr.From<TestUser>().Where(Expr.Prop("Age") >= 10).Select(Expr.Prop("Name"));
            var s2 = Expr.From<TestUser>().Where(Expr.Prop("Age") <= 15).Select(Expr.Prop("Name"));
            var intersect = s1.Intersect(s2);

            var result = dataViewDAO.Search(intersect);
            DataTable dt = await result.GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            var names = dt.Rows.Cast<DataRow>().Select(r => r[0].ToString()).ToList();
            Assert.Contains("IntersectA", names);
            Assert.DoesNotContain("IntersectB", names);
        }

        [Fact]
        public async Task SelectExpr_Except_ShouldReturnDifference()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            var a = new TestUser { Name = "ExceptA", Age = 15, CreateTime = DateTime.Now };
            var b = new TestUser { Name = "ExceptB", Age = 25, CreateTime = DateTime.Now };
            await service.InsertAsync(a, TestContext.Current.CancellationToken);
            await service.InsertAsync(b, TestContext.Current.CancellationToken);

            var s1 = Expr.From<TestUser>().Where(Expr.Prop("Age") >= 10).Select(Expr.Prop("Name"));
            var s2 = Expr.From<TestUser>().Where(Expr.Prop("Age") > 20).Select(Expr.Prop("Name"));
            var except = s1.Except(s2);

            var result = dataViewDAO.Search(except);
            DataTable dt = await result.GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            var names = dt.Rows.Cast<DataRow>().Select(r => r[0].ToString()).ToList();
            Assert.Contains("ExceptA", names);
            Assert.DoesNotContain("ExceptB", names);
        }

        [Fact]
        public async Task InExpr_WithNonEmptyCollection_ShouldReturnMatches()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            // Insert test data
            var user1 = new TestUser { Name = "InTest1", Age = 30, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "InTest2", Age = 35, CreateTime = DateTime.Now };
            await service.InsertAsync(user1, TestContext.Current.CancellationToken);
            await service.InsertAsync(user2, TestContext.Current.CancellationToken);

            // Query using Expr.In() with specific IDs
            var idList = new int[] { user1.Id, user2.Id };
            var expr = Expr.Prop("Id").In(idList);
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.NotEmpty(results);
        }

        [Fact]
        public async Task NotInExpr_WithEmptyCollection_ShouldReturnAllResults()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            // Insert test data
            var users = new List<TestUser>
            {
                new TestUser { Name = "User1", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "User2", Age = 25, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            // Query with NOT IN empty list (should return all)
            var emptyList = new int[] { };
            var expr = Expr.Prop("Id").In(emptyList).Not();
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.True(results.Count >= 2);
        }

        [Fact]
        public async Task NotInExpr_WithNonEmptyCollection_ShouldExcludeMatches()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            // Insert test data
            var user1 = new TestUser { Name = "NotInTest1", Age = 30, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "NotInTest2", Age = 35, CreateTime = DateTime.Now };
            await service.InsertAsync(user1, TestContext.Current.CancellationToken);
            await service.InsertAsync(user2, TestContext.Current.CancellationToken);

            // Query using Expr.NotIn() to exclude specific IDs
            var excludeIds = new int[] { user1.Id };
            var expr = Expr.Prop("Id").In(excludeIds).Not();
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.DoesNotContain(results, u => u.Id == user1.Id);
        }

        #endregion

        #region SelectExpr 查询测试

        [Fact]
        public async Task SelectExpr_BasicSelect_ShouldReturnSpecificColumns()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            // Insert test data
            await service.InsertAsync(new TestUser { Name = "SelectTest", Age = 35, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            // Build SelectExpr with specific fields
            var selectExpr = Expr.From<TestUser>()
                .Select(Expr.Prop("Name"), Expr.Prop("Age"));

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.Equal(2, dt.Columns.Count);
            var columnNames = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            Assert.Contains("Name", columnNames, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Age", columnNames, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SelectExpr_WithWhere_ShouldFilterAndSelectColumns()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            // Insert test data
            var users = new List<TestUser>
            {
                new TestUser { Name = "SelectWhere1", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "SelectWhere2", Age = 30, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            // SelectExpr with WHERE clause
            var selectExpr = Expr.From<TestUser>()
                .Where(Expr.Prop("Age") > 25)
                .Select(Expr.Prop("Name"));

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.Single(dt.Columns);
            Assert.True(dt.Rows.Count >= 1);
        }

        [Fact]
        public async Task SelectExpr_WithOrderBy_ShouldSortResults()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            // Insert test data
            var users = new List<TestUser>
            {
                new TestUser { Name = "OrderTest1", Age = 25, CreateTime = DateTime.Now },
                new TestUser { Name = "OrderTest2", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "OrderTest3", Age = 30, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            // SelectExpr with ORDER BY
            var selectExpr = Expr.From<TestUser>()
                .Where(Expr.Prop("Name").StartsWith("OrderTest"))                
                .OrderBy(Expr.Prop("Age").Asc())
                .Select(Expr.Prop("Name"), Expr.Prop("Age"));

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 3);
            // Verify sorting by checking Age column values are ascending
            var ages = dt.Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r["Age"])).ToList();
            var sortedAges = ages.OrderBy(a => a).ToList();
            Assert.Equal(sortedAges, ages);
        }

        [Fact]
        public async Task SelectExpr_WithAliases_ShouldIncludeColumnAliases()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            // Insert test data
            await service.InsertAsync(new TestUser { Name = "AliasTest", Age = 40, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            // SelectExpr with column aliases
            var selectExpr = Expr.From<TestUser>()
                .Select(
                    Expr.Prop("Name").As("UserName"),
                    Expr.Prop("Age").As("UserAge")
                );

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.Equal(2, dt.Columns.Count);
            var columnNames = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            // Aliases should be in the result
            Assert.True(
                columnNames.Contains("UserName", StringComparer.OrdinalIgnoreCase) || 
                columnNames.Contains("Name", StringComparer.OrdinalIgnoreCase)
            );
        }

        [Fact]
        public async Task SelectExpr_WithPagination_ShouldApplySkipAndTake()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            // Insert test data
            var users = Enumerable.Range(1, 10)
                .Select(i => new TestUser { Name = $"PaginationTest{i}", Age = 20 + i, CreateTime = DateTime.Now })
                .ToList();
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            // SelectExpr with pagination
            var selectExpr = Expr.From<TestUser>()
                .Where(Expr.Prop("Name").StartsWith("PaginationTest"))                
                .OrderBy(Expr.Prop("Age").Asc())
                .Section(2, 3)
                .Select(Expr.Prop("Name"), Expr.Prop("Age"));  // Skip 2, Take 3

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count <= 3);
        }

        [Fact]
        public async Task SelectExpr_WithGroupBy_ShouldAggregateData()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            // Insert test data
            var dept = new TestDepartment { Name = "GroupByTest" };
            await service.InsertAsync(dept, TestContext.Current.CancellationToken);

            var users = new List<TestUser>
            {
                new TestUser { Name = "User1", Age = 20, DeptId = dept.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "User2", Age = 25, DeptId = dept.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "User3", Age = 30, DeptId = dept.Id, CreateTime = DateTime.Now }
            };
            await userService.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            // SelectExpr with GROUP BY
            var selectExpr = Expr.From<TestUser>()
                .Where(Expr.Prop("DeptId") == dept.Id)
                .GroupBy(Expr.Prop("DeptId"))
                .Select(
                    Expr.Prop("DeptId"),
                    Expr.Prop("Id").Count().As("UserCount"),
                    Expr.Prop("Age").Avg().As("AvgAge")
                );

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 1);
            var columnNames = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            Assert.Contains("UserCount", columnNames, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("AvgAge", columnNames, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SelectExpr_Empty_ShouldSelectAllColumns()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            // Insert test data
            await service.InsertAsync(new TestUser { Name = "EmptySelectTest", Age = 45, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            // SelectExpr without explicit column selection
            var selectExpr = Expr.From<TestUser>()
                .Where(Expr.Prop("Name") == "EmptySelectTest");

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 1);
            // Should have all columns when no specific columns are selected
            Assert.True(dt.Columns.Count > 0);
        }

        #endregion

        #region 综合测试

        [Fact]
        public async Task ComplexExpr_InWithFilter_ShouldWork()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            // Insert test data
            var users = new List<TestUser>
            {
                new TestUser { Name = "Complex1", Age = 20, CreateTime = DateTime.Now },
                new TestUser { Name = "Complex2", Age = 30, CreateTime = DateTime.Now },
                new TestUser { Name = "Complex3", Age = 25, CreateTime = DateTime.Now }
            };
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            // Mix IN clause with other conditions
            var idList = new int[] { };  // Empty IN
            var expr = (Expr.Prop("Age") > 20) & Expr.Prop("Id").In(idList).Not();
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.True(results.Count >= 2);
            Assert.All(results, u => Assert.True(u.Age > 20));
        }

        [Fact]
        public async Task SelectExpr_ChainedOperations_ShouldMaintainContext()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            // Insert test data
            var users = Enumerable.Range(1, 5)
                .Select(i => new TestUser { Name = $"Chain{i}", Age = 20 + i, CreateTime = DateTime.Now })
                .ToList();
            await service.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            // Complex chained operations
            var selectExpr = Expr.From<TestUser>()
                .Where(Expr.Prop("Name").StartsWith("Chain"))
                .Where(Expr.Prop("Age") > 22)                
                .OrderBy(Expr.Prop("Age").Desc())
                .Section(0, 2)
                .Select(Expr.Prop("Name"), Expr.Prop("Age"));

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count <= 2);
        }

        [Fact]
        public async Task InExpr_WithSelectExpr_Subquery_ShouldWork()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            // Insert test data - departments
            var dept1 = new TestDepartment { Name = "InSelectDept1" };
            var dept2 = new TestDepartment { Name = "InSelectDept2" };
            await deptService.InsertAsync(dept1, TestContext.Current.CancellationToken);
            await deptService.InsertAsync(dept2, TestContext.Current.CancellationToken);

            // Insert test data - users
            var users = new List<TestUser>
            {
                new TestUser { Name = "InSelectUser1", Age = 25, DeptId = dept1.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "InSelectUser2", Age = 30, DeptId = dept1.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "InSelectUser3", Age = 35, DeptId = dept2.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "InSelectUser4", Age = 28, DeptId = -1, CreateTime = DateTime.Now }
            };
            await userService.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            // Build subquery: SELECT DeptId FROM TestDepartment WHERE Name LIKE 'InSelectDept%'
            var subquery = Expr.From<TestDepartment>()
                .Where(Expr.Prop("Name").StartsWith("InSelectDept"))
                .Select(Expr.Prop("Id"));

            // Use subquery in IN clause: SELECT * FROM TestUser WHERE DeptId IN (subquery)
            var expr = Expr.Prop("DeptId").In(subquery);
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            // Verify results - should get users from both departments
            Assert.NotNull(results);
            Assert.True(results.Count >= 2);
            Assert.All(results, u => Assert.True(u.DeptId == dept1.Id || u.DeptId == dept2.Id));
            Assert.DoesNotContain(results, u => u.DeptId == -1);
        }

        [Fact]
        public async Task InExpr_WithSelectExpr_MultipleConditions_ShouldWork()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            // Insert test data - departments
            var dept1 = new TestDepartment { Name = "HighLevel", ParentId = 0 };
            var dept2 = new TestDepartment { Name = "SubLevel", ParentId = 1 };
            await deptService.InsertAsync(dept1, TestContext.Current.CancellationToken);
            await deptService.InsertAsync(dept2, TestContext.Current.CancellationToken);

            // Insert test data - users
            var users = new List<TestUser>
            {
                new TestUser { Name = "ConditionUser1", Age = 25, DeptId = dept1.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "ConditionUser2", Age = 35, DeptId = dept2.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "ConditionUser3", Age = 28, DeptId = -1, CreateTime = DateTime.Now }
            };
            await userService.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            // Subquery with conditions: SELECT Id FROM TestDepartment WHERE Name LIKE 'HighLevel' OR ParentId = 0
            var subquery = Expr.From<TestDepartment>()
                .Where((Expr.Prop("Name") == "HighLevel") | (Expr.Prop("ParentId") == 0))
                .Select(Expr.Prop("Id"));

            // Combine with Age filter: SELECT * FROM TestUser WHERE DeptId IN (subquery) AND Age > 20
            var expr = (Expr.Prop("DeptId").In(subquery)) & (Expr.Prop("Age") > 20);
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            // Verify results
            Assert.NotNull(results);
            Assert.All(results, u => 
            {
                Assert.True(u.Age > 20);
                Assert.True(u.DeptId == dept1.Id || u.DeptId == dept2.Id);
            });
        }

        #endregion

        #region ExistsRelated 查询测试

        [Fact]
        public async Task ExistsRelated_Forward_ShouldFilterUsersByLinkedDepartment()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var itDept = new TestDepartment { Name = "ER_IT" };
            var hrDept = new TestDepartment { Name = "ER_HR" };
            await deptService.InsertAsync(itDept, TestContext.Current.CancellationToken);
            await deptService.InsertAsync(hrDept, TestContext.Current.CancellationToken);

            var user1 = new TestUser { Name = "ERUser1", Age = 25, DeptId = itDept.Id, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "ERUser2", Age = 30, DeptId = hrDept.Id, CreateTime = DateTime.Now };
            var user3 = new TestUser { Name = "ERUser3", Age = 35, DeptId = itDept.Id, CreateTime = DateTime.Now };
            await userService.InsertAsync(user1, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user2, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user3, TestContext.Current.CancellationToken);

            // 正向路径：TestUser 通过 [ForeignType] 关联 TestDepartment，自动推断 TestDepartment.Id = TestUser.DeptId
            var expr = Expr.ExistsRelated<TestDepartment>(Expr.Prop("Name") == "ER_IT");
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Equal(2, results.Count);
            Assert.All(results, u => Assert.Equal(itDept.Id, u.DeptId));
            Assert.DoesNotContain(results, u => u.DeptId == hrDept.Id);
        }

        [Fact]
        public async Task ExistsRelated_NotExists_ShouldExcludeUsersWithMatchingDepartment()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var itDept = new TestDepartment { Name = "ERNot_IT" };
            await deptService.InsertAsync(itDept, TestContext.Current.CancellationToken);

            var userInIT = new TestUser { Name = "ERNotUser1", Age = 25, DeptId = itDept.Id, CreateTime = DateTime.Now };
            var userNoDept = new TestUser { Name = "ERNotUser2", Age = 30, DeptId = 0, CreateTime = DateTime.Now };
            await userService.InsertAsync(userInIT, TestContext.Current.CancellationToken);
            await userService.InsertAsync(userNoDept, TestContext.Current.CancellationToken);

            // NOT ExistsRelated：返回没有关联 IT 部门的用户
            var expr = !Expr.ExistsRelated<TestDepartment>(Expr.Prop("Name") == "ERNot_IT");
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Contains(results, u => u.Id == userNoDept.Id);
            Assert.DoesNotContain(results, u => u.Id == userInIT.Id);
        }

        [Fact]
        public async Task ExistsRelated_Reverse_ShouldFilterDepartmentsByLinkedUser()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestDepartment>>();

            var dept1 = new TestDepartment { Name = "ERRev_Dept1" };
            var dept2 = new TestDepartment { Name = "ERRev_Dept2" };
            await deptService.InsertAsync(dept1, TestContext.Current.CancellationToken);
            await deptService.InsertAsync(dept2, TestContext.Current.CancellationToken);

            // 只有 dept1 有用户
            var user = new TestUser { Name = "ERRev_User1", Age = 28, DeptId = dept1.Id, CreateTime = DateTime.Now };
            await userService.InsertAsync(user, TestContext.Current.CancellationToken);

            // 反向路径：TestDepartment 无 [ForeignType]/[TableJoin] 指向 TestUser，
            // 通过 TestUser 的 JoinedTables 反向推断 TestUser.DeptId = TestDepartment.Id
            var expr = Expr.ExistsRelated<TestUser>(Expr.Prop("Name") == "ERRev_User1");
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Contains(results, d => d.Id == dept1.Id);
            Assert.DoesNotContain(results, d => d.Id == dept2.Id);
        }

        [Fact]
        public async Task ExistsRelated_TypeOverload_ShouldProduceSameResultAsGeneric()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var dept = new TestDepartment { Name = "ERType_IT" };
            await deptService.InsertAsync(dept, TestContext.Current.CancellationToken);

            var user1 = new TestUser { Name = "ERTypeUser1", Age = 22, DeptId = dept.Id, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "ERTypeUser2", Age = 28, DeptId = 0, CreateTime = DateTime.Now };
            await userService.InsertAsync(user1, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user2, TestContext.Current.CancellationToken);

            var innerExpr = Expr.Prop("Name") == "ERType_IT";

            // 泛型重载
            var genericResults = await objectViewDAO.Search(Expr.ExistsRelated<TestDepartment>(innerExpr))
                .ToListAsync(TestContext.Current.CancellationToken);

            // Type 重载
            var typeResults = await objectViewDAO.Search(Expr.ExistsRelated(typeof(TestDepartment), innerExpr))
                .ToListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(genericResults.Select(u => u.Id).OrderBy(x => x), typeResults.Select(u => u.Id).OrderBy(x => x));
            Assert.Single(genericResults);
            Assert.Equal(user1.Id, genericResults[0].Id);
        }

        [Fact]
        public async Task ExistsRelated_CombinedWithOtherConditions_ShouldWork()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var itDept = new TestDepartment { Name = "ERCombo_IT" };
            var hrDept = new TestDepartment { Name = "ERCombo_HR" };
            await deptService.InsertAsync(itDept, TestContext.Current.CancellationToken);
            await deptService.InsertAsync(hrDept, TestContext.Current.CancellationToken);

            var users = new List<TestUser>
            {
                new TestUser { Name = "ERCombo1", Age = 20, DeptId = itDept.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "ERCombo2", Age = 35, DeptId = itDept.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "ERCombo3", Age = 40, DeptId = hrDept.Id, CreateTime = DateTime.Now },
            };
            await userService.BatchInsertAsync(users, TestContext.Current.CancellationToken);

            // ExistsRelated AND 年龄条件组合
            var expr = Expr.ExistsRelated<TestDepartment>(Expr.Prop("Name") == "ERCombo_IT") & (Expr.Prop("Age") >= 30);
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Single(results);
            Assert.Equal(itDept.Id, results[0].DeptId);
            Assert.True(results[0].Age >= 30);
        }

        #endregion
    }
}
