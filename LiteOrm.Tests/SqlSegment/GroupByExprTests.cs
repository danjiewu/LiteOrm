using LiteOrm.Common;
using LiteOrm.Tests.Models;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class GroupByExprTests
    {
        [Fact]
        public void Constructor_Parameterless_CreatesInstance()
        {
            var expr = new GroupByExpr();

            Assert.Null(expr.Source);
            Assert.NotNull(expr.GroupBys);
            Assert.Empty(expr.GroupBys);
            Assert.Equal(ExprType.GroupBy, expr.ExprType);
        }

        [Fact]
        public void Constructor_WithSourceAndGroupBys_SetsProperties()
        {
            var source = new FromExpr(typeof(TestUser));
            var gb1 = Expr.Prop("DeptId");
            var gb2 = Expr.Prop("Status");

            var expr = new GroupByExpr(source, gb1, gb2);

            Assert.Same(source, expr.Source);
            Assert.Equal(2, expr.GroupBys.Count);
            Assert.Same(gb1, expr.GroupBys[0]);
            Assert.Same(gb2, expr.GroupBys[1]);
        }

        [Fact]
        public void Constructor_WithSourceOnly_CreatesEmptyList()
        {
            var source = new FromExpr(typeof(TestUser));

            var expr = new GroupByExpr(source);

            Assert.Same(source, expr.Source);
            Assert.Empty(expr.GroupBys);
        }

        [Fact]
        public void Constructor_NullGroupBys_CreatesEmptyList()
        {
            var source = new FromExpr(typeof(TestUser));
            var expr = new GroupByExpr(source, null!);

            Assert.Empty(expr.GroupBys);
        }

        [Fact]
        public void ToString_IgnoresNullGroupByItems()
        {
            var expr = new GroupByExpr(new FromExpr(typeof(TestUser)))
            {
                GroupBys = new List<ValueTypeExpr> { null, Expr.Prop("DeptId") }
            };

            Assert.Equal($"{nameof(TestUser)} GROUP BY [DeptId]", expr.ToString());
        }

        [Fact]
        public void ToString_WithSourceAndGroupBys_FormatsCorrectly()
        {
            var expr = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId"), Expr.Prop("Status"));

            Assert.Contains("GROUP BY", expr.ToString());
            Assert.Contains("[DeptId]", expr.ToString());
            Assert.Contains("[Status]", expr.ToString());
        }

        [Fact]
        public void ToString_NoSourceNoGroupBys_ReturnsEmpty()
        {
            var expr = new GroupByExpr();

            Assert.Equal(string.Empty, expr.ToString());
        }

        [Fact]
        public void ToString_NoSourceWithGroupBys_ReturnsGroupByClauseOnly()
        {
            var expr = new GroupByExpr { GroupBys = { Expr.Prop("DeptId") } };

            Assert.StartsWith("GROUP BY", expr.ToString());
        }

        [Fact]
        public void ToString_WithSourceNoGroupBys_ReturnsSourceOnly()
        {
            var source = new FromExpr(typeof(TestUser));
            var expr = new GroupByExpr(source);

            Assert.Equal(source.ToString(), expr.ToString());
        }

        [Fact]
        public void Equals_SameValues_ReturnsTrue()
        {
            var left = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId"));
            var right = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId"));

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentSource_ReturnsFalse()
        {
            var left = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId"));
            var right = new GroupByExpr(new FromExpr(typeof(TestDepartment)), Expr.Prop("DeptId"));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentGroupBys_ReturnsFalse()
        {
            var left = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId"));
            var right = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Status"));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentCount_ReturnsFalse()
        {
            var left = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId"));
            var right = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId"), Expr.Prop("Status"));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId"));

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var expr = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId"));

            Assert.False(expr.Equals("not an expr"));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var expr = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId"));

            Assert.True(expr.Equals(expr));
        }

        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHash()
        {
            var left = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId"));
            var right = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId"));

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentGroupBys_DifferentHash()
        {
            var left = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId"));
            var right = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("Status"));

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_NullSource_DoesNotThrow()
        {
            var expr = new GroupByExpr { GroupBys = { Expr.Prop("DeptId") } };

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void GetHashCode_EmptyGroupBys_ReturnsValidHash()
        {
            var expr = new GroupByExpr(new FromExpr(typeof(TestUser)));

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void GetHashCode_SequenceOrderMatters()
        {
            var left = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("A"), Expr.Prop("B"));
            var right = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("B"), Expr.Prop("A"));

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void Clone_CreatesDeepCopy()
        {
            var expr = new GroupByExpr(new FromExpr(typeof(TestUser)), Expr.Prop("DeptId"), Expr.Prop("Status"));
            var clone = (GroupByExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
            Assert.NotSame(expr.Source, clone.Source);
            for (int i = 0; i < expr.GroupBys.Count; i++)
                Assert.NotSame(expr.GroupBys[i], clone.GroupBys[i]);
        }

        [Fact]
        public void Clone_NullSource_DoesNotThrow()
        {
            var expr = new GroupByExpr { GroupBys = { Expr.Prop("DeptId") } };
            var clone = (GroupByExpr)expr.Clone();

            Assert.Null(clone.Source);
            Assert.Single(clone.GroupBys);
        }

        [Fact]
        public void Clone_EmptyGroupBys_DoesNotThrow()
        {
            var expr = new GroupByExpr(new FromExpr(typeof(TestUser)));
            var clone = (GroupByExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.Empty(clone.GroupBys);
        }
    }
}
