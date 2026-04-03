using System.Collections.Generic;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class ValueEqualityTests
    {
        [Fact]
        public void ValueEquals_BothNull_ReturnsTrue()
        {
            Assert.True(ValueEquality.ValueEquals(null, null));
        }

        [Fact]
        public void ValueEquals_IntAndLongWithSameValue_ReturnsTrue()
        {
            Assert.True(ValueEquality.ValueEquals(1, 1L));
        }

        [Fact]
        public void ValueEquals_ListsWithSameOrderedItems_ReturnsTrue()
        {
            Assert.True(ValueEquality.ValueEquals(new List<int> { 1, 2 }, new List<int> { 1, 2 }));
        }

        [Fact]
        public void ValueEquals_ListsWithDifferentOrder_ReturnsFalse()
        {
            Assert.False(ValueEquality.ValueEquals(new List<int> { 1, 2 }, new List<int> { 2, 1 }));
        }

        [Fact]
        public void GetValueHashCode_ForEquivalentLists_ReturnsSameHash()
        {
            var left = new List<int> { 1, 2, 3 };
            var right = new List<int> { 1, 2, 3 };

            Assert.Equal(ValueEquality.GetValueHashCode(left), ValueEquality.GetValueHashCode(right));
        }
    }
}
