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
    /// RawSql 专用于动态但不适合使用参数的 SQL 片段，典型场景：LIMIT/OFFSET 的整数值、
    /// ORDER BY 的排序方向（ASC/DESC）、动态列名等；纯静态的 SQL 文本直接写在 ExprString 字面量中即可，无需使用 RawSql。
    /// </summary>
    [Collection("Database")]
    public class RawSqlExprStringTests : TestBase
    {
        public RawSqlExprStringTests(DatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public void ExprString_RawSql_DynamicLimitValue_InlinedWithoutParameterization()
        {
            // 典型场景：LIMIT 的值是运行时计算的整数，不适合以参数形式传入，
            // 通过 RawSql 内联到 SQL 文本中。调用方需自行保证 pageSize 已被校验为非负整数。
            int pageSize = 20;
            var sqlGen = new SqlGen(typeof(TestUser));
            var result = sqlGen.ToSql(
                $"WHERE {Expr.Prop("Age")} > {18} LIMIT {new RawSql(pageSize.ToString())}");

            Assert.EndsWith($"LIMIT {pageSize}", result.Sql);
            // 只应有 Age 条件这一个参数，LIMIT 的值不参数化
            Assert.Single(result.Params);
            Assert.Equal(18, result.Params[0].Value);
        }

        [Fact]
        public void ExprString_RawSql_FromFactory_DynamicOffset_Inlined()
        {
            int offset = 40;
            var sqlGen = new SqlGen(typeof(TestUser));
            var result = sqlGen.ToSql(
                $"WHERE {Expr.Prop("Name")} LIKE {"%test%"} LIMIT {RawSql.From(offset.ToString())}, {RawSql.From("10")}");

            Assert.Contains($"LIMIT {offset}, 10", result.Sql);
            Assert.Single(result.Params);
            Assert.Equal("%test%", result.Params[0].Value);
        }

        [Fact]
        public void ExprString_MultipleRawSql_DynamicValuesAssembled()
        {
            // LIMIT offset, pageSize 两个值都是运行时计算的整数
            int offset = 20;
            int pageSize = 10;
            var sqlGen = new SqlGen(typeof(TestUser));
            var result = sqlGen.ToSql(
                $"WHERE {Expr.Prop("Age")} >= {21} LIMIT {new RawSql(offset.ToString())}, {new RawSql(pageSize.ToString())}");

            Assert.Contains($"LIMIT {offset}, {pageSize}", result.Sql);
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
        public void ExprString_RawSql_DynamicValue_WithAtSign_NotTreatedAsParam()
        {
            // RawSql 内联的动态文本中即便含有 @ 字符，也不会被识别为参数占位符
            int rowCount = 100;
            var sqlGen = new SqlGen(typeof(TestUser));
            var result = sqlGen.ToSql($"WHERE {new RawSql($"dbo.Fn(@@ROWCOUNT, {rowCount})")} = {1}");

            Assert.Equal($"WHERE dbo.Fn(@@ROWCOUNT, {rowCount}) = @0", result.Sql);
            Assert.Single(result.Params);
            Assert.Equal(1, result.Params[0].Value);
        }

        [Fact]
        public void ExprString_RawSql_DynamicSortDirection_Inlined()
        {
            // 典型场景：ORDER BY 的排序方向 ASC/DESC 是 SQL 关键字，无法参数化，
            // 通过 RawSql 内联。调用方需用白名单校验（仅允许 ASC/DESC）。
            bool ascending = false;
            string direction = ascending ? "ASC" : "DESC";
            var sqlGen = new SqlGen(typeof(TestUser));
            var result = sqlGen.ToSql(
                $"WHERE {Expr.Prop("Age")} >= {18} ORDER BY Id {new RawSql(direction)}");

            Assert.EndsWith($"ORDER BY Id {direction}", result.Sql);
            Assert.Single(result.Params);
            Assert.Equal(18, result.Params[0].Value);
        }

        [Fact]
        public void ExprString_RawSql_DynamicColumnName_Inlined()
        {
            // 典型场景：动态列名/排序字段。调用方需用白名单校验列名（仅允许字母数字下划线）。
            // 注：简单列名也可用 Expr.Prop 表达（自带名称校验和引用符包裹）；
            //    此处演示列名来自外部白名单、或需绕过名称校验/拼接复杂表达式时使用 RawSql。
            string sortColumn = "Age";
            var sqlGen = new SqlGen(typeof(TestUser));
            var result = sqlGen.ToSql(
                $"WHERE {Expr.Prop("Name")} LIKE {"%test%"} ORDER BY {new RawSql(sortColumn)}");

            Assert.EndsWith($"ORDER BY {sortColumn}", result.Sql);
            Assert.Single(result.Params);
            Assert.Equal("%test%", result.Params[0].Value);
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
