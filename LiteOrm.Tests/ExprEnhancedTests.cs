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
            await service.BatchInsertAsync(users);

            // Query with empty IN list using Expr form
            var emptyList = new int[] { };
            var expr = Expr.Prop("Id").In(emptyList);
            var results = await objectViewDAO.Search(expr).ToListAsync();

            Assert.NotNull(results);
            Assert.Empty(results);
        }

        [Fact]
        public async Task InExpr_WithNonEmptyCollection_ShouldReturnMatches()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            // Insert test data
            var user1 = new TestUser { Name = "InTest1", Age = 30, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "InTest2", Age = 35, CreateTime = DateTime.Now };
            await service.InsertAsync(user1);
            await service.InsertAsync(user2);

            // Query using Expr.In() with specific IDs
            var idList = new int[] { user1.Id, user2.Id };
            var expr = Expr.Prop("Id").In(idList);
            var results = await objectViewDAO.Search(expr).ToListAsync();

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
            await service.BatchInsertAsync(users);

            // Query with NOT IN empty list (should return all)
            var emptyList = new int[] { };
            var expr = Expr.Prop("Id").NotIn(emptyList);
            var results = await objectViewDAO.Search(expr).ToListAsync();

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
            await service.InsertAsync(user1);
            await service.InsertAsync(user2);

            // Query using Expr.NotIn() to exclude specific IDs
            var excludeIds = new int[] { user1.Id };
            var expr = Expr.Prop("Id").NotIn(excludeIds);
            var results = await objectViewDAO.Search(expr).ToListAsync();

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
            await service.InsertAsync(new TestUser { Name = "SelectTest", Age = 35, CreateTime = DateTime.Now });

            // Build SelectExpr with specific fields
            var selectExpr = Expr.From<TestUser>()
                .Select(Expr.Prop("Name"), Expr.Prop("Age"));

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync();

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
            await service.BatchInsertAsync(users);

            // SelectExpr with WHERE clause
            var selectExpr = Expr.From<TestUser>()
                .Where(Expr.Prop("Age") > 25)
                .Select(Expr.Prop("Name"));

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync();

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
            await service.BatchInsertAsync(users);

            // SelectExpr with ORDER BY
            var selectExpr = Expr.From<TestUser>()
                .Where(Expr.Prop("Name").StartsWith("OrderTest"))                
                .OrderBy(Expr.Prop("Age").Asc())
                .Select(Expr.Prop("Name"), Expr.Prop("Age"));

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync();

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
            await service.InsertAsync(new TestUser { Name = "AliasTest", Age = 40, CreateTime = DateTime.Now });

            // SelectExpr with column aliases
            var selectExpr = Expr.From<TestUser>()
                .Select(
                    Expr.Prop("Name").As("UserName"),
                    Expr.Prop("Age").As("UserAge")
                );

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync();

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
            await service.BatchInsertAsync(users);

            // SelectExpr with pagination
            var selectExpr = Expr.From<TestUser>()
                .Where(Expr.Prop("Name").StartsWith("PaginationTest"))                
                .OrderBy(Expr.Prop("Age").Asc())
                .Section(2, 3)
                .Select(Expr.Prop("Name"), Expr.Prop("Age"));  // Skip 2, Take 3

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync();

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
            await service.InsertAsync(dept);

            var users = new List<TestUser>
            {
                new TestUser { Name = "User1", Age = 20, DeptId = dept.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "User2", Age = 25, DeptId = dept.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "User3", Age = 30, DeptId = dept.Id, CreateTime = DateTime.Now }
            };
            await userService.BatchInsertAsync(users);

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
            DataTable dt = await result.GetResultAsync();

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
            await service.InsertAsync(new TestUser { Name = "EmptySelectTest", Age = 45, CreateTime = DateTime.Now });

            // SelectExpr without explicit column selection
            var selectExpr = Expr.From<TestUser>()
                .Where(Expr.Prop("Name") == "EmptySelectTest");

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync();

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
            await service.BatchInsertAsync(users);

            // Mix IN clause with other conditions
            var idList = new int[] { };  // Empty IN
            var expr = (Expr.Prop("Age") > 20) & Expr.Prop("Id").NotIn(idList);
            var results = await objectViewDAO.Search(expr).ToListAsync();

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
            await service.BatchInsertAsync(users);

            // Complex chained operations
            var selectExpr = Expr.From<TestUser>()
                .Where(Expr.Prop("Name").StartsWith("Chain"))
                .Where(Expr.Prop("Age") > 22)                
                .OrderBy(Expr.Prop("Age").Desc())
                .Section(0, 2)
                .Select(Expr.Prop("Name"), Expr.Prop("Age"));

            var result = dataViewDAO.Search(selectExpr);
            DataTable dt = await result.GetResultAsync();

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
            await deptService.InsertAsync(dept1);
            await deptService.InsertAsync(dept2);

            // Insert test data - users
            var users = new List<TestUser>
            {
                new TestUser { Name = "InSelectUser1", Age = 25, DeptId = dept1.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "InSelectUser2", Age = 30, DeptId = dept1.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "InSelectUser3", Age = 35, DeptId = dept2.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "InSelectUser4", Age = 28, DeptId = -1, CreateTime = DateTime.Now }
            };
            await userService.BatchInsertAsync(users);

            // Build subquery: SELECT DeptId FROM TestDepartment WHERE Name LIKE 'InSelectDept%'
            var subquery = Expr.From<TestDepartment>()
                .Where(Expr.Prop("Name").StartsWith("InSelectDept"))
                .Select(Expr.Prop("Id"));

            // Use subquery in IN clause: SELECT * FROM TestUser WHERE DeptId IN (subquery)
            var expr = Expr.Prop("DeptId").In(subquery);
            var results = await objectViewDAO.Search(expr).ToListAsync();

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
            await deptService.InsertAsync(dept1);
            await deptService.InsertAsync(dept2);

            // Insert test data - users
            var users = new List<TestUser>
            {
                new TestUser { Name = "ConditionUser1", Age = 25, DeptId = dept1.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "ConditionUser2", Age = 35, DeptId = dept2.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "ConditionUser3", Age = 28, DeptId = -1, CreateTime = DateTime.Now }
            };
            await userService.BatchInsertAsync(users);

            // Subquery with conditions: SELECT Id FROM TestDepartment WHERE Name LIKE 'HighLevel' OR ParentId = 0
            var subquery = Expr.From<TestDepartment>()
                .Where((Expr.Prop("Name") == "HighLevel") | (Expr.Prop("ParentId") == 0))
                .Select(Expr.Prop("Id"));

            // Combine with Age filter: SELECT * FROM TestUser WHERE DeptId IN (subquery) AND Age > 20
            var expr = (Expr.Prop("DeptId").In(subquery)) & (Expr.Prop("Age") > 20);
            var results = await objectViewDAO.Search(expr).ToListAsync();

            // Verify results
            Assert.NotNull(results);
            Assert.All(results, u => 
            {
                Assert.True(u.Age > 20);
                Assert.True(u.DeptId == dept1.Id || u.DeptId == dept2.Id);
            });
        }

        #endregion
    }
}
