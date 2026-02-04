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
            var result = sqlGen.ToSql(new SelectExpr { Source = new TableExpr(sqlGen.Table) });

            Assert.Contains("SELECT", result.Sql);
            Assert.Contains("FROM", result.Sql);
            Assert.Empty(result.Params);
        }

        [Fact]
        public void ToSelectSql_WithCondition_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var condition = Expr.Property("Age") > 18;
            var select = new SelectExpr { Source = new WhereExpr { Source = new TableExpr(sqlGen.Table), Where = condition } };
            var result = sqlGen.ToSql(select);

            Assert.Contains("SELECT", result.Sql);
            Assert.Contains("WHERE", result.Sql);
            Assert.Single(result.Params);
            Assert.Equal(18, result.Params[0].Value);
        }

        [Fact]
        public void ToCountSql_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var select = new SelectExpr
            {
                Source = new TableExpr(sqlGen.Table),
                Selects = new List<ValueTypeExpr> { new AggregateFunctionExpr("COUNT", new ValueExpr(1) { IsConst = true }) }
            };
            var result = sqlGen.ToSql(select);

            Assert.Contains("COUNT(", result.Sql);
            Assert.Contains("FROM", result.Sql);
        }

        [Fact]
        public void ToUpdateSql_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var update = new UpdateExpr
            {
                Source = new TableExpr(sqlGen.Table),
                Sets = new List<(string, ValueTypeExpr)>
                {
                    ("Name", Expr.Value("NewName")),
                    ("Age", Expr.Value(30))
                },
                Where = Expr.Property("Id") == 1
            };
            var result = sqlGen.ToSql(update);

            Assert.StartsWith("UPDATE", result.Sql);
            Assert.Contains("SET", result.Sql);
            Assert.Contains("WHERE", result.Sql);
            Assert.Equal(3, result.Params.Count);
        }

        [Fact]
        public void ToDeleteSql_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var delete = new DeleteExpr
            {
                Source = new TableExpr(sqlGen.Table),
                Where = Expr.Property("Id") == 1
            };
            var result = sqlGen.ToSql(delete);

            Assert.StartsWith("DELETE FROM", result.Sql);
            Assert.Contains("WHERE", result.Sql);
            Assert.Single(result.Params);
        }

        [Fact]
        public void ToSelectSql_OrderBy_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var orderBy = new OrderByExpr(new TableExpr(sqlGen.Table), (Expr.Property("Age"), false));
            var select = new SelectExpr { Source = orderBy };
            var result = sqlGen.ToSql(select);

            Assert.Contains("ORDER BY", result.Sql);
            Assert.Contains("DESC", result.Sql);
        }

        [Fact]
        public void ToSelectSql_GroupBy_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var table = new TableExpr(sqlGen.Table);
            var groupBy = new GroupByExpr(table, Expr.Property("DeptId"));
            var select = new SelectExpr
            {
                Source = groupBy,
                Selects = new List<ValueTypeExpr> { Expr.Property("DeptId"), new AggregateFunctionExpr("COUNT", Expr.Const(1), false) }
            };
            
            var result = sqlGen.ToSql(select);

            Assert.Contains("GROUP BY", result.Sql);
            Assert.Contains("COUNT(", result.Sql);
        }

        [Fact]
        public void ToSelectSql_Section_Test()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var section = new SectionExpr(new TableExpr(sqlGen.Table), 10, 5);
            var select = new SelectExpr { Source = section };
            var result = sqlGen.ToSql(select);

            Assert.NotEmpty(result.Sql);
        }
    }
}
