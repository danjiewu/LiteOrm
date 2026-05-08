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
        public void Constructor_Parameterless_CreatesInstance()
        {
            var expr = new DeleteExpr();

            Assert.Null(expr.Table);
            Assert.Null(expr.Where);
            Assert.Equal(ExprType.Delete, expr.ExprType);
        }

        [Fact]
        public void Constructor_WithTableOnly_WhereIsNull()
        {
            var table = new TableExpr(typeof(TestUser));
            var expr = new DeleteExpr(table);

            Assert.Same(table, expr.Table);
            Assert.Null(expr.Where);
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
        public void ToString_WithoutTable_ReturnsEmptyString()
        {
            var expr = new DeleteExpr();

            Assert.Equal(string.Empty, expr.ToString());
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

        [Fact]
        public void Clone_NullTableAndWhere_DoesNotThrow()
        {
            var expr = new DeleteExpr();
            var clone = (DeleteExpr)expr.Clone();

            Assert.Null(clone.Table);
            Assert.Null(clone.Where);
        }

        [Fact]
        public void Equals_SameValues_ReturnsTrue()
        {
            var left = new DeleteExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var right = new DeleteExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentTable_ReturnsFalse()
        {
            var left = new DeleteExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var right = new DeleteExpr(new TableExpr(typeof(TestDepartment)), Expr.Prop("Id") == 1);

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentWhere_ReturnsFalse()
        {
            var left = new DeleteExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var right = new DeleteExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 2);

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new DeleteExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var expr = new DeleteExpr(new TableExpr(typeof(TestUser)));

            Assert.False(expr.Equals("not an expr"));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var expr = new DeleteExpr(new TableExpr(typeof(TestUser)));

            Assert.True(expr.Equals(expr));
        }

        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHash()
        {
            var left = new DeleteExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var right = new DeleteExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentTable_DifferentHash()
        {
            var left = new DeleteExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var right = new DeleteExpr(new TableExpr(typeof(TestDepartment)), Expr.Prop("Id") == 1);

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentWhere_DifferentHash()
        {
            var left = new DeleteExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var right = new DeleteExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 2);

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_NullTable_DoesNotThrow()
        {
            var expr = new DeleteExpr(null!, Expr.Prop("Id") == 1);

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void GetHashCode_NullWhere_DoesNotThrow()
        {
            var expr = new DeleteExpr(new TableExpr(typeof(TestUser)), null!);

            var hash = expr.GetHashCode();
        }

        private class TestUser { }
        private class TestDepartment { }
    }
}
