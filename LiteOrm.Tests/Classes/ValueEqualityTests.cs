#nullable enable

#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the ValueEquality class.
    /// </summary>
    public partial class ValueEqualityTests
    {
        /// <summary>
        /// Tests that GetValueHashCode returns 0 for null input.
        /// </summary>
        [Fact]
        public void GetValueHashCode_NullValue_ReturnsZero()
        {
            // Arrange
            object? val = null;

            // Act
            int result = ValueEquality.GetValueHashCode(val);

            // Assert
            Assert.Equal(0, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode returns correct hash code for numeric types that convert to decimal.
        /// </summary>
        /// <param name="value">The numeric value to hash.</param>
        /// <param name="expectedDecimal">The expected decimal representation.</param>
        [Theory]
        [InlineData((byte)5, 5)]
        [InlineData((byte)0, 0)]
        [InlineData((byte)255, 255)]
        [InlineData((sbyte)5, 5)]
        [InlineData((sbyte)-5, -5)]
        [InlineData((sbyte)127, 127)]
        [InlineData((sbyte)-128, -128)]
        [InlineData((short)100, 100)]
        [InlineData((short)-100, -100)]
        [InlineData((short)32767, 32767)]
        [InlineData((short)-32768, -32768)]
        [InlineData((ushort)100, 100)]
        [InlineData((ushort)0, 0)]
        [InlineData((ushort)65535, 65535)]
        [InlineData(42, 42)]
        [InlineData(0, 0)]
        [InlineData(-42, -42)]
        [InlineData(int.MaxValue, int.MaxValue)]
        [InlineData(int.MinValue, int.MinValue)]
        [InlineData((uint)42, 42)]
        [InlineData((uint)0, 0)]
        [InlineData(uint.MaxValue, uint.MaxValue)]
        [InlineData((long)1000, 1000)]
        [InlineData((long)-1000, -1000)]
        [InlineData(long.MaxValue, long.MaxValue)]
        [InlineData(long.MinValue, long.MinValue)]
        [InlineData((ulong)1000, 1000)]
        [InlineData((ulong)0, 0)]
        public void GetValueHashCode_DecimalConvertibleNumericTypes_ReturnsDecimalHashCode(object value, decimal expectedDecimal)
        {
            // Arrange & Act
            int result = ValueEquality.GetValueHashCode(value);
            int expected = expectedDecimal.GetHashCode();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode returns correct hash code for floating point numeric types.
        /// </summary>
        [Fact]
        public void GetValueHashCode_FloatValue_ReturnsDoubleHashCode()
        {
            // Arrange
            float value = 3.14f;
            double expectedDouble = value;

            // Act
            int result = ValueEquality.GetValueHashCode(value);
            int expected = expectedDouble.GetHashCode();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode returns correct hash code for double values.
        /// </summary>
        [Fact]
        public void GetValueHashCode_DoubleValue_ReturnsDoubleHashCode()
        {
            // Arrange
            double value = 3.14159;

            // Act
            int result = ValueEquality.GetValueHashCode(value);
            int expected = value.GetHashCode();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode returns correct hash code for decimal converted to double.
        /// </summary>
        [Fact]
        public void GetValueHashCode_DecimalValue_ReturnsDoubleHashCode()
        {
            // Arrange
            decimal value = 123.456m;
            double expectedDouble = decimal.ToDouble(value);

            // Act
            int result = ValueEquality.GetValueHashCode(value);
            int expected = expectedDouble.GetHashCode();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode handles special double values correctly.
        /// </summary>
        /// <param name="value">The special double value.</param>
        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(0.0)]
        [InlineData(-0.0)]
        public void GetValueHashCode_SpecialDoubleValues_ReturnsDoubleHashCode(double value)
        {
            // Arrange & Act
            int result = ValueEquality.GetValueHashCode(value);
            int expected = value.GetHashCode();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode returns 17 for an empty IList.
        /// </summary>
        [Fact]
        public void GetValueHashCode_EmptyList_Returns17()
        {
            // Arrange
            IList emptyList = new List<int>();

            // Act
            int result = ValueEquality.GetValueHashCode(emptyList);

            // Assert
            Assert.Equal(17, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode calculates correct hash code for IList with single item.
        /// </summary>
        [Fact]
        public void GetValueHashCode_ListWithSingleItem_ReturnsCorrectHash()
        {
            // Arrange
            IList list = new List<int> { 42 };
            int expectedItemHash = ValueEquality.GetValueHashCode(42, 1);
            int expected = 17 * 31 + expectedItemHash;

            // Act
            int result = ValueEquality.GetValueHashCode(list);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode calculates correct hash code for IList with multiple items.
        /// </summary>
        [Fact]
        public void GetValueHashCode_ListWithMultipleItems_ReturnsCorrectHash()
        {
            // Arrange
            IList list = new List<int> { 1, 2, 3 };
            int hash = 17;
            hash = hash * 31 + ValueEquality.GetValueHashCode(1, 1);
            hash = hash * 31 + ValueEquality.GetValueHashCode(2, 1);
            hash = hash * 31 + ValueEquality.GetValueHashCode(3, 1);

            // Act
            int result = ValueEquality.GetValueHashCode(list);

            // Assert
            Assert.Equal(hash, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode returns 17 for an empty IEnumerable.
        /// </summary>
        [Fact]
        public void GetValueHashCode_EmptyEnumerable_Returns17()
        {
            // Arrange
            IEnumerable emptyEnumerable = new HashSet<int>();

            // Act
            int result = ValueEquality.GetValueHashCode(emptyEnumerable);

            // Assert
            Assert.Equal(17, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode calculates correct hash code for IEnumerable with items.
        /// </summary>
        [Fact]
        public void GetValueHashCode_EnumerableWithItems_ReturnsCorrectHash()
        {
            // Arrange
            IEnumerable enumerable = new HashSet<int> { 10, 20, 30 };
            int hash = 17;
            foreach (var item in enumerable)
            {
                hash = hash * 31 + ValueEquality.GetValueHashCode(item, 1);
            }

            // Act
            int result = ValueEquality.GetValueHashCode(enumerable);

            // Assert
            Assert.Equal(hash, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode does not treat strings as IEnumerable.
        /// </summary>
        [Fact]
        public void GetValueHashCode_StringValue_UsesDefaultHashCode()
        {
            // Arrange
            string value = "test string";

            // Act
            int result = ValueEquality.GetValueHashCode(value);
            int expected = value.GetHashCode();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode handles empty string correctly.
        /// </summary>
        [Fact]
        public void GetValueHashCode_EmptyString_UsesDefaultHashCode()
        {
            // Arrange
            string value = string.Empty;

            // Act
            int result = ValueEquality.GetValueHashCode(value);
            int expected = value.GetHashCode();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode uses default hash code for non-numeric, non-collection types.
        /// </summary>
        [Fact]
        public void GetValueHashCode_CustomObject_UsesDefaultHashCode()
        {
            // Arrange
            var obj = new object();

            // Act
            int result = ValueEquality.GetValueHashCode(obj);
            int expected = obj.GetHashCode();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode respects depth limit and uses default GetHashCode at depth 10.
        /// </summary>
        [Fact]
        public void GetValueHashCode_AtDepthLimit_UsesDefaultHashCode()
        {
            // Arrange
            IList list = new List<int> { 1, 2, 3 };

            // Act
            int result = ValueEquality.GetValueHashCode(list, 10);
            int expected = list.GetHashCode();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode respects depth limit and uses default GetHashCode above depth 10.
        /// </summary>
        [Fact]
        public void GetValueHashCode_AboveDepthLimit_UsesDefaultHashCode()
        {
            // Arrange
            IList list = new List<int> { 1, 2, 3 };

            // Act
            int result = ValueEquality.GetValueHashCode(list, 15);
            int expected = list.GetHashCode();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode processes nested collections correctly with increasing depth.
        /// </summary>
        [Fact]
        public void GetValueHashCode_NestedCollections_ProcessesWithIncrementingDepth()
        {
            // Arrange
            IList innerList = new List<int> { 1, 2 };
            IList outerList = new List<object> { innerList };

            // Calculate expected hash
            int innerHash = 17;
            innerHash = innerHash * 31 + ValueEquality.GetValueHashCode(1, 2);
            innerHash = innerHash * 31 + ValueEquality.GetValueHashCode(2, 2);

            int expectedHash = 17;
            expectedHash = expectedHash * 31 + innerHash;

            // Act
            int result = ValueEquality.GetValueHashCode(outerList);

            // Assert
            Assert.Equal(expectedHash, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode with depth 9 still processes collections recursively.
        /// </summary>
        [Fact]
        public void GetValueHashCode_AtDepth9_ProcessesCollectionRecursively()
        {
            // Arrange
            IList list = new List<int> { 5, 10 };

            // Calculate expected hash
            int expectedHash = 17;
            expectedHash = expectedHash * 31 + ValueEquality.GetValueHashCode(5, 10);
            expectedHash = expectedHash * 31 + ValueEquality.GetValueHashCode(10, 10);

            // Act
            int result = ValueEquality.GetValueHashCode(list, 9);

            // Assert
            Assert.Equal(expectedHash, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode handles list with null items correctly.
        /// </summary>
        [Fact]
        public void GetValueHashCode_ListWithNullItems_ReturnsCorrectHash()
        {
            // Arrange
            IList list = new List<object?> { null, 42, null };

            int expectedHash = 17;
            expectedHash = expectedHash * 31 + ValueEquality.GetValueHashCode(null, 1);
            expectedHash = expectedHash * 31 + ValueEquality.GetValueHashCode(42, 1);
            expectedHash = expectedHash * 31 + ValueEquality.GetValueHashCode(null, 1);

            // Act
            int result = ValueEquality.GetValueHashCode(list);

            // Assert
            Assert.Equal(expectedHash, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode produces same hash for equivalent numeric values of different types.
        /// </summary>
        [Fact]
        public void GetValueHashCode_EquivalentNumericTypes_ProduceSameHash()
        {
            // Arrange
            int intValue = 100;
            long longValue = 100L;
            short shortValue = 100;

            // Act
            int intHash = ValueEquality.GetValueHashCode(intValue);
            int longHash = ValueEquality.GetValueHashCode(longValue);
            int shortHash = ValueEquality.GetValueHashCode(shortValue);

            // Assert
            Assert.Equal(intHash, longHash);
            Assert.Equal(intHash, shortHash);
        }

        /// <summary>
        /// Tests that GetValueHashCode with explicit depth parameter of 0 behaves correctly.
        /// </summary>
        [Fact]
        public void GetValueHashCode_ExplicitDepthZero_ProcessesCollectionRecursively()
        {
            // Arrange
            IList list = new List<int> { 7 };

            int expectedHash = 17;
            expectedHash = expectedHash * 31 + ValueEquality.GetValueHashCode(7, 1);

            // Act
            int result = ValueEquality.GetValueHashCode(list, 0);

            // Assert
            Assert.Equal(expectedHash, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode handles large lists correctly.
        /// </summary>
        [Fact]
        public void GetValueHashCode_LargeList_ReturnsCorrectHash()
        {
            // Arrange
            IList list = new List<int>();
            for (int i = 0; i < 100; i++)
            {
                list.Add(i);
            }

            int expectedHash = 17;
            for (int i = 0; i < 100; i++)
            {
                expectedHash = unchecked(expectedHash * 31 + ValueEquality.GetValueHashCode(i, 1));
            }

            // Act
            int result = ValueEquality.GetValueHashCode(list);

            // Assert
            Assert.Equal(expectedHash, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode handles list containing mixed types correctly.
        /// </summary>
        [Fact]
        public void GetValueHashCode_MixedTypeList_ReturnsCorrectHash()
        {
            // Arrange
            IList list = new List<object> { 42, "test", 3.14, true };

            int expectedHash = 17;
            expectedHash = expectedHash * 31 + ValueEquality.GetValueHashCode(42, 1);
            expectedHash = expectedHash * 31 + ValueEquality.GetValueHashCode("test", 1);
            expectedHash = expectedHash * 31 + ValueEquality.GetValueHashCode(3.14, 1);
            expectedHash = expectedHash * 31 + ValueEquality.GetValueHashCode(true, 1);

            // Act
            int result = ValueEquality.GetValueHashCode(list);

            // Assert
            Assert.Equal(expectedHash, result);
        }

        /// <summary>
        /// Tests that GetValueHashCode handles boolean values correctly.
        /// </summary>
        /// <param name="value">The boolean value.</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetValueHashCode_BooleanValue_UsesDefaultHashCode(bool value)
        {
            // Arrange & Act
            int result = ValueEquality.GetValueHashCode(value);
            int expected = value.GetHashCode();

            // Assert
            Assert.Equal(expected, result);
        }
        #region Null Handling Tests

        /// <summary>
        /// Tests that ValueEquals returns true when both values are null.
        /// </summary>
        [Fact]
        public void ValueEquals_BothValuesNull_ReturnsTrue()
        {
            // Arrange
            object? val1 = null;
            object? val2 = null;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals returns false when first value is null and second is not.
        /// </summary>
        [Fact]
        public void ValueEquals_FirstValueNullSecondNotNull_ReturnsFalse()
        {
            // Arrange
            object? val1 = null;
            object val2 = 42;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValueEquals returns false when second value is null and first is not.
        /// </summary>
        [Fact]
        public void ValueEquals_FirstValueNotNullSecondNull_ReturnsFalse()
        {
            // Arrange
            object val1 = 42;
            object? val2 = null;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Numeric Type Tests - Same Type Comparisons

        /// <summary>
        /// Tests that ValueEquals correctly compares numeric values of the same type.
        /// </summary>
        /// <param name="val1">First value</param>
        /// <param name="val2">Second value</param>
        /// <param name="expected">Expected result</param>
        [Theory]
        [InlineData(5, 5, true)]
        [InlineData(5, 10, false)]
        [InlineData(0, 0, true)]
        [InlineData(-5, -5, true)]
        [InlineData(-5, 5, false)]
        [InlineData(int.MaxValue, int.MaxValue, true)]
        [InlineData(int.MinValue, int.MinValue, true)]
        [InlineData(int.MaxValue, int.MinValue, false)]
        public void ValueEquals_IntegersSameType_ReturnsExpectedResult(int val1, int val2, bool expected)
        {
            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares byte values.
        /// </summary>
        [Theory]
        [InlineData((byte)0, (byte)0, true)]
        [InlineData((byte)5, (byte)5, true)]
        [InlineData((byte)255, (byte)255, true)]
        [InlineData((byte)0, (byte)255, false)]
        public void ValueEquals_ByteValues_ReturnsExpectedResult(byte val1, byte val2, bool expected)
        {
            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares long values.
        /// </summary>
        [Theory]
        [InlineData(0L, 0L, true)]
        [InlineData(100L, 100L, true)]
        [InlineData(long.MaxValue, long.MaxValue, true)]
        [InlineData(long.MinValue, long.MinValue, true)]
        [InlineData(100L, 200L, false)]
        public void ValueEquals_LongValues_ReturnsExpectedResult(long val1, long val2, bool expected)
        {
            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares double values.
        /// </summary>
        [Theory]
        [InlineData(5.0, 5.0, true)]
        [InlineData(5.5, 5.5, true)]
        [InlineData(0.0, 0.0, true)]
        [InlineData(-5.5, -5.5, true)]
        [InlineData(5.0, 10.0, false)]
        [InlineData(double.MaxValue, double.MaxValue, true)]
        [InlineData(double.MinValue, double.MinValue, true)]
        public void ValueEquals_DoubleValues_ReturnsExpectedResult(double val1, double val2, bool expected)
        {
            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly handles special double values like NaN.
        /// NaN is not equal to NaN according to IEEE 754.
        /// </summary>
        [Fact]
        public void ValueEquals_DoubleNaN_ReturnsFalse()
        {
            // Arrange
            double val1 = double.NaN;
            double val2 = double.NaN;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares positive infinity values.
        /// </summary>
        [Fact]
        public void ValueEquals_DoublePositiveInfinity_ReturnsTrue()
        {
            // Arrange
            double val1 = double.PositiveInfinity;
            double val2 = double.PositiveInfinity;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares negative infinity values.
        /// </summary>
        [Fact]
        public void ValueEquals_DoubleNegativeInfinity_ReturnsTrue()
        {
            // Arrange
            double val1 = double.NegativeInfinity;
            double val2 = double.NegativeInfinity;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals returns false when comparing different infinity values.
        /// </summary>
        [Fact]
        public void ValueEquals_DifferentInfinities_ReturnsFalse()
        {
            // Arrange
            double val1 = double.PositiveInfinity;
            double val2 = double.NegativeInfinity;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares float values.
        /// </summary>
        [Theory]
        [InlineData(5.0f, 5.0f, true)]
        [InlineData(5.5f, 5.5f, true)]
        [InlineData(5.0f, 10.0f, false)]
        [InlineData(float.MaxValue, float.MaxValue, true)]
        [InlineData(float.MinValue, float.MinValue, true)]
        public void ValueEquals_FloatValues_ReturnsExpectedResult(float val1, float val2, bool expected)
        {
            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region Numeric Type Tests - Mixed Type Comparisons

        /// <summary>
        /// Tests that ValueEquals correctly compares different integer types with same value.
        /// </summary>
        [Fact]
        public void ValueEquals_DifferentIntegerTypesSameValue_ReturnsTrue()
        {
            // Arrange
            int val1 = 42;
            long val2 = 42L;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares different integer types with different values.
        /// </summary>
        [Fact]
        public void ValueEquals_DifferentIntegerTypesDifferentValue_ReturnsFalse()
        {
            // Arrange
            int val1 = 42;
            long val2 = 100L;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares byte and int with same value.
        /// </summary>
        [Fact]
        public void ValueEquals_ByteAndIntSameValue_ReturnsTrue()
        {
            // Arrange
            byte val1 = 5;
            int val2 = 5;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares integer and double with same value.
        /// </summary>
        [Fact]
        public void ValueEquals_IntAndDoubleSameValue_ReturnsTrue()
        {
            // Arrange
            int val1 = 42;
            double val2 = 42.0;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares integer and float with same value.
        /// </summary>
        [Fact]
        public void ValueEquals_IntAndFloatSameValue_ReturnsTrue()
        {
            // Arrange
            int val1 = 42;
            float val2 = 42.0f;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares float and double with same value.
        /// </summary>
        [Fact]
        public void ValueEquals_FloatAndDoubleSameValue_ReturnsTrue()
        {
            // Arrange
            float val1 = 42.5f;
            double val2 = 42.5;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region String Comparison Tests

        /// <summary>
        /// Tests that ValueEquals correctly compares identical strings.
        /// </summary>
        [Fact]
        public void ValueEquals_IdenticalStrings_ReturnsTrue()
        {
            // Arrange
            string val1 = "test";
            string val2 = "test";

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares different strings.
        /// </summary>
        [Fact]
        public void ValueEquals_DifferentStrings_ReturnsFalse()
        {
            // Arrange
            string val1 = "test1";
            string val2 = "test2";

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares empty strings.
        /// </summary>
        [Fact]
        public void ValueEquals_EmptyStrings_ReturnsTrue()
        {
            // Arrange
            string val1 = string.Empty;
            string val2 = string.Empty;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals returns false when comparing string with non-string.
        /// </summary>
        [Fact]
        public void ValueEquals_StringVsInteger_ReturnsFalse()
        {
            // Arrange
            string val1 = "42";
            int val2 = 42;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region IList Comparison Tests

        /// <summary>
        /// Tests that ValueEquals returns true for two empty lists.
        /// </summary>
        [Fact]
        public void ValueEquals_EmptyLists_ReturnsTrue()
        {
            // Arrange
            var val1 = new List<int>();
            var val2 = new List<int>();

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals returns true for lists with same elements in same order.
        /// </summary>
        [Fact]
        public void ValueEquals_ListsWithSameElements_ReturnsTrue()
        {
            // Arrange
            var val1 = new List<int> { 1, 2, 3, 4, 5 };
            var val2 = new List<int> { 1, 2, 3, 4, 5 };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals returns false for lists with different counts.
        /// </summary>
        [Fact]
        public void ValueEquals_ListsWithDifferentCounts_ReturnsFalse()
        {
            // Arrange
            var val1 = new List<int> { 1, 2, 3 };
            var val2 = new List<int> { 1, 2, 3, 4 };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValueEquals returns false for lists with different elements.
        /// </summary>
        [Fact]
        public void ValueEquals_ListsWithDifferentElements_ReturnsFalse()
        {
            // Arrange
            var val1 = new List<int> { 1, 2, 3 };
            var val2 = new List<int> { 1, 2, 4 };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValueEquals returns false for lists with same elements in different order.
        /// </summary>
        [Fact]
        public void ValueEquals_ListsWithSameElementsDifferentOrder_ReturnsFalse()
        {
            // Arrange
            var val1 = new List<int> { 1, 2, 3 };
            var val2 = new List<int> { 3, 2, 1 };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly handles nested lists with same structure.
        /// </summary>
        [Fact]
        public void ValueEquals_NestedListsSameStructure_ReturnsTrue()
        {
            // Arrange
            var val1 = new List<List<int>>
            {
                new List<int> { 1, 2 },
                new List<int> { 3, 4 }
            };
            var val2 = new List<List<int>>
            {
                new List<int> { 1, 2 },
                new List<int> { 3, 4 }
            };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly handles nested lists with different structure.
        /// </summary>
        [Fact]
        public void ValueEquals_NestedListsDifferentStructure_ReturnsFalse()
        {
            // Arrange
            var val1 = new List<List<int>>
            {
                new List<int> { 1, 2 },
                new List<int> { 3, 4 }
            };
            var val2 = new List<List<int>>
            {
                new List<int> { 1, 2 },
                new List<int> { 3, 5 }
            };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly handles lists with mixed numeric types.
        /// </summary>
        [Fact]
        public void ValueEquals_ListsWithMixedNumericTypes_ReturnsTrue()
        {
            // Arrange
            var val1 = new List<object> { 1, 2L, 3 };
            var val2 = new List<object> { 1, 2L, 3 };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals handles single element lists correctly.
        /// </summary>
        [Fact]
        public void ValueEquals_SingleElementLists_ReturnsTrue()
        {
            // Arrange
            var val1 = new List<int> { 42 };
            var val2 = new List<int> { 42 };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region IEnumerable Comparison Tests

        /// <summary>
        /// Tests that ValueEquals correctly compares non-list enumerables with same elements.
        /// </summary>
        [Fact]
        public void ValueEquals_HashSetsWithSameElements_ReturnsTrue()
        {
            // Arrange
            var val1 = new HashSet<int> { 1, 2, 3 };
            var val2 = new HashSet<int> { 1, 2, 3 };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares non-list enumerables with different elements.
        /// </summary>
        [Fact]
        public void ValueEquals_HashSetsWithDifferentElements_ReturnsFalse()
        {
            // Arrange
            var val1 = new HashSet<int> { 1, 2, 3 };
            var val2 = new HashSet<int> { 1, 2, 4 };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares empty enumerables.
        /// </summary>
        [Fact]
        public void ValueEquals_EmptyHashSets_ReturnsTrue()
        {
            // Arrange
            var val1 = new HashSet<int>();
            var val2 = new HashSet<int>();

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares enumerables with different counts.
        /// </summary>
        [Fact]
        public void ValueEquals_EnumerablesWithDifferentCounts_ReturnsFalse()
        {
            // Arrange
            var val1 = new HashSet<int> { 1, 2 };
            var val2 = new HashSet<int> { 1, 2, 3 };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region Depth Limit Tests

        /// <summary>
        /// Tests that ValueEquals uses default Equals when depth reaches the limit (10).
        /// </summary>
        [Fact]
        public void ValueEquals_AtMaxDepth_UsesDefaultEquals()
        {
            // Arrange
            var val1 = new List<int> { 1, 2, 3 };
            var val2 = new List<int> { 1, 2, 3 };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2, depth: 10);

            // Assert - At depth 10, it should use default Equals which returns false for different list instances
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValueEquals still recurses when depth is below the limit.
        /// </summary>
        [Fact]
        public void ValueEquals_BelowMaxDepth_RecursesCorrectly()
        {
            // Arrange
            var val1 = new List<int> { 1, 2, 3 };
            var val2 = new List<int> { 1, 2, 3 };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2, depth: 9);

            // Assert - At depth 9, it should still recurse and compare elements
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly handles deeply nested structures.
        /// </summary>
        [Fact]
        public void ValueEquals_DeeplyNestedStructures_HandlesCorrectly()
        {
            // Arrange - Create 5 levels of nesting
            var innerList1 = new List<int> { 1, 2 };
            var innerList2 = new List<int> { 1, 2 };

            var nested1 = new List<object> { innerList1 };
            var nested2 = new List<object> { innerList2 };

            var nested3 = new List<object> { nested1 };
            var nested4 = new List<object> { nested2 };

            var nested5 = new List<object> { nested3 };
            var nested6 = new List<object> { nested4 };

            // Act
            bool result = ValueEquality.ValueEquals(nested5, nested6);

            // Assert
            Assert.True(result);
        }

        #endregion

        #region Default Equals Fallback Tests

        /// <summary>
        /// Tests that ValueEquals falls back to default Equals for non-comparable types.
        /// </summary>
        [Fact]
        public void ValueEquals_CustomObjectsSameReference_ReturnsTrue()
        {
            // Arrange
            var obj = new object();

            // Act
            bool result = ValueEquality.ValueEquals(obj, obj);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals falls back to default Equals for different object instances.
        /// </summary>
        [Fact]
        public void ValueEquals_CustomObjectsDifferentReferences_ReturnsFalse()
        {
            // Arrange
            var obj1 = new object();
            var obj2 = new object();

            // Act
            bool result = ValueEquality.ValueEquals(obj1, obj2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValueEquals works with DateTime values using default Equals.
        /// </summary>
        [Fact]
        public void ValueEquals_DateTimeValues_UsesDefaultEquals()
        {
            // Arrange
            var val1 = new DateTime(2023, 1, 1);
            var val2 = new DateTime(2023, 1, 1);

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly compares different DateTime values.
        /// </summary>
        [Fact]
        public void ValueEquals_DifferentDateTimeValues_ReturnsFalse()
        {
            // Arrange
            var val1 = new DateTime(2023, 1, 1);
            var val2 = new DateTime(2023, 1, 2);

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValueEquals works with boolean values using default Equals.
        /// </summary>
        [Theory]
        [InlineData(true, true, true)]
        [InlineData(false, false, true)]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        public void ValueEquals_BooleanValues_ReturnsExpectedResult(bool val1, bool val2, bool expected)
        {
            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region Edge Case Tests

        /// <summary>
        /// Tests that ValueEquals handles zero values correctly across different numeric types.
        /// </summary>
        [Fact]
        public void ValueEquals_ZeroValuesDifferentTypes_ReturnsTrue()
        {
            // Arrange
            int val1 = 0;
            double val2 = 0.0;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly handles negative zero for double.
        /// </summary>
        [Fact]
        public void ValueEquals_PositiveAndNegativeZero_ReturnsTrue()
        {
            // Arrange
            double val1 = 0.0;
            double val2 = -0.0;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly handles lists with null elements.
        /// </summary>
        [Fact]
        public void ValueEquals_ListsWithNullElements_ReturnsTrue()
        {
            // Arrange
            var val1 = new List<string?> { "test", null, "data" };
            var val2 = new List<string?> { "test", null, "data" };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals correctly handles lists where null positions differ.
        /// </summary>
        [Fact]
        public void ValueEquals_ListsWithNullInDifferentPositions_ReturnsFalse()
        {
            // Arrange
            var val1 = new List<string?> { null, "test" };
            var val2 = new List<string?> { "test", null };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that ValueEquals handles comparison between array and list with same elements.
        /// </summary>
        [Fact]
        public void ValueEquals_ArrayAndListWithSameElements_ReturnsTrue()
        {
            // Arrange
            int[] val1 = new int[] { 1, 2, 3 };
            var val2 = new List<int> { 1, 2, 3 };

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals handles ulong max value correctly.
        /// </summary>
        [Fact]
        public void ValueEquals_UlongMaxValue_ReturnsTrue()
        {
            // Arrange
            ulong val1 = ulong.MaxValue;
            ulong val2 = ulong.MaxValue;

            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that ValueEquals handles sbyte min and max values.
        /// </summary>
        [Theory]
        [InlineData(sbyte.MinValue, sbyte.MinValue, true)]
        [InlineData(sbyte.MaxValue, sbyte.MaxValue, true)]
        [InlineData(sbyte.MinValue, sbyte.MaxValue, false)]
        public void ValueEquals_SByteExtremeValues_ReturnsExpectedResult(sbyte val1, sbyte val2, bool expected)
        {
            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ValueEquals handles ushort values correctly.
        /// </summary>
        [Theory]
        [InlineData((ushort)0, (ushort)0, true)]
        [InlineData((ushort)100, (ushort)100, true)]
        [InlineData(ushort.MaxValue, ushort.MaxValue, true)]
        [InlineData((ushort)100, (ushort)200, false)]
        public void ValueEquals_UShortValues_ReturnsExpectedResult(ushort val1, ushort val2, bool expected)
        {
            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ValueEquals handles short values correctly.
        /// </summary>
        [Theory]
        [InlineData((short)0, (short)0, true)]
        [InlineData((short)100, (short)100, true)]
        [InlineData(short.MinValue, short.MinValue, true)]
        [InlineData(short.MaxValue, short.MaxValue, true)]
        [InlineData((short)100, (short)200, false)]
        public void ValueEquals_ShortValues_ReturnsExpectedResult(short val1, short val2, bool expected)
        {
            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ValueEquals handles uint values correctly.
        /// </summary>
        [Theory]
        [InlineData(0u, 0u, true)]
        [InlineData(100u, 100u, true)]
        [InlineData(uint.MaxValue, uint.MaxValue, true)]
        [InlineData(100u, 200u, false)]
        public void ValueEquals_UIntValues_ReturnsExpectedResult(uint val1, uint val2, bool expected)
        {
            // Act
            bool result = ValueEquality.ValueEquals(val1, val2);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion
    }
}