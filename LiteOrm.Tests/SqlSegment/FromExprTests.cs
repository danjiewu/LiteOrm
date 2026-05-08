using LiteOrm.Common;
using LiteOrm.Tests.Models;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class FromExprTests
    {
        [Fact]
        public void Constructor_Default_CreatesInstanceWithTableExpr()
        {
            var expr = new FromExpr();

            Assert.NotNull(expr.Source);
            Assert.IsType<TableExpr>(expr.Source);
            Assert.Equal(ExprType.From, expr.ExprType);
            Assert.Empty(expr.Joins);
        }

        [Fact]
        public void Constructor_WithType_SetsSourceType()
        {
            var expr = new FromExpr(typeof(TestUser));

            var table = Assert.IsType<TableExpr>(expr.Source);
            Assert.Equal(typeof(TestUser), table.Type);
        }

        [Fact]
        public void Constructor_WithSourceExpr_SetsSource()
        {
            var table = new TableExpr(typeof(TestDepartment));
            var expr = new FromExpr(table);

            Assert.Same(table, expr.Source);
        }

        [Fact]
        public void ToString_IncludesJoinClauses()
        {
            var expr = new FromExpr(typeof(TestUser));
            expr.Joins.Add(new TableJoinExpr(new TableExpr(typeof(TestDepartment)), Expr.Prop("DeptId") == Expr.Prop("Id"))
            {
                JoinType = TableJoinType.Left
            });

            var text = expr.ToString();

            Assert.Contains(nameof(TestUser), text);
        }

        [Fact]
        public void ToString_IgnoresNullJoinItems()
        {
            var expr = new FromExpr(typeof(TestUser));
            expr.Joins.Add(null);
            expr.Joins.Add(new TableJoinExpr(new TableExpr(typeof(TestDepartment)), null)
            {
                JoinType = TableJoinType.Inner
            });

            Assert.Equal($"{nameof(TestUser)}", expr.ToString());
        }

        [Fact]
        public void ToString_WithNullSource_ReturnsEmptyString()
        {
            var expr = new FromExpr { Source = null! };

            Assert.Equal(string.Empty, expr.ToString());
        }

        [Fact]
        public void Equals_SameSourceAndJoins_ReturnsTrue()
        {
            var left = new FromExpr(typeof(TestUser));
            var right = new FromExpr(typeof(TestUser));

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentSource_ReturnsFalse()
        {
            var left = new FromExpr(typeof(TestUser));
            var right = new FromExpr(typeof(TestDepartment));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentJoins_ReturnsFalse()
        {
            var left = new FromExpr(typeof(TestUser));
            var right = new FromExpr(typeof(TestUser));
            right.Joins.Add(new TableJoinExpr(new TableExpr(typeof(TestDepartment)), Expr.Prop("Id") == Expr.Prop("Id")));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new FromExpr(typeof(TestUser));

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var expr = new FromExpr(typeof(TestUser));

            Assert.False(expr.Equals("not an expr"));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var expr = new FromExpr(typeof(TestUser));

            Assert.True(expr.Equals(expr));
        }

        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHash()
        {
            var left = new FromExpr(typeof(TestUser));
            var right = new FromExpr(typeof(TestUser));

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentSource_DifferentHash()
        {
            var left = new FromExpr(typeof(TestUser));
            var right = new FromExpr(typeof(TestDepartment));

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentJoins_DifferentHash()
        {
            var left = new FromExpr(typeof(TestUser));
            var right = new FromExpr(typeof(TestUser));
            right.Joins.Add(new TableJoinExpr(new TableExpr(typeof(TestDepartment)), Expr.Prop("Id") == Expr.Prop("Id")));

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void Clone_CreatesDeepCopy()
        {
            var expr = new FromExpr(typeof(TestUser));
            expr.Joins.Add(new TableJoinExpr(new TableExpr(typeof(TestDepartment)), Expr.Prop("Id") == Expr.Prop("Id")));
            var clone = (FromExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
            Assert.NotSame(expr.Source, clone.Source);
        }

        [Fact]
        public void Clone_NullJoins_DoesNotThrow()
        {
            var expr = new FromExpr(typeof(TestUser));
            var clone = (FromExpr)expr.Clone();

            Assert.Equal(expr, clone);
        }
    }
}
