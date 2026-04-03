using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class StringArrayEqualityComparerTests
    {
        [Fact]
        public void Equals_WithSameReference_ReturnsTrue()
        {
            var values = new[] { "a", "b" };

            Assert.True(StringArrayEqualityComparer.Instance.Equals(values, values));
        }

        [Fact]
        public void Equals_WithSameContentAndOrder_ReturnsTrue()
        {
            Assert.True(StringArrayEqualityComparer.Instance.Equals(new[] { "a", "b" }, new[] { "a", "b" }));
        }

        [Fact]
        public void Equals_WithDifferentOrder_ReturnsFalse()
        {
            Assert.False(StringArrayEqualityComparer.Instance.Equals(new[] { "a", "b" }, new[] { "b", "a" }));
        }

        [Fact]
        public void GetHashCode_WithNull_ReturnsZero()
        {
            Assert.Equal(0, StringArrayEqualityComparer.Instance.GetHashCode(null));
        }

        [Fact]
        public void GetHashCode_WithSameContent_ReturnsSameHash()
        {
            var left = new[] { "x", "y" };
            var right = new[] { "x", "y" };

            Assert.Equal(
                StringArrayEqualityComparer.Instance.GetHashCode(left),
                StringArrayEqualityComparer.Instance.GetHashCode(right));
        }
    }
}
