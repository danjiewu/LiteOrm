using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class UnaryExprTests
    {
        [Fact]
        public void Constructor_SetsOperatorAndOperand()
        {
            var operand = new ValueExpr(42);
            var expr = new UnaryExpr(UnaryOperator.Nagive, operand);

            Assert.Equal(UnaryOperator.Nagive, expr.Operator);
            Assert.Same(operand, expr.Operand);
        }

        [Fact]
        public void ToString_WithNagiveOperator_ReturnsMinusPrefix()
        {
            var expr = new UnaryExpr(UnaryOperator.Nagive, new ValueExpr(42));

            Assert.Equal("-42", expr.ToString());
        }

        [Fact]
        public void Clone_CreatesEquivalentCopy()
        {
            var expr = new UnaryExpr(UnaryOperator.BitwiseNot, new ValueExpr(1));
            var clone = (UnaryExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr.Operand, clone.Operand);
        }

        [Fact]
        public void GetHashCode_ForEqualObjects_IsEqual()
        {
            var left = new UnaryExpr(UnaryOperator.BitwiseNot, new ValueExpr(1));
            var right = new UnaryExpr(UnaryOperator.BitwiseNot, new ValueExpr(1));

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }
    }
}
