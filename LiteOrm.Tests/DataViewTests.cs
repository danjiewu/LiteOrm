using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    [Collection("Database")]
    public class DataViewTests : TestBase
    {
        public DataViewTests(DatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public async Task DataViewDAO_Search_ShouldReturnDataTable()
        {

            var dao = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            await userService.InsertAsync(new TestUser { Name = "DataViewTest1", Age = 25 });
            await userService.InsertAsync(new TestUser { Name = "DataViewTest2", Age = 30 });

            var result = dao.Search(Expr.Prop("Name").StartsWith("DataViewTest"));
            DataTable dt = await result.GetResultAsync();

            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 2);
            Assert.Contains("Name", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Age", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DataViewDAO_SearchByFields_ShouldReturnSpecificColumns()
        {
            var dao = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            await userService.InsertAsync(new TestUser { Name = "FieldTest", Age = 40 });

            string[] fields = { "Name" };
            var result = dao.Search(fields, Expr.Prop("Name") == "FieldTest");
            DataTable dt = await result.GetResultAsync();

            Assert.NotNull(dt);
            Assert.Single(dt.Columns);
            Assert.Equal("Name", dt.Columns[0].ColumnName, StringComparer.OrdinalIgnoreCase);
            Assert.True(dt.Rows.Count > 0);
            Assert.Equal("FieldTest", dt.Rows[0]["Name"]);
        }

#if NET8_0_OR_GREATER
        [Fact]
        public async Task DataViewDAO_Search_WithExprString_ShouldWork()
        {
            var dao = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            await userService.InsertAsync(new TestUser { Name = "DataViewExprStringTest1", Age = 25 });
            await userService.InsertAsync(new TestUser { Name = "DataViewExprStringTest2", Age = 30 });

            // 测试 ExprString 语法
            var ageThreshold = 20;
            var result = dao.Search($"SELECT {{AllFields}} FROM {{From}} WHERE {Expr.Prop("Age") > ageThreshold & Expr.Prop("Name").Like("DataViewExprStringTest%")}", true);
            DataTable dt = await result.GetResultAsync();

            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 2);
            Assert.Contains("Name", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Age", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
        }
#endif

        [Fact]
        public async Task DataViewDAO_GroupBy_WithUserDefinedAggregation_ShouldWork()
        {
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDao = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            var dept1 = new TestDepartment { Name = "GroupDept1" };
            var dept2 = new TestDepartment { Name = "GroupDept2" };
            await deptService.InsertAsync(dept1);
            await deptService.InsertAsync(dept2);

            await userService.InsertAsync(new TestUser { Name = "User1", Age = 20, CreateTime = DateTime.Now, DeptId = dept1.Id });
            await userService.InsertAsync(new TestUser { Name = "User2", Age = 25, CreateTime = DateTime.Now, DeptId = dept1.Id });
            await userService.InsertAsync(new TestUser { Name = "User3", Age = 30, CreateTime = DateTime.Now, DeptId = dept2.Id });

            // 简单的 GROUP BY 聚合
            var deptUserCounts = dataViewDao.Search(
                Expr.From<TestUser>()
                    .Where(Expr.Prop("DeptId") != -1)
                    .GroupBy(Expr.Prop("DeptId"))
                    .Select(Expr.Prop("DeptId"), Expr.Prop("Id").Count().As("UserCount"), Expr.Prop("Age").Avg().As("AvgAge"))
            );
            var result = await deptUserCounts.GetResultAsync();

            Assert.NotNull(result);
            Assert.True(result.Rows.Count >= 2);
            Assert.Contains("DeptId", result.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("UserCount", result.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("AvgAge", result.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
        }
    }
}
