using LiteOrm.Common;
using LiteOrm.Tests.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// Lambda 查询转换单元测试 - 纯内存测试，无需数据库连接
    /// 测试 LambdaExprConverter 将 LINQ 表达式转换为 SQL 表达式片段的功能
    /// </summary>
    [Collection("Database")]
    public class LambdaQueryTests
    {

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
            Assert.True(orderBy.OrderBys[0].Ascending);

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

            // Where 的源应该是 Select 表达式
            var select = Assert.IsType<SelectExpr>(where.Source);
            Assert.Equal(2, select.Selects.Count);
            Assert.Equal("Name", select.Selects[0].Alias);
            Assert.Equal("Age", select.Selects[1].Alias);

            // 条件应该是 Age > 18
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
            // 验证 OrderBy 中使用了别名
            var orderByValue = orderBy.OrderBys[0].Field;
            Assert.IsType<PropertyExpr>(orderByValue);
            Assert.Equal("Total", (orderByValue as PropertyExpr).PropertyName);

            var select = Assert.IsType<SelectExpr>(orderBy.Source);
            Assert.Equal(2, select.Selects.Count);
            Assert.Equal("DeptId", select.Selects[0].Alias);
            Assert.Equal("Total", select.Selects[1].Alias);

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
            // 测试：查询存在部门的用户
            // SELECT * FROM TestUsers u WHERE EXISTS (SELECT 1 FROM TestDepartments d WHERE d.Id = u.DeptId)
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q
                .Where(u => Expr.Exists<TestDepartment>(d => d.Id == u.DeptId));

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);

            Assert.IsType<WhereExpr>(expr);
            var where = (WhereExpr)expr;

            // 条件应该是 ForeignExpr（它是一个 LogicExpr）
            var condition = where.Where;
            Assert.IsType<ForeignExpr>(condition);

            var foreign = (ForeignExpr)condition;
            Assert.Equal(typeof(TestDepartment), foreign.Foreign);
            Assert.NotNull(foreign.InnerExpr);
        }

        [Fact]
        public void ExistsSubquery_WithOtherConditions()
        {
            // 测试：查询年龄大于 18 且有部门的用户
            // SELECT * FROM TestUsers u WHERE u.Age > 18 AND EXISTS (SELECT 1 FROM TestDepartments d WHERE d.Id = u.DeptId)
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q
                .Where(u => u.Age > 18 && Expr.Exists<TestDepartment>(d => d.Id == u.DeptId));

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);

            Assert.IsType<WhereExpr>(expr);
            var where = (WhereExpr)expr;

            // 条件应该是带 AND 的 LogicSet
            Assert.IsType<LogicSet>(where.Where);
            var logicSet = (LogicSet)where.Where;
            Assert.Equal(LogicJoinType.And, logicSet.JoinType);
            Assert.Equal(2, logicSet.Count);
        }

        [Fact]
        public void ExistsSubquery_ComplexInnerCondition()
        {
            // 测试：查询部门名称为 "IT" 的用户
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q
                .Where(u => Expr.Exists<TestDepartment>(d => d.Id == u.DeptId && d.Name == "IT"));

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);

            Assert.IsType<WhereExpr>(expr);
            var where = (WhereExpr)expr;

            var condition = where.Where;
            Assert.IsType<ForeignExpr>(condition);

            var foreign = (ForeignExpr)condition;
            Assert.Equal(typeof(TestDepartment), foreign.Foreign);

            // 内部表达式应该是 LogicSet（AND）
            Assert.IsType<LogicSet>(foreign.InnerExpr);
        }

        [Fact]
        public void ExistsSubquery_WithOrderAndSection()
        {
            // 测试：查询部门中的用户，带排序和分页
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
            // 测试：查询同时拥有 IT 和 HR 部门的用户
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

            // 两个条件都应该包含 ForeignExpr
            foreach (var item in logicSet.Items)
            {
                Assert.IsType<ForeignExpr>(item);
            }
        }

        [Fact]
        public void ExistsSubquery_Serialization()
        {
            // 测试：验证 Exists 表达式可以被序列化
            var foreignExpr = Expr.Exists<TestDepartment>(Expr.Prop("Id") == Expr.Prop("u", "DeptId"));
            Assert.NotNull(foreignExpr);

            // 使用 Expr.And 与其他逻辑表达式组合
            var condition = Expr.And(Expr.Prop("Age") > 18, foreignExpr);
            Assert.IsType<LogicSet>(condition);
        }

        [Fact]
        public void LambdaExpr_Equals_ManualExpr_Test()
        {
            // 测试：验证从 Lambda 生成的 Expr 在结构上等同于手动构造的 Expr
            // 这验证了两种不同的 Expr 构造方式能够产生功能相同的结果

            // 1. 从 Lambda 生成 Expr
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> lambdaExpr = q => q
                .Where(u => u.Age > 18 && u.Name.Contains("Test"));
            var lambdaGeneratedExpr = LambdaExprConverter.ToSqlSegment(lambdaExpr);

            // 2. 验证从 Lambda 生成的表达式的结构
            Assert.IsType<WhereExpr>(lambdaGeneratedExpr);
            var lambdaWhere = (WhereExpr)lambdaGeneratedExpr;
            Assert.IsType<FromExpr>(lambdaWhere.Source);
            Assert.IsType<LogicSet>(lambdaWhere.Where);

            // 3. 手动构造等效的 Expr
            var manualExpr = new WhereExpr
            {
                Source = new FromExpr(typeof(TestUser)).As(Constants.DefaultTableAlias),
                Where = Expr.And(
                    Expr.Prop("Age") > 18,
                    Expr.Prop("Name").Contains("Test")
                )
            };

            // 4. 验证手动构造的表达式的结构
            Assert.IsType<WhereExpr>(manualExpr);
            var manualWhere = (WhereExpr)manualExpr;
            Assert.IsType<FromExpr>(manualWhere.Source);
            Assert.IsType<LogicSet>(manualWhere.Where);

            // 5. 比较两个表达式是否相等
            Assert.True(lambdaGeneratedExpr.Equals(manualExpr),
                "Lambda-generated Expr should equal manually constructed Expr");

            // 6. 测试复杂的情况
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> lambdaExpr2 = q => q
                .Where(u => u.Age > 18 && u.Name.Contains("Test"))
                .OrderBy(u => u.Name)
                .Skip(10)
                .Take(5);
            var lambdaGeneratedExpr2 = LambdaExprConverter.ToSqlSegment(lambdaExpr2);

            // 7. 验证复杂的 Lambda 生成表达式的结构
            Assert.IsType<SectionExpr>(lambdaGeneratedExpr2);
            var lambdaSection = (SectionExpr)lambdaGeneratedExpr2;
            Assert.Equal(10, lambdaSection.Skip);
            Assert.Equal(5, lambdaSection.Take);

            Assert.IsType<OrderByExpr>(lambdaSection.Source);
            var lambdaOrderBy = (OrderByExpr)lambdaSection.Source;
            Assert.Single(lambdaOrderBy.OrderBys);

            Assert.IsType<WhereExpr>(lambdaOrderBy.Source);
            var lambdaWhere2 = (WhereExpr)lambdaOrderBy.Source;
            Assert.IsType<FromExpr>(lambdaWhere2.Source);
            Assert.IsType<LogicSet>(lambdaWhere2.Where);

            // 8. 为复杂情况手动构造等效的 Expr
            var manualExpr2 = new SectionExpr
            {
                Source = new OrderByExpr
                {
                    Source = new WhereExpr
                    {
                        Source = new FromExpr(typeof(TestUser)).As(Constants.DefaultTableAlias),
                        Where = Expr.And(
                            Expr.Prop("Age") > 18,
                            Expr.Prop("Name").Contains("Test")
                        )
                    },
                    OrderBys = new List<OrderByItemExpr> { new OrderByItemExpr(Expr.Prop("Name"), true) }
                },
                Skip = 10,
                Take = 5
            };

            // 9. 验证手动构造的复杂表达式的结构
            Assert.IsType<SectionExpr>(manualExpr2);
            var manualSection = (SectionExpr)manualExpr2;
            Assert.Equal(10, manualSection.Skip);
            Assert.Equal(5, manualSection.Take);

            // 10. 比较两个复杂表达式是否相等
            Assert.True(lambdaGeneratedExpr2.Equals(manualExpr2),
                "Complex lambda-generated Expr should equal manually constructed Expr");

            Assert.IsType<OrderByExpr>(manualSection.Source);
            var manualOrderBy = (OrderByExpr)manualSection.Source;
            Assert.Single(manualOrderBy.OrderBys);

            Assert.IsType<WhereExpr>(manualOrderBy.Source);
            var manualWhere2 = (WhereExpr)manualOrderBy.Source;
            Assert.IsType<FromExpr>(manualWhere2.Source);
            Assert.IsType<LogicSet>(manualWhere2.Where);
        }

        #region TimeSpan

        [Fact]
        public void TimeSpan_TotalDays_InWhere_YieldsDateDiffDaysInCondition()
        {
            var baseDate = new DateTime(2024, 1, 1);
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q
                .Where(u => (u.CreateTime - baseDate).TotalDays > 30);

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);
            var where = Assert.IsType<WhereExpr>(expr);
            var condition = Assert.IsType<LogicBinaryExpr>(where.Where);
            Assert.Equal(LogicOperator.GreaterThan, condition.Operator);
            var func = Assert.IsType<FunctionExpr>(condition.Left);
            Assert.Equal("DateDiffDays", func.FunctionName);
            Assert.Equal(2, func.Args.Count);
            Assert.Equal("CreateTime", Assert.IsType<PropertyExpr>(func.Args[0]).PropertyName);
        }

        [Fact]
        public void TimeSpan_TotalHours_InWhere_YieldsDateDiffHoursInCondition()
        {
            var baseDate = new DateTime(2024, 1, 1);
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q
                .Where(u => (u.CreateTime - baseDate).TotalHours > 720);

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);
            var where = Assert.IsType<WhereExpr>(expr);
            var condition = Assert.IsType<LogicBinaryExpr>(where.Where);
            var func = Assert.IsType<FunctionExpr>(condition.Left);
            Assert.Equal("DateDiffHours", func.FunctionName);
            Assert.Equal(2, func.Args.Count);
        }

        [Fact]
        public void TimeSpan_TotalMinutes_InSelect_YieldsDateDiffMinutesFunctionExpr()
        {
            var baseDate = new DateTime(2024, 1, 1);
            Expression<Func<IQueryable<TestUser>, IQueryable<object>>> queryExpr = q => q
                .Select(u => new { Minutes = (u.CreateTime - baseDate).TotalMinutes });

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);
            var select = Assert.IsType<SelectExpr>(expr);
            Assert.Single(select.Selects);
            Assert.Equal("Minutes", select.Selects[0].Alias);
            var func = Assert.IsType<FunctionExpr>(select.Selects[0].Value);
            Assert.Equal("DateDiffMinutes", func.FunctionName);
            Assert.Equal(2, func.Args.Count);
            Assert.Equal("CreateTime", Assert.IsType<PropertyExpr>(func.Args[0]).PropertyName);
        }

        [Fact]
        public void TimeSpan_TotalMilliseconds_InOrderBy_YieldsDateDiffMilliseconds()
        {
            var baseDate = new DateTime(2024, 1, 1);
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> queryExpr = q => q
                .OrderBy(u => (u.CreateTime - baseDate).TotalMilliseconds);

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);
            var orderBy = Assert.IsType<OrderByExpr>(expr);
            Assert.Single(orderBy.OrderBys);
            var func = Assert.IsType<FunctionExpr>(orderBy.OrderBys[0].Field);
            Assert.Equal("DateDiffMilliseconds", func.FunctionName);
        }

        [Fact]
        public void TimeSpan_TotalDays_WhereAndSelect_BothYieldDateDiffDays()
        {
            var baseDate = new DateTime(2024, 1, 1);
            Expression<Func<IQueryable<TestUser>, IQueryable<object>>> queryExpr = q => q
                .Where(u => (u.CreateTime - baseDate).TotalDays > 0)
                .Select(u => new { u.Name, DaysSince = (u.CreateTime - baseDate).TotalDays });

            var expr = LambdaExprConverter.ToSqlSegment(queryExpr);
            var select = Assert.IsType<SelectExpr>(expr);
            Assert.Equal(2, select.Selects.Count);
            var dayFunc = Assert.IsType<FunctionExpr>(select.Selects[1].Value);
            Assert.Equal("DateDiffDays", dayFunc.FunctionName);

            var where = Assert.IsType<WhereExpr>(select.Source);
            var condition = Assert.IsType<LogicBinaryExpr>(where.Where);
            var whereFunc = Assert.IsType<FunctionExpr>(condition.Left);
            Assert.Equal("DateDiffDays", whereFunc.FunctionName);
        }

        #endregion
    }
}

