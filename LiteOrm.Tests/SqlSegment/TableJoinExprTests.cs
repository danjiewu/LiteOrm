using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class TableJoinExprTests
    {
        [Fact]
        public void Constructor_SetsTableAndOn()
        {
            var table = new TableExpr(typeof(TestEntity));
            var on = Expr.Prop("Id") == 1;
            var expr = new TableJoinExpr(table, on);

            Assert.Same(table, expr.Source);
            Assert.Same(on, expr.On);
            Assert.Equal(TableJoinType.Left, expr.JoinType);
        }

        [Fact]
        public void Equals_WithSameValues_ReturnsTrue()
        {
            var left = new TableJoinExpr(new TableExpr(typeof(TestEntity)), Expr.Prop("Id") == 1) { JoinType = TableJoinType.Inner };
            var right = new TableJoinExpr(new TableExpr(typeof(TestEntity)), Expr.Prop("Id") == 1) { JoinType = TableJoinType.Inner };

            Assert.True(left.Equals(right));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void ToString_IncludesJoinTypeAndTable()
        {
            var expr = new TableJoinExpr(new TableExpr(typeof(TestEntity)), Expr.Prop("Id") == 1) { JoinType = TableJoinType.Right };

            var text = expr.ToString();

            Assert.Contains("Right JOIN", text);
            Assert.Contains(nameof(TestEntity), text);
        }

        [Fact]
        public void ToString_WithoutOn_DoesNotAppendOnClause()
        {
            var expr = new TableJoinExpr(new TableExpr(typeof(TestEntity)), null) { JoinType = TableJoinType.Inner };

            Assert.Equal($"Inner JOIN {nameof(TestEntity)}", expr.ToString());
        }

        [Fact]
        public void Clone_CreatesEquivalentCopy()
        {
            var expr = new TableJoinExpr(new TableExpr(typeof(TestEntity)), Expr.Prop("Id") == 1) { JoinType = TableJoinType.Full };

            var clone = (TableJoinExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr.Source, clone.Source);
            Assert.NotSame(expr.On, clone.On);
        }

        private class TestEntity
        {
        }
    }
}
