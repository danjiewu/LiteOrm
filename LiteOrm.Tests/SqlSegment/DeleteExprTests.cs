using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class DeleteExprTests
    {
        [Fact]
        public void Constructor_SetsTableAndWhere()
        {
            var table = new TableExpr(typeof(TestUser));
            var where = Expr.Prop("Id") == 1;
            var expr = new DeleteExpr(table, where);

            Assert.Same(table, expr.Table);
            Assert.Same(where, expr.Where);
            Assert.Equal(ExprType.Delete, expr.ExprType);
        }

        [Fact]
        public void ToString_WithWhere_FormatsDeleteStatement()
        {
            var expr = new DeleteExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Age") > 18);

            var text = expr.ToString();

            Assert.StartsWith("DELETE FROM ", text);
            Assert.Contains(" WHERE ", text);
        }

        [Fact]
        public void ToString_WithoutWhere_FormatsDeleteStatement()
        {
            var expr = new DeleteExpr(new TableExpr(typeof(TestUser)));

            var text = expr.ToString();

            Assert.StartsWith("DELETE FROM ", text);
            Assert.DoesNotContain(" WHERE ", text);
        }

        [Fact]
        public void Clone_CreatesEquivalentCopy()
        {
            var expr = new DeleteExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var clone = (DeleteExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr.Table, clone.Table);
            Assert.NotSame(expr.Where, clone.Where);
        }
    }
}
