using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Tests.Models;
using Xunit;

// Join / JoinRelated / 根级聚合只在 Lambda 内被解析，脱离 Lambda 会抛异常，
// 因此这些文档形态仍需通过 Expr.Query 构造表达式树
#pragma warning disable CS0618

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// 校验文档 core-usage/lambda-guide 5.4 的 Join / JoinRelated 示例可以真实编译并生成预期 SQL。
    /// lambda 连接只有 <c>Expr.From&lt;T1&gt;().Join&lt;T2&gt;(...)</c> 一种入口。
    /// </summary>
    public class ExprGuideJoinDocTests
    {
        private static PreparedSql ToSql(Expr expr)
            => expr.ToPreparedSql(new SqlBuildContext(SqlBuilder.Instance) { SingleTable = false });

        [Fact]
        public void Join_WithExplicitCondition_MatchesDocumentedShape()
        {
            // 对应文档 5.4 节第一段示例
            var query = Expr.Query<TestUser, IEnumerable<(TestUser, TestDepartment)>>(u =>
                Expr.From<TestUser>()
                    .Join<TestDepartment>((x, d) => x.DeptId == d.Id)
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
            // 对应文档 5.4 节第二段示例
            var query = Expr.Query<TestUser, IEnumerable<(TestUser, TestDepartment)>>(u =>
                Expr.From<TestUser>().JoinRelated<TestDepartment>());

            var from = Assert.IsType<FromExpr>(query);
            Assert.NotNull(from.Joins[0].On);
            Assert.Contains("DeptId", from.Joins[0].On!.ToString()!);
        }

        [Fact]
        public void JoinRelated_WithCondition_MergesByAnd()
        {
            // 对应文档 5.4 节第三段示例
            var query = Expr.Query<TestUser, IEnumerable<(TestUser, TestDepartment)>>(u =>
                Expr.From<TestUser>().JoinRelated<TestDepartment>((x, d) => d.Name != null));

            var from = Assert.IsType<FromExpr>(query);
            var on = Assert.IsType<AndExpr>(from.Joins[0].On);
            Assert.Equal(2, on.Count);
        }

        [Fact]
        public void Select_OnTupleElement_ResolvesJoinedTableAlias()
        {
            // 文档 5.4 节的 Select 写法：x.Item1.Id / x.Item2.Name 必须各自带正确别名
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<TestUser>, IEnumerable<object>>>)(
                    u => Expr.From<TestUser>()
                        .Join<TestDepartment>((x, d) => x.DeptId == d.Id)
                        .Select(x => new { x.Item1.Id, x.Item2.Name })));

            var select = Assert.IsType<SelectExpr>(converter.ToSqlSegment());
            var from = Assert.IsType<FromExpr>(select.Source);

            // 别名按分配顺序生成，不写死 T0/T1，从表达式实际别名断言
            string mainAlias = from.Source!.Alias!;
            string joinAlias = from.Joins[0].Source!.Alias!;
            Assert.NotEqual(mainAlias, joinAlias);

            var idColumn = Assert.IsType<PropertyExpr>(select.Selects[0].Value);
            var nameColumn = Assert.IsType<PropertyExpr>(select.Selects[1].Value);
            Assert.Equal("Id", idColumn.PropertyName);
            Assert.Equal("Name", nameColumn.PropertyName);
            // 主表别名与当前别名一致时省略；连接表别名必须显式带上
            Assert.True(idColumn.TableAlias == null || idColumn.TableAlias == mainAlias);
            Assert.Equal(joinAlias, nameColumn.TableAlias);

            // 不能出现把 Item1 / Item2 当作列名的渲染
            string sql = ToSql(select).Sql;
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
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
            Expr? scalar = Expr.Lambda<TestUser>(u => u.Age > Expr.From<TestUser>().Average(x => x.Age));
            Assert.Contains("(SELECT AVG(", ToSql(scalar!).Sql);

            // 不写 Lambda 时用结构性 Select 拼聚合段，等价于只保留聚合列的查询
            Expr structural = Expr.From(typeof(TestUser)).Select(Expr.Aggregate("COUNT", Expr.Const(1)));
            string structuralSql = ToSql(structural).Sql;
            Assert.Contains("COUNT(1)", structuralSql);
            Assert.Contains("FROM ", structuralSql);
        }
    }
}