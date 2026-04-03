using System;
using System.Collections.Generic;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for StringArrayEqualityComparer class
    /// </summary>
    public class StringArrayEqualityComparerTests
    {
        /// <summary>
        /// Tests that Equals returns true when both arrays are null (same reference)
        /// </summary>
        [Fact]
        public void Equals_BothArraysNull_ReturnsTrue()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[]? x = null;
            string[]? y = null;

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when first array is null and second is not
        /// </summary>
        [Fact]
        public void Equals_FirstArrayNull_ReturnsFalse()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[]? x = null;
            string[] y = new string[] { "test" };

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when second array is null and first is not
        /// </summary>
        [Fact]
        public void Equals_SecondArrayNull_ReturnsFalse()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[] { "test" };
            string[]? y = null;

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both arrays reference the same object
        /// </summary>
        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[] { "test", "data" };
            string[] y = x;

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both arrays are empty
        /// </summary>
        [Fact]
        public void Equals_BothEmptyArrays_ReturnsTrue()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[] { };
            string[] y = new string[] { };

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when arrays have different lengths
        /// </summary>
        [Theory]
        [InlineData(new string[] { "a" }, new string[] { })]
        [InlineData(new string[] { }, new string[] { "a" })]
        [InlineData(new string[] { "a", "b" }, new string[] { "a" })]
        [InlineData(new string[] { "a" }, new string[] { "a", "b" })]
        [InlineData(new string[] { "a", "b", "c" }, new string[] { "a", "b" })]
        public void Equals_DifferentLengths_ReturnsFalse(string[] x, string[] y)
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when arrays have identical elements in same order
        /// </summary>
        [Theory]
        [InlineData(new string[] { "a" }, new string[] { "a" })]
        [InlineData(new string[] { "a", "b" }, new string[] { "a", "b" })]
        [InlineData(new string[] { "a", "b", "c" }, new string[] { "a", "b", "c" })]
        [InlineData(new string[] { "test", "data", "values" }, new string[] { "test", "data", "values" })]
        public void Equals_SameElementsSameOrder_ReturnsTrue(string[] x, string[] y)
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when arrays have different elements at the first position
        /// </summary>
        [Fact]
        public void Equals_DifferentElementsAtStart_ReturnsFalse()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[] { "a", "b", "c" };
            string[] y = new string[] { "x", "b", "c" };

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when arrays have different elements in the middle
        /// </summary>
        [Fact]
        public void Equals_DifferentElementsAtMiddle_ReturnsFalse()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[] { "a", "b", "c" };
            string[] y = new string[] { "a", "x", "c" };

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when arrays have different elements at the end
        /// </summary>
        [Fact]
        public void Equals_DifferentElementsAtEnd_ReturnsFalse()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[] { "a", "b", "c" };
            string[] y = new string[] { "a", "b", "x" };

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when arrays have same elements but different order
        /// </summary>
        [Theory]
        [InlineData(new string[] { "a", "b" }, new string[] { "b", "a" })]
        [InlineData(new string[] { "a", "b", "c" }, new string[] { "c", "b", "a" })]
        [InlineData(new string[] { "test", "data" }, new string[] { "data", "test" })]
        public void Equals_SameElementsDifferentOrder_ReturnsFalse(string[] x, string[] y)
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals handles arrays containing null string elements correctly
        /// </summary>
        [Fact]
        public void Equals_ArraysWithNullElements_ReturnsTrue()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[] { "a", null, "c" };
            string[] y = new string[] { "a", null, "c" };

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when only one array has null element
        /// </summary>
        [Fact]
        public void Equals_DifferentNullElements_ReturnsFalse()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[] { "a", null, "c" };
            string[] y = new string[] { "a", "b", "c" };

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals handles arrays containing empty string elements correctly
        /// </summary>
        [Fact]
        public void Equals_ArraysWithEmptyStrings_ReturnsTrue()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[] { "a", "", "c" };
            string[] y = new string[] { "a", "", "c" };

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals distinguishes between empty string and null
        /// </summary>
        [Fact]
        public void Equals_EmptyStringVsNull_ReturnsFalse()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[] { "" };
            string[] y = new string[] { null };

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals handles arrays containing whitespace strings correctly
        /// </summary>
        [Fact]
        public void Equals_ArraysWithWhitespace_ReturnsTrue()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[] { "a", " ", "c" };
            string[] y = new string[] { "a", " ", "c" };

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals distinguishes between different whitespace strings
        /// </summary>
        [Fact]
        public void Equals_DifferentWhitespace_ReturnsFalse()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[] { " " };
            string[] y = new string[] { "  " };

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals handles arrays containing special characters correctly
        /// </summary>
        [Fact]
        public void Equals_ArraysWithSpecialCharacters_ReturnsTrue()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[] { "a", "!@#$%", "c\t\n\r" };
            string[] y = new string[] { "a", "!@#$%", "c\t\n\r" };

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals is case-sensitive
        /// </summary>
        [Fact]
        public void Equals_CaseSensitive_ReturnsFalse()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[] { "Test" };
            string[] y = new string[] { "test" };

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals handles large arrays correctly
        /// </summary>
        [Fact]
        public void Equals_LargeArrays_ReturnsTrue()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[1000];
            string[] y = new string[1000];
            for (int i = 0; i < 1000; i++)
            {
                x[i] = $"element_{i}";
                y[i] = $"element_{i}";
            }

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals detects difference in large arrays
        /// </summary>
        [Fact]
        public void Equals_LargeArraysWithOneDifference_ReturnsFalse()
        {
            // Arrange
            var comparer = StringArrayEqualityComparer.Instance;
            string[] x = new string[1000];
            string[] y = new string[1000];
            for (int i = 0; i < 1000; i++)
            {
                x[i] = $"element_{i}";
                y[i] = $"element_{i}";
            }
            y[500] = "different";

            // Act
            bool result = comparer.Equals(x, y);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that GetHashCode returns 0 when the input array is null
        /// </summary>
        [Fact]
        public void GetHashCode_NullArray_ReturnsZero()
        {
            // Arrange
            string[]? array = null;

            // Act
            int hashCode = StringArrayEqualityComparer.Instance.GetHashCode(array!);

            // Assert
            Assert.Equal(0, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns the initial hash value (17) for an empty array
        /// </summary>
        [Fact]
        public void GetHashCode_EmptyArray_ReturnsInitialHash()
        {
            // Arrange
            string[] array = new string[0];

            // Act
            int hashCode = StringArrayEqualityComparer.Instance.GetHashCode(array);

            // Assert
            Assert.Equal(17, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode computes correct hash for various single-element arrays
        /// </summary>
        /// <param name="value">The single value in the array</param>
        /// <param name="expectedHash">The expected hash code</param>
        [Theory]
        [InlineData("test", 17 * 31 + "test".GetHashCode())]
        [InlineData("", 17 * 31 + "".GetHashCode())]
        [InlineData(" ", 17 * 31 + " ".GetHashCode())]
        public void GetHashCode_SingleElementArray_ReturnsCorrectHash(string value, int expectedHash)
        {
            // Arrange
            string[] array = new[] { value };

            // Act
            int hashCode = StringArrayEqualityComparer.Instance.GetHashCode(array);

            // Assert
            Assert.Equal(expectedHash, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode handles arrays containing null elements correctly
        /// </summary>
        [Fact]
        public void GetHashCode_ArrayWithNullElement_ReturnsCorrectHash()
        {
            // Arrange
            string[] array = new string[] { null! };
            int expectedHash = 17 * 31 + 0; // null contributes 0

            // Act
            int hashCode = StringArrayEqualityComparer.Instance.GetHashCode(array);

            // Assert
            Assert.Equal(expectedHash, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode handles arrays with all null elements
        /// </summary>
        [Fact]
        public void GetHashCode_ArrayWithAllNullElements_ReturnsCorrectHash()
        {
            // Arrange
            string[] array = new string[] { null!, null!, null! };
            int expectedHash = 17;
            expectedHash = expectedHash * 31 + 0;
            expectedHash = expectedHash * 31 + 0;
            expectedHash = expectedHash * 31 + 0;

            // Act
            int hashCode = StringArrayEqualityComparer.Instance.GetHashCode(array);

            // Assert
            Assert.Equal(expectedHash, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns the same hash for identical arrays
        /// </summary>
        [Fact]
        public void GetHashCode_IdenticalArrays_ReturnsSameHash()
        {
            // Arrange
            string[] array1 = new[] { "a", "b", "c" };
            string[] array2 = new[] { "a", "b", "c" };

            // Act
            int hashCode1 = StringArrayEqualityComparer.Instance.GetHashCode(array1);
            int hashCode2 = StringArrayEqualityComparer.Instance.GetHashCode(array2);

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hashes for arrays with same elements in different order
        /// (order-sensitive comparison)
        /// </summary>
        [Fact]
        public void GetHashCode_SameElementsDifferentOrder_ReturnsDifferentHash()
        {
            // Arrange
            string[] array1 = new[] { "a", "b", "c" };
            string[] array2 = new[] { "c", "b", "a" };

            // Act
            int hashCode1 = StringArrayEqualityComparer.Instance.GetHashCode(array1);
            int hashCode2 = StringArrayEqualityComparer.Instance.GetHashCode(array2);

            // Assert
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode computes correct hash for arrays with multiple elements
        /// </summary>
        [Fact]
        public void GetHashCode_MultipleElements_ReturnsCorrectHash()
        {
            // Arrange
            string[] array = new[] { "hello", "world" };
            int expectedHash = 17;
            expectedHash = expectedHash * 31 + "hello".GetHashCode();
            expectedHash = expectedHash * 31 + "world".GetHashCode();

            // Act
            int hashCode = StringArrayEqualityComparer.Instance.GetHashCode(array);

            // Assert
            Assert.Equal(expectedHash, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode handles arrays with duplicate elements
        /// </summary>
        [Fact]
        public void GetHashCode_ArrayWithDuplicates_ReturnsCorrectHash()
        {
            // Arrange
            string[] array = new[] { "test", "test", "test" };
            int expectedHash = 17;
            expectedHash = expectedHash * 31 + "test".GetHashCode();
            expectedHash = expectedHash * 31 + "test".GetHashCode();
            expectedHash = expectedHash * 31 + "test".GetHashCode();

            // Act
            int hashCode = StringArrayEqualityComparer.Instance.GetHashCode(array);

            // Assert
            Assert.Equal(expectedHash, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode handles arrays with mixed null and non-null elements
        /// </summary>
        [Fact]
        public void GetHashCode_MixedNullAndNonNull_ReturnsCorrectHash()
        {
            // Arrange
            string[] array = new[] { "a", null!, "b" };
            int expectedHash = 17;
            expectedHash = expectedHash * 31 + "a".GetHashCode();
            expectedHash = expectedHash * 31 + 0;
            expectedHash = expectedHash * 31 + "b".GetHashCode();

            // Act
            int hashCode = StringArrayEqualityComparer.Instance.GetHashCode(array);

            // Assert
            Assert.Equal(expectedHash, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode handles arrays with empty strings
        /// </summary>
        [Fact]
        public void GetHashCode_ArrayWithEmptyStrings_ReturnsCorrectHash()
        {
            // Arrange
            string[] array = new[] { "", "", "" };
            int expectedHash = 17;
            expectedHash = expectedHash * 31 + "".GetHashCode();
            expectedHash = expectedHash * 31 + "".GetHashCode();
            expectedHash = expectedHash * 31 + "".GetHashCode();

            // Act
            int hashCode = StringArrayEqualityComparer.Instance.GetHashCode(array);

            // Assert
            Assert.Equal(expectedHash, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode handles arrays with special characters
        /// </summary>
        [Fact]
        public void GetHashCode_ArrayWithSpecialCharacters_ReturnsCorrectHash()
        {
            // Arrange
            string[] array = new[] { "!@#$%", "\t\n\r", "äöü" };
            int expectedHash = 17;
            expectedHash = expectedHash * 31 + "!@#$%".GetHashCode();
            expectedHash = expectedHash * 31 + "\t\n\r".GetHashCode();
            expectedHash = expectedHash * 31 + "äöü".GetHashCode();

            // Act
            int hashCode = StringArrayEqualityComparer.Instance.GetHashCode(array);

            // Assert
            Assert.Equal(expectedHash, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode handles large arrays correctly
        /// </summary>
        [Fact]
        public void GetHashCode_LargeArray_ReturnsCorrectHash()
        {
            // Arrange
            string[] array = new string[100];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = $"item{i}";
            }

            int expectedHash = 17;
            foreach (string item in array)
            {
                expectedHash = expectedHash * 31 + item.GetHashCode();
            }

            // Act
            int hashCode = StringArrayEqualityComparer.Instance.GetHashCode(array);

            // Assert
            Assert.Equal(expectedHash, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode is consistent with Equals - equal arrays must have equal hash codes
        /// </summary>
        [Fact]
        public void GetHashCode_ConsistentWithEquals_EqualArraysReturnSameHash()
        {
            // Arrange
            string[] array1 = new[] { "test1", "test2", null!, "test3" };
            string[] array2 = new[] { "test1", "test2", null!, "test3" };

            // Act
            bool areEqual = StringArrayEqualityComparer.Instance.Equals(array1, array2);
            int hashCode1 = StringArrayEqualityComparer.Instance.GetHashCode(array1);
            int hashCode2 = StringArrayEqualityComparer.Instance.GetHashCode(array2);

            // Assert
            Assert.True(areEqual);
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hashes for arrays of different lengths
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentLengthArrays_ReturnsDifferentHash()
        {
            // Arrange
            string[] array1 = new[] { "a", "b" };
            string[] array2 = new[] { "a", "b", "c" };

            // Act
            int hashCode1 = StringArrayEqualityComparer.Instance.GetHashCode(array1);
            int hashCode2 = StringArrayEqualityComparer.Instance.GetHashCode(array2);

            // Assert
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode handles whitespace-only strings
        /// </summary>
        [Fact]
        public void GetHashCode_WhitespaceStrings_ReturnsCorrectHash()
        {
            // Arrange
            string[] array = new[] { "   ", "\t", "\n", "\r\n" };
            int expectedHash = 17;
            expectedHash = expectedHash * 31 + "   ".GetHashCode();
            expectedHash = expectedHash * 31 + "\t".GetHashCode();
            expectedHash = expectedHash * 31 + "\n".GetHashCode();
            expectedHash = expectedHash * 31 + "\r\n".GetHashCode();

            // Act
            int hashCode = StringArrayEqualityComparer.Instance.GetHashCode(array);

            // Assert
            Assert.Equal(expectedHash, hashCode);
        }
    }
}