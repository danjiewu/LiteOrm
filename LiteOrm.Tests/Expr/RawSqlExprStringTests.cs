using LiteOrm.CodeGen;
using LiteOrm.Common;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;

using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// RawSql 在 ExprString 中的插入行为单元测试。
    /// 验证 RawSql 文本原样内联、不参数化，且与 Expr / 普通值混合使用时各自走对应路径。
    /// </summary>
    [Collection("Database")]
    public class RawSqlExprStringTests : TestBase
    {
        public RawSqlExprStringTests(DatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public void ExprString_RawSql_InlinedWithoutParameterization()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var result = sqlGen.ToSql($"WHERE {new RawSql("Status & 1 = 1")} AND {Expr.Prop("Age")} > {18}");

            Assert.StartsWith("WHERE Status & 1 = 1 AND ", result.Sql);
            Assert.EndsWith("> @0", result.Sql);
            Assert.DoesNotContain("Status", result.Params[0].Value.ToString());
            Assert.Single(result.Params);
            Assert.Equal(18, result.Params[0].Value);
        }

        [Fact]
        public void ExprString_RawSql_FromFactory_Inlined()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var result = sqlGen.ToSql($"SELECT {RawSql.From("COUNT(*)")} FROM {{From}} WHERE {Expr.Prop("Name")} LIKE {"%test%"}");

            Assert.Contains("COUNT(*)", result.Sql);
            Assert.Contains("LIKE @0", result.Sql);
            Assert.Single(result.Params);
            Assert.Equal("%test%", result.Params[0].Value);
        }

        [Fact]
        public void ExprString_MultipleRawSql_FragmentsAssembled()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var result = sqlGen.ToSql(
                $"SELECT {new RawSql("TOP 10 *")} FROM {{From}} WHERE {new RawSql("Status = 1")} AND {Expr.Prop("Age")} >= {21}");

            Assert.Contains("TOP 10 *", result.Sql);
            Assert.Contains("Status = 1", result.Sql);
            Assert.Contains(">= @0", result.Sql);
            Assert.Single(result.Params);
            Assert.Equal(21, result.Params[0].Value);
        }

        [Fact]
        public void ExprString_RawSql_NullSql_ProducesEmptyFragment()
        {
            var sqlGen = new SqlGen(typeof(TestUser));
            var rawSql = new RawSql(null);
            var result = sqlGen.ToSql($"WHERE {rawSql}1 = 1");

            Assert.Equal("WHERE 1 = 1", result.Sql);
            Assert.Empty(result.Params);
        }

        [Fact]
        public void ExprString_RawSql_NotMixedWithExprParams()
        {
            // RawSql 文本中即便含有 @ 字符，也不会被识别为参数占位符
            var sqlGen = new SqlGen(typeof(TestUser));
            var result = sqlGen.ToSql($"WHERE {new RawSql("dbo.Fn(@@ROWCOUNT)")} = {1}");

            Assert.Equal("WHERE dbo.Fn(@@ROWCOUNT) = @0", result.Sql);
            Assert.Single(result.Params);
            Assert.Equal(1, result.Params[0].Value);
        }

        [Fact]
        public void RawSql_Constructor_SetsSqlProperty()
        {
            var rawSql = new RawSql("a > b");
            Assert.Equal("a > b", rawSql.Sql);
        }

        [Fact]
        public void RawSql_From_CreatesInstance()
        {
            var rawSql = RawSql.From("a > b");
            Assert.Equal("a > b", rawSql.Sql);
        }

        [Fact]
        public void RawSql_ToString_ReturnsSqlText()
        {
            var rawSql = new RawSql("a > b");
            Assert.Equal("a > b", rawSql.ToString());
        }

        [Fact]
        public void RawSql_DefaultConstructor_SqlIsNull()
        {
            var rawSql = default(RawSql);
            Assert.Null(rawSql.Sql);
            Assert.Equal(string.Empty, rawSql.ToString());
        }
    }
}
