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
    /// 覆盖 <c>Join</c> / <c>JoinRelated</c> 的 lambda 解析：<see cref="FromExpr"/> 承载连接表，
    /// <see cref="TableJoinExpr"/> 只描述 JOIN 段本身。
    /// </summary>
    public class JoinExprTests
    {
        private static PreparedSql ToSql(Expr expr)
            => expr.ToPreparedSql(new SqlBuildContext(SqlBuilder.Instance) { SingleTable = false });

        #region 占位方法

        [Fact]
        public void Join_DirectCall_Throws()
        {
            var source = new List<JoinOrder>();
            var ex = Assert.Throws<InvalidOperationException>(
                () => source.Join<JoinOrder, JoinCustomer>((o, c) => o.CustomerId == c.Id));
            Assert.Contains("only used for parsing lambda expressions", ex.Message);
        }

        [Fact]
        public void JoinRelated_DirectCall_Throws()
        {
            var source = new List<JoinOrder>();
            Assert.Throws<InvalidOperationException>(() => source.JoinRelated<JoinOrder, JoinCustomer>());
            Assert.Throws<InvalidOperationException>(
                () => source.JoinRelated<JoinOrder, JoinCustomer>((o, c) => o.Amount > 0));
        }

        #endregion

        #region Join 结构

        [Fact]
        public void Convert_JoinWithExplicitOn_ProducesFromExprWithOneJoin()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinCustomer)>>>)(
                    q => q.Join<JoinOrder, JoinCustomer>((o, c) => o.CustomerId == c.Id)));

            var expr = converter.ToSqlSegment();

            // Join 构造 FromExpr，TableJoinExpr 只作为 Joins 的一项
            var from = Assert.IsType<FromExpr>(expr);
            Assert.IsType<TableExpr>(from.Source);
            var join = Assert.Single(from.Joins);
            Assert.IsType<TableExpr>(join.Source);
            Assert.Equal(typeof(JoinCustomer), ((TableExpr)join.Source!).Type);
            Assert.Equal(TableJoinType.Left, join.JoinType);
            Assert.NotNull(join.On);
        }

        [Fact]
        public void Convert_JoinWithExplicitOn_RendersFromClauseWithJoin()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinCustomer)>>>)(
                    q => q.Join<JoinOrder, JoinCustomer>((o, c) => o.CustomerId == c.Id)));

            string sql = ToSql(converter.ToSqlSegment()).Sql;

            // FromExpr 作为根源只输出「主表 + JOIN 段」，FROM 关键字由上层 SelectExpr 补齐
            Assert.Contains("JoinOrders", sql);
            Assert.Contains("LEFT JOIN", sql);
            Assert.Contains("JoinCustomers", sql);
            Assert.Contains("ON", sql);
        }

        [Fact]
        public void Convert_JoinRelated_InfersOnFromForeignType()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinCustomer)>>>)(
                    q => q.JoinRelated<JoinOrder, JoinCustomer>()));

            var from = Assert.IsType<FromExpr>(converter.ToSqlSegment());
            var join = Assert.Single(from.Joins);

            // 自动推断出来的 ON 条件必须包含外键列 CustomerId
            Assert.NotNull(join.On);
            Assert.Contains("CustomerId", join.On!.ToString());
        }

        [Fact]
        public void Convert_JoinRelatedWithLambda_MergesAutoAndExplicitCondition()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinCustomer)>>>)(
                    q => q.JoinRelated<JoinOrder, JoinCustomer>((o, c) => o.Amount > 10)));

            var from = Assert.IsType<FromExpr>(converter.ToSqlSegment());
            var join = Assert.Single(from.Joins);

            // 自动关联条件与显式条件按 AND 合并
            var on = Assert.IsType<AndExpr>(join.On);
            Assert.Equal(2, on.Count);
            Assert.Contains("CustomerId", on.ToString());
            Assert.Contains("Amount", on.ToString());
        }

        [Fact]
        public void Convert_JoinRelatedWithoutMetadata_Throws()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinUnrelated)>>>)(
                    q => q.JoinRelated<JoinOrder, JoinUnrelated>()));

            var ex = Assert.Throws<InvalidOperationException>(() => converter.ToSqlSegment());
            Assert.Contains("No relation", ex.Message);
        }

        [Fact]
        public void Convert_NestedJoin_AppendsSecondJoin()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<((JoinOrder, JoinCustomer), JoinRegion)>>>)(
                    q => q.Join<JoinOrder, JoinCustomer>((o, c) => o.CustomerId == c.Id)
                          .Join<(JoinOrder, JoinCustomer), JoinRegion>((x, r) => x.Item1.RegionId == r.Id)));

            var from = Assert.IsType<FromExpr>(converter.ToSqlSegment());

            Assert.Equal(2, from.Joins.Count);
            Assert.Equal(typeof(JoinCustomer), ((TableExpr)from.Joins[0].Source!).Type);
            Assert.Equal(typeof(JoinRegion), ((TableExpr)from.Joins[1].Source!).Type);

            string sql = ToSql(from).Sql;
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(sql, "JOIN").Count);
        }

        #endregion

        #region 元组元素解析

        [Fact]
        public void Convert_JoinWhereOnItem1AndItem2_GeneratesQualifiedColumns()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinCustomer)>>>)(
                    q => q.Join<JoinOrder, JoinCustomer>((o, c) => o.CustomerId == c.Id)
                          .Where(x => x.Item1.Amount > 10 && x.Item2.Name == "VIP")));

            var where = Assert.IsType<WhereExpr>(converter.ToSqlSegment());
            var from = Assert.IsType<FromExpr>(where.Source);
            var logic = Assert.IsType<AndExpr>(where.Where);

            // Item1 应落到主表别名，Item2 应落到连接表别名，不能出现 [Item1] / [Item2] 这样的列名
            var mainAlias = ((TableExpr)from.Source!).Alias;
            var joinAlias = ((TableExpr)from.Joins[0].Source!).Alias;

            var amountCond = Assert.IsType<LogicBinaryExpr>(logic[0]);
            Assert.Equal("Amount", Assert.IsType<PropertyExpr>(amountCond.Left).PropertyName);
            // 主表别名与当前别名一致时按既有约定省略，Item1 不应被写成连接表别名
            string? amountAlias = Assert.IsType<PropertyExpr>(amountCond.Left).TableAlias;
            Assert.True(amountAlias == null || amountAlias == mainAlias);

            var nameCond = Assert.IsType<LogicBinaryExpr>(logic[1]);
            Assert.Equal("Name", Assert.IsType<PropertyExpr>(nameCond.Left).PropertyName);
            Assert.Equal(joinAlias, Assert.IsType<PropertyExpr>(nameCond.Left).TableAlias);
        }

        [Fact]
        public void Convert_JoinWhereOnItem2_ResolvesToJoinedTableAlias()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinCustomer)>>>)(
                    q => q.Join<JoinOrder, JoinCustomer>((o, c) => o.CustomerId == c.Id)
                          .Where(x => x.Item2.Name == "VIP")));

            var where = Assert.IsType<WhereExpr>(converter.ToSqlSegment());
            var condition = Assert.IsType<LogicBinaryExpr>(where.Where);
            var left = Assert.IsType<PropertyExpr>(condition.Left);

            Assert.Equal("Name", left.PropertyName);
            // 连接表别名与主表别名必须不同，否则列引用会串表
            Assert.NotNull(left.TableAlias);
            Assert.NotEqual(((TableExpr)((FromExpr)where.Source!).Source!).Alias, left.TableAlias);
        }

        [Fact]
        public void Convert_JoinSelectAnonymousItems_CarriesAliases()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<object>>>)(
                    q => q.Join<JoinOrder, JoinCustomer>((o, c) => o.CustomerId == c.Id)
                          .Select(x => new { x.Item1.Amount, x.Item2.Name })));

            var select = Assert.IsType<SelectExpr>(converter.ToSqlSegment());

            Assert.Equal(2, select.Selects.Count);
            Assert.Equal("Amount", select.Selects[0].Alias);
            Assert.Equal("Name", select.Selects[1].Alias);
        }

        #endregion

        #region 测试模型

        [Table("JoinOrders")]
        private class JoinOrder
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("Amount")]
            public decimal Amount { get; set; }

            [Column("CustomerId")]
            [ForeignType(typeof(JoinCustomer), Alias = "Customer")]
            public int CustomerId { get; set; }

            [Column("RegionId")]
            public int RegionId { get; set; }
        }

        [Table("JoinCustomers")]
        private class JoinCustomer
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("Name")]
            public string? Name { get; set; }
        }

        [Table("JoinRegions")]
        private class JoinRegion
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("Name")]
            public string? Name { get; set; }
        }

        [Table("JoinUnrelated")]
        private class JoinUnrelated
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }
        }

        #endregion
    }
}