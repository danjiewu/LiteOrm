using System;
using System.Collections.Generic;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class GenericSqlExprTests
    {
        [Fact]
        public void Register_WithNullKey_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => GenericSqlExpr.Register(null!, (_, _) => "sql"));
        }

        [Fact]
        public void Get_WithUnknownKey_ThrowsKeyNotFoundException()
        {
            Assert.Throws<KeyNotFoundException>(() => GenericSqlExpr.Get(Guid.NewGuid().ToString("N")));
        }

        [Fact]
        public void Register_AndGet_ReturnsExpressionWithSameKey()
        {
            var key = Guid.NewGuid().ToString("N");
            GenericSqlExpr.Register(key, (_, _) => "sql");

            var expr = GenericSqlExpr.Get(key);

            Assert.Equal(key, expr.Key);
        }

        [Fact]
        public void Get_WithArg_SetsArg()
        {
            var key = Guid.NewGuid().ToString("N");
            GenericSqlExpr.Register(key, (_, arg) => arg?.ToString() ?? string.Empty);

            var expr = GenericSqlExpr.Get(key, 5);

            Assert.Equal(5, expr.Arg);
        }

        [Fact]
        public void GenerateSql_UsesRegisteredHandler()
        {
            var key = Guid.NewGuid().ToString("N");
            GenericSqlExpr.Register(key, (_, arg) => $"X{arg}");
            var expr = GenericSqlExpr.Get(key, 3);

            var sql = expr.GenerateSql(null!);

            Assert.Equal("X3", sql);
        }

        [Fact]
        public void Clone_CopiesKeyAndArg()
        {
            var expr = new GenericSqlExpr("k") { Arg = 7 };
            var clone = (GenericSqlExpr)expr.Clone();

            Assert.Equal(expr, clone);
        }

        [Fact]
        public void ImplicitConversion_AssignsToValueTypeExpr_WrapsInValueExpr()
        {
            var key = Guid.NewGuid().ToString("N");
            GenericSqlExpr.Register(key, (_, _) => "sql");

            ValueTypeExpr value = Expr.Sql(key);

            var wrapper = Assert.IsType<ValueExpr>(value);
            var inner = Assert.IsType<GenericSqlExpr>(wrapper.Value);
            Assert.Equal(key, inner.Key);
        }

        [Fact]
        public void ImplicitConversion_MatchesAsValue()
        {
            var key = Guid.NewGuid().ToString("N");
            GenericSqlExpr.Register(key, (_, arg) => $"X{arg}");

            var expr = Expr.Sql(key, 9);

            ValueTypeExpr converted = expr;

            Assert.Equal(expr.AsValue(), converted);
        }

        [Fact]
        public void ImplicitConversion_NullSource_ProducesNullValueExpr()
        {
            GenericSqlExpr? expr = null;

            ValueTypeExpr value = expr!;

            var wrapper = Assert.IsType<ValueExpr>(value);
            Assert.Null(wrapper.Value);
        }

        [Fact]
        public void ImplicitConversion_InSelectItem_RendersRegisteredSql()
        {
            var key = Guid.NewGuid().ToString("N");
            GenericSqlExpr.Register(key, (_, _) => "RAW_SQL_FRAGMENT");

            var item = new SelectItemExpr(Expr.Sql(key), "Alias");

            Assert.IsType<ValueExpr>(item.Value);

            var prepared = item.ToPreparedSql(new SqlBuildContext(SqlBuilder.Instance));

            Assert.Contains("RAW_SQL_FRAGMENT", prepared.Sql);
            Assert.Contains("Alias", prepared.Sql);
        }
    }
}
