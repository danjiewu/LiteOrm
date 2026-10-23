using System;
using System.Collections.Generic;
using System.Linq;
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Tests.Models;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// 校验 IQueryable 查询树到 Expr 的解析：根改写、终结符剥离、TableArgs 回填，
    /// 以及 <see cref="QuerySource{T1}"/> 终端方法在 Lambda 内的预注册解析。
    /// </summary>
    public class QuerySourceExprTests
    {
        private static PreparedSql ToSql(Expr expr)
            => expr.ToPreparedSql(new SqlBuildContext(SqlBuilder.Instance) { SingleTable = false });

        #region IQueryable 转 Expr

        [Fact]
        public void ToExpr_FromOnly_ReturnsTableExpr()
        {
            var segment = QuerySourceExprConverter.ToExpr(Expr.From<TestUser>());

            var table = Assert.IsType<TableExpr>(segment);
            Assert.Equal(typeof(TestUser), table.Type);
            Assert.Equal(Constants.DefaultTableAlias, table.Alias);
        }

        [Fact]
        public void ToExpr_Where_ProducesWhereOverTable()
        {
            var segment = QuerySourceExprConverter.ToExpr(
                Expr.From<TestUser>().Where(u => u.Age > 18));

            var where = Assert.IsType<WhereExpr>(segment);
            Assert.IsType<TableExpr>(where.Source);

            var condition = Assert.IsType<LogicBinaryExpr>(where.Where);
            Assert.Equal("Age", Assert.IsType<PropertyExpr>(condition.Left).PropertyName);
        }

        [Fact]
        public void ToExpr_WhereSelectOrderBy_ProducesSelectChain()
        {
            var segment = QuerySourceExprConverter.ToExpr(
                Expr.From<TestUser>()
                    .Where(u => u.Age > 18)
                    .OrderBy(u => u.Id)
                    .Select(u => new { u.Id, u.Name }));

            var select = Assert.IsType<SelectExpr>(segment);
            var orderBy = Assert.IsType<OrderByExpr>(select.Source);
            var where = Assert.IsType<WhereExpr>(orderBy.Source);
            Assert.IsType<TableExpr>(where.Source);

            Assert.Equal(2, select.Selects.Count);
            Assert.Equal("Id", select.Selects[0].Alias);
            Assert.Equal("Name", select.Selects[1].Alias);
        }

        [Fact]
        public void ToExpr_WithTableArgs_BackfillsRootTable()
        {
            var segment = QuerySourceExprConverter.ToExpr(
                Expr.From<TestUser>("202405").Where(u => u.Age > 18));

            var where = Assert.IsType<WhereExpr>(segment);
            var table = Assert.IsType<TableExpr>(where.Source);
            Assert.Equal(new[] { "202405" }, table.TableArgs);
        }

        [Fact]
        public void ToExpr_TableArgsCondition_BackfillsRootTable()
        {
            // Where 里的 TableArgs 赋值条件必须落到根表，并且不产生 WHERE
            var segment = QuerySourceExprConverter.ToExpr(
                Expr.From<TestLog>().Where(l => l.TableArgs == new[] { "202401" }));

            var table = Assert.IsType<TableExpr>(segment);
            Assert.Equal(new[] { "202401" }, table.TableArgs);

            string sql = ToSql(new SelectExpr(table, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("TestLog_202401", sql);
        }

        [Fact]
        public void ToExpr_TableArgsConditionWithPredicate_AppliesBoth()
        {
            var segment = QuerySourceExprConverter.ToExpr(
                Expr.From<TestLog>().Where(l => l.TableArgs == new[] { "202401" } && l.Amount > 10));

            var where = Assert.IsType<WhereExpr>(segment);
            var table = Assert.IsType<TableExpr>(where.Source);
            Assert.Equal(new[] { "202401" }, table.TableArgs);
            Assert.Contains("Amount", where.Where!.ToString());

            string sql = ToSql(new SelectExpr(where, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("TestLog_202401", sql);
        }

        [Fact]
        public void ToExpr_TableArgsConditionAfterWhere_BackfillsRootTable()
        {
            var segment = QuerySourceExprConverter.ToExpr(
                Expr.From<TestLog>()
                    .Where(l => l.Amount > 10)
                    .Where(l => l.TableArgs == new[] { "202401" }));

            var where = Assert.IsType<WhereExpr>(segment);
            var table = Assert.IsType<TableExpr>(where.Source);
            Assert.Equal(new[] { "202401" }, table.TableArgs);
            Assert.Contains("Amount", where.Where!.ToString());
        }

        [Fact]
        public void ToExpr_TableArgsConditionOnJoinTupleElement_BackfillsMainTable()
        {
            // Join 场景：TableArgs 赋值目标可以是元组元素 x.Item1
            var segment = QuerySourceExprConverter.ToExpr(
                Expr.From<ShardJoinOrder>()
                    .JoinRelated<ShardJoinUser>()
                    .Where(x => ((IArged)x.Item1).TableArgs == new[] { "202401" }));

            var from = Assert.IsType<FromExpr>(segment);
            var main = Assert.IsType<TableExpr>(from.Source);
            Assert.Equal(new[] { "202401" }, main.TableArgs);

            string sql = ToSql(new SelectExpr(from, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("ShardJoinOrders_202401", sql);
        }

        [Fact]
        public void ToExpr_TableArgsConditionOnJoinTupleElements_BackfillsEachTable()
        {
            var segment = QuerySourceExprConverter.ToExpr(
                Expr.From<ShardJoinOrder>()
                    .JoinRelated<ShardJoinUser>()
                    .Where(x => ((IArged)x.Item1).TableArgs == new[] { "202401" }
                             && ((IArged)x.Item2).TableArgs == new[] { "202402" }));

            var from = Assert.IsType<FromExpr>(segment);
            Assert.Equal(new[] { "202401" }, Assert.IsType<TableExpr>(from.Source).TableArgs);
            Assert.Equal(new[] { "202402" }, Assert.IsType<TableExpr>(from.Joins[0].Source!).TableArgs);

            string sql = ToSql(new SelectExpr(from, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("ShardJoinOrders_202401", sql);
            Assert.Contains("ShardJoinUsers_202402", sql);
        }

        [Fact]
        public void ToExpr_TableArgsConditionOnJoinTupleElementWithPredicate_KeepsPredicate()
        {
            var segment = QuerySourceExprConverter.ToExpr(
                Expr.From<ShardJoinOrder>()
                    .JoinRelated<ShardJoinUser>()
                    .Where(x => ((IArged)x.Item1).TableArgs == new[] { "202401" } && x.Item2.Id > 0));

            var where = Assert.IsType<WhereExpr>(segment);
            var from = Assert.IsType<FromExpr>(where.Source);
            Assert.Equal(new[] { "202401" }, Assert.IsType<TableExpr>(from.Source).TableArgs);
            Assert.Contains("Id", where.Where!.ToString());
        }

        [Fact]
        public void ToExpr_NonQuerySourceRoot_Works()
        {
            // 不限制源类型：任意 IQueryable 都能作为查询根
            var segment = QuerySourceExprConverter.ToExpr(
                new List<TestUser>().AsQueryable().Where(u => u.Age > 18));

            var where = Assert.IsType<WhereExpr>(segment);
            Assert.Equal(typeof(TestUser), Assert.IsType<TableExpr>(where.Source).Type);
        }

        [Fact]
        public void ToExpr_NoIQueryableRoot_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => QuerySourceExprConverter.ToExpr(System.Linq.Expressions.Expression.Constant(42)));
            Assert.Contains("not rooted at an IQueryable", ex.Message);
        }

        [Fact]
        public void ToExpr_UnsupportedOperator_Throws()
        {
            var ex = Assert.Throws<NotSupportedException>(
                () => QuerySourceExprConverter.ToExpr(Expr.From<TestUser>().Select(u => u.Name).Distinct()));
            Assert.Contains("Distinct", ex.Message);
        }

        [Fact]
        public void ToExpr_TerminalFirstOrDefault_Unwrapped()
        {
            // 终结符不改变 SQL 形态，应被剥离后按普通查询解析
            var source = Expr.From<TestUser>();
            System.Linq.Expressions.Expression call = System.Linq.Expressions.Expression.Call(
                typeof(Queryable),
                nameof(Queryable.FirstOrDefault),
                new[] { typeof(TestUser) },
                source.Expression);

            var segment = QuerySourceExprConverter.ToExpr(call);
            Assert.IsType<TableExpr>(segment);
        }

        [Fact]
        public void QuerySource_WithoutExecutor_Throws()
        {
            var source = Expr.From<TestUser>();

            Assert.Throws<InvalidOperationException>(() => source.Count());
            Assert.Throws<InvalidOperationException>(() => source.FirstOrDefault());
            Assert.Throws<InvalidOperationException>(() => source.ToList());
        }

        #endregion

        #region GetQueryExpr 公开入口

        [Fact]
        public void GetQueryExpr_OnQuerySource_ReturnsQuerySegment()
        {
            QuerySource<TestUser> source = Expr.From<TestUser>();

            var table = Assert.IsType<TableExpr>(source.GetQueryExpr());
            Assert.Equal(typeof(TestUser), table.Type);
        }

        [Fact]
        public void GetQueryExpr_OnChain_ReturnsWhereOverTable()
        {
            IQueryable<TestUser> query = Expr.From<TestUser>().Where(u => u.Age > 18);

            var where = Assert.IsType<WhereExpr>(query.GetQueryExpr());
            Assert.IsType<TableExpr>(where.Source);
            Assert.NotNull(where.Where);
        }

        [Fact]
        public void GetQueryExpr_AfterOrderBy_Works()
        {
            // OrderBy 的静态返回类型是 IOrderedQueryable<T>，链尾靠同名扩展方法收口
            SqlSegment segment = Expr.From<TestUser>()
                .Where(u => u.Age > 18)
                .OrderBy(u => u.Id)
                .GetQueryExpr();

            Assert.IsType<OrderByExpr>(segment);
        }

        [Fact]
        public void GetQueryExpr_FromRelatedOutsideLambda_BehavesLikeFrom()
        {
            // FromRelated 只在 Lambda 内补关联条件，直接使用等价于 From<T>()
            SqlSegment segment = Expr.FromRelated<TestUser>().GetQueryExpr();

            Assert.Equal(typeof(TestUser), Assert.IsType<TableExpr>(segment).Type);
        }

        #endregion

        #region Lambda 内终端方法解析

        // 终端方法（Count / FirstOrDefault / ToList）没有执行器时会抛异常，
        // 只有在 Lambda 内才被预注册 handler 接管，因此这里仍需 Expr.Query

        [Fact]
        public void Lambda_FromThenCount_ParsesToAggregate()
        {
            Expr expr = Expr.Query<TestUser, int>(q => Expr.From<TestUser>().Count());

            var select = Assert.IsType<SelectExpr>(expr);
            var function = Assert.IsType<FunctionExpr>(select.Selects[0].Value);
            Assert.Equal("COUNT", function.FunctionName);
            Assert.True(function.IsAggregate);

            string sql = ToSql(select).Sql;
            Assert.Contains("COUNT(1)", sql);
        }

        [Fact]
        public void Lambda_FromThenToList_ParsesToSource()
        {
            Expr expr = Expr.Query<TestUser, List<TestUser>>(q => Expr.From<TestUser>().ToList());

            // 结果集本身就是源，不应残留 ToList 函数
            Assert.IsType<TableExpr>(expr);
            Assert.DoesNotContain("ToList", ToSql(expr).Sql);
        }

        [Fact]
        public void Lambda_FromThenFirstOrDefault_LimitsOneRow()
        {
            Expr expr = Expr.Query<TestUser, TestUser>(q => Expr.From<TestUser>().FirstOrDefault());

            var section = Assert.IsType<SectionExpr>(expr);
            Assert.Equal(0, section.Skip);
            Assert.Equal(1, section.Take);
        }

        [Fact]
        public void Lambda_FromThenJoinThenCount_Parses()
        {
            Expr expr = Expr.Query<TestUser, int>(q =>
                Expr.From<TestUser>().JoinRelated<TestDepartment>().Count());

            string sql = ToSql(expr).Sql;
            Assert.Contains("LEFT JOIN", sql);
            Assert.Contains("COUNT(1)", sql);
        }

        #endregion

        #region 方法处理器查找

        /// <summary>用于验证泛型方法处理器查找的探针类型。</summary>
        private static class GenericProbe<T>
        {
            public static int Probe() => 0;
        }

        [Fact]
        public void MethodHandler_PrefersSpecializedTypeThenFallsBackToGenericDefinition()
        {
            try
            {
                // 只注册泛型定义：特化类型应回退命中
                LambdaExprConverter.RegisterMethodHandler(
                    typeof(GenericProbe<>),
                    nameof(GenericProbe<object>.Probe),
                    (node, converter) => new FunctionExpr("GENERIC_PROBE"));

                FunctionExpr generic = ConvertProbe();
                Assert.Equal("GENERIC_PROBE", generic.FunctionName);

                // 再注册特化类型：应覆盖泛型定义
                LambdaExprConverter.RegisterMethodHandler(
                    typeof(GenericProbe<int>),
                    nameof(GenericProbe<int>.Probe),
                    (node, converter) => new FunctionExpr("SPECIALIZED_PROBE"));

                FunctionExpr specialized = ConvertProbe();
                Assert.Equal("SPECIALIZED_PROBE", specialized.FunctionName);
            }
            finally
            {
                // 注册表是全局静态的，撤掉特化条目避免影响其他用例
                LambdaExprConverter.RegisterMethodHandler(
                    typeof(GenericProbe<int>),
                    nameof(GenericProbe<int>.Probe),
                    (node, converter) => new FunctionExpr("GENERIC_PROBE"));
            }
        }

        private static FunctionExpr ConvertProbe()
        {
            var converter = new LambdaExprConverter(
                (System.Linq.Expressions.Expression<Func<TestUser, int>>)(u => GenericProbe<int>.Probe()));
            return Assert.IsType<FunctionExpr>(converter.ToValueExpr());
        }

        #endregion

        #region TableArgs 分表测试模型

        [Table("ShardJoinOrders_{0}")]
        private class ShardJoinOrder : IArged
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("UserId")]
            [ForeignType(typeof(ShardJoinUser), Alias = "User")]
            public int UserId { get; set; }

            [Column(false)]
            public string[] TableArgs => Array.Empty<string>();
        }

        [Table("ShardJoinUsers_{0}")]
        private class ShardJoinUser : IArged
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column(false)]
            public string[] TableArgs => Array.Empty<string>();
        }

        #endregion
    }
}
