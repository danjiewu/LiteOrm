using LiteOrm.Common;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using System;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace LiteOrm.Tests
{
    public class LambdaQueryTests : TestBase
    {
        [Fact]
        public void BasicQuery_Test()
        {
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q.Where(u => u.Age > 18);
            var expr = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);

            Assert.IsType<WhereExpr>(expr);
            var where = (WhereExpr)expr;
            Assert.IsType<TableExpr>(where.Source);
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

            var expr = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);

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
            // Note: We need a custom 'Having' extension method for IQueryable if we want to support it in Lambda
            // Or just test GroupBy and Select for now.
            Expression<Func<IQueryable<TestUser>, IQueryable<int>>> queryExpr = q => q
                .GroupBy(u => u.DeptId)
                .Select(g => g.Key);

            var expr = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);
            Assert.IsType<SelectExpr>(expr);
            var select = (SelectExpr)expr;
            Assert.IsType<GroupByExpr>(select.Source);
        }

        [Fact]
        public void AnonymousSelect_Test()
        {
            Expression<Func<IQueryable<TestUser>, IQueryable<object>>> queryExpr = q => q
                .Select(u => new { u.Name, u.Age });

            var expr = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);

            Assert.IsType<SelectExpr>(expr);
            var select = (SelectExpr)expr;
            Assert.Equal(2, select.Selects.Count);
            Assert.Equal("Name", (select.Selects[0] as PropertyExpr).PropertyName);
            Assert.Equal("Age", (select.Selects[1] as PropertyExpr).PropertyName);
        }

        [Fact]
        public void DoubleWhere_Test()
        {
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q
                .Where(u => u.Age > 18)
                .Where(u => u.Name.Contains("A"));

            var expr = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);

            Assert.IsType<WhereExpr>(expr);
            var where2 = (WhereExpr)expr;
            Assert.IsType<WhereExpr>(where2.Source);
        }
    }
}
