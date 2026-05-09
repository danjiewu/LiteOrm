using LiteOrm.CodeGen;
using LiteOrm.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LiteOrm.Tests
{
    [Collection("Database")]
    public class CodeGenSchemaTests : TestBase
    {
        public CodeGenSchemaTests(DatabaseFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public void DatabaseSchemaReader_ShouldReadSQLiteSchema()
        {
            var reader = new DatabaseSchemaReader(
                ServiceProvider.GetRequiredService<DAOContextPoolFactory>(),
                ServiceProvider.GetRequiredService<SqlBuilderFactory>());

            var schema = reader.ReadSchema("SQLite", new[] { "TestUsers", "TestDepartments" });
            var users = schema.GetTable("TestUsers");
            var departments = schema.GetTable("TestDepartments");

            Assert.NotNull(users);
            Assert.NotNull(departments);
            Assert.Equal("TestUser", users!.ClassName);
            Assert.Contains(users.Columns, c => c.Name == "DeptId" && c.PropertyName == "DeptId");
            Assert.Contains(departments!.Columns, c => c.Name == "ParentId");
        }

        [Fact]
        public void DatabaseSchemaReader_ShouldFilterTablesWhenRequested()
        {
            var reader = new DatabaseSchemaReader(
                ServiceProvider.GetRequiredService<DAOContextPoolFactory>(),
                ServiceProvider.GetRequiredService<SqlBuilderFactory>());

            var schema = reader.ReadSchema("SQLite", new[] { "TestUsers" });

            var users = Assert.Single(schema.Tables);
            Assert.Equal("TestUsers", users.Name);
            Assert.Equal("TestUser", users.ClassName);
            Assert.Contains(users.Columns, c => c.Name == "DeptId" && c.PropertyName == "DeptId");
        }
    }
}
