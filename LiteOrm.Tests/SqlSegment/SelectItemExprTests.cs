using System;
using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class SelectItemExprTests
    {
        [Fact]
        public void Constructor_Parameterless_CreatesInstance()
        {
            var expr = new SelectItemExpr();

            Assert.Null(expr.Value);
            Assert.Null(expr.Alias);
            Assert.Equal(ExprType.SelectItem, expr.ExprType);
        }

        [Fact]
        public void Constructor_WithValue_SetsValue()
        {
            var value = Expr.Prop("Name");
            var expr = new SelectItemExpr(value);

            Assert.Same(value, expr.Value);
            Assert.Null(expr.Alias);
        }

        [Fact]
        public void Constructor_WithValueAndAlias_SetsBoth()
        {
            var value = Expr.Prop("Name");
            var expr = new SelectItemExpr(value, "UserName");

            Assert.Same(value, expr.Value);
            Assert.Equal("UserName", expr.Alias);
        }

        [Fact]
        public void Constructor_NullValue_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new SelectItemExpr(null!));
        }

        [Fact]
        public void ToString_WithAlias_FormatsCorrectly()
        {
            var expr = new SelectItemExpr(Expr.Prop("Name"), "UserName");

            Assert.Equal("[Name] AS UserName", expr.ToString());
        }

        [Fact]
        public void ToString_WithoutAlias_ReturnsValueOnly()
        {
            var expr = new SelectItemExpr(Expr.Prop("Name"));

            Assert.Equal("[Name]", expr.ToString());
        }

        [Fact]
        public void ToString_WithoutValue_ReturnsEmptyString()
        {
            var expr = new SelectItemExpr { Value = null! };

            Assert.Equal(string.Empty, expr.ToString());
        }

        [Fact]
        public void Equals_SameValueAndAlias_ReturnsTrue()
        {
            var left = new SelectItemExpr(Expr.Prop("Name"), "UserName");
            var right = new SelectItemExpr(Expr.Prop("Name"), "UserName");

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentValue_ReturnsFalse()
        {
            var left = new SelectItemExpr(Expr.Prop("Name"), "Alias");
            var right = new SelectItemExpr(Expr.Prop("Age"), "Alias");

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentAlias_ReturnsFalse()
        {
            var left = new SelectItemExpr(Expr.Prop("Name"), "Alias1");
            var right = new SelectItemExpr(Expr.Prop("Name"), "Alias2");

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new SelectItemExpr(Expr.Prop("Name"));

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var expr = new SelectItemExpr(Expr.Prop("Name"));

            Assert.False(expr.Equals("not an expr"));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var expr = new SelectItemExpr(Expr.Prop("Name"));

            Assert.True(expr.Equals(expr));
        }

        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHash()
        {
            var left = new SelectItemExpr(Expr.Prop("Name"), "UserName");
            var right = new SelectItemExpr(Expr.Prop("Name"), "UserName");

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentAlias_DifferentHash()
        {
            var left = new SelectItemExpr(Expr.Prop("Name"), "Alias1");
            var right = new SelectItemExpr(Expr.Prop("Name"), "Alias2");

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentValue_DifferentHash()
        {
            var left = new SelectItemExpr(Expr.Prop("Name"));
            var right = new SelectItemExpr(Expr.Prop("Age"));

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_NullValue_DoesNotThrow()
        {
            var expr = new SelectItemExpr { Value = null! };

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void Alias_WithInvalidValue_ThrowsArgumentException()
        {
            var expr = new SelectItemExpr();

            Assert.Throws<ArgumentException>(() => expr.Alias = "bad name");
        }

        [Fact]
        public void Alias_SetToNull_Accepted()
        {
            var expr = new SelectItemExpr { Alias = null };

            Assert.Null(expr.Alias);
        }

        [Fact]
        public void Clone_CreatesDeepCopy()
        {
            var expr = new SelectItemExpr(Expr.Prop("Name"), "UserName");
            var clone = (SelectItemExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
            Assert.NotSame(expr.Value, clone.Value);
            Assert.Equal(expr.Alias, clone.Alias);
        }
    }
}
