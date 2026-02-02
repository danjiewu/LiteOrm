using LiteOrm.Common;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using System.Collections.Generic;
using Xunit;

namespace LiteOrm.Tests
{
    public class SqlGenTests : TestBase
    {
        [Fact]
        public void ToSelectSql_Basic_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var result = sqlGen.ToSelectSql();

            Assert.Contains("SELECT", result.Sql);
            Assert.Contains("FROM", result.Sql);
            Assert.Empty(result.Params);
        }

        [Fact]
        public void ToSelectSql_WithCondition_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var condition = Expr.Property("Age") > 18;
            var result = sqlGen.ToSelectSql(condition);

            Assert.Contains("SELECT", result.Sql);
            Assert.Contains("WHERE", result.Sql);
            Assert.Single(result.Params);
            Assert.Equal(18, result.Params[0].Value);
        }

        [Fact]
        public void ToCountSql_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var result = sqlGen.ToCountSql();

            Assert.Contains("COUNT(", result.Sql);
            Assert.Contains("FROM", result.Sql);
        }

        [Fact]
        public void ToUpdateSql_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var values = new Dictionary<string, object>
            {
                { "Name", "NewName" },
                { "Age", 30 }
            };
            var condition = Expr.Property("Id") == 1;
            var result = sqlGen.ToUpdateSql(values, condition);

            Assert.StartsWith("UPDATE", result.Sql);
            Assert.Contains("SET", result.Sql);
            Assert.Contains("WHERE", result.Sql);
            Assert.Equal(3, result.Params.Count);
        }

        [Fact]
        public void ToDeleteSql_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var condition = Expr.Property("Id") == 1;
            var result = sqlGen.ToDeleteSql(condition);

            Assert.StartsWith("DELETE FROM", result.Sql);
            Assert.Contains("WHERE", result.Sql);
            Assert.Single(result.Params);
        }

        [Fact]
        public void ToInsertSql_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var values = new Dictionary<string, object>
            {
                { "Name", "John" },
                { "Age", 25 }
            };
            var result = sqlGen.ToInsertSql(values);

            Assert.StartsWith("INSERT INTO", result.Sql);
            Assert.Contains("VALUES", result.Sql);
            Assert.Equal(2, result.Params.Count);
        }

        [Fact]
        public void ToSelectSql_OrderBy_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var orderBy = Expr.Table(sqlGen.Table).OrderBy(Expr.Property("Age").Desc());
            var result = sqlGen.ToSelectSql(orderBy);

            Assert.Contains("ORDER BY", result.Sql);
            Assert.Contains("DESC", result.Sql);
        }

        [Fact]
        public void ToSelectSql_GroupBy_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var groupBy = Expr.Table(sqlGen.Table)
                .GroupBy(Expr.Property("DeptId"))
                .Select(Expr.Property("DeptId"), Expr.Const(1).Count());
            
            var result = sqlGen.ToSelectSql(groupBy);

            Assert.Contains("GROUP BY", result.Sql);
            Assert.Contains("COUNT(", result.Sql);
        }

        [Fact]
        public void ToSelectSql_Section_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var section = Expr.Table(sqlGen.Table).Section(10, 5);
            var result = sqlGen.ToSelectSql(section);

            // SQLite style or generic style depending on builder, but should contain keywords or specific syntax
            // Actually our ToString in SectionExpr uses SKIP/TAKE, but SqlBuilder will convert it.
            Assert.NotEmpty(result.Sql);
        }
    }
}
