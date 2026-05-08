using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class OrderByItemExprTests
    {
        [Fact]
        public void Constructor_Parameterless_CreatesInstance()
        {
            var expr = new OrderByItemExpr();

            Assert.Null(expr.Field);
            Assert.True(expr.Ascending);
            Assert.Equal(ExprType.OrderByItem, expr.ExprType);
        }

        [Fact]
        public void Constructor_WithFieldAndAscending_SetsProperties()
        {
            var field = Expr.Prop("Name");

            var expr = new OrderByItemExpr(field, false);

            Assert.Same(field, expr.Field);
            Assert.False(expr.Ascending);
        }

        [Fact]
        public void Constructor_AscendingDefaultsToTrue()
        {
            var expr = new OrderByItemExpr(Expr.Prop("Name"));

            Assert.True(expr.Ascending);
        }

        [Fact]
        public void ToString_Ascending_NoDescSuffix()
        {
            var expr = new OrderByItemExpr(Expr.Prop("Name"), true);

            Assert.Equal("[Name]", expr.ToString());
        }

        [Fact]
        public void ToString_Descending_DescSuffix()
        {
            var expr = new OrderByItemExpr(Expr.Prop("Name"), false);

            Assert.Equal("[Name] DESC", expr.ToString());
        }

        [Fact]
        public void ToString_NullField_DoesNotThrow()
        {
            var expr = new OrderByItemExpr(null, true);

            var result = expr.ToString();
            Assert.NotNull(result);
        }

        [Fact]
        public void Equals_SameFieldAndAscending_ReturnsTrue()
        {
            var left = new OrderByItemExpr(Expr.Prop("Name"), true);
            var right = new OrderByItemExpr(Expr.Prop("Name"), true);

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentField_ReturnsFalse()
        {
            var left = new OrderByItemExpr(Expr.Prop("Name"), true);
            var right = new OrderByItemExpr(Expr.Prop("Age"), true);

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentAscending_ReturnsFalse()
        {
            var left = new OrderByItemExpr(Expr.Prop("Name"), true);
            var right = new OrderByItemExpr(Expr.Prop("Name"), false);

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new OrderByItemExpr(Expr.Prop("Name"), true);

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var expr = new OrderByItemExpr(Expr.Prop("Name"), true);

            Assert.False(expr.Equals("not an expr"));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var expr = new OrderByItemExpr(Expr.Prop("Name"), true);

            Assert.True(expr.Equals(expr));
        }

        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHash()
        {
            var left = new OrderByItemExpr(Expr.Prop("Name"), true);
            var right = new OrderByItemExpr(Expr.Prop("Name"), true);

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentField_DifferentHash()
        {
            var left = new OrderByItemExpr(Expr.Prop("Name"), true);
            var right = new OrderByItemExpr(Expr.Prop("Age"), true);

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentAscending_DifferentHash()
        {
            var left = new OrderByItemExpr(Expr.Prop("Name"), true);
            var right = new OrderByItemExpr(Expr.Prop("Name"), false);

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_NullField_DoesNotThrow()
        {
            var expr = new OrderByItemExpr(null, true);

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void Clone_CreatesDeepCopy()
        {
            var expr = new OrderByItemExpr(Expr.Prop("Name"), false);
            var clone = (OrderByItemExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
            Assert.NotSame(expr.Field, clone.Field);
            Assert.Equal(expr.Ascending, clone.Ascending);
        }

        [Fact]
        public void Clone_NullField_DoesNotThrow()
        {
            var expr = new OrderByItemExpr(null, true);
            var clone = (OrderByItemExpr)expr.Clone();

            Assert.Null(clone.Field);
            Assert.True(clone.Ascending);
        }

        [Fact]
        public void ImplicitConversion_FromValueTypeExprBoolTuple()
        {
            OrderByItemExpr expr = (Expr.Prop("Name"), false);

            Assert.Equal(Expr.Prop("Name"), expr.Field);
            Assert.False(expr.Ascending);
        }

        [Fact]
        public void ImplicitConversion_FromStringBoolTuple()
        {
            OrderByItemExpr expr = ("Name", false);

            Assert.Equal(Expr.Prop("Name").PropertyName, ((PropertyExpr)expr.Field).PropertyName);
            Assert.False(expr.Ascending);
        }

        [Fact]
        public void ImplicitConversion_ToValueTypeExprBoolTuple()
        {
            var expr = new OrderByItemExpr(Expr.Prop("Name"), false);
            (ValueTypeExpr, bool) tuple = expr;

            Assert.Equal(Expr.Prop("Name"), tuple.Item1);
            Assert.False(tuple.Item2);
        }
    }
}
