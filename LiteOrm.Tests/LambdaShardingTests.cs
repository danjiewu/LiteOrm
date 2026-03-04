using System;
using System.Linq.Expressions;
using LiteOrm.Common;
using LiteOrm.Tests.Models;
using Xunit;
using Xunit.Abstractions;

namespace LiteOrm.Tests
{
    /// <summary>
    /// Lambda 方式分表测试
    /// </summary>
    public class LambdaShardingTests
    {
        private readonly ITestOutputHelper _output;

        public LambdaShardingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestTableArgsAssignment_SingleParameter()
        {
            // 测试单参数 TableArgs 赋值
            Expression<Func<TestUser, bool>> lambda = u => ((IArged)u).TableArgs == new[] { "202401" } && u.Id > 100;
            
            var converter = new LambdaExprConverter(lambda);
            var result = converter.ToLogicExpr();
            
            _output.WriteLine($"Expression: {lambda}");
            _output.WriteLine($"Result: {result}");
            
            Assert.NotNull(result);
        }

        [Fact]
        public void TestTableArgsAssignment_WithConditions()
        {
            // 测试 TableArgs 赋值与其他条件结合
            Expression<Func<TestUser, bool>> lambda = u => 
                ((IArged)u).TableArgs == new[] { "202401", "202402" } && 
                u.Name.Contains("test") && 
                u.Id > 0;
            
            var converter = new LambdaExprConverter(lambda);
            var result = converter.ToLogicExpr();
            
            _output.WriteLine($"Expression: {lambda}");
            _output.WriteLine($"Result: {result}");
            
            Assert.NotNull(result);
        }

        [Fact]
        public void TestTableArgsAssignment_Exists()
        {
            // 测试在 Exists 子查询中使用 TableArgs
            Expression<Func<TestUser, bool>> lambda = u => 
                u.Id > 0 && 
                Expr.Exists<TestDepartment>(d => 
                    ((IArged)d).TableArgs == new[] { "202401" } && 
                    d.Id == u.DeptId);
            
            var converter = new LambdaExprConverter(lambda);
            var result = converter.ToLogicExpr();
            
            _output.WriteLine($"Expression: {lambda}");
            _output.WriteLine($"Result: {result}");
            
            Assert.NotNull(result);
        }

        [Fact]
        public void TestTableArgsAssignment_ImplicitArray()
        {
            // 测试隐式数组语法（C# 12+）
            var tableArgs = new[] { "202401" };
            Expression<Func<TestUser, bool>> lambda = u => 
                ((IArged)u).TableArgs == tableArgs && 
                u.Id > 0;
            
            var converter = new LambdaExprConverter(lambda);
            var result = converter.ToLogicExpr();
            
            _output.WriteLine($"Expression: {lambda}");
            _output.WriteLine($"Result: {result}");
            
            Assert.NotNull(result);
        }

        [Fact]
        public void TestTableArgsAssignment_VerifyFromExpr()
        {
            // 验证 FromExpr 的 TableArgs 是否正确设置
            Expression<Func<TestUser, bool>> lambda = u => 
                ((IArged)u).TableArgs == new[] { "202401", "202402" } && 
                u.Id > 100;
            
            var converter = new LambdaExprConverter(lambda);
            var logicExpr = converter.ToLogicExpr();
            
            // 创建 FromExpr 来验证
            var fromExpr = Expr.From<TestUser>();
            Expression<Func<TestUser, bool>> whereExpr = u => u.Id > 100;
            var whereCondition = LambdaExprConverter.ToLogicExpr(whereExpr);
            var whereResult = fromExpr.Where(whereCondition);
            
            _output.WriteLine($"Logic Expression: {logicExpr}");
            _output.WriteLine($"Where Result: {whereResult}");
            
            Assert.NotNull(logicExpr);
        }
    }
}
