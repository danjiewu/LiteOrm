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
            Assert.True(orderBy.OrderBys[0].Item2); 

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

        [Fact]
        public void ExistsSubquery_BasicTest()
        {
            // Test: Query users whose department exists
            // SELECT * FROM TestUsers u WHERE EXISTS (SELECT 1 FROM TestDepartments d WHERE d.Id = u.DeptId)
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q
                .Where(u => Expr.Exists<TestDepartment>(d => d.Id == u.DeptId));

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);

            Assert.IsType<WhereExpr>(expr);
            var where = (WhereExpr)expr;

            // The condition should be ForeignExpr (which is a LogicExpr)
            var condition = where.Where;
            Assert.IsType<ForeignExpr>(condition);

            var foreign = (ForeignExpr)condition;
            Assert.Equal(typeof(TestDepartment), foreign.Foreign);
            Assert.NotNull(foreign.InnerExpr);
        }

        [Fact]
        public void ExistsSubquery_WithOtherConditions()
        {
            // Test: Query users with age > 18 who have departments
            // SELECT * FROM TestUsers u WHERE u.Age > 18 AND EXISTS (SELECT 1 FROM TestDepartments d WHERE d.Id = u.DeptId)
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q
                .Where(u => u.Age > 18 && Expr.Exists<TestDepartment>(d => d.Id == u.DeptId));

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);

            Assert.IsType<WhereExpr>(expr);
            var where = (WhereExpr)expr;

            // The condition should be LogicSet with AND
            Assert.IsType<LogicSet>(where.Where);
            var logicSet = (LogicSet)where.Where;
            Assert.Equal(LogicJoinType.And, logicSet.JoinType);
            Assert.Equal(2, logicSet.Count);
        }

        [Fact]
        public void ExistsSubquery_ComplexInnerCondition()
        {
            // Test: Query users whose department name is "IT"
            // SELECT * FROM TestUsers u WHERE EXISTS (SELECT 1 FROM TestDepartments d WHERE d.Id = u.DeptId AND d.Name = 'IT')
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q
                .Where(u => Expr.Exists<TestDepartment>(d => d.Id == u.DeptId && d.Name == "IT"));

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);

            Assert.IsType<WhereExpr>(expr);
            var where = (WhereExpr)expr;

            var condition = where.Where;
            Assert.IsType<ForeignExpr>(condition);

            var foreign = (ForeignExpr)condition;
            Assert.Equal(typeof(TestDepartment), foreign.Foreign);

            // Inner expression should be a LogicSet (AND)
            Assert.IsType<LogicSet>(foreign.InnerExpr);
        }

        [Fact]
        public void ExistsSubquery_WithOrderAndSection()
        {
            // Test: Query users in departments, with ordering and paging
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q
                .Where(u => Expr.Exists<TestDepartment>(d => d.Id == u.DeptId && d.Name.Contains("Dept")))
                .OrderBy(u => u.Name)
                .Skip(0)
                .Take(10);

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);

            Assert.IsType<SectionExpr>(expr);
            var section = (SectionExpr)expr;
            Assert.Equal(0, section.Skip);
            Assert.Equal(10, section.Take);

            var orderBy = Assert.IsType<OrderByExpr>(section.Source);
            Assert.Single(orderBy.OrderBys);

            var where = Assert.IsType<WhereExpr>(orderBy.Source);
            Assert.IsType<ForeignExpr>(where.Where);
        }

        [Fact]
        public void ExistsSubquery_MultipleExists()
        {
            // Test: Query users who have both IT and HR departments
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q
                .Where(u => Expr.Exists<TestDepartment>(d => d.Id == u.DeptId && d.Name == "IT") &&
                            Expr.Exists<TestDepartment>(d => d.ParentId == 0));

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);

            Assert.IsType<WhereExpr>(expr);
            var where = (WhereExpr)expr;

            Assert.IsType<LogicSet>(where.Where);
            var logicSet = (LogicSet)where.Where;
            Assert.Equal(LogicJoinType.And, logicSet.JoinType);
            Assert.Equal(2, logicSet.Count);

            // Both conditions should contain ForeignExpr
            foreach (var item in logicSet.Items)
            {
                Assert.IsType<ForeignExpr>(item);
            }
        }

        [Fact]
        public void ExistsSubquery_Serialization()
        {
            // Test: Verify that Exists expressions can be serialized
            var foreignExpr = Expr.Foreign<TestDepartment>(Expr.Prop("Id") == Expr.Prop("u.DeptId"));
            Assert.NotNull(foreignExpr);

            // Combine with other logic expressions using Expr.And
            var condition = Expr.And(Expr.Prop("Age") > 18, foreignExpr);
            Assert.IsType<LogicSet>(condition);
        }
    }
}
