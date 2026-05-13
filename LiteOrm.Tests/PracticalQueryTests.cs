using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 实际数据库查询应用测试类，测试 DeleteExpr、UpdateExpr、TableJoin 等表达式
    /// 在实际数据库操作中的使用。
    /// </summary>
    [Collection("Database")]
    public class PracticalQueryTests : TestBase
    {
        public PracticalQueryTests(DatabaseFixture fixture) : base(fixture) { }

        #region DeleteExpr 实际数据库操作测试

        [Fact]
        public async Task DeleteExpr_BasicDelete_ShouldDeleteMatchingRows()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user1 = new TestUser { Name = "DeleteTest1", Age = 20, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "DeleteTest2", Age = 30, CreateTime = DateTime.Now };
            var user3 = new TestUser { Name = "DeleteTest3", Age = 40, CreateTime = DateTime.Now };
            await service.InsertAsync(user1, TestContext.Current.CancellationToken);
            await service.InsertAsync(user2, TestContext.Current.CancellationToken);
            await service.InsertAsync(user3, TestContext.Current.CancellationToken);

            var deleteExpr = Expr.Prop("Name") == "DeleteTest2";
            var deleteResult = await service.DeleteAsync(deleteExpr, cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(deleteResult > 0);

            var remainingUsers = await objectViewDAO.Search(Expr.Prop("Name").StartsWith("DeleteTest")).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, remainingUsers.Count);
            Assert.DoesNotContain(remainingUsers, u => u.Name == "DeleteTest2");
        }

        [Fact]
        public async Task DeleteExpr_WithAndCondition_ShouldDeleteMatchingRows()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user1 = new TestUser { Name = "AndDelete1", Age = 25, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "AndDelete2", Age = 25, CreateTime = DateTime.Now };
            var user3 = new TestUser { Name = "AndDelete3", Age = 35, CreateTime = DateTime.Now };
            await service.InsertAsync(user1, TestContext.Current.CancellationToken);
            await service.InsertAsync(user2, TestContext.Current.CancellationToken);
            await service.InsertAsync(user3, TestContext.Current.CancellationToken);

            var deleteExpr = (Expr.Prop("Name") == "AndDelete1").And(Expr.Prop("Age") == 25);
            var deleteResult = await service.DeleteAsync(deleteExpr, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(1, deleteResult);

            var remainingUsers = await objectViewDAO.Search(Expr.Prop("Name").StartsWith("AndDelete")).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, remainingUsers.Count);
        }

        [Fact]
        public async Task DeleteExpr_WithOrCondition_ShouldDeleteMatchingRows()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user1 = new TestUser { Name = "OrDelete1", Age = 20, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "OrDelete2", Age = 30, CreateTime = DateTime.Now };
            var user3 = new TestUser { Name = "OrDelete3", Age = 40, CreateTime = DateTime.Now };
            await service.InsertAsync(user1, TestContext.Current.CancellationToken);
            await service.InsertAsync(user2, TestContext.Current.CancellationToken);
            await service.InsertAsync(user3, TestContext.Current.CancellationToken);

            var deleteExpr = (Expr.Prop("Age") <= 25).Or(Expr.Prop("Age") >= 40);
            var deleteResult = await service.DeleteAsync(deleteExpr, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(2, deleteResult);

            var remainingUsers = await objectViewDAO.Search(Expr.Prop("Name").StartsWith("OrDelete")).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Single(remainingUsers);
        }

        [Fact]
        public async Task DeleteExpr_WithNotCondition_ShouldDeleteNonMatchingRows()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user1 = new TestUser { Name = "NotDelete1", Age = 20, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "NotDelete2", Age = 30, CreateTime = DateTime.Now };
            var user3 = new TestUser { Name = "NotDelete3", Age = 40, CreateTime = DateTime.Now };
            await service.InsertAsync(user1, TestContext.Current.CancellationToken);
            await service.InsertAsync(user2, TestContext.Current.CancellationToken);
            await service.InsertAsync(user3, TestContext.Current.CancellationToken);

            var deleteExpr = (Expr.Prop("Age") == 30).Not();
            var deleteResult = await service.DeleteAsync(deleteExpr, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(2, deleteResult);

            var remainingUsers = await objectViewDAO.Search(Expr.Prop("Name").StartsWith("NotDelete")).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Single(remainingUsers);
            Assert.Equal("NotDelete2", remainingUsers[0].Name);
        }

        #endregion

        #region UpdateExpr 实际数据库操作测试

        [Fact]
        public async Task UpdateExpr_BasicUpdate_ShouldUpdateMatchingRows()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user1 = new TestUser { Name = "UpdateTest1", Age = 20, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "UpdateTest2", Age = 30, CreateTime = DateTime.Now };
            await service.InsertAsync(user1, TestContext.Current.CancellationToken);
            await service.InsertAsync(user2, TestContext.Current.CancellationToken);

            var updateExpr = new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Name") == "UpdateTest1");
            updateExpr.Set(("Age", (ValueTypeExpr)Expr.Const(25)));

            var updateResult = await service.UpdateAsync(updateExpr, cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(updateResult > 0);

            var updatedUser = (await objectViewDAO.Search(Expr.Prop("Name") == "UpdateTest1").ToListAsync(TestContext.Current.CancellationToken)).FirstOrDefault();
            Assert.NotNull(updatedUser);
            Assert.Equal(25, updatedUser.Age);
        }

        [Fact]
        public async Task UpdateExpr_MultipleColumns_ShouldUpdateAllColumns()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user = new TestUser { Name = "MultiUpdate", Age = 20, CreateTime = DateTime.Now };
            await service.InsertAsync(user, TestContext.Current.CancellationToken);

            var updateExpr = new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Name") == "MultiUpdate");
            updateExpr.Set(
                ("Name", (ValueTypeExpr)Expr.Const("MultiUpdate_Updated")),
                ("Age", (ValueTypeExpr)Expr.Const(99))
            );

            var updateResult = await service.UpdateAsync(updateExpr, cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(updateResult > 0);

            var updatedUser = (await objectViewDAO.Search(Expr.Prop("Name") == "MultiUpdate_Updated").ToListAsync(TestContext.Current.CancellationToken)).FirstOrDefault();
            Assert.NotNull(updatedUser);
            Assert.Equal(99, updatedUser.Age);
        }

        [Fact]
        public async Task UpdateExpr_WithAndCondition_ShouldUpdateMatchingRows()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user1 = new TestUser { Name = "AndUpdate1", Age = 25, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "AndUpdate2", Age = 25, CreateTime = DateTime.Now };
            await service.InsertAsync(user1, TestContext.Current.CancellationToken);
            await service.InsertAsync(user2, TestContext.Current.CancellationToken);

            var updateExpr = new UpdateExpr(new TableExpr(typeof(TestUser)),
                (Expr.Prop("Name") == "AndUpdate1").And(Expr.Prop("Age") == 25));
            updateExpr.Set(("Age", (ValueTypeExpr)Expr.Const(50)));

            var updateResult = await service.UpdateAsync(updateExpr, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(1, updateResult);

            var updatedUser = (await objectViewDAO.Search(Expr.Prop("Name") == "AndUpdate1").ToListAsync(TestContext.Current.CancellationToken)).FirstOrDefault();
            Assert.NotNull(updatedUser);
            Assert.Equal(50, updatedUser.Age);

            var unchangedUser = (await objectViewDAO.Search(Expr.Prop("Name") == "AndUpdate2").ToListAsync(TestContext.Current.CancellationToken)).FirstOrDefault();
            Assert.NotNull(unchangedUser);
            Assert.Equal(25, unchangedUser.Age);
        }

        [Fact]
        public async Task UpdateExpr_WithOrCondition_ShouldUpdateMatchingRows()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user1 = new TestUser { Name = "OrUpdate1", Age = 20, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "OrUpdate2", Age = 30, CreateTime = DateTime.Now };
            await service.InsertAsync(user1, TestContext.Current.CancellationToken);
            await service.InsertAsync(user2, TestContext.Current.CancellationToken);

            var updateExpr = new UpdateExpr(new TableExpr(typeof(TestUser)),
                (Expr.Prop("Age") <= 20).Or(Expr.Prop("Age") >= 30));
            updateExpr.Set(("Age", (ValueTypeExpr)Expr.Const(99)));

            var updateResult = await service.UpdateAsync(updateExpr, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(2, updateResult);

            var allUsers = await objectViewDAO.Search(Expr.Prop("Name").StartsWith("OrUpdate")).ToListAsync(TestContext.Current.CancellationToken);
            Assert.All(allUsers, u => Assert.Equal(99, u.Age));
        }

        #endregion

        #region CommonTableExpr 实际数据库查询测试

        [Fact]
        public async Task CommonTableExpr_BasicQuery_ShouldReturnFilteredRows()
        {
            var ct = TestContext.Current.CancellationToken;
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            await userService.BatchInsertAsync(
            [
                new TestUser { Name = "CteBasic_User1", Age = 22, DeptId = 1, CreateTime = DateTime.Now },
                new TestUser { Name = "CteBasic_User2", Age = 31, DeptId = 1, CreateTime = DateTime.Now },
                new TestUser { Name = "CteBasic_User3", Age = 38, DeptId = 2, CreateTime = DateTime.Now }
            ], ct);

            var cteDef = new SelectExpr(
                Expr.From<TestUser>().Where(Expr.Prop("Name").StartsWith("CteBasic_")),
                Expr.Prop("Name").As("Name"),
                Expr.Prop("Age").As("Age"),
                Expr.Prop("DeptId").As("DeptId"));

            var query = cteDef.With("AdultUsers")
                .Where(Expr.Prop("Age") >= 30)
                .OrderBy(Expr.Prop("Name").Asc())
                .Select(Expr.Prop("Name"), Expr.Prop("Age"), Expr.Prop("DeptId"));

            var prepared = query.ToPreparedSql(dataViewDAO.CreateSqlBuildContext(), dataViewDAO.SqlBuilder);
            var dt = await dataViewDAO.Search(query).GetResultAsync(ct);

            Assert.Contains("WITH", prepared.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("AdultUsers", prepared.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, dt.Rows.Count);

            var names = dt.Rows.Cast<DataRow>().Select(r => r["Name"]?.ToString()).ToList();
            Assert.Equal(["CteBasic_User2", "CteBasic_User3"], names);
        }

        [Fact]
        public async Task CommonTableExpr_AggregateQuery_ShouldReturnGroupedRows()
        {
            var ct = TestContext.Current.CancellationToken;
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            var dept1 = new TestDepartment { Name = "CteStatsDept1" };
            var dept2 = new TestDepartment { Name = "CteStatsDept2" };
            await deptService.InsertAsync(dept1, ct);
            await deptService.InsertAsync(dept2, ct);

            await userService.BatchInsertAsync(
            [
                new TestUser { Name = "CteStats_User1", Age = 25, DeptId = dept1.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "CteStats_User2", Age = 35, DeptId = dept1.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "CteStats_User3", Age = 26, DeptId = dept2.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "CteStats_User4", Age = 31, DeptId = dept2.Id, CreateTime = DateTime.Now },
                new TestUser { Name = "CteStats_User5", Age = 42, DeptId = dept2.Id, CreateTime = DateTime.Now }
            ], ct);

            var cteDef = Expr.From<TestUser>()
                .Where(Expr.Prop("Name").StartsWith("CteStats_"))
                .GroupBy(Expr.Prop("DeptId"))
                .Select(
                    Expr.Prop("DeptId"),
                    Expr.Prop("Id").Count().As("UserCount"),
                    Expr.Prop("Age").Avg().As("AvgAge"));

            var query = cteDef.With("DeptStats")
                .Where(Expr.Prop("UserCount") >= 2)
                .OrderBy(Expr.Prop("UserCount").Desc())
                .Select(Expr.Prop("DeptId"), Expr.Prop("UserCount"), Expr.Prop("AvgAge"));

            var prepared = query.ToPreparedSql(dataViewDAO.CreateSqlBuildContext(), dataViewDAO.SqlBuilder);
            var dt = await dataViewDAO.Search(query).GetResultAsync(ct);

            Assert.Contains("WITH", prepared.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DeptStats", prepared.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, dt.Rows.Count);

            var rows = dt.Rows.Cast<DataRow>()
                .Select(r => new
                {
                    DeptId = Convert.ToInt32(r["DeptId"]),
                    UserCount = Convert.ToInt32(r["UserCount"])
                })
                .OrderBy(r => r.DeptId)
                .ToList();

            Assert.Equal(dept1.Id, rows[0].DeptId);
            Assert.Equal(2, rows[0].UserCount);
            Assert.Equal(dept2.Id, rows[1].DeptId);
            Assert.Equal(3, rows[1].UserCount);
        }

        [Fact]
        public async Task CommonTableExpr_ReuseSameExprInUnion_ShouldKeepSingleDefinitionAndReturnRows()
        {
            var ct = TestContext.Current.CancellationToken;
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            await userService.BatchInsertAsync(
            [
                new TestUser { Name = "CteUnionReuse_User1", Age = 22, DeptId = 1, CreateTime = DateTime.Now },
                new TestUser { Name = "CteUnionReuse_User2", Age = 27, DeptId = 1, CreateTime = DateTime.Now },
                new TestUser { Name = "CteUnionReuse_User3", Age = 31, DeptId = 2, CreateTime = DateTime.Now },
                new TestUser { Name = "CteUnionReuse_User4", Age = 36, DeptId = 2, CreateTime = DateTime.Now }
            ], ct);

            var adultUsers = Expr.From<TestUser>()
                .Where(Expr.Prop("Name").StartsWith("CteUnionReuse_"))
                .Select(
                    Expr.Prop("Name").As("Name"),
                    Expr.Prop("Age").As("Age"),
                    Expr.Prop("DeptId").As("DeptId"))
                .With("AdultUsers");

            var query = adultUsers
                .Where(Expr.Prop("Age") < 30)
                .Select(Expr.Prop("Name"), Expr.Prop("Age"), Expr.Prop("DeptId"), Expr.Const("18-29").As("AgeGroup"))
                .UnionAll(
                    adultUsers
                        .Where(Expr.Prop("Age") >= 30)
                        .Select(Expr.Prop("Name"), Expr.Prop("Age"), Expr.Prop("DeptId"), Expr.Const("30+").As("AgeGroup")));

            var prepared = query.ToPreparedSql(dataViewDAO.CreateSqlBuildContext(), dataViewDAO.SqlBuilder);
            var dt = await dataViewDAO.Search(query).GetResultAsync(ct);

            var cteName = dataViewDAO.SqlBuilder.ToSqlName("AdultUsers");
            Assert.Equal(1, Regex.Matches(prepared.Sql, $"{cteName} AS", RegexOptions.IgnoreCase).Count);   
            Assert.Equal(4, dt.Rows.Count);

            var groups = dt.Rows.Cast<DataRow>()
                .Select(r => r["AgeGroup"]?.ToString())
                .OrderBy(x => x)
                .ToList();
            Assert.Equal(["18-29", "18-29", "30+", "30+"], groups);
        }

        [Fact]
        public void CommonTableExpr_DuplicateEquivalentAliases_ShouldKeepSingleDefinition()
        {
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            var first = Expr.From<TestUser>()
                .Select(Expr.Prop("Name").As("Name"))
                .With("DupCte");
            var second = Expr.From<TestUser>()
                .Select(Expr.Prop("Name").As("Name"))
                .With("DupCte");

            var query = first
                .Select(Expr.Prop("Name"))
                .Union(second.Select(Expr.Prop("Name")));

            var prepared = query.ToPreparedSql(dataViewDAO.CreateSqlBuildContext(), dataViewDAO.SqlBuilder);
            var cteName = dataViewDAO.SqlBuilder.ToSqlName("DupCte");
            Assert.Equal(1, Regex.Matches(prepared.Sql, $"{cteName} AS", RegexOptions.IgnoreCase).Count);
        }

        [Fact]
        public void CommonTableExpr_DuplicateDifferentAliases_ShouldThrowInvalidOperationException()
        {
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            var first = Expr.From<TestUser>()
                .Select(Expr.Prop("Name").As("Name"))
                .With("DupCte");
            var second = Expr.From<TestUser>()
                .Where(Expr.Prop("Age") > 30)
                .Select(Expr.Prop("Name").As("Name"))
                .With("DupCte");

            var query = first
                .Select(Expr.Prop("Name"))
                .Union(second.Select(Expr.Prop("Name")));

            var ex = Assert.Throws<InvalidOperationException>(() => query.ToPreparedSql(dataViewDAO.CreateSqlBuildContext(), dataViewDAO.SqlBuilder));
            Assert.Contains("DupCte", ex.Message);
        }

        #endregion

        #region TableJoinExpr 实际数据库查询测试

        [Fact]
        public async Task TableJoinExpr_LeftJoin_ShouldReturnAllLeftRows()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            var dept = new TestDepartment { Name = "JoinTestDept" };
            await deptService.InsertAsync(dept, TestContext.Current.CancellationToken);

            var user1 = new TestUser { Name = "JoinUser1", Age = 25, DeptId = dept.Id, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "JoinUser2", Age = 30, DeptId = 0, CreateTime = DateTime.Now };
            await userService.InsertAsync(user1, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user2, TestContext.Current.CancellationToken);

            var userViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUserView>>();

            Expr query = null;

            var results = await userViewDAO.Search(query).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.True(results.Count >= 2);
        }

        [Fact]
        public async Task TableJoinExpr_InnerJoin_ShouldReturnOnlyMatchedRows()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            var dept = new TestDepartment { Name = "InnerJoinDept" };
            await deptService.InsertAsync(dept, TestContext.Current.CancellationToken);

            var user1 = new TestUser { Name = "InnerJoinUser1", Age = 25, DeptId = dept.Id, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "InnerJoinUser2", Age = 30, DeptId = 0, CreateTime = DateTime.Now };
            await userService.InsertAsync(user1, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user2, TestContext.Current.CancellationToken);

            var userViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUserView>>();

            var query = Expr.From<TestUserView>()
                .Where(Expr.Prop("DeptName").IsNotNull());

            var results = await userViewDAO.Search(query).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.All(results, u => Assert.NotNull(u.DeptName));
        }

        #endregion

        #region Subquery 实际数据库查询测试

        [Fact]
        public async Task Subquery_InSelect_ShouldWork()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var dept1 = new TestDepartment { Name = "SubqueryDept1" };
            var dept2 = new TestDepartment { Name = "SubqueryDept2" };
            await deptService.InsertAsync(dept1, TestContext.Current.CancellationToken);
            await deptService.InsertAsync(dept2, TestContext.Current.CancellationToken);

            var user1 = new TestUser { Name = "SubqueryUser1", Age = 25, DeptId = dept1.Id, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "SubqueryUser2", Age = 30, DeptId = dept2.Id, CreateTime = DateTime.Now };
            var user3 = new TestUser { Name = "SubqueryUser3", Age = 35, DeptId = -1, CreateTime = DateTime.Now };
            await userService.InsertAsync(user1, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user2, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user3, TestContext.Current.CancellationToken);

            var subquery = Expr.From<TestDepartment>()
                .Where(Expr.Prop("Name").StartsWith("SubqueryDept"))
                .Select(Expr.Prop("Id"));

            var expr = Expr.Prop("DeptId").In(subquery);
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.True(results.Count >= 2);
            Assert.DoesNotContain(results, u => u.DeptId == -1);
            Assert.Contains(results, u => u.DeptId == dept1.Id);
            Assert.Contains(results, u => u.DeptId == dept2.Id);
        }

        [Fact]
        public async Task Subquery_NotInSelect_ShouldExcludeMatchedRows()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var dept = new TestDepartment { Name = "NotInDept" };
            await deptService.InsertAsync(dept, TestContext.Current.CancellationToken);

            var user1 = new TestUser { Name = "NotInUser1", Age = 25, DeptId = dept.Id, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "NotInUser2", Age = 30, DeptId = 0, CreateTime = DateTime.Now };
            await userService.InsertAsync(user1, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user2, TestContext.Current.CancellationToken);

            var subquery = Expr.From<TestDepartment>()
                .Where(Expr.Prop("Name") == "NotInDept")
                .Select(Expr.Prop("Id"));

            var expr = Expr.Prop("DeptId").In(subquery).Not();
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.DoesNotContain(results, u => u.DeptId == dept.Id);
            Assert.Contains(results, u => u.DeptId == 0);
        }

        [Fact]
        public async Task Subquery_ExistsInWhere_ShouldFilterCorrectly()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var itDept = new TestDepartment { Name = "SubExists_IT" };
            var hrDept = new TestDepartment { Name = "SubExists_HR" };
            await deptService.InsertAsync(itDept, TestContext.Current.CancellationToken);
            await deptService.InsertAsync(hrDept, TestContext.Current.CancellationToken);

            var user1 = new TestUser { Name = "SubExists_User1", Age = 25, DeptId = itDept.Id, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "SubExists_User2", Age = 30, DeptId = hrDept.Id, CreateTime = DateTime.Now };
            var user3 = new TestUser { Name = "SubExists_User3", Age = 35, DeptId = 0, CreateTime = DateTime.Now };
            await userService.InsertAsync(user1, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user2, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user3, TestContext.Current.CancellationToken);

            var expr = Expr.Exists<TestDepartment>(Expr.Prop("Id") == Expr.Prop("T0", "DeptId"));
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.True(results.Count >= 2);
            Assert.DoesNotContain(results, u => u.DeptId == 0);
        }

        #endregion

        #region Complex Query 实际数据库测试

        [Fact]
        public async Task ComplexQuery_AllClauses_ShouldWork()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            var dept1 = new TestDepartment { Name = "ComplexDept1" };
            var dept2 = new TestDepartment { Name = "ComplexDept2" };
            await deptService.InsertAsync(dept1, TestContext.Current.CancellationToken);
            await deptService.InsertAsync(dept2, TestContext.Current.CancellationToken);

            for (int i = 1; i <= 5; i++)
            {
                var deptId = i <= 3 ? dept1.Id : dept2.Id;
                await userService.InsertAsync(new TestUser
                {
                    Name = $"ComplexUser{i}",
                    Age = 20 + i,
                    DeptId = deptId,
                    CreateTime = DateTime.Now
                }, TestContext.Current.CancellationToken);
            }

            var query = Expr.From<TestUser>()
                .Where(Expr.Prop("Age") > 22)
                .GroupBy(Expr.Prop("DeptId"))
                .Having(Expr.Prop("Id").Count() >= 1)
                .OrderBy(Expr.Prop("DeptId").Asc())
                .Select(
                    Expr.Prop("DeptId"),
                    Expr.Prop("Id").Count().As("UserCount"),
                    Expr.Prop("Age").Avg().As("AvgAge")
                );

            var dt = await dataViewDAO.Search(query).GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 1);
            Assert.Contains(dt.Columns.Cast<DataColumn>(), c => c.ColumnName.Equals("UserCount", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(dt.Columns.Cast<DataColumn>(), c => c.ColumnName.Equals("AvgAge", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ComplexQuery_Pagination_ShouldReturnCorrectPage()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            for (int i = 1; i <= 10; i++)
            {
                await userService.InsertAsync(new TestUser
                {
                    Name = $"PageUser{i}",
                    Age = 20 + i,
                    CreateTime = DateTime.Now
                }, TestContext.Current.CancellationToken);
            }

            var query = Expr.From<TestUser>()
                .Where(Expr.Prop("Name").StartsWith("PageUser"))
                .OrderBy(Expr.Prop("Age").Asc())
                .Section(2, 3)
                .Select(Expr.Prop("Name"), Expr.Prop("Age"));

            var dt = await dataViewDAO.Search(query).GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count <= 3);
        }

        [Fact]
        public async Task ComplexQuery_CaseWhen_ShouldWork()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            await userService.InsertAsync(new TestUser { Name = "CaseUser1", Age = 15, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "CaseUser2", Age = 25, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "CaseUser3", Age = 35, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var caseExpr = Expr.Case(
                new[] {
                    new KeyValuePair<LogicExpr, ValueTypeExpr>(Expr.Prop("Age") < 18, Expr.Const("Minor")),
                    new KeyValuePair<LogicExpr, ValueTypeExpr>(Expr.Prop("Age") < 30, Expr.Const("Young")),
                    new KeyValuePair<LogicExpr, ValueTypeExpr>(Expr.Prop("Age") < 50, Expr.Const("Adult"))
                },
                Expr.Const("Senior")
            );

            var query = Expr.From<TestUser>()
                .Where(Expr.Prop("Name").StartsWith("CaseUser"))
                .Select(
                    Expr.Prop("Name"),
                    Expr.Prop("Age"),
                    caseExpr.As("AgeGroup")
                );

            var dt = await dataViewDAO.Search(query).GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 3);
            Assert.Contains(dt.Columns.Cast<DataColumn>(), c => c.ColumnName.Equals("AgeGroup", StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Union/Intersect/Except 查询测试

        [Fact]
        public async Task Union_Query_ShouldCombineResults()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            await userService.InsertAsync(new TestUser { Name = "UnionA", Age = 20, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "UnionB", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var s1 = Expr.From<TestUser>().Where(Expr.Prop("Name") == "UnionA").Select(Expr.Prop("Name"));
            var s2 = Expr.From<TestUser>().Where(Expr.Prop("Name") == "UnionB").Select(Expr.Prop("Name"));
            var union = s1.Union(s2);

            var dt = await dataViewDAO.Search(union).GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            var names = dt.Rows.Cast<DataRow>().Select(r => r[0].ToString()).ToList();
            Assert.Contains("UnionA", names);
            Assert.Contains("UnionB", names);
        }

        [Fact]
        public async Task Except_Query_ShouldReturnDifference()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            await userService.InsertAsync(new TestUser { Name = "ExceptA", Age = 15, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "ExceptB", Age = 25, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var s1 = Expr.From<TestUser>().Where(Expr.Prop("Age") >= 10).Select(Expr.Prop("Name"));
            var s2 = Expr.From<TestUser>().Where(Expr.Prop("Age") > 20).Select(Expr.Prop("Name"));
            var except = s1.Except(s2);

            var dt = await dataViewDAO.Search(except).GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            var names = dt.Rows.Cast<DataRow>().Select(r => r[0].ToString()).ToList();
            Assert.Contains("ExceptA", names);
            Assert.DoesNotContain("ExceptB", names);
        }

        #endregion

        #region ExistsRelated 实际数据库测试

        [Fact]
        public async Task ExistsRelated_Forward_FilterByDepartment()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var itDept = new TestDepartment { Name = "Practical_IT" };
            var hrDept = new TestDepartment { Name = "Practical_HR" };
            await deptService.InsertAsync(itDept, TestContext.Current.CancellationToken);
            await deptService.InsertAsync(hrDept, TestContext.Current.CancellationToken);

            var user1 = new TestUser { Name = "Practical_User1", Age = 25, DeptId = itDept.Id, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "Practical_User2", Age = 30, DeptId = hrDept.Id, CreateTime = DateTime.Now };
            var user3 = new TestUser { Name = "Practical_User3", Age = 35, DeptId = itDept.Id, CreateTime = DateTime.Now };
            await userService.InsertAsync(user1, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user2, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user3, TestContext.Current.CancellationToken);

            var expr = Expr.ExistsRelated<TestDepartment>(Expr.Prop("Name") == "Practical_IT");
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Equal(2, results.Count);
            Assert.All(results, u => Assert.Equal(itDept.Id, u.DeptId));
        }

        [Fact]
        public async Task ExistsRelated_NotExists_FilterByDepartment()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var itDept = new TestDepartment { Name = "NotExists_IT" };
            await deptService.InsertAsync(itDept, TestContext.Current.CancellationToken);

            var user1 = new TestUser { Name = "NotExists_User1", Age = 25, DeptId = itDept.Id, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "NotExists_User2", Age = 30, DeptId = 0, CreateTime = DateTime.Now };
            await userService.InsertAsync(user1, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user2, TestContext.Current.CancellationToken);

            var expr = !Expr.ExistsRelated<TestDepartment>(Expr.Prop("Name") == "NotExists_IT");
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Contains(results, u => u.Name == "NotExists_User2");
            Assert.DoesNotContain(results, u => u.Name == "NotExists_User1");
        }

        #endregion

        #region Lambda Query 实际数据库测试

        [Fact]
        public async Task LambdaQuery_WithWhereAndSelect_ShouldWork()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            await userService.InsertAsync(new TestUser { Name = "LambdaUser1", Age = 25, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "LambdaUser2", Age = 35, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var query = Expr.Query<TestUser, IQueryable<object>>(q => q
                .Where(u => u.Age > 20)
                .Select(u => new { u.Name, u.Age }));

            var dt = await dataViewDAO.Search(query).GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 2);
        }

        [Fact]
        public async Task LambdaQuery_WithGroupBy_ShouldWork()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            for (int i = 1; i <= 3; i++)
            {
                await userService.InsertAsync(new TestUser
                {
                    Name = $"GroupUser{i}",
                    Age = i <= 2 ? 25 : 35,
                    CreateTime = DateTime.Now
                }, TestContext.Current.CancellationToken);
            }

            var query = Expr.Query<TestUser, IQueryable<object>>(q => q
                .GroupBy(u => u.Age)
                .Select(g => new { Age = g.Key, Count = g.Count() }));

            var dt = await dataViewDAO.Search(query).GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 1);
        }

        #endregion

        #region ValueBinaryExpr 实际数据库测试

        [Fact]
        public async Task ValueBinaryExpr_ArithmeticOperations_ShouldWork()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            await userService.InsertAsync(new TestUser { Name = "ArithUser", Age = 25, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var addExpr = (Expr.Prop("Age") + 5).As("AgePlus5");
            var multiplyExpr = (Expr.Prop("Age") * 2).As("AgeDouble");

            var query = Expr.From<TestUser>()
                .Where(Expr.Prop("Name") == "ArithUser")
                .Select(addExpr, multiplyExpr);

            var dt = await dataViewDAO.Search(query).GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 1);
        }

        [Fact]
        public async Task ValueBinaryExpr_Concat_ShouldWork()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            await userService.InsertAsync(new TestUser { Name = "ConcatUser", Age = 25, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var concatExpr = Expr.Prop("Name").Concat(Expr.Const("_Suffix")).As("FullName");

            var query = Expr.From<TestUser>()
                .Where(Expr.Prop("Name") == "ConcatUser")
                .Select(concatExpr);

            var dt = await dataViewDAO.Search(query).GetResultAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 1);
        }

        #endregion

        #region String Functions 实际数据库测试

        [Fact]
        public async Task StringFunctions_Like_StartsWith_ShouldWork()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            await userService.InsertAsync(new TestUser { Name = "LikeTest_ABC", Age = 25, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "LikeTest_XYZ", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var expr = Expr.Prop("Name").StartsWith("LikeTest_");
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task StringFunctions_Like_EndsWith_ShouldWork()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            await userService.InsertAsync(new TestUser { Name = "Prefix_ABC", Age = 25, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);
            await userService.InsertAsync(new TestUser { Name = "Prefix_XYZ", Age = 30, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var expr = Expr.Prop("Name").EndsWith("_ABC");
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Single(results);
            Assert.Equal("Prefix_ABC", results[0].Name);
        }

        [Fact]
        public async Task StringFunctions_Like_Contains_ShouldWork()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            await userService.InsertAsync(new TestUser { Name = "ContainsTest_MiddleValue", Age = 25, CreateTime = DateTime.Now }, TestContext.Current.CancellationToken);

            var expr = Expr.Prop("Name").Contains("Middle");
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Single(results);
        }

        #endregion

        #region Null Checks 实际数据库测试

        [Fact]
        public async Task NullChecks_IsNull_ShouldWork()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user1 = new TestUser { Name = null, Age = 25, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "NotNullUser", Age = 30, CreateTime = DateTime.Now };
            await userService.InsertAsync(user1, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user2, TestContext.Current.CancellationToken);

            var expr = Expr.Prop("Name").IsNull();
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Contains(results, u => u.Name == null);
        }

        [Fact]
        public async Task NullChecks_IsNotNull_ShouldWork()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user1 = new TestUser { Name = "NotNullUser1", Age = 25, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "NotNullUser2", Age = 30, CreateTime = DateTime.Now };
            await userService.InsertAsync(user1, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user2, TestContext.Current.CancellationToken);

            var expr = Expr.Prop("CreateTime").IsNotNull();
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.True(results.Count >= 2);
        }

        #endregion

        #region Between 实际数据库测试

        [Fact]
        public async Task Between_Inclusive_ShouldWork()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            for (int i = 1; i <= 5; i++)
            {
                await userService.InsertAsync(new TestUser
                {
                    Name = $"BetweenUser{i}",
                    Age = 20 + i,
                    CreateTime = DateTime.Now
                }, TestContext.Current.CancellationToken);
            }

            var expr = Expr.Prop("Age").Between(22, 24);
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Equal(3, results.Count);
            Assert.All(results, u => Assert.True(u.Age >= 22 && u.Age <= 24));
        }

        #endregion

        #region In/NotIn 实际数据库测试

        [Fact]
        public async Task In_WithArray_ShouldWork()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user1 = new TestUser { Name = "InUser1", Age = 25, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "InUser2", Age = 30, CreateTime = DateTime.Now };
            var user3 = new TestUser { Name = "InUser3", Age = 35, CreateTime = DateTime.Now };
            await userService.InsertAsync(user1, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user2, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user3, TestContext.Current.CancellationToken);

            var ids = new[] { user1.Id, user2.Id };
            var expr = Expr.Prop("Id").In(ids);
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.Equal(2, results.Count);
            Assert.All(results, u => Assert.Contains(u.Id, ids));
        }

        [Fact]
        public async Task NotIn_WithArray_ShouldExclude()
        {
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            var user1 = new TestUser { Name = "NotInUser1", Age = 25, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "NotInUser2", Age = 30, CreateTime = DateTime.Now };
            await userService.InsertAsync(user1, TestContext.Current.CancellationToken);
            await userService.InsertAsync(user2, TestContext.Current.CancellationToken);

            var excludeIds = new[] { user1.Id };
            var expr = Expr.Prop("Id").In(excludeIds).Not();
            var results = await objectViewDAO.Search(expr).ToListAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(results);
            Assert.DoesNotContain(results, u => u.Id == user1.Id);
        }

        #endregion
    }
}
