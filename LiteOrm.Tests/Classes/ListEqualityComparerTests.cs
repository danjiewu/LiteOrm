using System.Collections.Generic;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class ListEqualityComparerTests
    {
        [Fact]
        public void Equals_WithSameReference_ReturnsTrue()
        {
            var list = new List<int> { 1, 2 };
            var comparer = new ListEqualityComparer<int>();

            Assert.True(comparer.Equals(list, list));
        }

        [Fact]
        public void Equals_WithSameContent_ReturnsTrue()
        {
            var comparer = new ListEqualityComparer<int>();

            Assert.True(comparer.Equals(new List<int> { 1, 2 }, new List<int> { 1, 2 }));
        }

        [Fact]
        public void Equals_WithDifferentOrder_ReturnsFalse()
        {
            var comparer = new ListEqualityComparer<int>();

            Assert.False(comparer.Equals(new List<int> { 1, 2 }, new List<int> { 2, 1 }));
        }

        [Fact]
        public void GetHashCode_WithNull_ReturnsZero()
        {
            var comparer = new ListEqualityComparer<int>();

            Assert.Equal(0, comparer.GetHashCode(null));
        }

        [Fact]
        public void GetHashCode_WithSameContent_ReturnsSameHash()
        {
            var comparer = new ListEqualityComparer<int>();
            var left = new List<int> { 1, 2, 3 };
            var right = new List<int> { 1, 2, 3 };

            Assert.Equal(comparer.GetHashCode(left), comparer.GetHashCode(right));
        }
    }
}
