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
            // Arrange
            var dao = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            // Add some test data
            await userService.InsertAsync(new TestUser { Name = "DataViewTest1", Age = 25 });
            await userService.InsertAsync(new TestUser { Name = "DataViewTest2", Age = 30 });

            // Act
            var result = dao.Search(Expr.Prop("Name").StartsWith("DataViewTest"));
            DataTable dt = await result.GetResultAsync();

            // Assert
            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 2);
            Assert.Contains("Name", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Age", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task DataViewDAO_SearchByFields_ShouldReturnSpecificColumns()
        {
            // Arrange
            var dao = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            await userService.InsertAsync(new TestUser { Name = "FieldTest", Age = 40 });

            // Act
            string[] fields = { "Name" };
            var result = dao.Search(fields, Expr.Prop("Name") == "FieldTest");
            DataTable dt = await result.GetResultAsync();

            // Assert
            Assert.NotNull(dt);
            Assert.Single(dt.Columns);
            Assert.Equal("Name", dt.Columns[0].ColumnName, StringComparer.OrdinalIgnoreCase);
            Assert.True(dt.Rows.Count > 0);
            Assert.Equal("FieldTest", dt.Rows[0]["Name"]);
        }

        [Fact]
        public async Task DataViewDAO_ComplexExpr_ShouldWork()
        {
            // Arrange
            var dao = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();
            
            // Act
            var query = Expr.From<TestUser>()
                .Select(Expr.Prop("Name").As("AliasName"), Expr.Prop("Age"))
                .Where(Expr.Prop("Age") > 10)
                .OrderBy(Expr.Prop("Age").Asc())
                .Select("AliasName","Age");// 等价于 Select(Expr.Prop("AliasName"),Expr.Prop("Age"))
           
            var result = dao.Search(query);
            DataTable dt = await result.GetResultAsync();

            // Assert
            Assert.NotNull(dt);
            Assert.Contains("AliasName", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Age", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
        }

#if NET8_0_OR_GREATER
        [Fact]
        public async Task DataViewDAO_Search_WithExprString_ShouldWork()
        {
            // Arrange
            var dao = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();

            // Add some test data
            await userService.InsertAsync(new TestUser { Name = "DataViewExprStringTest1", Age = 25 });
            await userService.InsertAsync(new TestUser { Name = "DataViewExprStringTest2", Age = 30 });

            // Act
            // Test with ExprString syntax
            var ageThreshold = 20;
            var result = dao.Search($"SELECT {{AllFields}} FROM {{From}} WHERE {Expr.Prop("Age")} > {ageThreshold} AND {Expr.Prop("Name")} LIKE 'DataViewExprStringTest%'", true);
            DataTable dt = await result.GetResultAsync();

            // Assert
            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 2);
            Assert.Contains("Name", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Age", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
        }
#endif

        [Fact]
        public async Task DataViewDAO_GroupBy_ExistsLambda_ShouldWork()
        {
            // Arrange
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDao = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            // 创建测试部门
            var dept1 = new TestDepartment { Name = "Dept1" };
            var dept2 = new TestDepartment { Name = "Dept2" };
            await deptService.InsertAsync(dept1);
            await deptService.InsertAsync(dept2);

            // 创建测试用户
            await userService.InsertAsync(new TestUser { Name = "User1", Age = 20, CreateTime = DateTime.Now, DeptId = dept1.Id });
            await userService.InsertAsync(new TestUser { Name = "User2", Age = 25, CreateTime = DateTime.Now, DeptId = dept1.Id });
            await userService.InsertAsync(new TestUser { Name = "User3", Age = 30, CreateTime = DateTime.Now, DeptId = dept2.Id });
            await userService.InsertAsync(new TestUser { Name = "User4", Age = 35, CreateTime = DateTime.Now, DeptId = -1 });

            // Act - 使用 group by 后再使用 Expr.Exists lambda 方式查询
            var deptUserCounts = dataViewDao.Search(
                u => u.Where(t => t.DeptId != -1)
                .GroupBy(g => g.DeptId)
                .Select(s => new { DeptId = s.Key, UserCount = s.Count(), AgeSum = s.Sum(u => u.Age) })
                .Where(t => t.UserCount > 1)
                .OrderBy(t => t.DeptId)
                .Select(t => new { t.DeptId, t.UserCount, AvgAge = t.AgeSum / t.UserCount })
            );
            var result = await deptUserCounts.GetResultAsync();

            // Assert
            // 验证查询能够成功执行
            Assert.NotNull(result);
            Assert.Equal(1, result.Rows.Count);
            
            // 验证结果列
            Assert.Contains("DeptId", result.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("UserCount", result.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("AvgAge", result.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
            
            // 验证结果值
            DataRow row = result.Rows[0];
            Assert.Equal(dept1.Id, Convert.ToInt32(row["DeptId"]));
            Assert.Equal(2, Convert.ToInt32(row["UserCount"]));
            int expectedAvgAge = (20 + 25) / 2; // (User1.Age + User2.Age) / 2
            Assert.Equal(expectedAvgAge, Convert.ToInt32(row["AvgAge"]));
        }
    }
}
