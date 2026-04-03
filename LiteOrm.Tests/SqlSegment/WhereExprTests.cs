using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class WhereExprTests
    {
        [Fact]
        public void Constructor_SetsSourceAndWhere()
        {
            var source = new FromExpr(typeof(TestUser));
            var where = Expr.Prop("Id") == 1;
            var expr = new WhereExpr(source, where);

            Assert.Same(source, expr.Source);
            Assert.Same(where, expr.Where);
            Assert.Equal(ExprType.Where, expr.ExprType);
        }

        [Fact]
        public void Equals_WithSameValues_ReturnsTrue()
        {
            var left = new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var right = new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id") == 1);

            Assert.True(left.Equals(right));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void ToString_FormatsWhereClause()
        {
            var expr = new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Age") > 18);

            Assert.Contains(" WHERE ", expr.ToString());
        }
    }
}
