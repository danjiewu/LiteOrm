using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class NotExprTests
    {
        [Fact]
        public void Constructor_Parameterless_CreatesInstance()
        {
            var expr = new NotExpr();

            Assert.Null(expr.Operand);
            Assert.Equal(ExprType.Not, expr.ExprType);
        }

        [Fact]
        public void Constructor_WithOperand_SetsOperand()
        {
            var operand = Expr.Prop("Id") == new ValueExpr(1);

            var expr = new NotExpr(operand);

            Assert.Same(operand, expr.Operand);
        }

        [Fact]
        public void Constructor_NullOperand_AcceptsNull()
        {
            var expr = new NotExpr(null);

            Assert.Null(expr.Operand);
        }

        [Fact]
        public void ToString_WithOperand_FormatsCorrectly()
        {
            var operand = Expr.Prop("Id") == new ValueExpr(1);
            var expr = new NotExpr(operand);

            Assert.Equal("NOT [Id] = 1", expr.ToString());
        }

        [Fact]
        public void ToString_WithNullOperand_DoesNotThrow()
        {
            var expr = new NotExpr(null);

            var result = expr.ToString();
            Assert.NotNull(result);
        }

        [Fact]
        public void Equals_SameOperand_ReturnsTrue()
        {
            var operand = Expr.Prop("Id") == new ValueExpr(1);
            var left = new NotExpr(operand);
            var right = new NotExpr(operand);

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_EqualOperands_ReturnsTrue()
        {
            var left = new NotExpr(Expr.Prop("Id") == new ValueExpr(1));
            var right = new NotExpr(Expr.Prop("Id") == new ValueExpr(1));

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentOperand_ReturnsFalse()
        {
            var left = new NotExpr(Expr.Prop("Id") == new ValueExpr(1));
            var right = new NotExpr(Expr.Prop("Id") == new ValueExpr(2));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new NotExpr(Expr.Prop("Id") == new ValueExpr(1));

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var expr = new NotExpr(Expr.Prop("Id") == new ValueExpr(1));

            Assert.False(expr.Equals("not an expr"));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var expr = new NotExpr(Expr.Prop("Id") == new ValueExpr(1));

            Assert.True(expr.Equals(expr));
        }

        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHash()
        {
            var left = new NotExpr(Expr.Prop("Id") == new ValueExpr(1));
            var right = new NotExpr(Expr.Prop("Id") == new ValueExpr(1));

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentOperand_DifferentHash()
        {
            var left = new NotExpr(Expr.Prop("Id") == new ValueExpr(1));
            var right = new NotExpr(Expr.Prop("Id") == new ValueExpr(2));

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_NullOperand_DoesNotThrow()
        {
            var expr = new NotExpr(null);

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void Clone_CreatesDeepCopy()
        {
            var expr = new NotExpr(Expr.Prop("Id") == new ValueExpr(1));
            var clone = (NotExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
            Assert.NotSame(expr.Operand, clone.Operand);
        }

        [Fact]
        public void Clone_NullOperand_DoesNotThrow()
        {
            var expr = new NotExpr(null);
            var clone = (NotExpr)expr.Clone();

            Assert.Null(clone.Operand);
        }
    }
}
