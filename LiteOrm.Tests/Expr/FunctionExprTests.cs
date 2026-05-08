using System;
using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class FunctionExprTests
    {
        [Fact]
        public void Constructor_Parameterless_CreatesInstance()
        {
            var expr = new FunctionExpr();

            Assert.Null(expr.FunctionName);
            Assert.Empty(expr.Args);
            Assert.False(expr.IsAggregate);
            Assert.Equal(ExprType.Function, expr.ExprType);
        }

        [Fact]
        public void Constructor_WithNameAndParams_SetsProperties()
        {
            var arg1 = Expr.Prop("Id");
            var arg2 = new ValueExpr(1);

            var expr = new FunctionExpr("SUM", arg1, arg2);

            Assert.Equal("SUM", expr.FunctionName);
            Assert.Equal(2, expr.Args.Count);
            Assert.Same(arg1, expr.Args[0]);
            Assert.Same(arg2, expr.Args[1]);
        }

        [Fact]
        public void Constructor_WithNameAndNoParams_CreatesEmptyArgs()
        {
            var expr = new FunctionExpr("NOW");

            Assert.Equal("NOW", expr.FunctionName);
            Assert.Empty(expr.Args);
        }

        [Fact]
        public void Constructor_InvalidName_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new FunctionExpr("bad name", Expr.Prop("Id")));
        }

        [Fact]
        public void FunctionName_SetToNull_IsAccepted()
        {
            var expr = new FunctionExpr("SUM") { FunctionName = null };

            Assert.Null(expr.FunctionName);
        }

        [Fact]
        public void FunctionName_SetToValidValue_Updates()
        {
            var expr = new FunctionExpr("SUM") { FunctionName = "COUNT" };

            Assert.Equal("COUNT", expr.FunctionName);
        }

        [Fact]
        public void FunctionName_SetToInvalidValue_ThrowsArgumentException()
        {
            var expr = new FunctionExpr("SUM");
            Assert.Throws<ArgumentException>(() => expr.FunctionName = "bad name");
        }

        [Fact]
        public void IsAggregate_Default_False()
        {
            var expr = new FunctionExpr("NOW");

            Assert.False(expr.IsAggregate);
        }

        [Fact]
        public void IsAggregate_SetToTrue_Updates()
        {
            var expr = new FunctionExpr("COUNT") { IsAggregate = true };

            Assert.True(expr.IsAggregate);
        }

        [Fact]
        public void ExprType_ReturnsFunction()
        {
            var expr = new FunctionExpr();

            Assert.Equal(ExprType.Function, expr.ExprType);
        }

        [Fact]
        public void ToString_WithArgs_FormatsAsFunctionCall()
        {
            var expr = new FunctionExpr("SUM", Expr.Prop("Price"));

            Assert.Equal("SUM([Price])", expr.ToString());
        }

        [Fact]
        public void ToString_WithMultipleArgs_FormatsCommaSeparated()
        {
            var expr = new FunctionExpr("COALESCE", Expr.Prop("Name"), new ValueExpr("default"));

            Assert.Equal("COALESCE([Name],default)", expr.ToString());
        }

        [Fact]
        public void ToString_WithoutArgs_FormatsWithEmptyParens()
        {
            var expr = new FunctionExpr("NOW");

            Assert.Equal("NOW()", expr.ToString());
        }

        [Fact]
        public void ToString_WithNullFunctionName_DoesNotThrow()
        {
            var expr = new FunctionExpr();
            var result = expr.ToString();

            Assert.NotNull(result);
        }

        [Fact]
        public void Equals_SameNameAndArgs_ReturnsTrue()
        {
            var left = new FunctionExpr("SUM", Expr.Prop("Price"));
            var right = new FunctionExpr("SUM", Expr.Prop("Price"));

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentName_ReturnsFalse()
        {
            var left = new FunctionExpr("SUM", Expr.Prop("Price"));
            var right = new FunctionExpr("COUNT", Expr.Prop("Price"));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentArgs_ReturnsFalse()
        {
            var left = new FunctionExpr("SUM", Expr.Prop("Price"));
            var right = new FunctionExpr("SUM", Expr.Prop("Amount"));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentArgCount_ReturnsFalse()
        {
            var left = new FunctionExpr("COALESCE", Expr.Prop("Name"), new ValueExpr("x"));
            var right = new FunctionExpr("COALESCE", Expr.Prop("Name"));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentIsAggregate_ReturnsFalse()
        {
            var left = new FunctionExpr("COUNT") { IsAggregate = true };
            var right = new FunctionExpr("COUNT") { IsAggregate = false };

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new FunctionExpr("SUM", Expr.Prop("Price"));

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var expr = new FunctionExpr("SUM", Expr.Prop("Price"));

            Assert.False(expr.Equals("not an expr"));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var expr = new FunctionExpr("SUM", Expr.Prop("Price"));

            Assert.True(expr.Equals(expr));
        }

        [Fact]
        public void GetHashCode_EqualFunctions_ReturnsSameHash()
        {
            var left = new FunctionExpr("SUM", Expr.Prop("Price"));
            var right = new FunctionExpr("SUM", Expr.Prop("Price"));

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentName_DifferentHash()
        {
            var left = new FunctionExpr("SUM", Expr.Prop("Price"));
            var right = new FunctionExpr("COUNT", Expr.Prop("Price"));

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentIsAggregate_DifferentHash()
        {
            var left = new FunctionExpr("COUNT") { IsAggregate = true };
            var right = new FunctionExpr("COUNT") { IsAggregate = false };

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentArgs_DifferentHash()
        {
            var left = new FunctionExpr("SUM", Expr.Prop("Price"));
            var right = new FunctionExpr("SUM", Expr.Prop("Amount"));

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void Clone_CreatesIndependentDeepCopy()
        {
            var expr = new FunctionExpr("SUM", Expr.Prop("Price")) { IsAggregate = true };
            var clone = (FunctionExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
            Assert.NotSame(expr.Args, clone.Args);
            Assert.NotSame(expr.Args[0], clone.Args[0]);
            Assert.Equal(expr.IsAggregate, clone.IsAggregate);
        }

        [Fact]
        public void Clone_EmptyArgs_DoesNotThrow()
        {
            var expr = new FunctionExpr("NOW");
            var clone = (FunctionExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.Empty(clone.Args);
        }

        [Fact]
        public void Clone_PreservesIsAggregate()
        {
            var expr = new FunctionExpr("COUNT") { IsAggregate = true };
            var clone = (FunctionExpr)expr.Clone();

            Assert.True(clone.IsAggregate);
        }
    }
}
