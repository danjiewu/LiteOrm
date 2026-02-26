using LiteOrm.Common;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using System;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace LiteOrm.Tests
{
    [Collection("Database")]
    public class LambdaQueryTests : TestBase
    {
        public LambdaQueryTests(DatabaseFixture fixture) : base(fixture) { }

        [Fact]
        public void BasicQuery_Test()
        {
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q.Where(u => u.Age > 18);
            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);

            Assert.IsType<WhereExpr>(expr);
            var where = (WhereExpr)expr;
            Assert.IsType<FromExpr>(where.Source);
            Assert.IsType<LogicBinaryExpr>(where.Where);
        }

        [Fact]
        public void ChainQuery_Test()
        {
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q
                .Where(u => u.Age > 18)
                .OrderBy(u => u.Name)
                .Skip(10)
                .Take(20);

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);

            Assert.IsType<SectionExpr>(expr);
            var section = (SectionExpr)expr;
            Assert.Equal(10, section.Skip);
            Assert.Equal(20, section.Take);

            Assert.IsType<OrderByExpr>(section.Source);
            var orderBy = (OrderByExpr)section.Source;
            Assert.Single(orderBy.OrderBys);
            Assert.True(orderBy.OrderBys[0].Item2); // Ascending

            Assert.IsType<WhereExpr>(orderBy.Source);
        }

        [Fact]
        public void GroupByHavingSelect_Test()
        {
            Expression<Func<IQueryable<TestUser>, IQueryable<int>>> queryExpr = q => q
                .GroupBy(u => u.DeptId)
                .Select(g => g.Key);

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);
            Assert.IsType<SelectExpr>(expr);
            var select = (SelectExpr)expr;
            Assert.IsType<GroupByExpr>(select.Source);
        }

        [Fact]
        public void AnonymousSelect_Test()
        {
            Expression<Func<IQueryable<TestUser>, IQueryable<object>>> queryExpr = q => q
                .Select(u => new { u.Name, u.Age });

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);

            Assert.IsType<SelectExpr>(expr);
            var select = (SelectExpr)expr;
            Assert.Equal(2, select.Selects.Count);
            Assert.Equal("Name", (select.Selects[0].Value as PropertyExpr).PropertyName);
            Assert.Equal("Age", (select.Selects[1].Value as PropertyExpr).PropertyName);
        }

        [Fact]
        public void NestedSelect_Test()
        {
            // q.Select(...).Where(...) effectively creates a subquery structure: 
            // SELECT * FROM (SELECT Name, Age FROM TestUsers) WHERE Age > 18
            Expression<Func<IQueryable<TestUser>, IQueryable<object>>> queryExpr = q => q
                .Select(u => new { u.Name, u.Age })
                .Where(x => x.Age > 18);

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);

            Assert.IsType<WhereExpr>(expr);
            var where = (WhereExpr)expr;

            // The source of the Where should be the Select expression
            var select = Assert.IsType<SelectExpr>(where.Source);
            Assert.Equal(2, select.Selects.Count);
            Assert.Equal("Name", select.Selects[0].Name);
            Assert.Equal("Age", select.Selects[1].Name);

            // The condition should be Age > 18
            var condition = Assert.IsType<LogicBinaryExpr>(where.Where);
            Assert.Equal("Age", (condition.Left as PropertyExpr)?.PropertyName);
            Assert.Equal(18, (condition.Right as ValueExpr)?.Value);
        }

        [Fact]
        public void DoubleWhere_Test()
        {
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q
                .Where(u => u.Age > 18)
                .Where(u => u.Name.Contains("A"));

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);

            // 多个 Where 应该合并为一个 WhereExpr
            Assert.IsType<WhereExpr>(expr);
            var where = (WhereExpr)expr;
            
            // 条件应该合并为 LogicSet（AND 连接）
            Assert.IsType<LogicSet>(where.Where);
            var logicSet = (LogicSet)where.Where;
            Assert.Equal(LogicJoinType.And, logicSet.JoinType);
            Assert.Equal(2, logicSet.Count);
        }

        [Fact]
        public void FullComplexQuery_Test()
        {
            Expression<Func<IQueryable<TestUser>, IQueryable<object>>> queryExpr = q => q
                .Where(u => u.Age > 20)
                .GroupBy(u => u.DeptId)
                .Where(g => g.Count() > 5)
                .Select(g => new { DeptId = g.Key, Total = g.Count() })
                .OrderBy(res => res.Total)
                .Skip(10)
                .Take(20);

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);

            Assert.IsType<SectionExpr>(expr);
            var section = (SectionExpr)expr;
            Assert.Equal(10, section.Skip);
            Assert.Equal(20, section.Take);

            var orderBy = Assert.IsType<OrderByExpr>(section.Source);
            Assert.Single(orderBy.OrderBys);
            // Verify alias name is used in OrderBy
            var orderByValue = orderBy.OrderBys[0].Item1;
            Assert.IsType<PropertyExpr>(orderByValue);
            Assert.Equal("Total", (orderByValue as PropertyExpr).PropertyName);

            var select = Assert.IsType<SelectExpr>(orderBy.Source);
            Assert.Equal(2, select.Selects.Count);
            Assert.Equal("DeptId", select.Selects[0].Name);
            Assert.Equal("Total", select.Selects[1].Name);

            var having = Assert.IsType<HavingExpr>(select.Source);
            Assert.IsType<LogicBinaryExpr>(having.Having);

            var groupBy = Assert.IsType<GroupByExpr>(having.Source);
            Assert.Single(groupBy.GroupBys);

            var where = Assert.IsType<WhereExpr>(groupBy.Source);
            Assert.IsType<FromExpr>(where.Source);
        }
    }
}
