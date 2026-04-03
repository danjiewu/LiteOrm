using System;
using System.Collections.Generic;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the ListEqualityComparer class.
    /// </summary>
    public class ListEqualityComparerTests
    {
        /// <summary>
        /// Tests that GetHashCode returns 0 when the input list is null.
        /// </summary>
        [Fact]
        public void GetHashCode_NullList_ReturnsZero()
        {
            // 准备
            var comparer = new ListEqualityComparer<int>();
            List<int>? nullList = null;

            // 执行
            int hashCode = comparer.GetHashCode(nullList);

            // 断言
            Assert.Equal(0, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns the initial hash value (17) for an empty list.
        /// </summary>
        [Fact]
        public void GetHashCode_EmptyList_ReturnsSeventeen()
        {
            // 准备
            var comparer = new ListEqualityComparer<int>();
            var emptyList = new List<int>();

            // 执行
            int hashCode = comparer.GetHashCode(emptyList);

            // 断言
            Assert.Equal(17, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode computes correct hash for a single element list.
        /// Formula: hash = 17 * 31 + element.GetHashCode()
        /// </summary>
        [Theory]
        [InlineData(0, 527)]  // 17 * 31 + 0 = 527
        [InlineData(1, 528)]  // 17 * 31 + 1 = 528
        [InlineData(5, 532)]  // 17 * 31 + 5 = 532
        [InlineData(-1, 526)] // 17 * 31 + (-1) = 526
        [InlineData(int.MaxValue, 2147484174)] // 17 * 31 + int.MaxValue
        [InlineData(int.MinValue, -2147483121)] // 17 * 31 + int.MinValue
        public void GetHashCode_SingleElementList_ReturnsCorrectHash(int element, int expectedHash)
        {
            // 准备
            var comparer = new ListEqualityComparer<int>();
            var list = new List<int> { element };

            // 执行
            int hashCode = comparer.GetHashCode(list);

            // 断言
            Assert.Equal(expectedHash, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode computes correct hash for multiple element lists.
        /// </summary>
        [Fact]
        public void GetHashCode_MultipleElements_ReturnsCorrectHash()
        {
            // 准备
            var comparer = new ListEqualityComparer<int>();
            var list = new List<int> { 1, 2, 3 };
            // Expected: ((17 * 31 + 1) * 31 + 2) * 31 + 3
            // = (528 * 31 + 2) * 31 + 3
            // = 16370 * 31 + 3
            // = 507473
            int expectedHash = 507473;

            // 执行
            int hashCode = comparer.GetHashCode(list);

            // 断言
            Assert.Equal(expectedHash, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode handles null elements correctly in reference type lists.
        /// </summary>
        [Fact]
        public void GetHashCode_ListWithNullElements_HandlesNullCorrectly()
        {
            // 准备
            var comparer = new ListEqualityComparer<string>();
            var list = new List<string> { "test", null, "test2" };
            // Expected: ((17 * 31 + "test".GetHashCode()) * 31 + 0) * 31 + "test2".GetHashCode()
            int expectedHash = ((17 * 31 + "test".GetHashCode()) * 31 + 0) * 31 + "test2".GetHashCode();

            // 执行
            int hashCode = comparer.GetHashCode(list);

            // 断言
            Assert.Equal(expectedHash, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns the same value when called multiple times on the same list.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsSameValue()
        {
            // 准备
            var comparer = new ListEqualityComparer<int>();
            var list = new List<int> { 1, 2, 3, 4, 5 };

            // 执行
            int hashCode1 = comparer.GetHashCode(list);
            int hashCode2 = comparer.GetHashCode(list);
            int hashCode3 = comparer.GetHashCode(list);

            // 断言
            Assert.Equal(hashCode1, hashCode2);
            Assert.Equal(hashCode2, hashCode3);
        }

        /// <summary>
        /// Tests that GetHashCode produces different hashes for lists with same elements in different order.
        /// </summary>
        [Fact]
        public void GetHashCode_SameElementsDifferentOrder_ReturnsDifferentHashes()
        {
            // 准备
            var comparer = new ListEqualityComparer<int>();
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 3, 2, 1 };

            // 执行
            int hashCode1 = comparer.GetHashCode(list1);
            int hashCode2 = comparer.GetHashCode(list2);

            // 断言
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode handles duplicate elements correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_DuplicateElements_ReturnsCorrectHash()
        {
            // 准备
            var comparer = new ListEqualityComparer<int>();
            var list = new List<int> { 5, 5, 5 };
            // Expected: ((17 * 31 + 5) * 31 + 5) * 31 + 5
            // = (532 * 31 + 5) * 31 + 5
            // = 16497 * 31 + 5
            // = 511412
            int expectedHash = 511412;

            // 执行
            int hashCode = comparer.GetHashCode(list);

            // 断言
            Assert.Equal(expectedHash, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns the same hash for two different list instances with identical elements.
        /// </summary>
        [Fact]
        public void GetHashCode_TwoListsWithSameElements_ReturnsSameHash()
        {
            // 准备
            var comparer = new ListEqualityComparer<string>();
            var list1 = new List<string> { "a", "b", "c" };
            var list2 = new List<string> { "a", "b", "c" };

            // 执行
            int hashCode1 = comparer.GetHashCode(list1);
            int hashCode2 = comparer.GetHashCode(list2);

            // 断言
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode handles a large list without errors.
        /// </summary>
        [Fact]
        public void GetHashCode_LargeList_ComputesHashWithoutError()
        {
            // 准备
            var comparer = new ListEqualityComparer<int>();
            var largeList = new List<int>();
            for (int i = 0; i < 10000; i++)
            {
                largeList.Add(i);
            }

            // 执行
            int hashCode = comparer.GetHashCode(largeList);

            // 断言
            Assert.NotEqual(0, hashCode);
            Assert.NotEqual(17, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode handles a list containing only null elements (reference type).
        /// </summary>
        [Fact]
        public void GetHashCode_ListWithOnlyNullElements_ReturnsCorrectHash()
        {
            // 准备
            var comparer = new ListEqualityComparer<string>();
            var list = new List<string> { null, null, null };
            // Expected: ((17 * 31 + 0) * 31 + 0) * 31 + 0
            // = (527 * 31) * 31
            // = 16337 * 31
            // = 506447
            int expectedHash = 506447;

            // 执行
            int hashCode = comparer.GetHashCode(list);

            // 断言
            Assert.Equal(expectedHash, hashCode);
        }

        /// <summary>
        /// Tests that Equals returns true when both parameters reference the same list instance.
        /// </summary>
        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list = new List<int> { 1, 2, 3 };

            // Act
            var result = comparer.Equals(list, list);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both parameters are null.
        /// </summary>
        [Fact]
        public void Equals_BothNull_ReturnsTrue()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();

            // Act
            var result = comparer.Equals(null, null);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when the first parameter is null and the second is not.
        /// </summary>
        [Fact]
        public void Equals_FirstNullSecondNotNull_ReturnsFalse()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list = new List<int> { 1, 2, 3 };

            // Act
            var result = comparer.Equals(null, list);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when the first parameter is not null and the second is null.
        /// </summary>
        [Fact]
        public void Equals_FirstNotNullSecondNull_ReturnsFalse()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list = new List<int> { 1, 2, 3 };

            // Act
            var result = comparer.Equals(list, null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both lists are empty.
        /// </summary>
        [Fact]
        public void Equals_BothEmpty_ReturnsTrue()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list1 = new List<int>();
            var list2 = new List<int>();

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one list is empty and the other is not.
        /// </summary>
        [Fact]
        public void Equals_OneEmptyOneNot_ReturnsFalse()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var emptyList = new List<int>();
            var nonEmptyList = new List<int> { 1 };

            // Act
            var result = comparer.Equals(emptyList, nonEmptyList);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when lists have different counts.
        /// </summary>
        [Fact]
        public void Equals_DifferentCounts_ReturnsFalse()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 1, 2 };

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when lists have the same elements in the same order.
        /// </summary>
        [Fact]
        public void Equals_SameElementsSameOrder_ReturnsTrue()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 1, 2, 3 };

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when lists have the same elements in different order.
        /// </summary>
        [Fact]
        public void Equals_SameElementsDifferentOrder_ReturnsFalse()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 3, 2, 1 };

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when lists have the same count but different elements.
        /// </summary>
        [Fact]
        public void Equals_SameCountDifferentElements_ReturnsFalse()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list1 = new List<int> { 1, 2, 3 };
            var list2 = new List<int> { 4, 5, 6 };

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both lists contain a single equal element.
        /// </summary>
        [Fact]
        public void Equals_SingleElementEqual_ReturnsTrue()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list1 = new List<int> { 42 };
            var list2 = new List<int> { 42 };

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when both lists contain a single different element.
        /// </summary>
        [Fact]
        public void Equals_SingleElementNotEqual_ReturnsFalse()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list1 = new List<int> { 42 };
            var list2 = new List<int> { 99 };

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals correctly handles lists with duplicate elements when they are equal.
        /// </summary>
        [Fact]
        public void Equals_WithDuplicateElements_ReturnsTrue()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list1 = new List<int> { 1, 2, 2, 3, 3, 3 };
            var list2 = new List<int> { 1, 2, 2, 3, 3, 3 };

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when lists have duplicate elements but in different positions.
        /// </summary>
        [Fact]
        public void Equals_DuplicatesInDifferentPositions_ReturnsFalse()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list1 = new List<int> { 1, 2, 2, 3 };
            var list2 = new List<int> { 2, 1, 2, 3 };

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals works correctly with string reference types.
        /// </summary>
        [Fact]
        public void Equals_WithStringType_ReturnsTrue()
        {
            // Arrange
            var comparer = new ListEqualityComparer<string>();
            var list1 = new List<string> { "apple", "banana", "cherry" };
            var list2 = new List<string> { "apple", "banana", "cherry" };

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when string lists differ.
        /// </summary>
        [Fact]
        public void Equals_WithStringTypeDifferent_ReturnsFalse()
        {
            // Arrange
            var comparer = new ListEqualityComparer<string>();
            var list1 = new List<string> { "apple", "banana" };
            var list2 = new List<string> { "apple", "cherry" };

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals correctly handles lists with null elements.
        /// </summary>
        [Fact]
        public void Equals_WithNullElements_ReturnsTrue()
        {
            // Arrange
            var comparer = new ListEqualityComparer<string>();
            var list1 = new List<string> { "apple", null, "cherry" };
            var list2 = new List<string> { "apple", null, "cherry" };

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when null elements are in different positions.
        /// </summary>
        [Fact]
        public void Equals_NullElementsInDifferentPositions_ReturnsFalse()
        {
            // Arrange
            var comparer = new ListEqualityComparer<string>();
            var list1 = new List<string> { "apple", null, "cherry" };
            var list2 = new List<string> { null, "apple", "cherry" };

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals works with large lists.
        /// </summary>
        [Fact]
        public void Equals_WithLargeLists_ReturnsTrue()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list1 = new List<int>();
            var list2 = new List<int>();
            for (int i = 0; i < 10000; i++)
            {
                list1.Add(i);
                list2.Add(i);
            }

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when large lists differ in the last element.
        /// </summary>
        [Fact]
        public void Equals_LargeListsDifferentLastElement_ReturnsFalse()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list1 = new List<int>();
            var list2 = new List<int>();
            for (int i = 0; i < 10000; i++)
            {
                list1.Add(i);
                list2.Add(i);
            }
            list2[9999] = -1;

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals works correctly with extreme integer values.
        /// </summary>
        [Fact]
        public void Equals_WithExtremeValues_ReturnsTrue()
        {
            // Arrange
            var comparer = new ListEqualityComparer<int>();
            var list1 = new List<int> { int.MinValue, 0, int.MaxValue };
            var list2 = new List<int> { int.MinValue, 0, int.MaxValue };

            // Act
            var result = comparer.Equals(list1, list2);

            // Assert
            Assert.True(result);
        }
    }
}