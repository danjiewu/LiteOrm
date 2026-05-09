using LiteOrm.CodeGen;
using System;
using Xunit;

namespace LiteOrm.Tests
{
    public class CodeGenGeneratorTests
    {
        [Fact]
        public void EntityCodeGenerator_ShouldGenerateEntityClass()
        {
            var schema = BuildSchema();
            var generator = new EntityCodeGenerator();

            var result = generator.Generate(schema, new EntityGenerationOptions
            {
                Namespace = "Demo.Models",
                Tables = new() { "Users" }
            });

            var code = result.CombinedCode;
            Assert.Contains("[Table(\"Users\")]", code);
            Assert.Contains("public class User : ObjectBase", code);
            Assert.Contains("[Column(\"DeptId\")]", code);
        }

        [Fact]
        public void SelectArtifactsGenerator_ShouldGenerateViewAndExprCode()
        {
            var schema = BuildSchema();
            var generator = new SelectArtifactsGenerator();

            var result = generator.Generate(
                schema,
                "SELECT u.Id, u.Name, d.Name AS DeptName FROM Users u LEFT JOIN Departments d ON u.DeptId = d.Id WHERE u.Age >= 18 ORDER BY u.Name",
                new SelectGenerationOptions { Namespace = "Demo.Models", ViewName = "UserReportView" });

            Assert.True(result.Succeeded);
            Assert.NotNull(result.ViewCode);
            Assert.NotNull(result.QueryCode);
            Assert.Contains("[TableJoin(typeof(Department), nameof(User.DeptId), Alias = \"d\"", result.ViewCode);
            Assert.Contains("[ForeignColumn(\"d\", Property = nameof(Department.Name))]", result.ViewCode);
            Assert.Contains("new TableJoinExpr", result.QueryCode);
            Assert.Contains("Expr.Prop(\"u\", nameof(User.Age)) >= 18", result.QueryCode);
            Assert.Contains("nameof(UserReportView.DeptName)", result.QueryCode);
        }

        [Fact]
        public void SelectArtifactsGenerator_ShouldGenerateAggregateViewAndGroupByCode()
        {
            var schema = BuildSchema();
            var generator = new SelectArtifactsGenerator();

            var result = generator.Generate(
                schema,
                "SELECT d.Name AS DeptName, COUNT(u.Id) AS UserCount, SUM(u.Age) AS TotalAge FROM Users u LEFT JOIN Departments d ON u.DeptId = d.Id WHERE u.Age >= 18 GROUP BY d.Name ORDER BY d.Name",
                new SelectGenerationOptions { Namespace = "Demo.Models", ViewName = "UserSummaryView" });

            Assert.True(result.Succeeded);
            Assert.NotNull(result.ViewCode);
            Assert.NotNull(result.QueryCode);
            Assert.Contains("[ForeignColumn(\"d\", Property = nameof(Department.Name))]", result.ViewCode);
            Assert.Contains("public int UserCount { get; set; }", result.ViewCode);
            Assert.Contains("public int TotalAge { get; set; }", result.ViewCode);
            Assert.Contains("source = new GroupByExpr(source, Expr.Prop(\"d\", nameof(Department.Name)))", result.QueryCode);
            Assert.Contains("Expr.Prop(\"u\", nameof(User.Id)).Count(false).As(\"UserCount\")", result.QueryCode);
            Assert.Contains("Expr.Prop(\"u\", nameof(User.Age)).Sum().As(\"TotalAge\")", result.QueryCode);
        }

        [Fact]
        public void SelectArtifactsGenerator_ShouldReportUnsupportedSql()
        {
            var schema = BuildSchema();
            var generator = new SelectArtifactsGenerator();

            var result = generator.Generate(
                schema,
                "WITH cte AS (SELECT Id FROM Users) SELECT * FROM cte",
                new SelectGenerationOptions { Namespace = "Demo.Models", ViewName = "UserReportView" });

            Assert.False(result.Succeeded);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("WITH", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void SelectArtifactsGenerator_ShouldReportUnsupportedHaving()
        {
            var schema = BuildSchema();
            var generator = new SelectArtifactsGenerator();

            var result = generator.Generate(
                schema,
                "SELECT d.Name AS DeptName, COUNT(u.Id) AS UserCount FROM Users u LEFT JOIN Departments d ON u.DeptId = d.Id GROUP BY d.Name HAVING COUNT(u.Id) > 1",
                new SelectGenerationOptions { Namespace = "Demo.Models", ViewName = "UserSummaryView" });

            Assert.False(result.Succeeded);
            Assert.Contains(result.Diagnostics, d => d.Message.Contains("HAVING", StringComparison.OrdinalIgnoreCase));
        }

        private static DatabaseSchema BuildSchema()
        {
            var departments = new TableSchema
            {
                Name = "Departments",
                ClassName = "Department"
            };
            departments.Columns.Add(new ColumnSchema { Name = "Id", PropertyName = "Id", ClrType = typeof(int), IsPrimaryKey = true });
            departments.Columns.Add(new ColumnSchema { Name = "Name", PropertyName = "Name", ClrType = typeof(string), IsNullable = true });

            var users = new TableSchema
            {
                Name = "Users",
                ClassName = "User"
            };
            users.Columns.Add(new ColumnSchema { Name = "Id", PropertyName = "Id", ClrType = typeof(int), IsPrimaryKey = true });
            users.Columns.Add(new ColumnSchema { Name = "Name", PropertyName = "Name", ClrType = typeof(string), IsNullable = true });
            users.Columns.Add(new ColumnSchema { Name = "Age", PropertyName = "Age", ClrType = typeof(int) });
            users.Columns.Add(new ColumnSchema { Name = "DeptId", PropertyName = "DeptId", ClrType = typeof(int) });
            users.ForeignKeys.Add(new ForeignKeySchema { SourceColumn = "DeptId", TargetTable = "Departments", TargetColumn = "Id" });

            var schema = new DatabaseSchema();
            schema.Tables.Add(users);
            schema.Tables.Add(departments);
            return schema;
        }
    }
}
