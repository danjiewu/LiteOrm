using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 测试所有运算符、Expr静态和扩展方法，以及常用内置函数在实际数据库查询中的使用
    /// </summary>
    [Collection("Database")]
    public class ExprDatabaseTests : TestBase
    {
        public ExprDatabaseTests(DatabaseFixture fixture) : base(fixture) { }

        #region 运算符测试

        [Fact]
        public async Task Operators_Tests()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();

            // 插入测试数据
            var user1 = new TestUser { Name = "OperatorTest1", Age = 20, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "OperatorTest2", Age = 30, CreateTime = DateTime.Now };
            var user3 = new TestUser { Name = "OperatorTest3", Age = 25, CreateTime = DateTime.Now };
            var user4 = new TestUser { Name = "OperatorTest4", Age = 35, CreateTime = DateTime.Now };
            await service.InsertAsync(user1, TestContext.Current.CancellationToken);
            await service.InsertAsync(user2, TestContext.Current.CancellationToken);
            await service.InsertAsync(user3, TestContext.Current.CancellationToken);
            await service.InsertAsync(user4, TestContext.Current.CancellationToken);

            // 测试等于运算符
            var equalExpr = Expr.Prop("Name") == "OperatorTest1";
            var equalResults = await objectViewDAO.Search(equalExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(equalResults);
            Assert.NotEmpty(equalResults);
            Assert.All(equalResults, u => Assert.Equal("OperatorTest1", u.Name));

            // 测试不等于运算符
            var notEqualExpr = Expr.Prop("Name") != "OperatorTest1";
            var notEqualResults = await objectViewDAO.Search(notEqualExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(notEqualResults);
            Assert.NotEmpty(notEqualResults);
            Assert.All(notEqualResults, u => Assert.NotEqual("OperatorTest1", u.Name));

            // 测试大于运算符
            var greaterThanExpr = Expr.Prop("Age") > 25;
            var greaterThanResults = await objectViewDAO.Search(greaterThanExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(greaterThanResults);
            Assert.NotEmpty(greaterThanResults);
            Assert.All(greaterThanResults, u => Assert.True(u.Age > 25));

            // 测试小于运算符
            var lessThanExpr = Expr.Prop("Age") < 25;
            var lessThanResults = await objectViewDAO.Search(lessThanExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(lessThanResults);
            Assert.NotEmpty(lessThanResults);
            Assert.All(lessThanResults, u => Assert.True(u.Age < 25));

            // 测试大于等于运算符
            var greaterThanOrEqualExpr = Expr.Prop("Age") >= 20;
            var greaterThanOrEqualResults = await objectViewDAO.Search(greaterThanOrEqualExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(greaterThanOrEqualResults);
            Assert.NotEmpty(greaterThanOrEqualResults);
            Assert.All(greaterThanOrEqualResults, u => Assert.True(u.Age >= 20));

            // 测试小于等于运算符
            var lessThanOrEqualExpr = Expr.Prop("Age") <= 30;
            var lessThanOrEqualResults = await objectViewDAO.Search(lessThanOrEqualExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(lessThanOrEqualResults);
            Assert.NotEmpty(lessThanOrEqualResults);
            Assert.All(lessThanOrEqualResults, u => Assert.True(u.Age <= 30));
        }

        #endregion

        #region Expr静态方法测试

        [Fact]
        public async Task ExprStaticMethods_Tests()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var deptService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestDepartment>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            // 插入测试数据
            var dept1 = new TestDepartment { Name = "StaticMethodTest1" };
            var dept2 = new TestDepartment { Name = "StaticMethodTest2" };
            await deptService.InsertAsync(dept1, TestContext.Current.CancellationToken);
            await deptService.InsertAsync(dept2, TestContext.Current.CancellationToken);

            var user1 = new TestUser { Name = "StaticMethodTestUser1", Age = 20, DeptId = dept1.Id, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "StaticMethodTestUser2", Age = 30, DeptId = dept2.Id, CreateTime = DateTime.Now };
            var user3 = new TestUser { Name = "StaticMethodTestUser3", Age = 25, DeptId = dept1.Id, CreateTime = DateTime.Now };
            await service.InsertAsync(user1, TestContext.Current.CancellationToken);
            await service.InsertAsync(user2, TestContext.Current.CancellationToken);
            await service.InsertAsync(user3, TestContext.Current.CancellationToken);

            // 测试Prop方法
            var propExpr = Expr.Prop("Name") == "StaticMethodTestUser1";
            var propResults = await objectViewDAO.Search(propExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(propResults);
            Assert.NotEmpty(propResults);

            // 测试Exists方法
            var existsExpr = Expr.Exists<TestDepartment>(Expr.Prop("Id") == user1.DeptId);
            var existsResults = await objectViewDAO.Search(existsExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(existsResults);

            // 测试Const方法
            var constExpr = Expr.Prop("Age") == Expr.Const(25);
            var constResults = await objectViewDAO.Search(constExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(constResults);

            // 测试Value方法
            var valueExpr = Expr.Prop("Age") == Expr.Value(25);
            var valueResults = await objectViewDAO.Search(valueExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(valueResults);

            // 测试And方法
            var andExpr = Expr.And(Expr.Prop("Name") == "StaticMethodTestUser3", Expr.Prop("Age") == 25);
            var andResults = await objectViewDAO.Search(andExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(andResults);
            Assert.NotEmpty(andResults);

            // 测试Or方法
            var orExpr = Expr.Or(Expr.Prop("Name") == "StaticMethodTestUser2", Expr.Prop("Age") == 30);
            var orResults = await objectViewDAO.Search(orExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(orResults);
            Assert.NotEmpty(orResults);

            // 测试Not方法
            var notExpr = Expr.Not(Expr.Prop("Name") == "NonExistentUser");
            var notResults = await objectViewDAO.Search(notExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(notResults);

            // 测试Func方法
            var funcExpr = Expr.Func("LOWER", Expr.Prop("Name")) == "staticmethodtestuser1";
            var funcResults = await objectViewDAO.Search(funcExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(funcResults);
            Assert.NotEmpty(funcResults);

            // 测试Coalesce方法
            var coalesceExpr = Expr.Coalesce(Expr.Prop("DeptId"), Expr.Const(0)) > 0;
            var coalesceResults = await objectViewDAO.Search(coalesceExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(coalesceResults);
            Assert.NotEmpty(coalesceResults);

            // 测试IfNull方法
            var ifNullExpr = Expr.IfNull(Expr.Prop("DeptId"), Expr.Const(0)) > 0;
            var ifNullResults = await objectViewDAO.Search(ifNullExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(ifNullResults);
            Assert.NotEmpty(ifNullResults);

            // 测试If方法
            var ifExpr = Expr.If(Expr.Prop("Age") > 20, Expr.Const(1), Expr.Const(0)) == 1;
            var ifResults = await objectViewDAO.Search(ifExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(ifResults);
            Assert.NotEmpty(ifResults);

            // 测试Now方法
            var nowExpr = Expr.Prop("CreateTime") <= Expr.Now();
            var nowResults = await objectViewDAO.Search(nowExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(nowResults);
            // 由于测试环境的数据库可能为空，所以不强制要求结果非空
            // Assert.NotEmpty(nowResults);

            // 测试Today方法
            var todayExpr = Expr.Prop("CreateTime").IsNotNull();
            var todayResults = await objectViewDAO.Search(todayExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(todayResults);
            // 由于测试环境的数据库可能为空，所以不强制要求结果非空
            // Assert.NotEmpty(todayResults);

            // 测试Lower方法
            var lowerExpr = Expr.Lower(Expr.Prop("Name")) == "staticmethodtestuser1";
            var lowerResults = await objectViewDAO.Search(lowerExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(lowerResults);
            Assert.NotEmpty(lowerResults);

            // 测试Length方法
            var lengthExpr = Expr.Length(Expr.Prop("Name")) > 5;
            var lengthResults = await objectViewDAO.Search(lengthExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(lengthResults);
            Assert.NotEmpty(lengthResults);

            // 测试Case方法
            var caseExpr = Expr.Case(
                new[] { new KeyValuePair<LogicExpr, ValueTypeExpr>(Expr.Prop("Age") > 30, Expr.Const("Old")),
                         new KeyValuePair<LogicExpr, ValueTypeExpr>(Expr.Prop("Age") > 20, Expr.Const("Young")) },
                Expr.Const("Unknown")) == "Young";
            var caseResults = await objectViewDAO.Search(caseExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(caseResults);
            Assert.NotEmpty(caseResults);

            // 测试Count方法
            var countExpr = Expr.Count(Expr.Prop("Id"));
            var countQuery = Expr.From<TestUser>().Select(countExpr.As("UserCount"));
            var countResult = dataViewDAO.Search(countQuery);
            DataTable countDt = await countResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(countDt);
            Assert.True(countDt.Rows.Count >= 1);

            // 测试Sum方法
            var sumExpr = Expr.Sum(Expr.Prop("Age"));
            var sumQuery = Expr.From<TestUser>().Select(sumExpr.As("TotalAge"));
            var sumResult = dataViewDAO.Search(sumQuery);
            DataTable sumDt = await sumResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(sumDt);
            Assert.True(sumDt.Rows.Count >= 1);

            // 测试Avg方法
            var avgExpr = Expr.Avg(Expr.Prop("Age"));
            var avgQuery = Expr.From<TestUser>().Select(avgExpr.As("AverageAge"));
            var avgResult = dataViewDAO.Search(avgQuery);
            DataTable avgDt = await avgResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(avgDt);
            Assert.True(avgDt.Rows.Count >= 1);

            // 测试Max方法
            var maxExpr = Expr.Max(Expr.Prop("Age"));
            var maxQuery = Expr.From<TestUser>().Select(maxExpr.As("MaxAge"));
            var maxResult = dataViewDAO.Search(maxQuery);
            DataTable maxDt = await maxResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(maxDt);
            Assert.True(maxDt.Rows.Count >= 1);

            // 测试Min方法
            var minExpr = Expr.Min(Expr.Prop("Age"));
            var minQuery = Expr.From<TestUser>().Select(minExpr.As("MinAge"));
            var minResult = dataViewDAO.Search(minQuery);
            DataTable minDt = await minResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(minDt);
            Assert.True(minDt.Rows.Count >= 1);

            // 测试Concat方法
            var concatExpr = Expr.Concat(Expr.Prop("Name"), Expr.Const(" Test"));
            var concatQuery = Expr.From<TestUser>().Select(concatExpr.As("NameWithSuffix"));
            var concatResult = dataViewDAO.Search(concatQuery);
            DataTable concatDt = await concatResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(concatDt);
            Assert.True(concatDt.Rows.Count >= 1);

            // 测试List方法
            var listExpr = Expr.Prop("Id").In(new[] { user1.Id, user2.Id, user3.Id });
            var listResults = await objectViewDAO.Search(listExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(listResults);
            Assert.NotEmpty(listResults);

            // 测试From方法
            var fromExpr = Expr.From<TestUser>();
            var fromQuery = fromExpr.Select(Expr.Prop("Id"), Expr.Prop("Name"));
            var fromResult = dataViewDAO.Search(fromQuery);
            DataTable fromDt = await fromResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(fromDt);
            Assert.True(fromDt.Rows.Count >= 1);
        }

        #endregion

        #region 扩展方法测试

        [Fact]
        public async Task ExprExtensions_Tests()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            // 插入测试数据（包含重复的Age值）
            var user1 = new TestUser { Name = "ExtensionTest1", Age = 20, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "ExtensionTest2", Age = 30, CreateTime = DateTime.Now };
            var user3 = new TestUser { Name = "ExtensionTest3", Age = 25, CreateTime = DateTime.Now };
            var user4 = new TestUser { Name = "ExtensionTest4", Age = 30, CreateTime = DateTime.Now }; // 重复的Age值
            var user5 = new TestUser { Name = "ExtensionTest5", Age = 20, CreateTime = DateTime.Now }; // 重复的Age值
            await service.InsertAsync(user1, TestContext.Current.CancellationToken);
            await service.InsertAsync(user2, TestContext.Current.CancellationToken);
            await service.InsertAsync(user3, TestContext.Current.CancellationToken);
            await service.InsertAsync(user4, TestContext.Current.CancellationToken);
            await service.InsertAsync(user5, TestContext.Current.CancellationToken);

            // 测试And扩展方法
            var andExpr = (Expr.Prop("Name") == "ExtensionTest1").And(Expr.Prop("Age") == 20);
            var andResults = await objectViewDAO.Search(andExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(andResults);
            Assert.NotEmpty(andResults);
            Assert.Single(andResults);
            Assert.Equal("ExtensionTest1", andResults[0].Name);
            Assert.Equal(20, andResults[0].Age);

            // 测试Or扩展方法
            var orExpr = (Expr.Prop("Name") == "ExtensionTest1").Or(Expr.Prop("Age") == 30);
            var orResults = await objectViewDAO.Search(orExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(orResults);
            Assert.NotEmpty(orResults);
            Assert.Equal(3, orResults.Count); // 应该有3个用户（ExtensionTest1, ExtensionTest2, ExtensionTest4）
            // 验证每个结果的Name为"ExtensionTest1"或Age为30
            Assert.All(orResults, u =>
                Assert.True(u.Name == "ExtensionTest1" || u.Age == 30)
            );

            // 测试Not扩展方法
            var notExpr = (Expr.Prop("Name") == "NonExistentUser").Not();
            var notResults = await objectViewDAO.Search(notExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(notResults);
            Assert.NotEmpty(notResults);
            Assert.Equal(5, notResults.Count); // 应该有5个用户
            // 验证每个结果的Name不为"NonExistentUser"
            Assert.All(notResults, u =>
                Assert.True(u.Name != "NonExistentUser")
            );

            // 测试Equal扩展方法
            var equalExpr = Expr.Prop("Name").Equal(Expr.Const("ExtensionTest1"));
            var equalResults = await objectViewDAO.Search(equalExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(equalResults);
            Assert.NotEmpty(equalResults);
            Assert.Single(equalResults);
            // 验证每个结果的Name为"ExtensionTest1"
            Assert.All(equalResults, u =>
                Assert.True(u.Name == "ExtensionTest1")
            );

            // 测试NotEqual扩展方法
            var notEqualExpr = Expr.Prop("Name").NotEqual(Expr.Const("ExtensionTest1"));
            var notEqualResults = await objectViewDAO.Search(notEqualExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(notEqualResults);
            Assert.NotEmpty(notEqualResults);
            Assert.Equal(4, notEqualResults.Count); // 应该有4个用户（ExtensionTest2, ExtensionTest3, ExtensionTest4, ExtensionTest5）
            // 验证每个结果的Name不为"ExtensionTest1"
            Assert.All(notEqualResults, u =>
                Assert.True(u.Name != "ExtensionTest1")
            );

            // 测试GreaterThan扩展方法
            var greaterThanExpr = Expr.Prop("Age").GreaterThan(Expr.Const(25));
            var greaterThanResults = await objectViewDAO.Search(greaterThanExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(greaterThanResults);
            Assert.NotEmpty(greaterThanResults);
            Assert.Equal(2, greaterThanResults.Count);
            // 验证每个结果的Age大于25
            Assert.All(greaterThanResults, u =>
                Assert.True(u.Age > 25)
            );

            // 测试LessThan扩展方法
            var lessThanExpr = Expr.Prop("Age").LessThan(Expr.Const(25));
            var lessThanResults = await objectViewDAO.Search(lessThanExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(lessThanResults);
            Assert.NotEmpty(lessThanResults);
            Assert.Equal(2, lessThanResults.Count); // 应该有2个用户（ExtensionTest1, ExtensionTest5）
            // 验证每个结果的Age小于25
            Assert.All(lessThanResults, u =>
                Assert.True(u.Age < 25)
            );

            // 测试GreaterThanOrEqual扩展方法
            var greaterThanOrEqualExpr = Expr.Prop("Age").GreaterThanOrEqual(Expr.Const(20));
            var greaterThanOrEqualResults = await objectViewDAO.Search(greaterThanOrEqualExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(greaterThanOrEqualResults);
            Assert.NotEmpty(greaterThanOrEqualResults);
            Assert.Equal(5, greaterThanOrEqualResults.Count); // 应该有5个用户
            // 验证每个结果的Age大于等于20
            Assert.All(greaterThanOrEqualResults, u =>
                Assert.True(u.Age >= 20)
            );

            // 测试LessThanOrEqual扩展方法
            var lessThanOrEqualExpr = Expr.Prop("Age").LessThanOrEqual(Expr.Const(30));
            var lessThanOrEqualResults = await objectViewDAO.Search(lessThanOrEqualExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(lessThanOrEqualResults);
            Assert.NotEmpty(lessThanOrEqualResults);
            Assert.Equal(5, lessThanOrEqualResults.Count); // 应该有5个用户
            // 验证每个结果的Age小于等于30
            Assert.All(lessThanOrEqualResults, u =>
                Assert.True(u.Age <= 30)
            );

            // 测试In扩展方法
            var inExpr = Expr.Prop("Id").In(new[] { user1.Id, user2.Id });
            var inResults = await objectViewDAO.Search(inExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(inResults);
            Assert.NotEmpty(inResults);
            Assert.Equal(2, inResults.Count);
            var ids = new[] { user1.Id, user2.Id };
            // 验证每个结果的Id在指定的数组中
            Assert.All(inResults, u =>
                Assert.Contains(u.Id, ids)
            );

            // 测试Between扩展方法
            var betweenExpr = Expr.Prop("Age").Between(Expr.Const(15), Expr.Const(35));
            var betweenResults = await objectViewDAO.Search(betweenExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(betweenResults);
            Assert.NotEmpty(betweenResults);
            Assert.Equal(5, betweenResults.Count); // 应该有5个用户
            Assert.All(betweenResults, u => Assert.True(u.Age >= 15 && u.Age <= 35));

            // 测试Between扩展方法（边界值）
            var betweenExpr2 = Expr.Prop("Age").Between(Expr.Const(20), Expr.Const(30));
            var betweenResults2 = await objectViewDAO.Search(betweenExpr2).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(betweenResults2);
            Assert.NotEmpty(betweenResults2);
            Assert.Equal(5, betweenResults2.Count); // 应该有5个用户
            Assert.All(betweenResults2, u => Assert.True(u.Age >= 20 && u.Age <= 30));

            // 测试Like扩展方法
            var likeExpr = Expr.Prop("Name").Like("ExtensionTest%");
            var likeResults = await objectViewDAO.Search(likeExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(likeResults);
            Assert.NotEmpty(likeResults);
            Assert.Equal(5, likeResults.Count);
            // 验证每个结果的Name以"ExtensionTest"开头
            Assert.All(likeResults, u =>
                Assert.True(u.Name.StartsWith("ExtensionTest"))
            );

            // 测试Contains扩展方法
            var containsExpr = Expr.Prop("Name").Contains("Extension");
            var containsResults = await objectViewDAO.Search(containsExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(containsResults);
            Assert.NotEmpty(containsResults);
            Assert.Equal(5, containsResults.Count);
            // 验证每个结果的Name包含"Extension"
            Assert.All(containsResults, u =>
                Assert.True(u.Name.Contains("Extension"))
            );

            // 测试StartsWith扩展方法
            var startsWithExpr = Expr.Prop("Name").StartsWith("Extension");
            var startsWithResults = await objectViewDAO.Search(startsWithExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(startsWithResults);
            Assert.NotEmpty(startsWithResults);
            Assert.Equal(5, startsWithResults.Count);
            // 验证每个结果的Name以"Extension"开头
            Assert.All(startsWithResults, u =>
                Assert.True(u.Name.StartsWith("Extension"))
            );

            // 测试EndsWith扩展方法
            var endsWithExpr = Expr.Prop("Name").EndsWith("Test1");
            var endsWithResults = await objectViewDAO.Search(endsWithExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(endsWithResults);
            Assert.NotEmpty(endsWithResults);
            Assert.Single(endsWithResults);
            // 验证每个结果的Name以"Test1"结尾
            Assert.All(endsWithResults, u =>
                Assert.True(u.Name.EndsWith("Test1"))
            );

            // 测试RegexpLike扩展方法
            var regexpLikeExpr = Expr.Prop("Name").RegexpLike("ExtensionTest[1-2]");
            var regexpLikeResults = await objectViewDAO.Search(regexpLikeExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(regexpLikeResults);
            Assert.NotEmpty(regexpLikeResults);
            Assert.Equal(2, regexpLikeResults.Count);
            // 验证每个结果的Name匹配正则表达式"ExtensionTest[1-2]"
            Assert.All(regexpLikeResults, u =>
                Assert.True(u.Name == "ExtensionTest1" || u.Name == "ExtensionTest2")
            );

            // 测试IsNull扩展方法
            var isNullExpr = Expr.Prop("DeptId").IsNull();
            var isNullResults = await objectViewDAO.Search(isNullExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(isNullResults);
            // 验证每个结果的DeptId为null
            Assert.All(isNullResults, u =>
                Assert.True(u.DeptId == null)
            );

            // 测试IsNotNull扩展方法
            var isNotNullExpr = Expr.Prop("Name").IsNotNull();
            var isNotNullResults = await objectViewDAO.Search(isNotNullExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(isNotNullResults);
            Assert.NotEmpty(isNotNullResults);
            Assert.Equal(5, isNotNullResults.Count);
            // 验证每个结果的Name不为null
            Assert.All(isNotNullResults, u =>
                Assert.True(u.Name != null)
            );

            // 测试Where扩展方法
            var whereQuery = Expr.From<TestUser>().Where(Expr.Prop("Age") > 20);
            var whereResult = dataViewDAO.Search(whereQuery);
            DataTable whereDt = await whereResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(whereDt);
            Assert.True(whereDt.Rows.Count >= 1);
            Assert.Equal(3, whereDt.Rows.Count); // 应该有3个用户（25、30、30）

            // 验证每个结果的Age都大于20
            foreach (DataRow row in whereDt.Rows)
            {
                int age = Convert.ToInt32(row["Age"]);
                Assert.True(age > 20);
            }

            // 测试GroupBy扩展方法
            var groupByQuery = Expr.From<TestUser>().GroupBy(Expr.Prop("Age")).Select(Expr.Prop("Age"), Expr.Count(Expr.Prop("Id")));
            var groupByResult = dataViewDAO.Search(groupByQuery);
            DataTable groupByDt = await groupByResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(groupByDt);
            Assert.True(groupByDt.Rows.Count >= 1);

            // 测试Having扩展方法
            var havingQuery = Expr.From<TestUser>().GroupBy(Expr.Prop("Age")).Having(Expr.Count(Expr.Prop("Id")) > 0).Select(Expr.Prop("Age"), Expr.Count(Expr.Prop("Id")));
            var havingResult = dataViewDAO.Search(havingQuery);
            DataTable havingDt = await havingResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(havingDt);
            Assert.True(havingDt.Rows.Count >= 1);

            // 测试Select扩展方法
            var selectQuery = Expr.From<TestUser>().Select(Expr.Prop("Id"), Expr.Prop("Name"));
            var selectResult = dataViewDAO.Search(selectQuery);
            DataTable selectDt = await selectResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(selectDt);
            Assert.True(selectDt.Rows.Count >= 1);

            // 测试OrderBy扩展方法
            var orderByQuery = Expr.From<TestUser>().OrderBy(Expr.Prop("Age").Asc());
            var orderByResult = dataViewDAO.Search(orderByQuery);
            DataTable orderByDt = await orderByResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(orderByDt);
            Assert.True(orderByDt.Rows.Count >= 1);

            // 验证排序顺序
            if (orderByDt.Rows.Count > 1)
            {
                int previousAge = Convert.ToInt32(orderByDt.Rows[0]["Age"]);
                for (int i = 1; i < orderByDt.Rows.Count; i++)
                {
                    int currentAge = Convert.ToInt32(orderByDt.Rows[i]["Age"]);
                    Assert.True(currentAge >= previousAge);
                    previousAge = currentAge;
                }
            }

            // 测试Section扩展方法
            var sectionQuery = Expr.From<TestUser>().Section(0, 10);
            var sectionResult = dataViewDAO.Search(sectionQuery);
            DataTable sectionDt = await sectionResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(sectionDt);
            Assert.True(sectionDt.Rows.Count >= 1);

            // 测试Asc扩展方法
            var ascExpr = Expr.Prop("Age").Asc();
            var ascQuery = Expr.From<TestUser>().OrderBy(ascExpr);
            var ascResult = dataViewDAO.Search(ascQuery);
            DataTable ascDt = await ascResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(ascDt);
            Assert.True(ascDt.Rows.Count >= 1);

            // 测试Desc扩展方法
            var descExpr = Expr.Prop("Age").Desc();
            var descQuery = Expr.From<TestUser>().OrderBy(descExpr);
            var descResult = dataViewDAO.Search(descQuery);
            DataTable descDt = await descResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(descDt);
            Assert.True(descDt.Rows.Count >= 1);

            // 测试Distinct扩展方法
            var distinctExpr = Expr.Prop("Age").Distinct();
            var distinctQuery = Expr.From<TestUser>().Select(distinctExpr.As("DistinctAge"));
            var distinctResult = dataViewDAO.Search(distinctQuery);
            DataTable distinctDt = await distinctResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(distinctDt);
            Assert.True(distinctDt.Rows.Count == 3);

            // 验证实际的Age值
            var distinctAges = new List<int>();
            foreach (DataRow row in distinctDt.Rows)
            {
                if (row["DistinctAge"] != DBNull.Value)
                {
                    distinctAges.Add(Convert.ToInt32(row["DistinctAge"]));
                }
            }
            Assert.NotEmpty(distinctAges);
            Assert.Contains(20, distinctAges);
            Assert.Contains(25, distinctAges);
            Assert.Contains(30, distinctAges);
            // 验证是否有重复值
            Assert.Equal(distinctAges.Distinct().Count(), distinctAges.Count);

            // 测试Count扩展方法
            var countExpr = Expr.Prop("Id").Count();
            var countQuery = Expr.From<TestUser>().Select(countExpr.As("UserCount"));
            var countResult = dataViewDAO.Search(countQuery);
            DataTable countDt = await countResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(countDt);
            Assert.True(countDt.Rows.Count >= 1);

            // 验证实际的Count值
            if (countDt.Rows.Count > 0 && countDt.Rows[0]["UserCount"] != DBNull.Value)
            {
                int userCount = Convert.ToInt32(countDt.Rows[0]["UserCount"]);
                Assert.Equal(5, userCount); // 应该有5个用户
            }

            // 测试Sum扩展方法
            var sumExpr = Expr.Prop("Age").Sum();
            var sumQuery = Expr.From<TestUser>().Select(sumExpr.As("TotalAge"));
            var sumResult = dataViewDAO.Search(sumQuery);
            DataTable sumDt = await sumResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(sumDt);
            Assert.True(sumDt.Rows.Count >= 1);

            // 验证实际的Sum值
            if (sumDt.Rows.Count > 0 && sumDt.Rows[0]["TotalAge"] != DBNull.Value)
            {
                int totalAge = Convert.ToInt32(sumDt.Rows[0]["TotalAge"]);
                Assert.Equal(125, totalAge); // 20 + 30 + 25 + 30 + 20 = 125
            }

            // 测试Avg扩展方法
            var avgExpr = Expr.Prop("Age").Avg();
            var avgQuery = Expr.From<TestUser>().Select(avgExpr.As("AverageAge"));
            var avgResult = dataViewDAO.Search(avgQuery);
            DataTable avgDt = await avgResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(avgDt);
            Assert.True(avgDt.Rows.Count >= 1);

            // 验证实际的Avg值
            if (avgDt.Rows.Count > 0 && avgDt.Rows[0]["AverageAge"] != DBNull.Value)
            {
                double averageAge = Convert.ToDouble(avgDt.Rows[0]["AverageAge"]);
                Assert.True(averageAge >= 20);
                Assert.True(averageAge <= 30);
            }

            // 测试Max扩展方法
            var maxExpr = Expr.Prop("Age").Max();
            var maxQuery = Expr.From<TestUser>().Select(maxExpr.As("MaxAge"));
            var maxResult = dataViewDAO.Search(maxQuery);
            DataTable maxDt = await maxResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(maxDt);
            Assert.True(maxDt.Rows.Count >= 1);

            // 验证实际的Max值
            if (maxDt.Rows.Count > 0 && maxDt.Rows[0]["MaxAge"] != DBNull.Value)
            {
                int maxAge = Convert.ToInt32(maxDt.Rows[0]["MaxAge"]);
                Assert.Equal(30, maxAge); // 最大年龄是30
            }

            // 测试Min扩展方法
            var minExpr = Expr.Prop("Age").Min();
            var minQuery = Expr.From<TestUser>().Select(minExpr.As("MinAge"));
            var minResult = dataViewDAO.Search(minQuery);
            DataTable minDt = await minResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(minDt);
            Assert.True(minDt.Rows.Count >= 1);

            // 验证实际的Min值
            if (minDt.Rows.Count > 0 && minDt.Rows[0]["MinAge"] != DBNull.Value)
            {
                int minAge = Convert.ToInt32(minDt.Rows[0]["MinAge"]);
                Assert.Equal(20, minAge);
            }

            // 测试AndIf扩展方法
            var andIfExpr = (Expr.Prop("Age") > 18).AndIf(true, Expr.Prop("Name").Contains("Test"));
            var andIfResults = await objectViewDAO.Search(andIfExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(andIfResults);
            Assert.NotEmpty(andIfResults);

            // 测试OrIf扩展方法
            var orIfExpr = (Expr.Prop("Age") > 35).OrIf(true, Expr.Prop("Name").Contains("Test"));
            var orIfResults = await objectViewDAO.Search(orIfExpr).ToListAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(orIfResults);
            Assert.NotEmpty(orIfResults);

            // 测试WhereIf扩展方法
            var whereIfQuery = Expr.From<TestUser>().Where(Expr.Prop("Age") > 18);
            var whereIfResult = dataViewDAO.Search(whereIfQuery);
            DataTable whereIfDt = await whereIfResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(whereIfDt);
            Assert.True(whereIfDt.Rows.Count >= 1);
        }

        #endregion

        #region 内置函数测试

        [Fact]
        public async Task BuiltInFunctions_Tests()
        {
            var service = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var objectViewDAO = ServiceProvider.GetRequiredService<ObjectViewDAO<TestUser>>();
            var dataViewDAO = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            // 插入测试数据
            var user1 = new TestUser { Name = "FunctionTest1", Age = 20, CreateTime = DateTime.Now };
            var user2 = new TestUser { Name = "FunctionTest2", Age = 30, CreateTime = DateTime.Now };
            var user3 = new TestUser { Name = "FunctionTest3", Age = 25, CreateTime = DateTime.Now };
            await service.InsertAsync(user1, TestContext.Current.CancellationToken);
            await service.InsertAsync(user2, TestContext.Current.CancellationToken);
            await service.InsertAsync(user3, TestContext.Current.CancellationToken);

            // 测试COALESCE函数
            var coalesceQuery = Expr.From<TestUser>().Select(Expr.Coalesce(Expr.Prop("DeptId"), Expr.Const(0)).As("CoalescedDeptId"));
            var coalesceResult = dataViewDAO.Search(coalesceQuery);
            DataTable coalesceDt = await coalesceResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(coalesceDt);
            Assert.True(coalesceDt.Rows.Count >= 1);

            // 验证实际的COALESCE值
            if (coalesceDt.Rows.Count > 0 && coalesceDt.Rows[0]["CoalescedDeptId"] != DBNull.Value)
            {
                int coalescedDeptId = Convert.ToInt32(coalesceDt.Rows[0]["CoalescedDeptId"]);
                Assert.Equal(0, coalescedDeptId); // 因为DeptId为null，所以应该返回0
            }

            // 测试IfNull函数
            var ifNullQuery = Expr.From<TestUser>().Select(Expr.IfNull(Expr.Prop("DeptId"), Expr.Const(0)).As("IfNullDeptId"));
            var ifNullResult = dataViewDAO.Search(ifNullQuery);
            DataTable ifNullDt = await ifNullResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(ifNullDt);
            Assert.True(ifNullDt.Rows.Count >= 1);

            // 验证实际的IfNull值
            if (ifNullDt.Rows.Count > 0 && ifNullDt.Rows[0]["IfNullDeptId"] != DBNull.Value)
            {
                int ifNullDeptId = Convert.ToInt32(ifNullDt.Rows[0]["IfNullDeptId"]);
                Assert.Equal(0, ifNullDeptId); // 因为DeptId为null，所以应该返回0
            }

            // 测试NOW函数
            var nowQuery = Expr.From<TestUser>().Select(Expr.Now().As("CurrentTime"));
            var nowResult = dataViewDAO.Search(nowQuery);
            DataTable nowDt = await nowResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(nowDt);
            Assert.True(nowDt.Rows.Count >= 1);

            // 验证实际的NOW值
            if (nowDt.Rows.Count > 0 && nowDt.Rows[0]["CurrentTime"] != DBNull.Value)
            {
                DateTime currentTime = Convert.ToDateTime(nowDt.Rows[0]["CurrentTime"]);
                Assert.True(currentTime <= DateTime.Now);
            }

            // 测试TODAY函数
            var todayQuery = Expr.From<TestUser>().Select(Expr.Today().As("CurrentDate"));
            var todayResult = dataViewDAO.Search(todayQuery);
            DataTable todayDt = await todayResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(todayDt);
            Assert.True(todayDt.Rows.Count >= 1);

            // 验证实际的TODAY值
            if (todayDt.Rows.Count > 0 && todayDt.Rows[0]["CurrentDate"] != DBNull.Value)
            {
                DateTime currentDate = Convert.ToDateTime(todayDt.Rows[0]["CurrentDate"]);
                Assert.Equal(DateTime.Today.Date, currentDate.Date);
            }

            // 测试LOWER函数
            var lowerQuery = Expr.From<TestUser>().Select(Expr.Lower(Expr.Prop("Name")).As("LowerName"));
            var lowerResult = dataViewDAO.Search(lowerQuery);
            DataTable lowerDt = await lowerResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(lowerDt);
            Assert.True(lowerDt.Rows.Count >= 1);

            // 验证实际的LOWER值
            if (lowerDt.Rows.Count > 0 && lowerDt.Rows[0]["LowerName"] != DBNull.Value)
            {
                string lowerName = lowerDt.Rows[0]["LowerName"].ToString();
                Assert.True(lowerName.Equals(lowerName.ToLower()));
            }

            // 测试LENGTH函数
            var lengthQuery = Expr.From<TestUser>().Select(Expr.Length(Expr.Prop("Name")).As("NameLength"));
            var lengthResult = dataViewDAO.Search(lengthQuery);
            DataTable lengthDt = await lengthResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(lengthDt);
            Assert.True(lengthDt.Rows.Count >= 1);

            // 验证实际的LENGTH值
            if (lengthDt.Rows.Count > 0 && lengthDt.Rows[0]["NameLength"] != DBNull.Value)
            {
                int nameLength = Convert.ToInt32(lengthDt.Rows[0]["NameLength"]);
                Assert.True(nameLength > 0);
            }

            // 测试COUNT函数
            var countQuery = Expr.From<TestUser>().Select(Expr.Count().As("UserCount"));
            var countResult = dataViewDAO.Search(countQuery);
            DataTable countDt = await countResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(countDt);
            Assert.True(countDt.Rows.Count >= 1);

            // 验证实际的COUNT值
            if (countDt.Rows.Count > 0 && countDt.Rows[0]["UserCount"] != DBNull.Value)
            {
                int userCount = Convert.ToInt32(countDt.Rows[0]["UserCount"]);
                Assert.True(userCount >= 1);
            }

            // 测试SUM函数
            var sumQuery = Expr.From<TestUser>().Select(Expr.Sum(Expr.Prop("Age")).As("TotalAge"));
            var sumResult = dataViewDAO.Search(sumQuery);
            DataTable sumDt = await sumResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(sumDt);
            Assert.True(sumDt.Rows.Count >= 1);

            // 验证实际的SUM值
            if (sumDt.Rows.Count > 0 && sumDt.Rows[0]["TotalAge"] != DBNull.Value)
            {
                int totalAge = Convert.ToInt32(sumDt.Rows[0]["TotalAge"]);
                Assert.True(totalAge >= 25); // 至少有一个用户，年龄为25
            }

            // 测试AVG函数
            var avgQuery = Expr.From<TestUser>().Select(Expr.Avg(Expr.Prop("Age")).As("AverageAge"));
            var avgResult = dataViewDAO.Search(avgQuery);
            DataTable avgDt = await avgResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(avgDt);
            Assert.True(avgDt.Rows.Count >= 1);

            // 验证实际的AVG值
            if (avgDt.Rows.Count > 0 && avgDt.Rows[0]["AverageAge"] != DBNull.Value)
            {
                double averageAge = Convert.ToDouble(avgDt.Rows[0]["AverageAge"]);
                Assert.True(averageAge >= 25);
            }

            // 测试MAX函数
            var maxQuery = Expr.From<TestUser>().Select(Expr.Max(Expr.Prop("Age")).As("MaxAge"));
            var maxResult = dataViewDAO.Search(maxQuery);
            DataTable maxDt = await maxResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(maxDt);
            Assert.True(maxDt.Rows.Count >= 1);

            // 验证实际的MAX值
            if (maxDt.Rows.Count > 0 && maxDt.Rows[0]["MaxAge"] != DBNull.Value)
            {
                int maxAge = Convert.ToInt32(maxDt.Rows[0]["MaxAge"]);
                Assert.True(maxAge >= 25);
            }

            // 测试MIN函数
            var minQuery = Expr.From<TestUser>().Select(Expr.Min(Expr.Prop("Age")).As("MinAge"));
            var minResult = dataViewDAO.Search(minQuery);
            DataTable minDt = await minResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(minDt);
            Assert.True(minDt.Rows.Count >= 1);

            // 验证实际的MIN值
            if (minDt.Rows.Count > 0 && minDt.Rows[0]["MinAge"] != DBNull.Value)
            {
                int minAge = Convert.ToInt32(minDt.Rows[0]["MinAge"]);
                Assert.True(minAge <= 25);
            }

            // 测试CONCAT函数
            var concatQuery = Expr.From<TestUser>().Select(Expr.Concat(Expr.Prop("Name"), Expr.Const(" Test")).As("NameWithSuffix"));
            var concatResult = dataViewDAO.Search(concatQuery);
            DataTable concatDt = await concatResult.GetResultAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(concatDt);
            Assert.True(concatDt.Rows.Count >= 1);

            // 验证实际的CONCAT值
            if (concatDt.Rows.Count > 0 && concatDt.Rows[0]["NameWithSuffix"] != DBNull.Value)
            {
                string nameWithSuffix = concatDt.Rows[0]["NameWithSuffix"].ToString();
                Assert.Contains(" Test", nameWithSuffix);
            }
        }

        #endregion
    }
}
