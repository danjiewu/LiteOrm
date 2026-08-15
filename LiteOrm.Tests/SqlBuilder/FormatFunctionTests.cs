using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using LiteOrm.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 测试 ToString(format) → FunctionExpr("Format", ...) 在实际数据库查询中的完整链路：
    /// Lambda 转换、SQL 生成、数据库执行与结果验证。
    /// </summary>
    [Collection("Database")]
    public class FormatFunctionTests : TestBase
    {
        public FormatFunctionTests(DatabaseFixture fixture) : base(fixture) { }

        /// <summary>
        /// 使用 FunctionExpr 直接构造 Format 查询，验证 SELECT 返回的格式化日期字符串正确。
        /// </summary>
        [Fact]
        public async Task Format_InSelect_ReturnsFormattedDateString()
        {
            var ct = TestContext.Current.CancellationToken;
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDao = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            var user = new TestUser { Name = "FormatSelectTest", Age = 25, CreateTime = new DateTime(2024, 6, 15, 10, 30, 0) };
            await userService.InsertAsync(user, ct);

            var formatExpr = new FunctionExpr("Format", Expr.Prop("CreateTime"), new ValueExpr("yyyy-MM-dd"));
            var query = Expr.From<TestUser>()
                .Where(Expr.Prop("Id") == user.Id)
                .Select(formatExpr.As("FormattedDate"));
            var dt = await dataViewDao.Search(query).GetResultAsync(ct);

            Assert.NotNull(dt);
            Assert.Single(dt.Rows.Cast<DataRow>());
            Assert.Equal("2024-06-15", dt.Rows[0]["FormattedDate"]?.ToString());
        }

        /// <summary>
        /// 通过 Lambda u.CreateTime.ToString("yyyy-MM-dd") 自动转换为 FunctionExpr，
        /// 再用于 SELECT 查询，验证结果与直接使用 FunctionExpr 一致。
        /// </summary>
        [Fact]
        public async Task Format_ViaLambdaToString_InSelect_ReturnsFormattedDateString()
        {
            var ct = TestContext.Current.CancellationToken;
            var userService = ServiceProvider.GetRequiredService<IEntityServiceAsync<TestUser>>();
            var dataViewDao = ServiceProvider.GetRequiredService<DataViewDAO<TestUser>>();

            var user = new TestUser { Name = "FormatLambdaTest", Age = 30, CreateTime = new DateTime(2024, 12, 25, 8, 0, 0) };
            await userService.InsertAsync(user, ct);

            Expression<Func<TestUser, string>> lambdaExpr = u => u.CreateTime.ToString("yyyy-MM-dd");
            var func = Assert.IsType<FunctionExpr>(LambdaExprConverter.ToValueExpr(lambdaExpr));

            var query = Expr.From<TestUser>()
                .Where(Expr.Prop("Id") == user.Id)
                .Select(func.As("FormattedDate"));
            var dt = await dataViewDao.Search(query).GetResultAsync(ct);

            Assert.NotNull(dt);
            Assert.Single(dt.Rows.Cast<DataRow>());
            Assert.Equal("2024-12-25", dt.Rows[0]["FormattedDate"]?.ToString());
        }
    }
}
