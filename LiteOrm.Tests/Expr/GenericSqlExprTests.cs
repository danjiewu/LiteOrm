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
            Assert.Throws<ArgumentNullException>(() => GenericSqlExpr.Register(null!, (_, _, _, _) => "sql"));
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
            GenericSqlExpr.Register(key, (_, _, _, _) => "sql");

            var expr = GenericSqlExpr.Get(key);

            Assert.Equal(key, expr.Key);
        }

        [Fact]
        public void Get_WithArg_SetsArg()
        {
            var key = Guid.NewGuid().ToString("N");
            GenericSqlExpr.Register(key, (_, _, _, arg) => arg?.ToString() ?? string.Empty);

            var expr = GenericSqlExpr.Get(key, 5);

            Assert.Equal(5, expr.Arg);
        }

        [Fact]
        public void GenerateSql_UsesRegisteredHandler()
        {
            var key = Guid.NewGuid().ToString("N");
            GenericSqlExpr.Register(key, (_, _, _, arg) => $"X{arg}");
            var expr = GenericSqlExpr.Get(key, 3);

            var sql = expr.GenerateSql(null!, null!, new List<KeyValuePair<string, object>>());

            Assert.Equal("X3", sql);
        }

        [Fact]
        public void Clone_CopiesKeyAndArg()
        {
            var expr = new GenericSqlExpr("k") { Arg = 7 };
            var clone = (GenericSqlExpr)expr.Clone();

            Assert.Equal(expr, clone);
        }
    }
}
