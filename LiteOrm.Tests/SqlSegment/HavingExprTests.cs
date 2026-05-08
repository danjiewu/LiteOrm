using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class HavingExprTests
    {
        [Fact]
        public void Constructor_SetsSourceAndHaving()
        {
            var source = new GroupByExpr();
            var having = Expr.Prop("Count") > 1;
            var expr = new HavingExpr(source, having);

            Assert.Same(source, expr.Source);
            Assert.Same(having, expr.Having);
            Assert.Equal(ExprType.Having, expr.ExprType);
        }

        [Fact]
        public void Constructor_Parameterless_CreatesInstance()
        {
            var expr = new HavingExpr();

            Assert.Null(expr.Source);
            Assert.Null(expr.Having);
            Assert.Equal(ExprType.Having, expr.ExprType);
        }

        [Fact]
        public void Constructor_NullSource_Accepted()
        {
            var having = Expr.Prop("Count") > 1;
            var expr = new HavingExpr(null!, having);

            Assert.Null(expr.Source);
            Assert.Same(having, expr.Having);
        }

        [Fact]
        public void Constructor_NullHaving_Accepted()
        {
            var source = new GroupByExpr();
            var expr = new HavingExpr(source, null!);

            Assert.Same(source, expr.Source);
            Assert.Null(expr.Having);
        }

        [Fact]
        public void ToString_FormatsSourceAndHaving()
        {
            var source = new GroupByExpr();
            var having = Expr.Prop("Count") > 1;
            var expr = new HavingExpr(source, having);

            Assert.Contains("HAVING", expr.ToString());
            Assert.Contains(having.ToString(), expr.ToString());
        }

        [Fact]
        public void ToString_NullSourceNonNullHaving_ReturnsHavingOnly()
        {
            var having = Expr.Prop("Count") > 1;
            var expr = new HavingExpr(null!, having);

            Assert.StartsWith("HAVING", expr.ToString());
        }

        [Fact]
        public void ToString_NonNullSourceNullHaving_ReturnsSource()
        {
            var source = new GroupByExpr();
            var expr = new HavingExpr(source, null!);

            Assert.Equal(source.ToString(), expr.ToString());
        }

        [Fact]
        public void ToString_NullSourceNullHaving_ReturnsEmpty()
        {
            var expr = new HavingExpr();

            Assert.Equal(string.Empty, expr.ToString());
        }

        [Fact]
        public void Clone_CreatesEquivalentCopy()
        {
            var expr = new HavingExpr(new GroupByExpr(), Expr.Prop("Count") > 1);
            var clone = (HavingExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr.Having, clone.Having);
        }

        [Fact]
        public void Clone_NullSourceAndNullHaving_DoesNotThrow()
        {
            var expr = new HavingExpr();
            var clone = (HavingExpr)expr.Clone();

            Assert.Null(clone.Source);
            Assert.Null(clone.Having);
        }

        [Fact]
        public void Equals_SameValues_ReturnsTrue()
        {
            var left = new HavingExpr(new GroupByExpr(), Expr.Prop("Count") > 1);
            var right = new HavingExpr(new GroupByExpr(), Expr.Prop("Count") > 1);

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentSource_ReturnsFalse()
        {
            var source1 = new GroupByExpr(new FromExpr(typeof(TestUser)));
            var source2 = new GroupByExpr(new FromExpr(typeof(TestDepartment)));
            var left = new HavingExpr(source1, Expr.Prop("Count") > 1);
            var right = new HavingExpr(source2, Expr.Prop("Count") > 1);

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentHaving_ReturnsFalse()
        {
            var left = new HavingExpr(new GroupByExpr(), Expr.Prop("Count") > 1);
            var right = new HavingExpr(new GroupByExpr(), Expr.Prop("Count") > 2);

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new HavingExpr(new GroupByExpr(), Expr.Prop("Count") > 1);

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var expr = new HavingExpr(new GroupByExpr(), Expr.Prop("Count") > 1);

            Assert.False(expr.Equals("not an expr"));
        }

        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHash()
        {
            var left = new HavingExpr(new GroupByExpr(), Expr.Prop("Count") > 1);
            var right = new HavingExpr(new GroupByExpr(), Expr.Prop("Count") > 1);

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentSource_DifferentHash()
        {
            var source1 = new GroupByExpr(new FromExpr(typeof(TestUser)));
            var source2 = new GroupByExpr(new FromExpr(typeof(TestDepartment)));
            var left = new HavingExpr(source1, Expr.Prop("Count") > 1);
            var right = new HavingExpr(source2, Expr.Prop("Count") > 1);

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentHaving_DifferentHash()
        {
            var left = new HavingExpr(new GroupByExpr(), Expr.Prop("Count") > 1);
            var right = new HavingExpr(new GroupByExpr(), Expr.Prop("Count") > 2);

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_NullSource_DoesNotThrow()
        {
            var expr = new HavingExpr(null!, Expr.Prop("Count") > 1);

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void GetHashCode_NullHaving_DoesNotThrow()
        {
            var expr = new HavingExpr(new GroupByExpr(), null!);

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void Clone_DeepCopy_ModifyingCloneDoesNotAffectOriginal()
        {
            var expr = new HavingExpr(new GroupByExpr(), Expr.Prop("Count") > 1);
            var clone = (HavingExpr)expr.Clone();

            clone.Having = Expr.Prop("Count") > 10;

            Assert.NotEqual(expr.Having, clone.Having);
        }

        private class TestUser { }
        private class TestDepartment { }
    }
}
