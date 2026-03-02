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
            var result = dao.Search($"SELECT {{AllFields}} FROM {{From}} WHERE {Expr.Prop("Age")} > {ageThreshold} AND {Expr.Prop("Name")} LIKE 'DataViewExprStringTest%'");
            DataTable dt = await result.GetResultAsync();

            // Assert
            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 2);
            Assert.Contains("Name", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Age", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
        }
#endif
    }
}
