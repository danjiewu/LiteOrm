using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class ValueBinaryExprTests
    {
        [Fact]
        public void Constructor_Parameterless_CreatesInstance()
        {
            var expr = new ValueBinaryExpr();

            Assert.Null(expr.Left);
            Assert.Null(expr.Right);
            Assert.Equal(default, expr.Operator);
            Assert.Equal(ExprType.ValueBinary, expr.ExprType);
        }

        [Fact]
        public void Constructor_WithLeftOperatorRight_SetsProperties()
        {
            var left = Expr.Prop("Price");
            var right = new ValueExpr(10);

            var expr = new ValueBinaryExpr(left, ValueOperator.Add, right);

            Assert.Same(left, expr.Left);
            Assert.Same(right, expr.Right);
            Assert.Equal(ValueOperator.Add, expr.Operator);
        }

        [Fact]
        public void Constructor_NullOperands_AcceptsNull()
        {
            var expr = new ValueBinaryExpr(null, ValueOperator.Add, null);

            Assert.Null(expr.Left);
            Assert.Null(expr.Right);
        }

        [Theory]
        [InlineData(ValueOperator.Add, "+")]
        [InlineData(ValueOperator.Subtract, "-")]
        [InlineData(ValueOperator.Multiply, "*")]
        [InlineData(ValueOperator.Divide, "/")]
        [InlineData(ValueOperator.Concat, "||")]
        public void ToString_WithAllOperators_FormatsCorrectly(ValueOperator op, string expectedSymbol)
        {
            var expr = new ValueBinaryExpr(Expr.Prop("X"), op, new ValueExpr(1));

            Assert.Equal($"[X] {expectedSymbol} 1", expr.ToString());
        }

        [Fact]
        public void ToString_WithModulo_FallsBackToEnumName()
        {
            var expr = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Modulo, new ValueExpr(2));

            Assert.Equal("[X] Modulo 2", expr.ToString());
        }

        [Fact]
        public void Equals_SameValues_ReturnsTrue()
        {
            var left = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Add, new ValueExpr(1));
            var right = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Add, new ValueExpr(1));

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentOperator_ReturnsFalse()
        {
            var left = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Add, new ValueExpr(1));
            var right = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Subtract, new ValueExpr(1));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentLeft_ReturnsFalse()
        {
            var left = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Add, new ValueExpr(1));
            var right = new ValueBinaryExpr(Expr.Prop("Y"), ValueOperator.Add, new ValueExpr(1));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentRight_ReturnsFalse()
        {
            var left = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Add, new ValueExpr(1));
            var right = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Add, new ValueExpr(2));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Add, new ValueExpr(1));

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var expr = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Add, new ValueExpr(1));

            Assert.False(expr.Equals("not an expr"));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var expr = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Add, new ValueExpr(1));

            Assert.True(expr.Equals(expr));
        }

        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHash()
        {
            var left = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Add, new ValueExpr(1));
            var right = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Add, new ValueExpr(1));

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentOperator_DifferentHash()
        {
            var left = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Add, new ValueExpr(1));
            var right = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Subtract, new ValueExpr(1));

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_NullLeft_DoesNotThrow()
        {
            var expr = new ValueBinaryExpr(null, ValueOperator.Add, new ValueExpr(1));

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void GetHashCode_NullRight_DoesNotThrow()
        {
            var expr = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Add, null);

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void Clone_CreatesIndependentDeepCopy()
        {
            var expr = new ValueBinaryExpr(Expr.Prop("X"), ValueOperator.Multiply, new ValueExpr(2));
            var clone = (ValueBinaryExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
            Assert.NotSame(expr.Left, clone.Left);
            Assert.NotSame(expr.Right, clone.Right);
        }

        [Fact]
        public void Clone_NullOperands_DoesNotThrow()
        {
            var expr = new ValueBinaryExpr(null, ValueOperator.Add, null);
            var clone = (ValueBinaryExpr)expr.Clone();

            Assert.Null(clone.Left);
            Assert.Null(clone.Right);
        }
    }
}
