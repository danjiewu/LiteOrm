using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class SelectExprTests
    {
        [Fact]
        public void SelectItemExpr_WithValue_SetsValue()
        {
            var value = Expr.Prop("Id");
            var item = new SelectItemExpr(value);

            Assert.Same(value, item.Value);
            Assert.Null(item.Alias);
        }

        [Fact]
        public void SelectItemExpr_WithAlias_FormatsToString()
        {
            var item = new SelectItemExpr(Expr.Prop("Id"), "UserId");

            Assert.Equal("[Id] AS UserId", item.ToString());
        }

        [Fact]
        public void SelectItemExpr_WithInvalidAlias_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new SelectItemExpr(Expr.Prop("Id"), "bad alias"));
        }

        [Fact]
        public void SelectExpr_ConstructorWithValueExpressions_WrapsIntoSelectItems()
        {
            var source = new FromExpr(typeof(TestUser));
            var select = new SelectExpr(source, Expr.Prop("Id"), Expr.Prop("Name"));

            Assert.Equal(2, select.Selects.Count);
            Assert.Equal("SELECT [Id], [Name] FROM TestUser", select.ToString());
        }

        [Fact]
        public void SelectExpr_Equals_UsesSourceSelectsAndAlias()
        {
            var left = new SelectExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id")) { Alias = "q" };
            var right = new SelectExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id")) { Alias = "q" };

            Assert.True(left.Equals(right));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void SelectExpr_Clone_CreatesEquivalentCopy()
        {
            var select = new SelectExpr(new FromExpr(typeof(TestUser)), new SelectItemExpr(Expr.Prop("Id"), "UserId")) { Alias = "q" };
            var clone = (SelectExpr)select.Clone();

            Assert.Equal(select, clone);
            Assert.NotSame(select.Selects[0], clone.Selects[0]);
        }
    }
}
