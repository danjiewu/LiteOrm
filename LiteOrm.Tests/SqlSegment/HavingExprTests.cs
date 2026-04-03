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
        public void ToString_FormatsSourceAndHaving()
        {
            var source = new GroupByExpr();
            var having = Expr.Prop("Count") > 1;
            var expr = new HavingExpr(source, having);

            Assert.Contains("HAVING", expr.ToString());
            Assert.Contains(having.ToString(), expr.ToString());
        }

        [Fact]
        public void Clone_CreatesEquivalentCopy()
        {
            var expr = new HavingExpr(new GroupByExpr(), Expr.Prop("Count") > 1);
            var clone = (HavingExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr.Having, clone.Having);
        }
    }
}
