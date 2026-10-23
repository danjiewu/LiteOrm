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
    /// lambda 连接只有 <c>From&lt;T1&gt;().Join&lt;T2&gt;(...)</c> 一种入口。
    /// </summary>
    public class JoinExprTests
    {
        private static PreparedSql ToSql(Expr expr)
            => expr.ToPreparedSql(new SqlBuildContext(SqlBuilder.Instance) { SingleTable = false });

        #region 占位方法

        [Fact]
        public void QuerySource_HasParameterlessAndExecutorConstructors()
        {
            // Expr.From<T>() 现在返回真实实例，同时支持传入执行器直接取数
            Assert.NotNull(typeof(QuerySource<JoinOrder>).GetConstructor(Type.EmptyTypes));
            Assert.NotNull(typeof(QuerySource<JoinOrder>).GetConstructor(new[] { typeof(IQuerySourceExecutor<JoinOrder>) }));
        }

        [Fact]
        public void From_ReturnsUnboundQuerySource()
        {
            var source = Expr.From<JoinOrder>();

            Assert.IsType<QuerySource<JoinOrder>>(source);
            Assert.IsAssignableFrom<IQueryable<JoinOrder>>(source);

            // 未提供执行器：只能用于解析表达式，取数即抛
            var ex = Assert.Throws<InvalidOperationException>(() => source.ToList());
            Assert.Contains("no executor", ex.Message);
        }

        #endregion

        #region Join 结构

        [Fact]
        public void Convert_JoinWithExplicitOn_ProducesFromExprWithOneJoin()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinCustomer)>>>)(
                    q => Expr.From<JoinOrder>().Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)));

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
                    q => Expr.From<JoinOrder>().Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)));

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
                    q => Expr.From<JoinOrder>().JoinRelated<JoinCustomer>()));

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
                    q => Expr.From<JoinOrder>().JoinRelated<JoinCustomer>((o, c) => o.Amount > 10)));

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
                    q => Expr.From<JoinOrder>().JoinRelated<JoinUnrelated>()));

            var ex = Assert.Throws<InvalidOperationException>(() => converter.ToSqlSegment());
            Assert.Contains("No relation", ex.Message);
        }

        [Fact]
        public void Convert_ChainedJoin_AppendsSecondJoin()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinCustomer, JoinRegion)>>>)(
                    q => Expr.From<JoinOrder>()
                          .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                          .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id)));

            var from = Assert.IsType<FromExpr>(converter.ToSqlSegment());

            Assert.Equal(2, from.Joins.Count);
            Assert.Equal(typeof(JoinCustomer), ((TableExpr)from.Joins[0].Source!).Type);
            Assert.Equal(typeof(JoinRegion), ((TableExpr)from.Joins[1].Source!).Type);

            // 第二个连接参数 c 必须落到第一段连接表的别名上，不能留下元组占位符
            string on = from.Joins[1].On!.ToString()!;
            Assert.Contains("RegionId", on);
            Assert.DoesNotContain("Item1", on);
            Assert.DoesNotContain("Item2", on);

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
                    q => Expr.From<JoinOrder>()
                          .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
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
                    q => Expr.From<JoinOrder>()
                          .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
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
        public void QuerySource_TwoParameterWhere_MatchesItemAccess()
        {
            var twoParameter = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Where((o, c) => o.Amount > 10 && c.Name == "VIP")
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Where(x => x.Item1.Amount > 10 && x.Item2.Name == "VIP")
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), twoParameter.ToString());

            string sql = ToSql(new SelectExpr(twoParameter, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("Amount", sql);
            Assert.Contains("Name", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_ThreeParameterWhere_MatchesItemAccess()
        {
            var parameterized = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id)
                .Where((o, c, r) => o.Amount > 10 && c.Name == "VIP" && r.Id > 0)
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id)
                .Where(x => x.Item1.Amount > 10 && x.Item2.Name == "VIP" && x.Item3.Id > 0)
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), parameterized.ToString());

            string sql = ToSql(new SelectExpr(parameterized, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
            Assert.DoesNotContain("Item3", sql);
        }

        [Fact]
        public void QuerySource_SevenParameterWhere_MatchesItemAccess()
        {
            var parameterized = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id)
                .Join<JoinZone>((o, c, r, z) => r.Id == z.RegionId)
                .Join<JoinStore>((o, c, r, z, s) => r.Id == s.RegionId)
                .Join<JoinCity>((o, c, r, z, s, ci) => s.Id == ci.StoreId)
                .Join<JoinCountry>((o, c, r, z, s, ci, co) => ci.Id == co.CityId)
                .Where((o, c, r, z, s, ci, co) => o.Amount > 0 && c.Name == "VIP" && co.Id > 0)
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id)
                .Join<JoinZone>((o, c, r, z) => r.Id == z.RegionId)
                .Join<JoinStore>((o, c, r, z, s) => r.Id == s.RegionId)
                .Join<JoinCity>((o, c, r, z, s, ci) => s.Id == ci.StoreId)
                .Join<JoinCountry>((o, c, r, z, s, ci, co) => ci.Id == co.CityId)
                .Where(x => x.Item1.Amount > 0 && x.Item2.Name == "VIP" && x.Item7.Id > 0)
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), parameterized.ToString());

            string sql = ToSql(new SelectExpr(parameterized, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item7", sql);
        }

        [Fact]
        public void QuerySource_TwoParameterSelect_MatchesItemAccess()
        {
            var parameterized = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Select((o, c) => new { OrderId = o.Id, CustomerName = c.Name })
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Select(x => new { OrderId = x.Item1.Id, CustomerName = x.Item2.Name })
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), parameterized.ToString());

            string sql = ToSql(parameterized).Sql;
            Assert.Contains("OrderId", sql);
            Assert.Contains("CustomerName", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_TwoParameterOrderByThenBy_MatchesItemAccess()
        {
            var parameterized = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .OrderBy((o, c) => o.Amount)
                .ThenByDescending((o, c) => c.Name)
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .OrderBy(x => x.Item1.Amount)
                .ThenByDescending(x => x.Item2.Name)
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), parameterized.ToString());

            string sql = ToSql(new SelectExpr(parameterized, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("ORDER BY", sql);
            Assert.Contains("Amount", sql);
            Assert.Contains("Name", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_TwoParameterOrderByDescending_MatchesItemAccess()
        {
            var parameterized = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .OrderByDescending((o, c) => o.Amount)
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .OrderByDescending(x => x.Item1.Amount)
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), parameterized.ToString());

            string sql = ToSql(new SelectExpr(parameterized, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("DESC", sql);
            Assert.DoesNotContain("Item1", sql);
        }

        [Fact]
        public void QuerySource_TwoParameterGroupBy_MatchesItemAccess()
        {
            var parameterized = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => o.CustomerId)
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy(x => x.Item1.CustomerId)
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), parameterized.ToString());

            string sql = ToSql(new SelectExpr(parameterized, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("GROUP BY", sql);
            Assert.Contains("CustomerId", sql);
            Assert.DoesNotContain("Item1", sql);
        }

        [Fact]
        public void QuerySource_TwoParameterGroupByThenSelect_MatchesItemAccess()
        {
            var parameterized = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => o.CustomerId)
                .Select(g => new { CustomerId = g.Key, Count = g.Count() })
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy(x => x.Item1.CustomerId)
                .Select(g => new { CustomerId = g.Key, Count = g.Count() })
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), parameterized.ToString());

            string sql = ToSql(parameterized).Sql;
            Assert.Contains("GROUP BY", sql);
            Assert.Contains("Count", sql);
            Assert.DoesNotContain("Item1", sql);
        }

        [Fact]
        public void QuerySource_ThreeParameterSelect_MatchesItemAccess()
        {
            var parameterized = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id)
                .Select((o, c, r) => new { OrderId = o.Id, CustomerName = c.Name, RegionId = r.Id })
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id)
                .Select(x => new { OrderId = x.Item1.Id, CustomerName = x.Item2.Name, RegionId = x.Item3.Id })
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), parameterized.ToString());

            string sql = ToSql(parameterized).Sql;
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
            Assert.DoesNotContain("Item3", sql);
        }

        [Fact]
        public void QuerySource_ThreeParameterOrderByThenBy_MatchesItemAccess()
        {
            var parameterized = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id)
                .OrderBy((o, c, r) => o.Amount)
                .ThenBy((o, c, r) => r.Name)
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id)
                .OrderBy(x => x.Item1.Amount)
                .ThenBy(x => x.Item3.Name)
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), parameterized.ToString());

            string sql = ToSql(new SelectExpr(parameterized, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("ORDER BY", sql);
            Assert.Contains("Amount", sql);
            Assert.Contains("Name", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item3", sql);
        }

        [Fact]
        public void QuerySource_ThreeParameterGroupBy_MatchesItemAccess()
        {
            var parameterized = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id)
                .GroupBy((o, c, r) => r.Id)
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id)
                .GroupBy(x => x.Item3.Id)
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), parameterized.ToString());

            string sql = ToSql(new SelectExpr(parameterized, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("GROUP BY", sql);
            Assert.DoesNotContain("Item3", sql);
        }

        [Fact]
        public void QuerySource_SevenParameterSelect_MatchesItemAccess()
        {
            var parameterized = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id)
                .Join<JoinZone>((o, c, r, z) => r.Id == z.RegionId)
                .Join<JoinStore>((o, c, r, z, s) => r.Id == s.RegionId)
                .Join<JoinCity>((o, c, r, z, s, ci) => s.Id == ci.StoreId)
                .Join<JoinCountry>((o, c, r, z, s, ci, co) => ci.Id == co.CityId)
                .Select((o, c, r, z, s, ci, co) => new { OrderId = o.Id, CustomerName = c.Name, CountryId = co.Id })
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id)
                .Join<JoinZone>((o, c, r, z) => r.Id == z.RegionId)
                .Join<JoinStore>((o, c, r, z, s) => r.Id == s.RegionId)
                .Join<JoinCity>((o, c, r, z, s, ci) => s.Id == ci.StoreId)
                .Join<JoinCountry>((o, c, r, z, s, ci, co) => ci.Id == co.CityId)
                .Select(x => new { OrderId = x.Item1.Id, CustomerName = x.Item2.Name, CountryId = x.Item7.Id })
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), parameterized.ToString());

            string sql = ToSql(parameterized).Sql;
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item7", sql);
        }

        [Fact]
        public void QuerySource_GroupByThenOrderByKey_MatchesKey()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => o.CustomerId)
                .OrderBy(g => g.Key)
                .GetQueryExpr();

            string sql = ToSql(new SelectExpr(query, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("ORDER BY", sql);
            Assert.DoesNotContain("Key", sql);
        }

        [Fact]
        public void QuerySource_GroupByThenOrderByThenBy_MatchesKeyAndCount()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => o.CustomerId)
                .OrderBy(g => g.Key)
                .ThenByDescending(g => g.Count())
                .GetQueryExpr();

            string sql = ToSql(new SelectExpr(query, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("ORDER BY", sql);
            Assert.Contains("COUNT", sql.ToUpperInvariant());
            Assert.DoesNotContain("Key", sql);
        }

        [Fact]
        public void QuerySource_GroupByMultiKeyOrderByKeyMember_MatchesKeyMember()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => new { o.CustomerId, c.Name })
                .OrderBy(g => g.Key.CustomerId)
                .ThenBy(g => g.Key.Name)
                .GetQueryExpr();

            string sql = ToSql(new SelectExpr(query, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("ORDER BY", sql);
            Assert.DoesNotContain("Key", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_GroupByThenOrderByThenHaving_PushesHavingBelowOrderBy()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => o.CustomerId)
                .OrderBy(g => g.Key)
                .Where(g => g.Count() > 1)
                .GetQueryExpr();

            string sql = ToSql(new SelectExpr(query, new SelectItemExpr(Expr.Const(1)))).Sql;
            int having = sql.IndexOf("HAVING");
            int orderBy = sql.IndexOf("ORDER BY");
            Assert.True(having >= 0, sql);
            Assert.True(orderBy > having, sql);
            Assert.DoesNotContain("Key", sql);
        }

        [Fact]
        public void QuerySource_OrderByThenSelect_MatchesItemAccess()
        {
            var parameterized = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .OrderBy((o, c) => o.Amount)
                .Select((o, c) => new { o.Id, c.Name })
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .OrderBy(x => x.Item1.Amount)
                .Select(x => new { x.Item1.Id, x.Item2.Name })
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), parameterized.ToString());

            string sql = ToSql(parameterized).Sql;
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_WhereAfterOrderByThenSelect_MatchesItemAccess()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .OrderBy((o, c) => o.Amount)
                .Where((o, c) => c.Name == "VIP")
                .Select((o, c) => new { o.Id, c.Name })
                .GetQueryExpr();

            string sql = ToSql(query).Sql;
            int where = sql.IndexOf("WHERE");
            int orderBy = sql.IndexOf("ORDER BY");
            Assert.True(where >= 0, sql);
            Assert.True(orderBy > where, sql);
            Assert.Contains("VIP", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_GroupByAnonymousKey_MatchesItemAccess()
        {
            var parameterized = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => new { o.CustomerId, c.Name })
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy(x => new { x.Item1.CustomerId, x.Item2.Name })
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), parameterized.ToString());

            string sql = ToSql(new SelectExpr(parameterized, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("GROUP BY", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_GroupByMultiKeyOrderByWholeKey_MapsAllKeys()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => new { o.CustomerId, c.Name })
                .OrderBy(g => g.Key)
                .GetQueryExpr();

            string sql = ToSql(new SelectExpr(query, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("ORDER BY", sql);
            Assert.DoesNotContain("Key", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_GroupByMultiKeySelectWholeKey_MapsAllKeys()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => new { o.CustomerId, c.Name })
                .Select(g => g.Key)
                .GetQueryExpr();

            string sql = ToSql(query).Sql;
            Assert.DoesNotContain("Key", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_GroupBySumTupleField_MatchesItemAccess()
        {
            var parameterized = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => o.CustomerId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.Item1.Amount) })
                .GetQueryExpr();

            var itemAccess = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy(x => x.Item1.CustomerId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.Item1.Amount) })
                .GetQueryExpr();

            Assert.Equal(itemAccess.ToString(), parameterized.ToString());

            string sql = ToSql(parameterized).Sql;
            Assert.Contains("\"T0\".\"Amount\"", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_GroupBySumFirstTableTupleField_UsesFirstTableAlias()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => o.CustomerId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.Item1.Id) })
                .GetQueryExpr();

            string sql = ToSql(query).Sql;
            Assert.Contains("\"T0\".\"Id\"", sql);
            Assert.DoesNotContain("Item1", sql);
        }

        [Fact]
        public void QuerySource_GroupBySumSecondTableTupleField_UsesSecondTableAlias()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => o.CustomerId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.Item2.Id) })
                .GetQueryExpr();

            string sql = ToSql(query).Sql;
            Assert.Contains("Sum(\"T1\".\"Id\")", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_GroupByHavingSumTupleField_UsesSecondTableAlias()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => o.CustomerId)
                .Where(g => g.Sum(x => x.Item2.Id) > 0)
                .GetQueryExpr();

            string sql = ToSql(new SelectExpr(query, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("Sum(\"T1\".\"Id\")", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_GroupByOrderBySumTupleField_UsesSecondTableAlias()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => o.CustomerId)
                .OrderBy(g => g.Sum(x => x.Item2.Id))
                .GetQueryExpr();

            string sql = ToSql(new SelectExpr(query, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("Sum(\"T1\".\"Id\")", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_GroupBySectionSelect_MapsKey()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => o.CustomerId)
                .Skip(1)
                .Take(10)
                .Select(g => g.Key)
                .GetQueryExpr();

            string sql = ToSql(query).Sql;
            Assert.DoesNotContain("Key", sql);
            Assert.Contains("CustomerId", sql);
        }

        [Fact]
        public void QuerySource_GroupByMultiKeySelect_MapsKeyMembers()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => new { o.CustomerId, c.Name })
                .Select(g => new { g.Key.CustomerId, g.Key.Name, Count = g.Count() })
                .GetQueryExpr();

            string sql = ToSql(query).Sql;
            Assert.DoesNotContain("Key", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_GroupByHavingSelect_MapsKeyMembers()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => new { o.CustomerId, c.Name })
                .Where(g => g.Count() > 1)
                .Select(g => new { g.Key.CustomerId, g.Key.Name, Count = g.Count() })
                .GetQueryExpr();

            string sql = ToSql(query).Sql;
            Assert.Contains("HAVING", sql);
            Assert.DoesNotContain("Key", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_GroupByOrderBySelect_MapsKeyMembers()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .GroupBy((o, c) => new { o.CustomerId, c.Name })
                .OrderBy(g => g.Key.CustomerId)
                .Select(g => new { g.Key.CustomerId, g.Key.Name, Count = g.Count() })
                .GetQueryExpr();

            string sql = ToSql(query).Sql;
            Assert.Contains("ORDER BY", sql);
            Assert.DoesNotContain("Key", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void QuerySource_OrderByAnonymousKey_MatchesItemAccess()
        {
            var query = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .OrderBy((o, c) => new { o.Amount, c.Name })
                .GetQueryExpr();

            string sql = ToSql(new SelectExpr(query, new SelectItemExpr(Expr.Const(1)))).Sql;
            Assert.Contains("ORDER BY", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void Convert_JoinSelectAnonymousItems_CarriesAliases()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<object>>>)(
                    q => Expr.From<JoinOrder>()
                          .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                          .Select(x => new { x.Item1.Amount, x.Item2.Name })));

            var select = Assert.IsType<SelectExpr>(converter.ToSqlSegment());

            Assert.Equal(2, select.Selects.Count);
            Assert.Equal("Amount", select.Selects[0].Alias);
            Assert.Equal("Name", select.Selects[1].Alias);
        }

        #endregion

        #region 多级连接

        [Fact]
        public void QuerySource_TripleJoin_AppendsThreeJoins()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinCustomer, JoinRegion, JoinZone)>>>)(
                    q => Expr.From<JoinOrder>()
                          .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                          .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id)
                          .Join<JoinZone>((o, c, r, z) => r.Id == z.RegionId)));

            var from = Assert.IsType<FromExpr>(converter.ToSqlSegment());

            Assert.Equal(3, from.Joins.Count);
            Assert.Equal(typeof(JoinCustomer), ((TableExpr)from.Joins[0].Source!).Type);
            Assert.Equal(typeof(JoinRegion), ((TableExpr)from.Joins[1].Source!).Type);
            Assert.Equal(typeof(JoinZone), ((TableExpr)from.Joins[2].Source!).Type);

            // 第三段的参数 r 指向第二段连接的表 JoinRegion
            string on = from.Joins[2].On!.ToString()!;
            Assert.Contains("RegionId", on);
            Assert.DoesNotContain("Item1", on);
            Assert.DoesNotContain("Item2", on);

            string sql = ToSql(from).Sql;
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(sql, "JOIN").Count);
        }

        [Fact]
        public void QuerySource_TupleJoin_CanContinueWithWhereAndSelect()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<object>>>)(
                    q => Expr.From<JoinOrder>()
                          .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                          .Where(x => x.Item1.Amount > 0 && x.Item2.Name != null)
                          .Select(x => new { x.Item1.Id, x.Item2.Name })));

            string sql = ToSql(converter.ToSqlSegment()).Sql;

            Assert.Contains("LEFT JOIN", sql);
            Assert.Contains("WHERE", sql);
            Assert.DoesNotContain("Item1", sql);
            Assert.DoesNotContain("Item2", sql);
        }

        [Fact]
        public void Convert_MultiTableJoin_ProducesFlatTupleAndSplitsConditions()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinCustomer, JoinRegion)>>>)(
                    q => Expr.From<JoinOrder>()
                          .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                          .Join<JoinRegion>((o, c, r) => c.Id == r.Id)));

            var from = Assert.IsType<FromExpr>(converter.ToSqlSegment());

            Assert.Equal(2, from.Joins.Count);
            Assert.Equal(typeof(JoinCustomer), ((TableExpr)from.Joins[0].Source!).Type);
            Assert.Equal(typeof(JoinRegion), ((TableExpr)from.Joins[1].Source!).Type);
            Assert.All(from.Joins, join => Assert.NotNull(join.On));
            Assert.All(from.Joins, join => Assert.NotEqual(TableJoinType.Cross, join.JoinType));

            // 两个 AND 分支分别落到 Customer 和 Region 的 JOIN 段
            string customerAlias = ((TableExpr)from.Joins[0].Source!).Alias!;
            string regionAlias = ((TableExpr)from.Joins[1].Source!).Alias!;
            Assert.Contains(customerAlias, from.Joins[0].On!.ToString());
            Assert.DoesNotContain(regionAlias, from.Joins[0].On!.ToString());
            Assert.Contains(regionAlias, from.Joins[1].On!.ToString());

            string sql = ToSql(from).Sql;
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(sql, "JOIN").Count);
            Assert.DoesNotContain("CROSS JOIN", sql);
        }

        [Fact]
        public void Convert_MultiTableJoin_WhereAndSelectOnItem3_ResolvesThirdTable()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<object>>>)(
                    q => Expr.From<JoinOrder>()
                          .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                          .Join<JoinRegion>((o, c, r) => c.Id == r.Id)
                          .Where(x => x.Item3.Name == "North")
                          .Select(x => new { CustomerName = x.Item2.Name, RegionName = x.Item3.Name })));

            var select = Assert.IsType<SelectExpr>(converter.ToSqlSegment());
            var where = Assert.IsType<WhereExpr>(select.Source);
            var from = Assert.IsType<FromExpr>(where.Source);
            string regionAlias = ((TableExpr)from.Joins[1].Source!).Alias!;

            var condition = Assert.IsType<LogicBinaryExpr>(where.Where);
            Assert.Equal(regionAlias, Assert.IsType<PropertyExpr>(condition.Left).TableAlias);

            Assert.Equal(2, select.Selects.Count);
            Assert.Equal("CustomerName", select.Selects[0].Alias);
            Assert.Equal("RegionName", select.Selects[1].Alias);
            Assert.Equal(regionAlias, Assert.IsType<PropertyExpr>(select.Selects[1].Value).TableAlias);
        }

        [Fact]
        public void Convert_MultiTableJoinRelated_InfersOnForEachRightTable()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinCustomer, JoinRegion)>>>)(
                    q => Expr.From<JoinOrder>().JoinRelated<JoinCustomer>().JoinRelated<JoinRegion>()));

            var from = Assert.IsType<FromExpr>(converter.ToSqlSegment());

            Assert.Equal(2, from.Joins.Count);
            Assert.All(from.Joins, join => Assert.NotNull(join.On));
            Assert.Contains("CustomerId", from.Joins[0].On!.ToString());
            Assert.Contains("RegionId", from.Joins[1].On!.ToString());
        }

        [Fact]
        public void Convert_MultiTableJoinRelatedWithLambda_MergesEachAutoCondition()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinCustomer, JoinRegion)>>>)(
                    q => Expr.From<JoinOrder>()
                          .JoinRelated<JoinCustomer>((o, c) => o.Amount > 10)
                          .JoinRelated<JoinRegion>((o, c, r) => r.Name != null)));

            var from = Assert.IsType<FromExpr>(converter.ToSqlSegment());

            var customerOn = Assert.IsType<AndExpr>(from.Joins[0].On);
            Assert.Equal(2, customerOn.Count);
            Assert.Contains("CustomerId", customerOn.ToString());
            Assert.Contains("Amount", customerOn.ToString());

            var regionOn = Assert.IsType<AndExpr>(from.Joins[1].On);
            Assert.Equal(2, regionOn.Count);
            Assert.Contains("RegionId", regionOn.ToString());
            Assert.Contains("Name", regionOn.ToString());
        }

        [Fact]
        public void Convert_ChainedJoinRelated_MatchesFromLastJoinedTableFirst()
        {
            // 主表 JoinChainOrders 只直接关联 JoinChainUsers；JoinChainUsers 再关联 JoinChainDepts。
            // 第二个 JoinRelated 必须优先用最近加入的 JoinChainUsers 推断，而不是回退到主表。
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinChainOrder>, IEnumerable<(JoinChainOrder, JoinChainUser, JoinChainDept)>>>)(
                    q => Expr.From<JoinChainOrder>()
                          .JoinRelated<JoinChainUser>()
                          .JoinRelated<JoinChainDept>()));

            var from = Assert.IsType<FromExpr>(converter.ToSqlSegment());

            Assert.Equal(2, from.Joins.Count);
            string userAlias = ((TableExpr)from.Joins[0].Source!).Alias!;
            string deptAlias = ((TableExpr)from.Joins[1].Source!).Alias!;
            string deptOn = from.Joins[1].On!.ToString()!;

            // 第二段 ON 必须落在 User -> Dept 的外键上，而不是主表
            Assert.Contains(userAlias, deptOn);
            Assert.Contains(deptAlias, deptOn);
            Assert.Contains("DeptId", deptOn);

            string sql = ToSql(from).Sql;
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(sql, "JOIN").Count);
            Assert.Contains("JoinChainDepts", sql);
        }

        [Fact]
        public void QuerySource_ChainedJoinRelated_OutsideLambda_MatchesFromLastJoinedTableFirst()
        {
            // 同样的多级推断在 Lambda 外的 QuerySource 实例链路上也必须生效
            var from = Assert.IsType<FromExpr>(
                Expr.From<JoinChainOrder>()
                    .JoinRelated<JoinChainUser>()
                    .JoinRelated<JoinChainDept>()
                    .GetQueryExpr());

            Assert.Equal(2, from.Joins.Count);
            string userAlias = ((TableExpr)from.Joins[0].Source!).Alias!;
            string deptAlias = ((TableExpr)from.Joins[1].Source!).Alias!;
            string deptOn = from.Joins[1].On!.ToString()!;

            Assert.Contains(userAlias, deptOn);
            Assert.Contains(deptAlias, deptOn);
            Assert.Contains("DeptId", deptOn);
        }

        [Fact]
        public void Convert_SevenTableJoin_SupportsItem7InWhereAndSelect()
        {
            var converter = new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<object>>>)(
                    q => Expr.From<JoinOrder>()
                          .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                          .Join<JoinRegion>((o, c, r) => c.Id == r.Id)
                          .Join<JoinZone>((o, c, r, z) => r.Id == z.RegionId)
                          .Join<JoinStore>((o, c, r, z, s) => z.Id == s.RegionId)
                          .Join<JoinCity>((o, c, r, z, s, ci) => s.Id == ci.StoreId)
                          .Join<JoinCountry>((o, c, r, z, s, ci, co) => ci.Id == co.CityId)
                          .Where(x => x.Item7.Id > 0)
                          .Select(x => new { x.Item7.Id })));

            var select = Assert.IsType<SelectExpr>(converter.ToSqlSegment());
            var where = Assert.IsType<WhereExpr>(select.Source);
            var from = Assert.IsType<FromExpr>(where.Source);
            string countryAlias = ((TableExpr)from.Joins[5].Source!).Alias!;

            Assert.Equal(6, from.Joins.Count);
            Assert.All(from.Joins, join => Assert.NotNull(join.On));
            Assert.Equal(countryAlias, Assert.IsType<PropertyExpr>(Assert.IsType<LogicBinaryExpr>(where.Where).Left).TableAlias);
            Assert.Equal(countryAlias, Assert.IsType<PropertyExpr>(select.Selects[0].Value).TableAlias);

            string sql = ToSql(select).Sql;
            Assert.Equal(6, System.Text.RegularExpressions.Regex.Matches(sql, "JOIN").Count);
            Assert.Contains("JoinCountries", sql);
            Assert.DoesNotContain("Item7", sql);
        }

        #endregion

        #region 实例方法与连接类型

        [Fact]
        public void Convert_ExplicitJoinType_RendersRequestedJoin()
        {
            AssertJoinType(
                q => Expr.From<JoinOrder>().Join<JoinCustomer>((o, c) => o.CustomerId == c.Id, TableJoinType.Inner),
                TableJoinType.Inner);

            AssertJoinType(
                q => Expr.From<JoinOrder>().Join<JoinCustomer>((o, c) => o.CustomerId == c.Id, TableJoinType.Right),
                TableJoinType.Right);

            AssertJoinType(
                q => Expr.From<JoinOrder>().Join<JoinCustomer>((o, c) => o.CustomerId == c.Id, TableJoinType.Full),
                TableJoinType.Full);
        }

        [Fact]
        public void Convert_JoinRelatedWithExplicitJoinType_OverridesMetadata()
        {
            var from = Assert.IsType<FromExpr>(new LambdaExprConverter(
                (Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinCustomer)>>>)(
                    q => Expr.From<JoinOrder>().JoinRelated<JoinCustomer>(TableJoinType.Right)))
                .ToSqlSegment());

            var join = Assert.Single(from.Joins);
            Assert.Equal(TableJoinType.Right, join.JoinType);
            Assert.NotNull(join.On);
        }

        [Fact]
        public void InstanceJoin_BuildsJoinExpressionAtRuntime()
        {
            // Lambda 外直接调用实例方法拼装连接，GetQueryExpr 得到真实 FromExpr
            QuerySource<JoinOrder, JoinCustomer> source = Expr.From<JoinOrder>()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id, TableJoinType.Inner);

            var from = Assert.IsType<FromExpr>(source.GetQueryExpr());
            var join = Assert.Single(from.Joins);

            Assert.Equal(TableJoinType.Inner, join.JoinType);
            Assert.Equal(typeof(JoinCustomer), ((TableExpr)join.Source!).Type);
            Assert.NotNull(join.On);
        }

        [Fact]
        public void InstanceJoin_ChainsAfterWhereAtRuntime()
        {
            // Where 之后静态类型退回 IQueryable<T>，先 AsSource 收窄再继续连接
            var source = Expr.From<JoinOrder>()
                .Where(o => o.Amount > 0)
                .AsSource()
                .Join<JoinCustomer>((o, c) => o.CustomerId == c.Id)
                .Join<JoinRegion>((o, c, r) => o.RegionId == r.Id);

            var where = Assert.IsType<WhereExpr>(source.GetQueryExpr());
            var from = Assert.IsType<FromExpr>(where.Source);

            Assert.Equal(2, from.Joins.Count);
            Assert.Equal(typeof(JoinCustomer), ((TableExpr)from.Joins[0].Source!).Type);
            Assert.Equal(typeof(JoinRegion), ((TableExpr)from.Joins[1].Source!).Type);
            Assert.Contains("Amount", where.Where!.ToString());
            Assert.DoesNotContain("Item1", from.Joins[1].On!.ToString());
        }

        private static void AssertJoinType(
            Expression<Func<IQueryable<JoinOrder>, IEnumerable<(JoinOrder, JoinCustomer)>>> query,
            TableJoinType expected)
        {
            var from = Assert.IsType<FromExpr>(new LambdaExprConverter(query).ToSqlSegment());
            var join = Assert.Single(from.Joins);

            Assert.Equal(expected, join.JoinType);
            Assert.Contains(expected.ToString().ToUpperInvariant() + " JOIN", ToSql(from).Sql);
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
            [ForeignType(typeof(JoinRegion), Alias = "Region")]
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

        [Table("JoinZones")]
        private class JoinZone
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("RegionId")]
            public int RegionId { get; set; }
        }

        [Table("JoinUnrelated")]
        private class JoinUnrelated
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }
        }

        [Table("JoinStores")]
        private class JoinStore
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("RegionId")]
            public int RegionId { get; set; }
        }

        [Table("JoinCities")]
        private class JoinCity
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("StoreId")]
            public int StoreId { get; set; }
        }

        [Table("JoinCountries")]
        private class JoinCountry
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("CityId")]
            public int CityId { get; set; }
        }

        [Table("JoinChainOrders")]
        private class JoinChainOrder
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("UserId")]
            [ForeignType(typeof(JoinChainUser), Alias = "User", AutoExpand = true)]
            public int UserId { get; set; }
        }

        [Table("JoinChainUsers")]
        private class JoinChainUser
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("DeptId")]
            [ForeignType(typeof(JoinChainDept), Alias = "Dept", AutoExpand = true)]
            public int DeptId { get; set; }
        }

        [Table("JoinChainDepts")]
        private class JoinChainDept
        {
            [Column("Id", IsPrimaryKey = true)]
            public int Id { get; set; }

            [Column("Name")]
            public string? Name { get; set; }
        }

        #endregion
    }
}