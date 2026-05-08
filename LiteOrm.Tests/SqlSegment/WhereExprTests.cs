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
        public void Constructor_Parameterless_CreatesInstance()
        {
            var expr = new WhereExpr();

            Assert.Null(expr.Source);
            Assert.Null(expr.Where);
            Assert.Equal(ExprType.Where, expr.ExprType);
        }

        [Fact]
        public void Constructor_NullSource_Accepted()
        {
            var where = Expr.Prop("Id") == 1;
            var expr = new WhereExpr(null!, where);

            Assert.Null(expr.Source);
            Assert.Same(where, expr.Where);
        }

        [Fact]
        public void Constructor_NullWhere_Accepted()
        {
            var source = new FromExpr(typeof(TestUser));
            var expr = new WhereExpr(source, null!);

            Assert.Same(source, expr.Source);
            Assert.Null(expr.Where);
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
        public void Equals_DifferentSource_ReturnsFalse()
        {
            var left = new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var right = new WhereExpr(new FromExpr(typeof(TestDepartment)), Expr.Prop("Id") == 1);

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentWhere_ReturnsFalse()
        {
            var left = new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var right = new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id") == 2);

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id") == 1);

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var expr = new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id") == 1);

            Assert.False(expr.Equals("not an expr"));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var expr = new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id") == 1);

            Assert.True(expr.Equals(expr));
        }

        [Fact]
        public void ToString_FormatsWhereClause()
        {
            var expr = new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Age") > 18);

            Assert.Contains(" WHERE ", expr.ToString());
        }

        [Fact]
        public void ToString_NullSourceNonNullWhere_ReturnsWhereOnly()
        {
            var where = Expr.Prop("Id") == 1;
            var expr = new WhereExpr(null!, where);

            Assert.StartsWith("WHERE", expr.ToString());
        }

        [Fact]
        public void ToString_NonNullSourceNullWhere_ReturnsSource()
        {
            var source = new FromExpr(typeof(TestUser));
            var expr = new WhereExpr(source, null!);

            Assert.Equal(source.ToString(), expr.ToString());
        }

        [Fact]
        public void ToString_NullSourceNullWhere_ReturnsEmpty()
        {
            var expr = new WhereExpr();

            Assert.Equal(string.Empty, expr.ToString());
        }

        [Fact]
        public void GetHashCode_DifferentSource_DifferentHash()
        {
            var left = new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var right = new WhereExpr(new FromExpr(typeof(TestDepartment)), Expr.Prop("Id") == 1);

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentWhere_DifferentHash()
        {
            var left = new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var right = new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id") == 2);

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_NullSource_DoesNotThrow()
        {
            var expr = new WhereExpr(null!, Expr.Prop("Id") == 1);

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void GetHashCode_NullWhere_DoesNotThrow()
        {
            var expr = new WhereExpr(new FromExpr(typeof(TestUser)), null!);

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void Clone_CreatesDeepCopy()
        {
            var expr = new WhereExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var clone = (WhereExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
            Assert.NotSame(expr.Source, clone.Source);
            Assert.NotSame(expr.Where, clone.Where);
        }

        [Fact]
        public void Clone_NullSourceAndNullWhere_DoesNotThrow()
        {
            var expr = new WhereExpr();
            var clone = (WhereExpr)expr.Clone();

            Assert.Null(clone.Source);
            Assert.Null(clone.Where);
        }

        private class TestUser { }
        private class TestDepartment { }
    }
}
