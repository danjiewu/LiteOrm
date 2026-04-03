using System;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class TableExprTests
    {
        [Fact]
        public void Constructor_WithType_SetsType()
        {
            var expr = new TableExpr(typeof(TestEntity));

            Assert.Equal(typeof(TestEntity), expr.Type);
        }

        [Fact]
        public void Equals_WithSameValues_ReturnsTrue()
        {
            var left = new TableExpr(typeof(TestEntity)) { Alias = "t", TableArgs = new[] { "A" } };
            var right = new TableExpr(typeof(TestEntity)) { Alias = "t", TableArgs = new[] { "A" } };

            Assert.True(left.Equals(right));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void Equals_WithDifferentTableArgs_ReturnsFalse()
        {
            var left = new TableExpr(typeof(TestEntity)) { TableArgs = new[] { "A" } };
            var right = new TableExpr(typeof(TestEntity)) { TableArgs = new[] { "B" } };

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Clone_CreatesIndependentTableArgsArray()
        {
            var expr = new TableExpr(typeof(TestEntity)) { Alias = "t", TableArgs = new[] { "A", "B" } };
            var clone = (TableExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr.TableArgs, clone.TableArgs);
        }

        [Fact]
        public void ToString_ReturnsTypeName()
        {
            var expr = new TableExpr(typeof(TestEntity));

            Assert.Equal(nameof(TestEntity), expr.ToString());
        }

        private class TestEntity
        {
        }
    }
}
