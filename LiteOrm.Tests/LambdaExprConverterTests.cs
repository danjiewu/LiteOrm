using LiteOrm.Common;
using LiteOrm.Tests.Models;
using System.Linq.Expressions;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// LambdaExprConverter 单元测试，覆盖各类表达式节点的转换逻辑。
    /// 纯内存测试，无需数据库连接。
    /// </summary>
    [Collection("Database")]
    public class LambdaExprConverterTests
    {
        #region ConvertLambda — 返回类型分支

        [Fact]
        public void ConvertLambda_BoolReturn_YieldsLogicExpr()
        {
            Expression<Func<TestUser, bool>> expr = u => u.Age > 18;
            var result = LambdaExprConverter.ToLogicExpr(expr);
            Assert.IsAssignableFrom<LogicExpr>(result);
        }

        [Fact]
        public void ConvertLambda_ValueReturn_YieldsValueTypeExpr()
        {
            Expression<Func<TestUser, int>> expr = u => u.Age;
            var result = LambdaExprConverter.ToValueExpr(expr);
            Assert.IsAssignableFrom<ValueTypeExpr>(result);
        }

        [Fact]
        public void ConvertLambda_NestedLambdaInLinq_IsConvertedCorrectly()
        {
            // Where 内嵌的 lambda 由 ConvertLambda 处理
            Expression<Func<IQueryable<TestUser>, IQueryable<TestUser>>> expr =
                q => q.Where(u => u.Age > 18);
            var result = LambdaExprConverter.ToSqlSegment(expr);
            var where = Assert.IsType<WhereExpr>(result);
            Assert.IsAssignableFrom<LogicExpr>(where.Where);
        }

        #endregion

        #region 一元表达式 (UnaryExpression)

        [Fact]
        public void Unary_Not_YieldsNotExpr()
        {
            Expression<Func<TestUser, bool>> expr = u => !(u.Age > 18);
            var result = LambdaExprConverter.ToLogicExpr(expr);
            var not = Assert.IsType<NotExpr>(result);
            Assert.NotNull(not.Operand);
        }

        [Fact]
        public void Unary_Negate_YieldsUnaryExprWithNagiveOperator()
        {
            Expression<Func<TestUser, int>> expr = u => -u.Age;
            var result = LambdaExprConverter.ToValueExpr(expr);
            var unary = Assert.IsType<UnaryExpr>(result);
            Assert.Equal(UnaryOperator.Nagive, unary.Operator);
            Assert.IsType<PropertyExpr>(unary.Operand);
        }

        [Fact]
        public void Unary_BitwiseNot_YieldsUnaryExprWithBitwiseNotOperator()
        {
            Expression<Func<TestUser, int>> expr = u => ~u.Age;
            var result = LambdaExprConverter.ToValueExpr(expr);
            var unary = Assert.IsType<UnaryExpr>(result);
            Assert.Equal(UnaryOperator.BitwiseNot, unary.Operator);
        }

        [Fact]
        public void Unary_Convert_IsTransparent()
        {
            // (long)u.Age 不应包装，直接透传内部的 PropertyExpr
            Expression<Func<TestUser, long>> expr = u => (long)u.Age;
            var result = LambdaExprConverter.ToValueExpr(expr);
            var prop = Assert.IsType<PropertyExpr>(result);
            Assert.Equal("Age", prop.PropertyName);
        }

        #endregion

        #region 二元 — Coalesce (??)

        [Fact]
        public void Binary_Coalesce_WithParamDependency_YieldsCoalesceFunctionExpr()
        {
            // d.ParentId ?? 0 依赖参数 → COALESCE(ParentId, 0)
            Expression<Func<TestDepartment, int>> expr = d => (d.ParentId ?? 0);
            var result = LambdaExprConverter.ToValueExpr(expr);
            var func = Assert.IsType<FunctionExpr>(result);
            Assert.Equal("COALESCE", func.FunctionName, ignoreCase: true);
            Assert.Equal(2, func.Parameters.Count);
            Assert.IsType<PropertyExpr>(func.Parameters[0]);
            Assert.IsType<ValueExpr>(func.Parameters[1]);
        }

        [Fact]
        public void Binary_Coalesce_WithoutParamDependency_LocalEval()
        {
            // 两侧均为闭包变量，无参数依赖 → 本地求值为 ValueExpr
            string s1 = null;
            string s2 = "fallback";
            Expression<Func<TestUser, string>> expr = u => (s1 ?? s2);
            var result = LambdaExprConverter.ToValueExpr(expr);
            var ve = Assert.IsType<ValueExpr>(result);
            Assert.Equal("fallback", ve.Value);
        }

        #endregion

        #region 二元 — 字符串连接 (Add on string)

        [Fact]
        public void Binary_StringAdd_YieldsValueBinaryExprWithConcatOperator()
        {
            Expression<Func<TestUser, string>> expr = u => u.Name + "!";
            var result = LambdaExprConverter.ToValueExpr(expr);
            var bin = Assert.IsType<ValueBinaryExpr>(result);
            Assert.Equal(ValueOperator.Concat, bin.Operator);
            Assert.IsType<PropertyExpr>(bin.Left);
            Assert.IsType<ValueExpr>(bin.Right);
        }

        #endregion

        #region 二元 — CompareTo 扁平化

        [Fact]
        public void Binary_CompareTo_Left_FlattensToDirectComparison()
        {
            // u.Age.CompareTo(18) > 0  →  Age > 18
            Expression<Func<TestUser, bool>> expr = u => u.Age.CompareTo(18) > 0;
            var result = LambdaExprConverter.ToLogicExpr(expr);
            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.GreaterThan, bin.Operator);
            var leftProp = Assert.IsType<PropertyExpr>(bin.Left);
            Assert.Equal("Age", leftProp.PropertyName);
            var rightVal = Assert.IsType<ValueExpr>(bin.Right);
            Assert.Equal(18, rightVal.Value);
        }

        [Fact]
        public void Binary_CompareTo_Right_FlattensAndSwapsOperands()
        {
            // 0 < u.Age.CompareTo(18)  →  18 < Age（交换操作数）
            Expression<Func<TestUser, bool>> expr = u => 0 < u.Age.CompareTo(18);
            var result = LambdaExprConverter.ToLogicExpr(expr);
            var bin = Assert.IsType<LogicBinaryExpr>(result);
            Assert.Equal(LogicOperator.LessThan, bin.Operator);
            var leftVal = Assert.IsType<ValueExpr>(bin.Left);
            Assert.Equal(18, leftVal.Value);
            var rightProp = Assert.IsType<PropertyExpr>(bin.Right);
            Assert.Equal("Age", rightProp.PropertyName);
        }

        [Fact]
        public void Binary_CompareTo_NonZeroRhs_ThrowsArgumentException()
        {
            // CompareTo 只能与 0 比较
            Expression<Func<TestUser, bool>> expr = u => u.Age.CompareTo(18) > 1;
            Assert.Throws<ArgumentException>(() => LambdaExprConverter.ToLogicExpr(expr));
        }

        #endregion

        #region 常量表达式 (ConstantExpression)

        [Fact]
        public void Constant_Primitive_IsConstTrue()
        {
            Expression<Func<TestUser, int>> expr = u => 42;
            var result = LambdaExprConverter.ToValueExpr(expr);
            var ve = Assert.IsType<ValueExpr>(result);
            Assert.Equal(42, ve.Value);
            Assert.True(ve.IsConst);
        }

        [Fact]
        public void Constant_Null_YieldsExprNull()
        {
            Expression<Func<TestUser, string>> expr = u => null;
            var result = LambdaExprConverter.ToValueExpr(expr);
            Assert.Equal(Expr.Null, result);
        }

        [Fact]
        public void Constant_ClosureVariable_IsConstFalse()
        {
            string name = "Alice";
            Expression<Func<TestUser, string>> expr = u => name;
            var result = LambdaExprConverter.ToValueExpr(expr);
            var ve = Assert.IsType<ValueExpr>(result);
            Assert.Equal("Alice", ve.Value);
            Assert.False(ve.IsConst);
        }

        [Fact]
        public void Constant_ClosureExprObject_ReturnedDirectly()
        {
            // 若常量本身是 Expr，应直接返回
            Expr existingExpr = Expr.Prop("Age") > 18;
            var constant = Expression.Constant(existingExpr, typeof(Expr));
            var lambda = Expression.Lambda<Func<TestUser, Expr>>(
                constant,
                Expression.Parameter(typeof(TestUser), "u"));
            var converter = new LambdaExprConverter(lambda);
            var result = converter.Convert(constant);
            Assert.Same(existingExpr, result);
        }

        #endregion

        #region 成员访问 (MemberExpression)

        [Fact]
        public void MemberAccess_EntityProperty_HasCorrectNameAndTableAlias()
        {
            Expression<Func<TestUser, bool>> expr = u => u.Age > 18;
            var result = LambdaExprConverter.ToLogicExpr(expr);
            var bin = Assert.IsType<LogicBinaryExpr>(result);
            var prop = Assert.IsType<PropertyExpr>(bin.Left);
            Assert.Equal("Age", prop.PropertyName);
            Assert.Null(prop.TableAlias);
        }

        [Fact]
        public void MemberAccess_NullableValue_UnwrapsToInnerProperty()
        {
            // d.ParentId.Value 应等同于 d.ParentId 的访问
            Expression<Func<TestDepartment, int>> exprWithValue = d => d.ParentId.Value;
            Expression<Func<TestDepartment, int?>> exprWithout = d => d.ParentId;
            var withValue = LambdaExprConverter.ToValueExpr(exprWithValue);
            var without = LambdaExprConverter.ToValueExpr(exprWithout);
            var propWithValue = Assert.IsType<PropertyExpr>(withValue);
            var propWithout = Assert.IsType<PropertyExpr>(without);
            Assert.Equal(propWithout.PropertyName, propWithValue.PropertyName);
        }

        [Fact]
        public void MemberAccess_RegisteredTypeMemberHandler_InvokesHandler()
        {
            // 演示自定义注册非实体属性（如 DateTime 的 Year）为特定 SQL 方法
            bool handlerCalled = false;
            LambdaExprConverter.RegisterMemberHandler(typeof(DateTime), "Year", (node, converter) =>
            {
                handlerCalled = true;
                var innerExpr = converter.Convert(node.Expression) as ValueTypeExpr;
                return new FunctionExpr("YEAR", innerExpr);
            });
            try
            {
                Expression<Func<TestUser, int>> expr = u => u.CreateTime.Year;
                var result = LambdaExprConverter.ToValueExpr(expr);
                Assert.True(handlerCalled);
                var func = Assert.IsType<FunctionExpr>(result);
                Assert.Equal("YEAR", func.FunctionName);
            }
            finally
            {
                // 恢复：注册为 null 使用默认处理器
                LambdaExprConverter.RegisterMemberHandler(typeof(DateTime), "Year", (node, converter) => null);
            }
        }

        [Fact]
        public void MemberAccess_ClosureObject_LocalEval()
        {
            // 闭包中的对象属性访问应在本地求值
            var user = new TestUser { Age = 30 };
            Expression<Func<TestUser, int>> expr = u => user.Age;
            var result = LambdaExprConverter.ToValueExpr(expr);
            var ve = Assert.IsType<ValueExpr>(result);
            Assert.Equal(30, ve.Value);
        }

        #endregion

        #region 方法调用 (MethodCallExpression)

        [Fact]
        public void MethodCall_RegisteredTypeMethodHandler_InvokesHandler()
        {
            bool handlerCalled = false;
            LambdaExprConverter.RegisterMethodHandler(typeof(string), "IsNullOrEmpty", (node, converter) =>
            {
                handlerCalled = true;
                return new FunctionExpr("IS_NULL_OR_EMPTY", converter.Convert(node.Arguments[0]) as ValueTypeExpr);
            });
            try
            {
                Expression<Func<TestUser, bool>> expr = u => string.IsNullOrEmpty(u.Name);
                var result = LambdaExprConverter.ToLogicExpr(expr);
                Assert.True(handlerCalled);
                Assert.NotNull(result);
            }
            finally
            {
                LambdaExprConverter.RegisterMethodHandler(typeof(string), "IsNullOrEmpty", (node, converter) => null);
            }
        }

        [Fact]
        public void MethodCall_WithParamDependency_InstanceMethod_ReturnsObjectConversion()
        {
            // 未注册处理器 + 依赖参数 + 实例方法 → ConvertInternal(node.Object) → FunctionExpr
            Expression<Func<TestUser, string>> expr = u => u.Name.ToUpper();
            var result = LambdaExprConverter.ToValueExpr(expr);
            // node.Object = u.Name.ToUpper()，转换结果为 FunctionExpr("ToUpper","PropertyExpr("Name"))
            var func = Assert.IsType<FunctionExpr>(result);
            Assert.Equal("ToUpper", func.FunctionName);
            var prop = Assert.IsType<PropertyExpr>(func.Parameters[0]);
            Assert.Equal("Name", prop.PropertyName);
        }

        [Fact]
        public void MethodCall_WithoutParamDependency_LocalEval()
        {
            // 不依赖参数的方法 → 本地求值
            int count = new List<int> { 1, 2, 3 }.Count;
            Expression<Func<TestUser, int>> expr = u => new List<int> { 1, 2, 3 }.Count;
            var result = LambdaExprConverter.ToValueExpr(expr);
            var ve = Assert.IsType<ValueExpr>(result);
            Assert.Equal(count, ve.Value);
        }

        [Fact]
        public void MethodCall_StaticMethod_WithParam_DefaultFunctionHandler()
        {
            // 对原始类型上的静态方法（Math.Abs 等）进入 DefaultFunctionHandler
            Expression<Func<TestUser, int>> expr = u => Math.Abs(u.Age);
            var result = LambdaExprConverter.ToValueExpr(expr);
            // Math.Abs 属于 Math 类，不是 primitive，但 u.Age 有参数依赖且是静态方法
            // node.Object == null → DefaultFunctionHandler → FunctionExpr
            var func = Assert.IsType<FunctionExpr>(result);
            Assert.Equal("Abs", func.FunctionName);
            Assert.Single(func.Parameters);
        }

        #endregion

        #region 数组/集合表达式

        [Fact]
        public void NewArray_YieldsValueSet_WithCorrectCount()
        {
            Expression<Func<TestUser, int[]>> expr = u => new int[] { u.Age, 18, 25 };
            var result = LambdaExprConverter.ToValueExpr(expr);
            var vs = Assert.IsType<ValueSet>(result);
            Assert.Equal(3, vs.Count);
            Assert.IsType<PropertyExpr>(vs[0]);
            Assert.IsType<ValueExpr>(vs[1]);
        }

        [Fact]
        public void ListInit_YieldsValueSet_WithCorrectCount()
        {
            Expression<Func<TestUser, List<int>>> expr = u => new List<int> { u.Age, 18 };
            var result = LambdaExprConverter.ToValueExpr(expr);
            var vs = Assert.IsType<ValueSet>(result);
            Assert.Equal(2, vs.Count);
        }

        #endregion

        #region NewExpression

        [Fact]
        public void NewExpression_AnonymousWithParams_YieldsValueSet()
        {
            // new { u.Name, u.Age } 依赖参数 → ValueSet
            Expression<Func<TestUser, object>> expr = u => new { u.Name, u.Age };
            var result = LambdaExprConverter.ToValueExpr(expr);
            var vs = Assert.IsType<ValueSet>(result);
            Assert.Equal(2, vs.Count);
        }

        [Fact]
        public void NewExpression_WithoutParams_LocalEval()
        {
            // new DateTime(2024, 1, 1) 无参数依赖 → 本地求值
            var expected = new DateTime(2024, 1, 1);
            Expression<Func<TestUser, DateTime>> expr = u => new DateTime(2024, 1, 1);
            var result = LambdaExprConverter.ToValueExpr(expr);
            var ve = Assert.IsType<ValueExpr>(result);
            Assert.Equal(expected, ve.Value);
        }

        #endregion

        #region 逻辑组合 (AndAlso / OrElse)

        [Fact]
        public void Binary_AndAlso_YieldsLogicSetAnd()
        {
            Expression<Func<TestUser, bool>> expr = u => u.Age > 18 && u.Age < 65;
            var result = LambdaExprConverter.ToLogicExpr(expr);
            var ls = Assert.IsType<LogicSet>(result);
            Assert.Equal(LogicJoinType.And, ls.JoinType);
            Assert.Equal(2, ls.Count);
        }

        [Fact]
        public void Binary_OrElse_YieldsLogicSetOr()
        {
            Expression<Func<TestUser, bool>> expr = u => u.Age < 0 || u.Age > 100;
            var result = LambdaExprConverter.ToLogicExpr(expr);
            var ls = Assert.IsType<LogicSet>(result);
            Assert.Equal(LogicJoinType.Or, ls.JoinType);
            Assert.Equal(2, ls.Count);
        }

        [Fact]
        public void Binary_AndAlso_NullBranch_ReturnsSingleSide()
        {
            // 用 null 作为一侧时，应直接返回另一侧（由 AsLogic(null) 返回 null，再经 And 处理）
            Expression<Func<TestUser, bool>> expr = u => u.Age > 0;
            var result = LambdaExprConverter.ToLogicExpr(expr);
            Assert.NotNull(result);
            Assert.IsType<LogicBinaryExpr>(result);
        }

        #endregion

        #region 算术二元运算

        [Fact]
        public void Binary_Add_YieldsValueBinaryExprWithAddOperator()
        {
            Expression<Func<TestUser, int>> expr = u => u.Age + 10;
            var result = LambdaExprConverter.ToValueExpr(expr);
            var bin = Assert.IsType<ValueBinaryExpr>(result);
            Assert.Equal(ValueOperator.Add, bin.Operator);
        }

        [Fact]
        public void Binary_Multiply_YieldsValueBinaryExprWithMultiplyOperator()
        {
            Expression<Func<TestUser, int>> expr = u => u.Age * 2;
            var result = LambdaExprConverter.ToValueExpr(expr);
            var bin = Assert.IsType<ValueBinaryExpr>(result);
            Assert.Equal(ValueOperator.Multiply, bin.Operator);
        }

        [Fact]
        public void Binary_Subtract_YieldsValueBinaryExprWithSubtractOperator()
        {
            Expression<Func<TestUser, int>> expr = u => u.Age - 1;
            var result = LambdaExprConverter.ToValueExpr(expr);
            var bin = Assert.IsType<ValueBinaryExpr>(result);
            Assert.Equal(ValueOperator.Subtract, bin.Operator);
        }

        [Fact]
        public void Binary_Divide_YieldsValueBinaryExprWithDivideOperator()
        {
            Expression<Func<TestUser, int>> expr = u => u.Age / 2;
            var result = LambdaExprConverter.ToValueExpr(expr);
            var bin = Assert.IsType<ValueBinaryExpr>(result);
            Assert.Equal(ValueOperator.Divide, bin.Operator);
        }

        #endregion

        #region 不支持的表达式类型

        [Fact]
        public void Convert_UnsupportedExpressionType_ThrowsNotSupportedException()
        {
            // 块表达式等不支持的节点应抛出 NotSupportedException
            var param = Expression.Parameter(typeof(TestUser), "u");
            var blockExpr = Expression.Block(Expression.Constant(1));
            var lambda = Expression.Lambda<Func<TestUser, int>>(blockExpr, param);
            var converter = new LambdaExprConverter(lambda);
            Assert.Throws<NotSupportedException>(() => converter.Convert(blockExpr));
        }

        #endregion
    }
}