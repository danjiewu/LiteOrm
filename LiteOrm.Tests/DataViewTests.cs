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
    public class DataViewTests : TestBase
    {
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
            DataTable dt = await dao.SearchAsync(Expr.Property("Name").StartsWith("DataViewTest"));

            // Assert
            Assert.NotNull(dt);
            Assert.True(dt.Rows.Count >= 2);
            Assert.Contains("Name", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
            Assert.Contains("Age", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
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
            DataTable dt = await dao.SearchAsync(fields, Expr.Property("Name") == "FieldTest");

            // Assert
            Assert.NotNull(dt);
            Assert.Single(dt.Columns);
            Assert.Equal("Name", dt.Columns[0].ColumnName);
            Assert.True(dt.Rows.Count > 0);
            Assert.Equal("FieldTest", dt.Rows[0]["Name"]);
        }

        [Fact]
        public async Task DataViewDAO_ComplexExpr_ShouldWork()
        {
            // Arrange
            var dao = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();
            
            // Act
            var query = Expr.Table<TestUser>()
                .Select(Expr.Property("Name").As("AliasName"), Expr.Property("Age"))
                .Where(Expr.Property("Age") > 10)
                .OrderBy(Expr.Property("Age").Asc())
                .Select("AliasName","Age");// µÈ¼ÛÓÚ Select(Expr.Property("AliasName"),Expr.Property("Age"))


            DataTable dt = await dao.SearchAsync(query);

            // Assert
            Assert.NotNull(dt);
            Assert.Contains("AliasName", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
            Assert.Contains("Age", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
        }
    }

    public class DataDAOTests : TestBase
    {
        [Fact]
        public async Task DataDAO_UpdateAllValues_ShouldWork()
        {
            // Arrange
            var dao = ServiceProvider.GetRequiredService<DataDAO<TestUser>>();
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var viewService = ServiceProvider.GetRequiredService<IEntityViewServiceAsync<TestUser>>();

            var user = new TestUser { Name = "DataDAOTest", Age = 10 };
            await userService.InsertAsync(user);

            // Act
            var updates = new Dictionary<string, object> { { "Age", 20 } };
            int updatedCount = await dao.UpdateAllValuesAsync(updates, Expr.Property("Name") == "DataDAOTest");

            // Assert
            Assert.Equal(1, updatedCount);
            var updatedUser = await viewService.GetObjectAsync(user.Id);
            Assert.Equal(20, updatedUser.Age);
        }
    }
}
