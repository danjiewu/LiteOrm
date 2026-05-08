using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class LogicBinaryExprTests
    {
        [Fact]
        public void Constructor_Parameterless_CreatesInstance()
        {
            var expr = new LogicBinaryExpr();

            Assert.Null(expr.Left);
            Assert.Null(expr.Right);
            Assert.Equal(default, expr.Operator);
            Assert.Equal(ExprType.LogicBinary, expr.ExprType);
        }

        [Fact]
        public void Constructor_WithLeftOperatorRight_SetsProperties()
        {
            var left = Expr.Prop("Age");
            var right = new ValueExpr(18);

            var expr = new LogicBinaryExpr(left, LogicOperator.GreaterThan, right);

            Assert.Same(left, expr.Left);
            Assert.Same(right, expr.Right);
            Assert.Equal(LogicOperator.GreaterThan, expr.Operator);
        }

        [Fact]
        public void Constructor_NullOperands_AcceptsNull()
        {
            var expr = new LogicBinaryExpr(null, LogicOperator.Equal, null);

            Assert.Null(expr.Left);
            Assert.Null(expr.Right);
        }

        [Fact]
        public void ToString_WithEqualOperator_FormatsCorrectly()
        {
            var expr = Expr.Prop("Id") == new ValueExpr(1);

            Assert.Equal("[Id] = 1", expr.ToString());
        }

        [Theory]
        [InlineData(LogicOperator.Equal, "=")]
        [InlineData(LogicOperator.GreaterThan, ">")]
        [InlineData(LogicOperator.LessThan, "<")]
        [InlineData(LogicOperator.NotEqual, "!=")]
        [InlineData(LogicOperator.GreaterThanOrEqual, ">=")]
        [InlineData(LogicOperator.LessThanOrEqual, "<=")]
        public void ToString_WithAllSixOperators_FormatsCorrectly(LogicOperator op, string expectedSymbol)
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Age"), op, new ValueExpr(18));

            Assert.Equal($"[Age] {expectedSymbol} 18", expr.ToString());
        }

        [Fact]
        public void ToString_WithInOperator_FallsBackToEnumName()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.In, new ValueExpr(1));

            Assert.Equal("[Id] In 1", expr.ToString());
        }

        [Fact]
        public void ToString_WithLikeOperator_FallsBackToEnumName()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Name"), LogicOperator.Like, new ValueExpr("%test%"));

            Assert.Equal("[Name] Like %test%", expr.ToString());
        }

        [Fact]
        public void Equals_SameValues_ReturnsTrue()
        {
            var left = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));
            var right = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));

            Assert.True(left.Equals(right));
            Assert.True(right.Equals(left));
        }

        [Fact]
        public void Equals_DifferentOperator_ReturnsFalse()
        {
            var left = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));
            var right = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.NotEqual, new ValueExpr(1));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentLeft_ReturnsFalse()
        {
            var left = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));
            var right = new LogicBinaryExpr(Expr.Prop("Age"), LogicOperator.Equal, new ValueExpr(1));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentRight_ReturnsFalse()
        {
            var left = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));
            var right = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(2));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));

            Assert.False(expr.Equals("not an expr"));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));

            Assert.True(expr.Equals(expr));
        }

        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHash()
        {
            var left = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));
            var right = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentOperator_DifferentHash()
        {
            var left = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));
            var right = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.NotEqual, new ValueExpr(1));

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_NullLeft_DoesNotThrow()
        {
            var expr = new LogicBinaryExpr(null, LogicOperator.Equal, new ValueExpr(1));

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void GetHashCode_NullRight_DoesNotThrow()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, null);

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void Clone_CreatesIndependentDeepCopy()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));
            var clone = (LogicBinaryExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
            Assert.NotSame(expr.Left, clone.Left);
            Assert.NotSame(expr.Right, clone.Right);
        }

        [Fact]
        public void Clone_NullOperands_DoesNotThrow()
        {
            var expr = new LogicBinaryExpr(null, LogicOperator.Equal, null);
            var clone = (LogicBinaryExpr)expr.Clone();

            Assert.Null(clone.Left);
            Assert.Null(clone.Right);
        }

        [Fact]
        public void Reverse_Simple_SwapsLeftAndRight()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));
            var reversed = expr.Reverse();

            Assert.Equal(Expr.Prop("Id"), reversed.Right);
            Assert.Equal(new ValueExpr(1), reversed.Left);
            Assert.Equal(LogicOperator.Equal, reversed.Operator);
        }

        [Fact]
        public void Reverse_KeepEquivalent_GreaterThanBecomesLessThan()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Age"), LogicOperator.GreaterThan, new ValueExpr(18));
            var reversed = expr.Reverse(keepEquivalent: true);

            Assert.Equal(new ValueExpr(18), reversed.Left);
            Assert.Equal(Expr.Prop("Age"), reversed.Right);
            Assert.Equal(LogicOperator.LessThan, reversed.Operator);
        }

        [Fact]
        public void Reverse_KeepEquivalent_LessThanBecomesGreaterThan()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Age"), LogicOperator.LessThan, new ValueExpr(18));
            var reversed = expr.Reverse(keepEquivalent: true);

            Assert.Equal(new ValueExpr(18), reversed.Left);
            Assert.Equal(Expr.Prop("Age"), reversed.Right);
            Assert.Equal(LogicOperator.GreaterThan, reversed.Operator);
        }

        [Fact]
        public void Reverse_KeepEquivalent_GreaterThanOrEqualBecomesLessThanOrEqual()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Age"), LogicOperator.GreaterThanOrEqual, new ValueExpr(18));
            var reversed = expr.Reverse(keepEquivalent: true);

            Assert.Equal(LogicOperator.LessThanOrEqual, reversed.Operator);
        }

        [Fact]
        public void Reverse_KeepEquivalent_LessThanOrEqualBecomesGreaterThanOrEqual()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Age"), LogicOperator.LessThanOrEqual, new ValueExpr(18));
            var reversed = expr.Reverse(keepEquivalent: true);

            Assert.Equal(LogicOperator.GreaterThanOrEqual, reversed.Operator);
        }

        [Fact]
        public void Reverse_KeepEquivalent_EqualStaysEqual()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));
            var reversed = expr.Reverse(keepEquivalent: true);

            Assert.Equal(LogicOperator.Equal, reversed.Operator);
        }

        [Fact]
        public void Reverse_KeepEquivalent_NotEqualStaysNotEqual()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.NotEqual, new ValueExpr(1));
            var reversed = expr.Reverse(keepEquivalent: true);

            Assert.Equal(LogicOperator.NotEqual, reversed.Operator);
        }

        [Fact]
        public void Reverse_KeepEquivalent_UnsupportedOperator_Throws()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.In, new ValueExpr(1));

            Assert.Throws<InvalidOperationException>(() => expr.Reverse(keepEquivalent: true));
        }

        [Fact]
        public void OriginOperator_WithEqual_ReturnsEqual()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.Equal, new ValueExpr(1));

            Assert.Equal(LogicOperator.Equal, expr.OriginOperator);
        }

        [Fact]
        public void OriginOperator_WithNotEqual_ReturnsEqual()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.NotEqual, new ValueExpr(1));

            Assert.Equal(LogicOperator.Equal, expr.OriginOperator);
        }

        [Fact]
        public void OriginOperator_WithNotIn_ReturnsIn()
        {
            var expr = new LogicBinaryExpr(Expr.Prop("Id"), LogicOperator.NotIn, new ValueExpr(1));

            Assert.Equal(LogicOperator.In, expr.OriginOperator);
        }
    }
}
