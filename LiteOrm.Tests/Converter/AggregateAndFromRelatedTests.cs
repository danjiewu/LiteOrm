using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using LiteOrm;
using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// 覆盖 <see cref="Expr.From{T}"/> / <see cref="Expr.FromRelated{T}"/> 的 lambda 占位语义，
    /// 以及根级与子查询内聚合函数的解析结果。
    /// </summary>
    public class AggregateAndFromRelatedTests
    {
        private static PreparedSql ToSql(Expr expr)
            => expr.ToPreparedSql(new SqlBuildContext(SqlBuilder.Instance) { SingleTable = false });

        #region 占位方法

        [Fact]
        public void From_Generic_ReturnsUnboundQuerySource()
        {
            // From<T>() 现在返回真实实例，只是没有执行器，取数时才抛异常
            var source = Expr.From<AggOrder>();
            Assert.IsType<QuerySource<AggOrder>>(source);

            var ex = Assert.Throws<InvalidOperationException>(() => source.ToList());
            Assert.Contains("no executor", ex.Message);
        }

        [Fact]
        public void FromRelated_Generic_ReturnsUnboundQuerySource()
        {
            // 与 From<T>() 一致：返回没有执行器的 QuerySource，可直接拼装查询
            var source = Expr.FromRelated<AggOrder>();
            Assert.IsType<QuerySource<AggOrder>>(source);

            var ex = Assert.Throws<InvalidOperationException>(() => source.ToList());
            Assert.Contains("no executor", ex.Message);
        }

        [Fact]
        public void From_Type_StillReturnsTableExpr()
        {
            var table = Expr.From(typeof(AggOrder));
            Assert.IsType<TableExpr>(table);
            Assert.Equal(typeof(AggOrder), table.Type);
        }

        #endregion

        #region From / FromRelated 在 lambda 内解析

        [Fact]
        public void Convert_FromInLambda_ResolvesToTableExpression()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<AggOrder, IEnumerable<AggOrder>>>)(o => Expr.From<AggOrder>()));

            var expr = converter.ToValueExpr();

            // From<T> 在 lambda 内解析成表，且不应抛异常
            var value = Assert.IsType<ValueExpr>(expr);
            var table = Assert.IsType<TableExpr>(value.Value);
            Assert.Equal(typeof(AggOrder), table.Type);
        }

        [Fact]
        public void Convert_FromRelatedInLambda_AppendsRelatedCondition()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<AggOrder, IEnumerable<AggCustomer>>>)(o => Expr.FromRelated<AggCustomer>()));

            var expr = converter.ToValueExpr();

            var value = Assert.IsType<ValueExpr>(expr);
            var where = Assert.IsType<WhereExpr>(value.Value);
            Assert.IsType<TableExpr>(where.Source);
            Assert.Equal(typeof(AggCustomer), ((TableExpr)where.Source!).Type);
            // 自动关联条件落在子查询自身的 WHERE 上
            Assert.NotNull(where.Where);
        }

        #endregion

        #region 聚合函数

        [Fact]
        public void Convert_Count_RootLevel_GeneratesCountSql()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<AggOrder>, int>>)(q => q.Count()));

            var expr = converter.ToSqlSegment();

            var select = Assert.IsType<SelectExpr>(expr);
            var item = Assert.Single(select.Selects);
            var function = Assert.IsType<FunctionExpr>(item.Value);
            Assert.Equal("COUNT", function.FunctionName);
            Assert.True(function.IsAggregate);
            Assert.Null(item.Alias);

            Assert.Contains("COUNT(1)", ToSql(expr).Sql);
        }

        [Fact]
        public void Convert_CountAfterWhere_KeepsFilterCondition()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<AggOrder>, int>>)(q => q.Where(o => o.Amount > 10).Count()));

            var expr = converter.ToSqlSegment();
            string sql = ToSql(expr).Sql;

            Assert.Contains("COUNT(1)", sql);
            Assert.Contains("WHERE", sql);
        }

        [Fact]
        public void Convert_SumWithSelector_GeneratesSumOfColumn()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<AggOrder>, decimal>>)(q => q.Sum(o => o.Amount)));

            var expr = converter.ToSqlSegment();

            var select = Assert.IsType<SelectExpr>(expr);
            var function = Assert.IsType<FunctionExpr>(Assert.Single(select.Selects).Value);
            Assert.Equal("SUM", function.FunctionName);
            Assert.True(function.IsAggregate);
            // 选择器里的列访问必须解析成 Amount，而不是退化成常量 1
            var arg = Assert.IsType<PropertyExpr>(Assert.Single(function.Args));
            Assert.Equal("Amount", arg.PropertyName);
        }

        [Fact]
        public void Convert_Average_GeneratesAvg()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<AggOrder>, decimal>>)(q => q.Average(o => o.Amount)));

            var expr = converter.ToSqlSegment();

            var select = Assert.IsType<SelectExpr>(expr);
            var function = Assert.IsType<FunctionExpr>(Assert.Single(select.Selects).Value);
            Assert.Equal("AVG", function.FunctionName);
            Assert.True(function.IsAggregate);
            Assert.Contains("AVG(", ToSql(expr).Sql);
        }

        [Fact]
        public void Convert_MaxAndMin_SetIsAggregateSoTheyRenderAsAggregates()
        {
            var maxConverter = new LambdaExprConverter(
                (Expression<Func<IQueryable<AggOrder>, decimal>>)(q => q.Max(o => o.Amount)));
            var minConverter = new LambdaExprConverter(
                (Expression<Func<IQueryable<AggOrder>, decimal>>)(q => q.Min(o => o.Amount)));

            string maxSql = ToSql(maxConverter.ToSqlSegment()).Sql;
            string minSql = ToSql(minConverter.ToSqlSegment()).Sql;

            // IsAggregate 未置位时 Max/Min 会被映射成 GREATEST/LEAST
            Assert.Contains("MAX(", maxSql);
            Assert.DoesNotContain("GREATEST", maxSql);
            Assert.Contains("MIN(", minSql);
            Assert.DoesNotContain("LEAST", minSql);
        }

        [Fact]
        public void Convert_ScalarSubquery_InComparison_IsParenthesized()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<AggOrder, bool>>)(o => o.Amount > Expr.From<AggOrder>().Average(x => x.Amount)));

            var expr = converter.ToLogicExpr();

            // 标量子查询参与比较时必须整体加括号，否则 SQL 非法
            Assert.Contains("(SELECT AVG(", ToSql(expr!).Sql);
        }

        [Fact]
        public void Convert_AggregateOnGroupedSource_StillUsesGroupingSemantics()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<AggOrder>, IQueryable<int>>>)(q => q.GroupBy(o => o.Status).Select(g => g.Count())));

            var expr = converter.ToSqlSegment();

            // GroupBy 之后的 Count 走分组路径，不应被根级聚合处理器包装成子查询
            Assert.Contains("GROUP BY", ToSql(expr).Sql);
            Assert.IsType<SelectExpr>(expr);
        }

        #endregion

        #region 测试模型

        [Table("AggOrders")]
        private class AggOrder
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("Amount")]
            public decimal Amount { get; set; }

            [Column("Status")]
            public int Status { get; set; }

            [Column("CustomerId")]
            [ForeignType(typeof(AggCustomer), Alias = "Customer")]
            public int CustomerId { get; set; }
        }

        [Table("AggCustomers")]
        private class AggCustomer
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("Name")]
            public string? Name { get; set; }
        }

        #endregion
    }
}