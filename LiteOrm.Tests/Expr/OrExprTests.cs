using System;
using System.Linq;
using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class OrExprTests
    {
        private static LogicExpr Condition(string name, int val) =>
            Expr.Prop(name) == new ValueExpr(val);

        [Fact]
        public void Constructor_Parameterless_CreatesEmptyInstance()
        {
            var expr = new OrExpr();

            Assert.Equal(0, expr.Count);
            Assert.False(expr.IsReadOnly);
            Assert.Equal(ExprType.Or, expr.ExprType);
        }

        [Fact]
        public void Constructor_WithParamArray_AddsAllItems()
        {
            var e1 = Condition("A", 1);
            var e2 = Condition("B", 2);
            var e3 = Condition("C", 3);

            var expr = new OrExpr(e1, e2, e3);

            Assert.Equal(3, expr.Count);
        }

        [Fact]
        public void Constructor_WithIEnumerable_AddsAllItems()
        {
            var items = new LogicExpr[] { Condition("A", 1), Condition("B", 2) };

            var expr = new OrExpr(items.AsEnumerable());

            Assert.Equal(2, expr.Count);
        }

        [Fact]
        public void Constructor_NullParamArray_DoesNotThrow()
        {
            var expr = new OrExpr((LogicExpr[])null);

            Assert.Empty(expr);
        }

        [Fact]
        public void Constructor_NullIEnumerable_DoesNotThrow()
        {
            var expr = new OrExpr((System.Collections.Generic.IEnumerable<LogicExpr>)null);

            Assert.Empty(expr);
        }

        [Fact]
        public void Add_SingleItem_IncreasesCount()
        {
            var expr = new OrExpr();
            expr.Add(Condition("A", 1));

            Assert.Equal(1, expr.Count);
        }

        [Fact]
        public void Add_NullItem_IsIgnored()
        {
            var expr = new OrExpr();
            expr.Add(null);

            Assert.Equal(0, expr.Count);
        }

        [Fact]
        public void Add_NestedOrExpr_FlattensItems()
        {
            var inner = new OrExpr(Condition("A", 1), Condition("B", 2));
            var outer = new OrExpr(Condition("C", 3));

            outer.Add(inner);

            Assert.Equal(3, outer.Count);
        }

        [Fact]
        public void Add_NestedAndExpr_DoesNotFlatten()
        {
            var inner = new AndExpr(Condition("A", 1), Condition("B", 2));
            var outer = new OrExpr();

            outer.Add(inner);

            Assert.Equal(1, outer.Count);
            Assert.IsType<AndExpr>(outer[0]);
        }

        [Fact]
        public void AddRange_MultipleItems_AddsAll()
        {
            var expr = new OrExpr();
            expr.AddRange(new[] { Condition("A", 1), Condition("B", 2), Condition("C", 3) });

            Assert.Equal(3, expr.Count);
        }

        [Fact]
        public void Clear_RemovesAllItems()
        {
            var expr = new OrExpr(Condition("A", 1), Condition("B", 2));
            expr.Clear();

            Assert.Equal(0, expr.Count);
        }

        [Fact]
        public void Contains_ExistingItem_ReturnsTrue()
        {
            var item = Condition("A", 1);
            var expr = new OrExpr(item);

            Assert.Contains(item, expr);
        }

        [Fact]
        public void Contains_NonExistingItem_ReturnsFalse()
        {
            var expr = new OrExpr(Condition("A", 1));

            Assert.DoesNotContain(Condition("B", 2), expr);
        }

        [Fact]
        public void Remove_ExistingItem_ReturnsTrue()
        {
            var item = Condition("A", 1);
            var expr = new OrExpr(item);

            Assert.True(expr.Remove(item));
            Assert.Empty(expr);
        }

        [Fact]
        public void Remove_NonExistingItem_ReturnsFalse()
        {
            var expr = new OrExpr(Condition("A", 1));

            Assert.False(expr.Remove(Condition("B", 2)));
            Assert.Single(expr);
        }

        [Fact]
        public void CopyTo_CopiesItemsToArray()
        {
            var e1 = Condition("A", 1);
            var e2 = Condition("B", 2);
            var expr = new OrExpr(e1, e2);
            var array = new LogicExpr[2];

            expr.CopyTo(array, 0);

            Assert.Equal(e1, array[0]);
            Assert.Equal(e2, array[1]);
        }

        [Fact]
        public void Indexer_AccessByIndex_ReturnsItem()
        {
            var e1 = Condition("A", 1);
            var e2 = Condition("B", 2);
            var expr = new OrExpr(e1, e2);

            Assert.Same(e1, expr[0]);
            Assert.Same(e2, expr[1]);
        }

        [Fact]
        public void GetEnumerator_EnumeratesAllItems()
        {
            var items = new[] { Condition("A", 1), Condition("B", 2), Condition("C", 3) };
            var expr = new OrExpr(items);

            var enumerated = expr.ToList();

            Assert.Equal(3, enumerated.Count);
        }

        [Fact]
        public void ToString_WithItems_JoinsWithOr()
        {
            var expr = new OrExpr(Condition("A", 1), Condition("B", 2));

            var result = expr.ToString();

            Assert.Contains(" OR ", result);
        }

        [Fact]
        public void ToString_EmptyOrExpr_ReturnsEmptyString()
        {
            var expr = new OrExpr();

            Assert.Equal(string.Empty, expr.ToString());
        }

        [Fact]
        public void Equals_SameItemsInDifferentOrder_ReturnsTrue()
        {
            var left = new OrExpr(Condition("A", 1), Condition("B", 2));
            var right = new OrExpr(Condition("B", 2), Condition("A", 1));

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentItems_ReturnsFalse()
        {
            var left = new OrExpr(Condition("A", 1), Condition("B", 2));
            var right = new OrExpr(Condition("A", 1), Condition("C", 3));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_DifferentCount_ReturnsFalse()
        {
            var left = new OrExpr(Condition("A", 1), Condition("B", 2));
            var right = new OrExpr(Condition("A", 1));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            var expr = new OrExpr(Condition("A", 1));

            Assert.False(expr.Equals(null));
        }

        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            var left = new OrExpr(Condition("A", 1));
            var right = new AndExpr(Condition("A", 1));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            var expr = new OrExpr(Condition("A", 1));

            Assert.True(expr.Equals(expr));
        }

        [Fact]
        public void Equals_EmptyOrExpr_ReturnsTrue()
        {
            var left = new OrExpr();
            var right = new OrExpr();

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void GetHashCode_EqualOrExpr_ReturnsSameHash()
        {
            var left = new OrExpr(Condition("A", 1), Condition("B", 2));
            var right = new OrExpr(Condition("A", 1), Condition("B", 2));

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_OrderIndependent()
        {
            var left = new OrExpr(Condition("A", 1), Condition("B", 2));
            var right = new OrExpr(Condition("B", 2), Condition("A", 1));

            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentItems_DifferentHash()
        {
            var left = new OrExpr(Condition("A", 1), Condition("B", 2));
            var right = new OrExpr(Condition("A", 1), Condition("C", 3));

            Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void GetHashCode_EmptyOrExpr_ReturnsStableHash()
        {
            var expr = new OrExpr();

            var hash = expr.GetHashCode();
        }

        [Fact]
        public void Clone_CreatesDeepCopy()
        {
            var expr = new OrExpr(Condition("A", 1), Condition("B", 2));
            var clone = (OrExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
            Assert.Equal(expr.Count, clone.Count);
            for (int i = 0; i < expr.Count; i++)
                Assert.NotSame(expr[i], clone[i]);
        }

        [Fact]
        public void Clone_EmptyOrExpr_CreatesNewEmptyInstance()
        {
            var expr = new OrExpr();
            var clone = (OrExpr)expr.Clone();

            Assert.Equal(expr, clone);
            Assert.NotSame(expr, clone);
            Assert.Empty(clone);
        }
    }
}
