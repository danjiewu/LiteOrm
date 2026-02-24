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
            DataTable dt = await dao.SearchAsync(Expr.Prop("Name").StartsWith("DataViewTest"));

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
            DataTable dt = await dao.SearchAsync(fields, Expr.Prop("Name") == "FieldTest");

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
                .Select("AliasName","Age");// 等价于 Select(Expr.Property("AliasName"),Expr.Property("Age"))
           
            DataTable dt = await dao.SearchAsync(query);

            // Assert
            Assert.NotNull(dt);
            Assert.Contains("AliasName", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Age", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName), StringComparer.OrdinalIgnoreCase);
        }
    }
}
