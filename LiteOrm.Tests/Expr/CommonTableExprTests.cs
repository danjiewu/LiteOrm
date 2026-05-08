using System;
using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class CommonTableExprTests
    {
        [Fact]
        public void Constructor_Parameterless_CreatesInstance()
        {
            var expr = new CommonTableExpr();

            Assert.Null(expr.Source);
            Assert.Null(expr.Alias);
            Assert.Equal(ExprType.CommonTable, expr.ExprType);
        }

        [Fact]
        public void Constructor_WithSelectExpr_SetsSource()
        {
            var selectExpr = new SelectExpr(null, Expr.Prop("Id").As("Id"));
            var expr = new CommonTableExpr(selectExpr);

            Assert.Same(selectExpr, expr.Source);
        }

        [Fact]
        public void Alias_DelegatesToSourceAlias()
        {
            var selectExpr = new SelectExpr(null, Expr.Prop("Id").As("Id")) { Alias = "MyCTE" };
            var expr = new CommonTableExpr(selectExpr);

            Assert.Equal("MyCTE", expr.Alias);
        }

        [Fact]
        public void Alias_Set_DelegatesToSourceAlias()
        {
            var selectExpr = new SelectExpr(null, Expr.Prop("Id").As("Id"));
            var expr = new CommonTableExpr(selectExpr);
            expr.Alias = "NewAlias";

            Assert.Equal("NewAlias", selectExpr.Alias);
        }

        [Fact]
        public void Alias_WithInvalidValue_ThrowsArgumentException()
        {
            var expr = new CommonTableExpr(new SelectExpr());

            Assert.Throws<ArgumentException>(() => expr.Alias = "bad name");
        }

        [Fact]
        public void Equals_SameSource_ReturnsTrue()
        {
            var select1 = new SelectExpr(null, Expr.Prop("Id").As("Id"));
            var left = new CommonTableExpr(select1);
            var right = new CommonTableExpr(select1);

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_EqualSources_ReturnsTrue()
        {
            var left = new CommonTableExpr(new SelectExpr(null, Expr.Prop("Id").As("Id")));
            var right = new CommonTableExpr(new SelectExpr(null, Expr.Prop("Id").As("Id")));

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentSource_ReturnsFalse()
        {
            var left = new CommonTableExpr(new SelectExpr(null, Expr.Prop("Id").As("Id")));
            var right = new CommonTableExpr(new SelectExpr(null, Expr.Prop("Name").As("Name")));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new CommonTableExpr(new SelectExpr());

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var expr = new CommonTableExpr(new SelectExpr());

            Assert.False(expr.Equals("not an expr"));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var expr = new CommonTableExpr(new SelectExpr());

            Assert.True(expr.Equals(expr));
        }

        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHash()
        {
            var left = new CommonTableExpr(new SelectExpr(null, Expr.Prop("Id").As("Id")));
            var right = new CommonTableExpr(new SelectExpr(null, Expr.Prop("Id").As("Id")));

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentSource_DifferentHash()
        {
            var left = new CommonTableExpr(new SelectExpr(null, Expr.Prop("Id").As("Id")));
            var right = new CommonTableExpr(new SelectExpr(null, Expr.Prop("Name").As("Name")));

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_NullSource_DoesNotThrow()
        {
            var expr = new CommonTableExpr();

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void Clone_CreatesDeepCopy()
        {
            var selectExpr = new SelectExpr(null, Expr.Prop("Id").As("Id")) { Alias = "MyCTE" };
            var expr = new CommonTableExpr(selectExpr);
            var clone = (CommonTableExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
            Assert.NotSame(expr.Source, clone.Source);
        }

        [Fact]
        public void Clone_NullSource_DoesNotThrow()
        {
            var expr = new CommonTableExpr();
            var clone = (CommonTableExpr)expr.Clone();

            Assert.Null(clone.Source);
        }

        [Fact]
        public void ToString_WithSourceAndAlias_FormatsCorrectly()
        {
            var selectExpr = new SelectExpr(null, Expr.Prop("Id").As("Id")) { Alias = "MyCTE" };
            var expr = new CommonTableExpr(selectExpr);

            var result = expr.ToString();

            Assert.Contains("MyCTE", result);
            Assert.Contains(" AS ", result);
        }

        [Fact]
        public void ExprType_ReturnsCommonTable()
        {
            var expr = new CommonTableExpr();

            Assert.Equal(ExprType.CommonTable, expr.ExprType);
        }

        [Fact]
        public void SelectExpr_With_Extension_CreatesFromExprWithCte()
        {
            var selectExpr = new SelectExpr(null, Expr.Prop("Id").As("Id"));
            var from = selectExpr.With("MyCTE");

            Assert.IsType<FromExpr>(from);
            var cte = Assert.IsType<CommonTableExpr>(from.Source);
            Assert.Equal("MyCTE", cte.Alias);
            Assert.Same(selectExpr, cte.Source);
        }

    }
}
