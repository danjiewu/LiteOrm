using LiteOrm.Common;
using LiteOrm.Tests.Models;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class UpdateExprTests
    {
        [Fact]
        public void Constructor_Parameterless_CreatesInstance()
        {
            var expr = new UpdateExpr();

            Assert.Null(expr.Table);
            Assert.Null(expr.Where);
            Assert.NotNull(expr.Sets);
            Assert.Empty(expr.Sets);
            Assert.Equal(ExprType.Update, expr.ExprType);
        }

        [Fact]
        public void Constructor_WithTableAndWhere_SetsProperties()
        {
            var table = new TableExpr(typeof(TestUser));
            var where = Expr.Prop("Id") == 1;

            var expr = new UpdateExpr(table, where);

            Assert.Same(table, expr.Table);
            Assert.Same(where, expr.Where);
        }

        [Fact]
        public void Constructor_WithTableOnly_WhereIsNull()
        {
            var table = new TableExpr(typeof(TestUser));
            var expr = new UpdateExpr(table);

            Assert.Same(table, expr.Table);
            Assert.Null(expr.Where);
        }

        [Fact]
        public void ToString_WithNullSets_DoesNotThrow()
        {
            var expr = new UpdateExpr(new TableExpr(typeof(TestUser)))
            {
                Sets = null
            };

            Assert.Equal($"UPDATE {nameof(TestUser)}", expr.ToString());
        }

        [Fact]
        public void ToString_WithSets_FormatsCorrectly()
        {
            var expr = new UpdateExpr(new TableExpr(typeof(TestUser)));
            expr.Sets.Add(new SetItem(Expr.Prop("Name"), new ValueExpr("newName")));

            var result = expr.ToString();

            Assert.Contains("SET", result);
            Assert.Contains("[Name] = newName", result);
        }

        [Fact]
        public void ToString_WithWhere_IncludesWhere()
        {
            var expr = new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            expr.Sets.Add(new SetItem(Expr.Prop("Name"), new ValueExpr("newName")));

            var result = expr.ToString();

            Assert.Contains("WHERE", result);
        }

        [Fact]
        public void ToString_WithNullTable_ReturnsPartial()
        {
            var expr = new UpdateExpr(null!);
            expr.Sets.Add(new SetItem(Expr.Prop("Name"), new ValueExpr("x")));

            var result = expr.ToString();

            Assert.Contains("SET", result);
        }

        [Fact]
        public void Equals_SameValues_ReturnsTrue()
        {
            var left = new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            left.Sets.Add(new SetItem(Expr.Prop("Name"), new ValueExpr("x")));
            var right = new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            right.Sets.Add(new SetItem(Expr.Prop("Name"), new ValueExpr("x")));

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentTable_ReturnsFalse()
        {
            var left = new UpdateExpr(new TableExpr(typeof(TestUser)));
            var right = new UpdateExpr(new TableExpr(typeof(TestDepartment)));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentSets_ReturnsFalse()
        {
            var left = new UpdateExpr(new TableExpr(typeof(TestUser)));
            left.Sets.Add(new SetItem(Expr.Prop("Name"), new ValueExpr("x")));
            var right = new UpdateExpr(new TableExpr(typeof(TestUser)));
            right.Sets.Add(new SetItem(Expr.Prop("Name"), new ValueExpr("y")));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentSetsCount_ReturnsFalse()
        {
            var left = new UpdateExpr(new TableExpr(typeof(TestUser)));
            left.Sets.Add(new SetItem(Expr.Prop("Name"), new ValueExpr("x")));
            var right = new UpdateExpr(new TableExpr(typeof(TestUser)));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentWhere_ReturnsFalse()
        {
            var left = new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var right = new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 2);

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new UpdateExpr(new TableExpr(typeof(TestUser)));

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var expr = new UpdateExpr(new TableExpr(typeof(TestUser)));

            Assert.False(expr.Equals("not an expr"));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var expr = new UpdateExpr(new TableExpr(typeof(TestUser)));

            Assert.True(expr.Equals(expr));
        }

        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHash()
        {
            var left = new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            left.Sets.Add(new SetItem(Expr.Prop("Name"), new ValueExpr("x")));
            var right = new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            right.Sets.Add(new SetItem(Expr.Prop("Name"), new ValueExpr("x")));

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentTable_DifferentHash()
        {
            var left = new UpdateExpr(new TableExpr(typeof(TestUser)));
            var right = new UpdateExpr(new TableExpr(typeof(TestDepartment)));

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentSets_DifferentHash()
        {
            var left = new UpdateExpr(new TableExpr(typeof(TestUser)));
            left.Sets.Add(new SetItem(Expr.Prop("Name"), new ValueExpr("x")));
            var right = new UpdateExpr(new TableExpr(typeof(TestUser)));
            right.Sets.Add(new SetItem(Expr.Prop("Email"), new ValueExpr("y")));

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentWhere_DifferentHash()
        {
            var left = new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            var right = new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 2);

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_NullSets_ReturnsValidHash()
        {
            var expr = new UpdateExpr(new TableExpr(typeof(TestUser))) { Sets = null! };

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void GetHashCode_NullTable_ReturnsValidHash()
        {
            var expr = new UpdateExpr(null!);

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void Clone_CreatesDeepCopy()
        {
            var expr = new UpdateExpr(new TableExpr(typeof(TestUser)), Expr.Prop("Id") == 1);
            expr.Sets.Add(new SetItem(Expr.Prop("Name"), new ValueExpr("x")));
            var clone = (UpdateExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
            Assert.NotSame(expr.Table, clone.Table);
            Assert.NotSame(expr.Where, clone.Where);
            Assert.Single(clone.Sets);
        }

        [Fact]
        public void Clone_WithNullSets_DoesNotThrow()
        {
            var expr = new UpdateExpr(new TableExpr(typeof(TestUser))) { Sets = null! };
            var clone = (UpdateExpr)expr.Clone();

            Assert.NotNull(clone.Sets);
            Assert.Empty(clone.Sets);
        }
    }
}
