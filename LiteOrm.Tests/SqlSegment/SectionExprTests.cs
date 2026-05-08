using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class SectionExprTests
    {
        [Fact]
        public void Constructor_Parameterless_CreatesInstance()
        {
            var expr = new SectionExpr();

            Assert.Equal(0, expr.Skip);
            Assert.Equal(0, expr.Take);
            Assert.Null(expr.Source);
            Assert.Equal(ExprType.Section, expr.ExprType);
        }

        [Fact]
        public void Constructor_WithSkipTake_SetsProperties()
        {
            var expr = new SectionExpr(10, 20);

            Assert.Equal(10, expr.Skip);
            Assert.Equal(20, expr.Take);
            Assert.Null(expr.Source);
        }

        [Fact]
        public void Constructor_WithSourceSkipTake_SetsAllProperties()
        {
            var source = new FromExpr(typeof(TestUser));
            var expr = new SectionExpr(source, 5, 15);

            Assert.Same(source, expr.Source);
            Assert.Equal(5, expr.Skip);
            Assert.Equal(15, expr.Take);
        }

        [Fact]
        public void ExprType_ReturnsSection()
        {
            var expr = new SectionExpr();

            Assert.Equal(ExprType.Section, expr.ExprType);
        }

        [Fact]
        public void ToString_NoSourceSkipZero_ReturnsTakeOnly()
        {
            var expr = new SectionExpr(0, 20);

            Assert.Equal("TAKE 20", expr.ToString());
        }

        [Fact]
        public void ToString_NoSourceNonZeroSkip_ReturnsSkipTake()
        {
            var expr = new SectionExpr(10, 20);

            Assert.Equal("SKIP 10 TAKE 20", expr.ToString());
        }

        [Fact]
        public void ToString_WithSourceSkipZero_ReturnsSourceTake()
        {
            var source = new FromExpr(typeof(TestUser));
            var expr = new SectionExpr(source, 0, 20);

            Assert.Contains("TAKE 20", expr.ToString());
        }

        [Fact]
        public void ToString_WithSourceNonZeroSkip_ReturnsFull()
        {
            var source = new FromExpr(typeof(TestUser));
            var expr = new SectionExpr(source, 10, 20);

            Assert.Contains("SKIP 10 TAKE 20", expr.ToString());
        }

        [Fact]
        public void Equals_SameValues_ReturnsTrue()
        {
            var left = new SectionExpr(new FromExpr(typeof(TestUser)), 10, 20);
            var right = new SectionExpr(new FromExpr(typeof(TestUser)), 10, 20);

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentSkip_ReturnsFalse()
        {
            var left = new SectionExpr(10, 20);
            var right = new SectionExpr(5, 20);

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentTake_ReturnsFalse()
        {
            var left = new SectionExpr(10, 20);
            var right = new SectionExpr(10, 30);

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentSource_ReturnsFalse()
        {
            var left = new SectionExpr(new FromExpr(typeof(TestUser)), 10, 20);
            var right = new SectionExpr(new FromExpr(typeof(TestDepartment)), 10, 20);

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new SectionExpr(10, 20);

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var expr = new SectionExpr(10, 20);

            Assert.False(expr.Equals("not an expr"));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var expr = new SectionExpr(10, 20);

            Assert.True(expr.Equals(expr));
        }

        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHash()
        {
            var left = new SectionExpr(new FromExpr(typeof(TestUser)), 10, 20);
            var right = new SectionExpr(new FromExpr(typeof(TestUser)), 10, 20);

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentSkip_DifferentHash()
        {
            var left = new SectionExpr(10, 20);
            var right = new SectionExpr(5, 20);

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentTake_DifferentHash()
        {
            var left = new SectionExpr(10, 20);
            var right = new SectionExpr(10, 30);

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_NullSource_DoesNotThrow()
        {
            var expr = new SectionExpr(10, 20);

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void Clone_CreatesDeepCopy()
        {
            var expr = new SectionExpr(new FromExpr(typeof(TestUser)), 10, 20);
            var clone = (SectionExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
            Assert.NotSame(expr.Source, clone.Source);
            Assert.Equal(expr.Skip, clone.Skip);
            Assert.Equal(expr.Take, clone.Take);
        }

        [Fact]
        public void Clone_NullSource_DoesNotThrow()
        {
            var expr = new SectionExpr(10, 20);
            var clone = (SectionExpr)expr.Clone();

            Assert.Null(clone.Source);
            Assert.Equal(10, clone.Skip);
            Assert.Equal(20, clone.Take);
        }

        private class TestUser { }
        private class TestDepartment { }
    }
}
