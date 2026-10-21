using System;
using System.Collections.Generic;
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Tests.Models;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// 校验文档 core-usage/expr-guide 中新增的 Join / JoinRelated 示例可以真实编译并生成预期 SQL。
    /// </summary>
    public class ExprGuideJoinDocTests
    {
        private static PreparedSql ToSql(Expr expr)
            => expr.ToPreparedSql(new SqlBuildContext(SqlBuilder.Instance) { SingleTable = false });

        [Fact]
        public void Join_WithExplicitCondition_MatchesDocumentedShape()
        {
            // 对应文档 1.3 节第一段示例
            var query = Expr.Query<TestUser, IEnumerable<(TestUser, TestDepartment)>>(u =>
                Expr.From<TestUser>()
                    .Join<TestUser, TestDepartment>((x, d) => x.DeptId == d.Id)
                    .Where(t => t.Item1.Age > 100 && t.Item2.Name != null));

            var where = Assert.IsType<WhereExpr>(query);
            var from = Assert.IsType<FromExpr>(where.Source);
            Assert.IsType<TableExpr>(from.Source);
            Assert.Single(from.Joins);
            Assert.NotNull(from.Joins[0].On);

            // 文档里"从表达式读别名"的写法必须可用
            string mainAlias = from.Source!.Alias!;
            string joinAlias = from.Joins[0].Source!.Alias!;
            Assert.NotEqual(mainAlias, joinAlias);

            var select = where.Select(
                Expr.Prop(mainAlias, nameof(TestUser.Id)).As("UserId"),
                Expr.Prop(joinAlias, nameof(TestDepartment.Name)).As("DeptName"));

            string sql = ToSql(select).Sql;
            Assert.Contains("LEFT JOIN", sql);
            Assert.Contains("UserId", sql);
            Assert.Contains("DeptName", sql);
        }

        [Fact]
        public void JoinRelated_WithoutCondition_InfersOn()
        {
            // 对应文档 1.3 节第二段示例
            var query = Expr.Query<TestUser, IEnumerable<(TestUser, TestDepartment)>>(u =>
                Expr.From<TestUser>().JoinRelated<TestUser, TestDepartment>());

            var from = Assert.IsType<FromExpr>(query);
            Assert.NotNull(from.Joins[0].On);
            Assert.Contains("DeptId", from.Joins[0].On!.ToString()!);
        }

        [Fact]
        public void JoinRelated_WithCondition_MergesByAnd()
        {
            // 对应文档 1.3 节第三段示例
            var query = Expr.Query<TestUser, IEnumerable<(TestUser, TestDepartment)>>(u =>
                Expr.From<TestUser>().JoinRelated<TestUser, TestDepartment>((x, d) => d.Name != null));

            var from = Assert.IsType<FromExpr>(query);
            var on = Assert.IsType<AndExpr>(from.Joins[0].On);
            Assert.Equal(2, on.Count);
        }

        [Fact]
        public void Select_OnTupleElement_ResolvesJoinedTableAlias()
        {
            // 文档 5.4 节的 Select 写法：x.Item1.Id / x.Item2.Name 必须各自带正确别名
            var expr = Expr.Query<TestUser, IEnumerable<object>>(q =>
                q.Join<TestUser, TestDepartment>((u, d) => u.DeptId == d.Id)
                 .Select(x => new { x.Item1.Id, x.Item2.Name }));

            string sql = ToSql(expr).Sql;

            // 早期版本会渲染成 Id("T0"."Item1")，必须已修正为直接列引用
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
            Assert.Contains("\"T0\".\"Id\"", sql);
            Assert.Contains("\"T1\".\"Name\"", sql);
        }

        [Fact]
        public void AggregateSamples_FromDocumentation_ProduceAggregateColumns()
        {
            // 对应文档 1.3 节的聚合示例
            Expr count = Expr.Query<TestUser, int>(q => q.Count());
            Expr maxAge = Expr.Query<TestUser, int>(q => q.Max(u => u.Age));
            Expr avgAge = Expr.Query<TestUser, double>(q => q.Average(u => (double)u.Age));

            Assert.Equal("COUNT", ((FunctionExpr)((SelectExpr)count).Selects[0].Value!).FunctionName);
            Assert.Equal("MAX", ((FunctionExpr)((SelectExpr)maxAge).Selects[0].Value!).FunctionName);
            Assert.Equal("AVG", ((FunctionExpr)((SelectExpr)avgAge).Selects[0].Value!).FunctionName);

            // 标量子查询示例：From<T>() 写在 Lambda 内
            Expr scalar = Expr.Lambda<TestUser>(u => u.Age > Expr.From<TestUser>().Average(x => x.Age));
            Assert.Contains("(SELECT AVG(", ToSql(scalar).Sql);
        }
    }
}