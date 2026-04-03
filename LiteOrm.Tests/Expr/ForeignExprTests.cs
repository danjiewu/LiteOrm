using System;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class ForeignExprTests
    {
        [Fact]
        public void Constructor_WithForeignAndExpr_InitializesProperties()
        {
            var inner = Expr.Prop("Id") == 1;
            var expr = new ForeignExpr(typeof(TestEntity), inner, "Shard1");

            Assert.Equal(typeof(TestEntity), expr.Foreign);
            Assert.Same(inner, expr.InnerExpr);
            Assert.Equal(new[] { "Shard1" }, expr.TableArgs);
        }

        [Fact]
        public void ToString_UsesForeignTypeName()
        {
            var expr = new ForeignExpr(typeof(TestEntity), Expr.Prop("Id") == 1);

            Assert.Contains(nameof(TestEntity), expr.ToString());
        }

        [Fact]
        public void Clone_CreatesIndependentCopy()
        {
            var expr = new ForeignExpr(typeof(TestEntity), "t", Expr.Prop("Id") == 1, "A");
            var clone = (ForeignExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr.TableArgs, clone.TableArgs);
        }

        [Fact]
        public void Equals_WithDifferentTableArgs_ReturnsFalse()
        {
            var left = new ForeignExpr(typeof(TestEntity), Expr.Prop("Id") == 1, "A");
            var right = new ForeignExpr(typeof(TestEntity), Expr.Prop("Id") == 1, "B");

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Alias_WithInvalidSqlName_ThrowsArgumentException()
        {
            var expr = new ForeignExpr();

            Assert.Throws<ArgumentException>(() => expr.Alias = "bad name");
        }

        private class TestEntity
        {
            public int Id { get; set; }
        }
    }
}
