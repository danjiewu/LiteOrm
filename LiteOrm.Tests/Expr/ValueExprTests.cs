using System;
using System.Collections;
using System.Collections.Generic;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the ValueExpr class.
    /// </summary>
    public partial class ValueExprTests
    {
        /// <summary>
        /// Tests that the parameterless constructor creates a valid ValueExpr instance
        /// with default property values (Value = null, IsConst = false) and correct ExprType.
        /// </summary>
        [Fact]
        public void ValueExpr_ParameterlessConstructor_CreatesInstanceWithDefaultValues()
        {
            // Arrange & Act
            var expr = new ValueExpr();

            // Assert
            Assert.NotNull(expr);
            Assert.Null(expr.Value);
            Assert.False(expr.IsConst);
            Assert.Equal(ExprType.Value, expr.ExprType);
        }

        /// <summary>
        /// Tests that Clone creates a new instance when Value is null.
        /// </summary>
        [Fact]
        public void Clone_ValueIsNull_ReturnsNewInstanceWithNullValue()
        {
            // Arrange
            var original = new ValueExpr(null) { IsConst = false };

            // Act
            var cloned = original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.IsType<ValueExpr>(cloned);
            var clonedValue = (ValueExpr)cloned;
            Assert.Null(clonedValue.Value);
            Assert.Equal(original.IsConst, clonedValue.IsConst);
        }

        /// <summary>
        /// Tests that Clone creates a new instance with the same primitive value (int).
        /// </summary>
        [Fact]
        public void Clone_ValueIsInt_ReturnsNewInstanceWithSameValue()
        {
            // Arrange
            var original = new ValueExpr(42) { IsConst = true };

            // Act
            var cloned = original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.IsType<ValueExpr>(cloned);
            var clonedValue = (ValueExpr)cloned;
            Assert.Equal(42, clonedValue.Value);
            Assert.Equal(original.IsConst, clonedValue.IsConst);
        }

        /// <summary>
        /// Tests that Clone creates a new instance with the same string value.
        /// </summary>
        [Fact]
        public void Clone_ValueIsString_ReturnsNewInstanceWithSameReference()
        {
            // Arrange
            var stringValue = "test string";
            var original = new ValueExpr(stringValue) { IsConst = false };

            // Act
            var cloned = original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.IsType<ValueExpr>(cloned);
            var clonedValue = (ValueExpr)cloned;
            Assert.Same(stringValue, clonedValue.Value);
            Assert.Equal(original.IsConst, clonedValue.IsConst);
        }

        /// <summary>
        /// Tests that Clone with IsConst true properly copies the flag.
        /// </summary>
        [Fact]
        public void Clone_IsConstTrue_CopiesIsConstFlag()
        {
            // Arrange
            var original = new ValueExpr("value") { IsConst = true };

            // Act
            var cloned = (ValueExpr)original.Clone();

            // Assert
            Assert.True(cloned.IsConst);
        }

        /// <summary>
        /// Tests that Clone with IsConst false properly copies the flag.
        /// </summary>
        [Fact]
        public void Clone_IsConstFalse_CopiesIsConstFlag()
        {
            // Arrange
            var original = new ValueExpr("value") { IsConst = false };

            // Act
            var cloned = (ValueExpr)original.Clone();

            // Assert
            Assert.False(cloned.IsConst);
        }

        /// <summary>
        /// Tests that Clone deeply clones when Value is an Expr.
        /// </summary>
        [Fact]
        public void Clone_ValueIsExpr_DeepClonesTheExpr()
        {
            // Arrange
            var innerExpr = new ValueExpr("inner value") { IsConst = true };
            var original = new ValueExpr(innerExpr) { IsConst = false };

            // Act
            var cloned = (ValueExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.IsType<ValueExpr>(cloned.Value);
            var clonedInnerExpr = (ValueExpr)cloned.Value;
            Assert.NotSame(innerExpr, clonedInnerExpr);
            Assert.Equal("inner value", clonedInnerExpr.Value);
            Assert.Equal(innerExpr.IsConst, clonedInnerExpr.IsConst);
            Assert.Equal(original.IsConst, cloned.IsConst);
        }

        /// <summary>
        /// Tests that Clone with various primitive types creates proper copies.
        /// </summary>
        /// <param name="value">The value to clone.</param>
        /// <param name="isConst">The IsConst flag value.</param>
        [Theory]
        [InlineData(0, true)]
        [InlineData(int.MinValue, false)]
        [InlineData(int.MaxValue, true)]
        [InlineData(-1, false)]
        public void Clone_VariousIntValues_ReturnsNewInstanceWithSameValue(int value, bool isConst)
        {
            // Arrange
            var original = new ValueExpr(value) { IsConst = isConst };

            // Act
            var cloned = (ValueExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.Equal(value, cloned.Value);
            Assert.Equal(isConst, cloned.IsConst);
        }

        /// <summary>
        /// Tests that Clone with long values creates proper copies.
        /// </summary>
        [Fact]
        public void Clone_ValueIsLong_ReturnsNewInstanceWithSameValue()
        {
            // Arrange
            var original = new ValueExpr(123456789L) { IsConst = true };

            // Act
            var cloned = (ValueExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.Equal(123456789L, cloned.Value);
            Assert.True(cloned.IsConst);
        }

        /// <summary>
        /// Tests that Clone with bool values creates proper copies.
        /// </summary>
        /// <param name="value">The boolean value to clone.</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Clone_ValueIsBool_ReturnsNewInstanceWithSameValue(bool value)
        {
            // Arrange
            var original = new ValueExpr(value) { IsConst = false };

            // Act
            var cloned = (ValueExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.Equal(value, cloned.Value);
            Assert.False(cloned.IsConst);
        }

        /// <summary>
        /// Tests that Clone with double values creates proper copies.
        /// </summary>
        /// <param name="value">The double value to clone.</param>
        [Theory]
        [InlineData(0.0)]
        [InlineData(3.14159)]
        [InlineData(-2.71828)]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void Clone_DoubleValues_ReturnsNewInstanceWithSameValue(double value)
        {
            // Arrange
            var original = new ValueExpr(value) { IsConst = true };

            // Act
            var cloned = (ValueExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            if (double.IsNaN(value))
            {
                Assert.True(double.IsNaN((double)cloned.Value));
            }
            else
            {
                Assert.Equal(value, cloned.Value);
            }
            Assert.True(cloned.IsConst);
        }

        /// <summary>
        /// Tests that Clone with decimal values creates proper copies.
        /// </summary>
        [Fact]
        public void Clone_ValueIsDecimal_ReturnsNewInstanceWithSameValue()
        {
            // Arrange
            var decimalValue = 123.456m;
            var original = new ValueExpr(decimalValue) { IsConst = false };

            // Act
            var cloned = (ValueExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.Equal(decimalValue, cloned.Value);
            Assert.False(cloned.IsConst);
        }

        /// <summary>
        /// Tests that Clone with DateTime values creates proper copies.
        /// </summary>
        [Fact]
        public void Clone_ValueIsDateTime_ReturnsNewInstanceWithSameValue()
        {
            // Arrange
            var dateTime = new DateTime(2023, 12, 25, 10, 30, 45);
            var original = new ValueExpr(dateTime) { IsConst = true };

            // Act
            var cloned = (ValueExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.Equal(dateTime, cloned.Value);
            Assert.True(cloned.IsConst);
        }

        /// <summary>
        /// Tests that Clone with collection values performs shallow copy of the collection.
        /// </summary>
        [Fact]
        public void Clone_ValueIsList_ReturnsNewInstanceWithSameCollectionReference()
        {
            // Arrange
            var list = new List<int> { 1, 2, 3 };
            var original = new ValueExpr(list) { IsConst = false };
            // Act
            var cloned = (ValueExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.Same(list, cloned.Value);
            Assert.Equal(original.IsConst, cloned.IsConst);
        }

        /// <summary>
        /// Tests that Clone with array values performs shallow copy of the array.
        /// </summary>
        [Fact]
        public void Clone_ValueIsArray_ReturnsNewInstanceWithSameArrayReference()
        {
            // Arrange
            var array = new int[] { 1, 2, 3 };
            var original = new ValueExpr(array) { IsConst = true };

            // Act
            var cloned = (ValueExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            // Since array is cloneable, we expect a new instance of the array to be created, so we check for value equality rather than reference equality.
            Assert.NotSame(array, cloned.Value);
            Assert.Equal(array, (int[])cloned.Value);
            Assert.True(cloned.IsConst);
        }

        /// <summary>
        /// Tests that Clone with empty string creates proper copy.
        /// </summary>
        [Fact]
        public void Clone_ValueIsEmptyString_ReturnsNewInstanceWithEmptyString()
        {
            // Arrange
            var original = new ValueExpr(string.Empty) { IsConst = false };

            // Act
            var cloned = (ValueExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.Equal(string.Empty, cloned.Value);
            Assert.False(cloned.IsConst);
        }

        /// <summary>
        /// Tests that Clone with nested Expr (Expr containing Expr) properly deep clones all levels.
        /// </summary>
        [Fact]
        public void Clone_NestedExpr_DeepClonesAllLevels()
        {
            // Arrange
            var innermost = new ValueExpr(42) { IsConst = true };
            var middle = new ValueExpr(innermost) { IsConst = false };
            var outer = new ValueExpr(middle) { IsConst = true };

            // Act
            var cloned = (ValueExpr)outer.Clone();

            // Assert
            Assert.NotSame(outer, cloned);
            Assert.True(cloned.IsConst);

            var clonedMiddle = (ValueExpr)cloned.Value;
            Assert.NotSame(middle, clonedMiddle);
            Assert.False(clonedMiddle.IsConst);

            var clonedInnermost = (ValueExpr)clonedMiddle.Value;
            Assert.NotSame(innermost, clonedInnermost);
            Assert.Equal(42, clonedInnermost.Value);
            Assert.True(clonedInnermost.IsConst);
        }

        /// <summary>
        /// Tests that Clone with default constructor (empty ValueExpr) creates proper copy.
        /// </summary>
        [Fact]
        public void Clone_DefaultConstructor_ReturnsNewInstanceWithNullValue()
        {
            // Arrange
            var original = new ValueExpr { IsConst = true };

            // Act
            var cloned = (ValueExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.Null(cloned.Value);
            Assert.True(cloned.IsConst);
        }

        /// <summary>
        /// Tests that GetHashCode returns consistent hash codes for null values.
        /// </summary>
        [Fact]
        public void GetHashCode_NullValue_ReturnsConsistentHashCode()
        {
            // Arrange
            var expr1 = new ValueExpr(null);
            var expr2 = new ValueExpr(null);

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns same hash code for equal primitive values.
        /// Verifies hash code consistency with equality contract.
        /// </summary>
        /// <param name="value1">First value to test.</param>
        /// <param name="value2">Second value to test (should equal first).</param>
        [Theory]
        [InlineData(123, 123)]
        [InlineData(0, 0)]
        [InlineData(-1, -1)]
        [InlineData(int.MinValue, int.MinValue)]
        [InlineData(int.MaxValue, int.MaxValue)]
        public void GetHashCode_EqualIntValues_ReturnsSameHashCode(int value1, int value2)
        {
            // Arrange
            var expr1 = new ValueExpr(value1);
            var expr2 = new ValueExpr(value2);

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns same hash code for numerically equal values of different types.
        /// This verifies that 123 (int) and 123L (long) produce the same hash code since they are equal.
        /// </summary>
        [Fact]
        public void GetHashCode_IntAndLongEqualValues_ReturnsSameHashCode()
        {
            // Arrange
            var exprInt = new ValueExpr(123);
            var exprLong = new ValueExpr(123L);

            // Act
            int hashInt = exprInt.GetHashCode();
            int hashLong = exprLong.GetHashCode();

            // Assert
            Assert.True(exprInt.Equals(exprLong), "Values should be equal");
            Assert.Equal(hashInt, hashLong);
        }

        /// <summary>
        /// Tests that GetHashCode returns same hash code for equal string values.
        /// </summary>
        /// <param name="value1">First string to test.</param>
        /// <param name="value2">Second string to test (should equal first).</param>
        [Theory]
        [InlineData("test", "test")]
        [InlineData("", "")]
        [InlineData(" ", " ")]
        [InlineData("abc123!@#$%^&*()", "abc123!@#$%^&*()")]
        public void GetHashCode_EqualStringValues_ReturnsSameHashCode(string value1, string value2)
        {
            // Arrange
            var expr1 = new ValueExpr(value1);
            var expr2 = new ValueExpr(value2);

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns same hash code for equal boolean values.
        /// </summary>
        /// <param name="value">Boolean value to test.</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetHashCode_EqualBoolValues_ReturnsSameHashCode(bool value)
        {
            // Arrange
            var expr1 = new ValueExpr(value);
            var expr2 = new ValueExpr(value);

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns same hash code for equal DateTime values.
        /// </summary>
        [Fact]
        public void GetHashCode_EqualDateTimeValues_ReturnsSameHashCode()
        {
            // Arrange
            var dateTime = new DateTime(2024, 1, 15, 10, 30, 0);
            var expr1 = new ValueExpr(dateTime);
            var expr2 = new ValueExpr(dateTime);

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns same hash code for equal double values.
        /// Tests edge cases including NaN, PositiveInfinity, and NegativeInfinity.
        /// </summary>
        /// <param name="value">Double value to test.</param>
        [Theory]
        [InlineData(3.14)]
        [InlineData(0.0)]
        [InlineData(-1.5)]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void GetHashCode_EqualDoubleValues_ReturnsSameHashCode(double value)
        {
            // Arrange
            var expr1 = new ValueExpr(value);
            var expr2 = new ValueExpr(value);

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns same hash code for equal decimal values.
        /// </summary>
        /// <param name="value">Decimal value to test.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(123.45)]
        [InlineData(-999.99)]
        public void GetHashCode_EqualDecimalValues_ReturnsSameHashCode(double value)
        {
            // Arrange
            var decimalValue = (decimal)value;
            var expr1 = new ValueExpr(decimalValue);
            var expr2 = new ValueExpr(decimalValue);

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns same hash code for equal collections with different underlying types.
        /// This verifies that List&lt;int&gt; {1,2,3} and int[] {1,2,3} produce the same hash code since they are equal.
        /// </summary>
        [Fact]
        public void GetHashCode_EqualCollections_ReturnsSameHashCode()
        {
            // Arrange
            var exprList = new ValueExpr(new List<int> { 1, 2, 3 });
            var exprArray = new ValueExpr(new int[] { 1, 2, 3 });

            // Act
            int hashList = exprList.GetHashCode();
            int hashArray = exprArray.GetHashCode();

            // Assert
            Assert.True(exprList.Equals(exprArray), "Collections should be equal");
            Assert.Equal(hashList, hashArray);
        }

        /// <summary>
        /// Tests that GetHashCode returns same hash code for empty collections.
        /// </summary>
        [Fact]
        public void GetHashCode_EmptyCollections_ReturnsSameHashCode()
        {
            // Arrange
            var exprList = new ValueExpr(new List<int>());
            var exprArray = new ValueExpr(new int[0]);

            // Act
            int hashList = exprList.GetHashCode();
            int hashArray = exprArray.GetHashCode();

            // Assert
            Assert.Equal(hashList, hashArray);
        }

        /// <summary>
        /// Tests that GetHashCode returns same hash code when called multiple times on the same instance.
        /// Verifies hash code stability.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsConsistentHashCode()
        {
            // Arrange
            var expr = new ValueExpr(42);

            // Act
            int hash1 = expr.GetHashCode();
            int hash2 = expr.GetHashCode();
            int hash3 = expr.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
            Assert.Equal(hash2, hash3);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes for different values.
        /// Note: This is not strictly required by the hash code contract, but is desirable for performance.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentValues_ReturnsDifferentHashCodes()
        {
            // Arrange
            var expr1 = new ValueExpr(123);
            var expr2 = new ValueExpr(456);
            var expr3 = new ValueExpr("test");

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();
            int hash3 = expr3.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
            Assert.NotEqual(hash1, hash3);
            Assert.NotEqual(hash2, hash3);
        }

        /// <summary>
        /// Tests that GetHashCode returns same hash code for very long equal strings.
        /// </summary>
        [Fact]
        public void GetHashCode_VeryLongEqualStrings_ReturnsSameHashCode()
        {
            // Arrange
            var longString = new string('a', 10000);
            var expr1 = new ValueExpr(longString);
            var expr2 = new ValueExpr(longString);

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles nested collections correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_NestedCollections_ReturnsConsistentHashCode()
        {
            // Arrange
            var nestedList1 = new List<object> { new List<int> { 1, 2 }, new List<int> { 3, 4 } };
            var nestedList2 = new List<object> { new List<int> { 1, 2 }, new List<int> { 3, 4 } };
            var expr1 = new ValueExpr(nestedList1);
            var expr2 = new ValueExpr(nestedList2);

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns consistent hash code for single-element collections.
        /// </summary>
        [Fact]
        public void GetHashCode_SingleElementCollections_ReturnsSameHashCode()
        {
            // Arrange
            var exprList = new ValueExpr(new List<int> { 42 });
            var exprArray = new ValueExpr(new int[] { 42 });

            // Act
            int hashList = exprList.GetHashCode();
            int hashArray = exprArray.GetHashCode();

            // Assert
            Assert.Equal(hashList, hashArray);
        }

        /// <summary>
        /// Tests that GetHashCode respects the IsConst property does not affect hash code.
        /// Two ValueExpr instances with the same value but different IsConst should have the same hash code
        /// since IsConst is not part of the hash code calculation.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentIsConstSameValue_ReturnsSameHashCode()
        {
            // Arrange
            var expr1 = new ValueExpr(123) { IsConst = true };
            var expr2 = new ValueExpr(123) { IsConst = false };

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles collections with duplicate elements correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_CollectionsWithDuplicates_ReturnsConsistentHashCode()
        {
            // Arrange
            var expr1 = new ValueExpr(new int[] { 1, 1, 2, 2, 3, 3 });
            var expr2 = new ValueExpr(new List<int> { 1, 1, 2, 2, 3, 3 });

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that ToString returns "NULL" when Value is null.
        /// </summary>
        [Fact]
        public void ToString_ValueIsNull_ReturnsNull()
        {
            // Arrange
            var expr = new ValueExpr(null);

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal("NULL", result);
        }

        /// <summary>
        /// Tests that ToString returns the string value itself when Value is a string.
        /// Strings should not be treated as enumerable despite implementing IEnumerable.
        /// </summary>
        /// <param name="value">The string value to test.</param>
        /// <param name="expected">The expected result.</param>
        [Theory]
        [InlineData("hello", "hello")]
        [InlineData("", "")]
        [InlineData(" ", " ")]
        [InlineData("  \t\n  ", "  \t\n  ")]
        [InlineData("test with spaces", "test with spaces")]
        [InlineData("special!@#$%^&*()", "special!@#$%^&*()")]
        public void ToString_ValueIsString_ReturnsStringValue(string value, string expected)
        {
            // Arrange
            var expr = new ValueExpr(value);

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ToString returns the string representation of primitive types.
        /// </summary>
        /// <param name="value">The primitive value to test.</param>
        /// <param name="expected">The expected string representation.</param>
        [Theory]
        [InlineData(0, "0")]
        [InlineData(42, "42")]
        [InlineData(-1, "-1")]
        [InlineData(int.MinValue, "-2147483648")]
        [InlineData(int.MaxValue, "2147483647")]
        public void ToString_ValueIsInteger_ReturnsStringRepresentation(int value, string expected)
        {
            // Arrange
            var expr = new ValueExpr(value);

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ToString returns the string representation of long values.
        /// </summary>
        /// <param name="value">The long value to test.</param>
        /// <param name="expected">The expected string representation.</param>
        [Theory]
        [InlineData(0L, "0")]
        [InlineData(123456789L, "123456789")]
        [InlineData(-123456789L, "-123456789")]
        public void ToString_ValueIsLong_ReturnsStringRepresentation(long value, string expected)
        {
            // Arrange
            var expr = new ValueExpr(value);

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ToString returns the string representation of boolean values.
        /// </summary>
        /// <param name="value">The boolean value to test.</param>
        /// <param name="expected">The expected string representation.</param>
        [Theory]
        [InlineData(true, "True")]
        [InlineData(false, "False")]
        public void ToString_ValueIsBoolean_ReturnsStringRepresentation(bool value, string expected)
        {
            // Arrange
            var expr = new ValueExpr(value);

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ToString returns the string representation of double values including special values.
        /// </summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(3.14)]
        [InlineData(-2.718)]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        public void ToString_ValueIsDouble_ReturnsStringRepresentation(double value)
        {
            // Arrange
            var expr = new ValueExpr(value);

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal(value.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString returns the string representation of special double values.
        /// </summary>
        /// <param name="value">The special double value to test.</param>
        /// <param name="expected">The expected string representation.</param>
        [Theory]
        [InlineData(double.NaN, "NaN")]
        [InlineData(double.PositiveInfinity, "∞")]
        [InlineData(double.NegativeInfinity, "-∞")]
        public void ToString_ValueIsSpecialDouble_ReturnsStringRepresentation(double value, string expected)
        {
            // Arrange
            var expr = new ValueExpr(value);

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ToString returns the string representation of decimal values.
        /// </summary>
        [Fact]
        public void ToString_ValueIsDecimal_ReturnsStringRepresentation()
        {
            // Arrange
            var value = 123.45m;
            var expr = new ValueExpr(value);

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal("123.45", result);
        }

        /// <summary>
        /// Tests that ToString returns the string representation of DateTime values.
        /// </summary>
        [Fact]
        public void ToString_ValueIsDateTime_ReturnsStringRepresentation()
        {
            // Arrange
            var value = new DateTime(2024, 1, 15, 10, 30, 45);
            var expr = new ValueExpr(value);

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal(value.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString returns empty parentheses for an empty collection.
        /// </summary>
        [Fact]
        public void ToString_ValueIsEmptyCollection_ReturnsEmptyParentheses()
        {
            // Arrange
            var expr = new ValueExpr(new List<int>());

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal("()", result);
        }

        /// <summary>
        /// Tests that ToString returns empty parentheses for an empty array.
        /// </summary>
        [Fact]
        public void ToString_ValueIsEmptyArray_ReturnsEmptyParentheses()
        {
            // Arrange
            var expr = new ValueExpr(new int[0]);

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal("()", result);
        }

        /// <summary>
        /// Tests that ToString returns parentheses with a single item for a collection with one element.
        /// </summary>
        [Fact]
        public void ToString_ValueIsSingleItemCollection_ReturnsSingleItemInParentheses()
        {
            // Arrange
            var expr = new ValueExpr(new List<int> { 42 });

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal("(42)", result);
        }

        /// <summary>
        /// Tests that ToString returns comma-separated values in parentheses for a collection with multiple items.
        /// </summary>
        [Fact]
        public void ToString_ValueIsMultipleItemCollection_ReturnsCommaSeparatedInParentheses()
        {
            // Arrange
            var expr = new ValueExpr(new List<int> { 1, 2, 3, 4, 5 });

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal("(1,2,3,4,5)", result);
        }

        /// <summary>
        /// Tests that ToString handles collections with null items by representing them as "NULL".
        /// </summary>
        [Fact]
        public void ToString_ValueIsCollectionWithNullItems_ReturnsNullAsNULL()
        {
            // Arrange
            var expr = new ValueExpr(new List<object> { 1, null, 3, null, 5 });

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal("(1,NULL,3,NULL,5)", result);
        }

        /// <summary>
        /// Tests that ToString handles collections with all null items.
        /// </summary>
        [Fact]
        public void ToString_ValueIsCollectionWithAllNullItems_ReturnsAllNULL()
        {
            // Arrange
            var expr = new ValueExpr(new List<object> { null, null, null });

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal("(NULL,NULL,NULL)", result);
        }

        /// <summary>
        /// Tests that ToString handles collections with mixed types correctly.
        /// </summary>
        [Fact]
        public void ToString_ValueIsCollectionWithMixedTypes_ReturnsCorrectString()
        {
            // Arrange
            var expr = new ValueExpr(new List<object> { 1, "test", 3.14, true, null });

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal("(1,test,3.14,True,NULL)", result);
        }

        /// <summary>
        /// Tests that ToString handles string arrays correctly as collections.
        /// </summary>
        [Fact]
        public void ToString_ValueIsStringArray_ReturnsCommaSeparatedStrings()
        {
            // Arrange
            var expr = new ValueExpr(new string[] { "apple", "banana", "cherry" });

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal("(apple,banana,cherry)", result);
        }

        /// <summary>
        /// Tests that ToString handles arrays with different value types.
        /// </summary>
        [Fact]
        public void ToString_ValueIsIntArray_ReturnsCommaSeparatedIntegers()
        {
            // Arrange
            var expr = new ValueExpr(new int[] { 10, 20, 30 });

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal("(10,20,30)", result);
        }

        /// <summary>
        /// Tests that ToString handles HashSet collections correctly.
        /// </summary>
        [Fact]
        public void ToString_ValueIsHashSet_ReturnsCommaSeparatedValues()
        {
            // Arrange
            var hashSet = new HashSet<int> { 1, 2, 3 };
            var expr = new ValueExpr(hashSet);

            // Act
            var result = expr.ToString();

            // Assert - Note: HashSet order may vary, so we check for presence of all items
            Assert.StartsWith("(", result);
            Assert.EndsWith(")", result);
            Assert.Contains("1", result);
            Assert.Contains("2", result);
            Assert.Contains("3", result);
        }

        /// <summary>
        /// Tests that ToString handles large collections efficiently.
        /// </summary>
        [Fact]
        public void ToString_ValueIsLargeCollection_ReturnsCorrectString()
        {
            // Arrange
            var largeList = new List<int>();
            for (int i = 0; i < 1000; i++)
            {
                largeList.Add(i);
            }
            var expr = new ValueExpr(largeList);

            // Act
            var result = expr.ToString();

            // Assert
            Assert.StartsWith("(0,1,2,3,4,5,6,7,8,9,", result);
            Assert.EndsWith(",999)", result);
            Assert.Contains(",500,", result);
        }

        /// <summary>
        /// Tests that ToString handles nested collections by treating inner collections as objects.
        /// </summary>
        [Fact]
        public void ToString_ValueIsNestedCollection_ReturnsCollectionOfCollections()
        {
            // Arrange
            var nestedList = new List<List<int>>
            {
                new List<int> { 1, 2 },
                new List<int> { 3, 4 }
            };
            var expr = new ValueExpr(nestedList);

            // Act
            var result = expr.ToString();

            // Assert - Inner collections will use their default ToString()
            Assert.StartsWith("(", result);
            Assert.EndsWith(")", result);
            Assert.Contains("System.Collections.Generic.List", result);
        }

        /// <summary>
        /// Tests that ToString handles ArrayList correctly.
        /// </summary>
        [Fact]
        public void ToString_ValueIsArrayList_ReturnsCommaSeparatedValues()
        {
            // Arrange
            var arrayList = new ArrayList { 1, "two", 3.0 };
            var expr = new ValueExpr(arrayList);

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal("(1,two,3)", result);
        }

        /// <summary>
        /// Tests that ToString handles custom objects by calling their ToString() method.
        /// </summary>
        [Fact]
        public void ToString_ValueIsCustomObject_ReturnsObjectToString()
        {
            // Arrange
            var customObj = new CustomTestObject { Name = "Test", Value = 42 };
            var expr = new ValueExpr(customObj);

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal("CustomTestObject: Test (42)", result);
        }

        /// <summary>
        /// Tests that ToString handles collections of custom objects.
        /// </summary>
        [Fact]
        public void ToString_ValueIsCollectionOfCustomObjects_ReturnsCommaSeparatedCustomObjects()
        {
            // Arrange
            var list = new List<CustomTestObject>
            {
                new CustomTestObject { Name = "First", Value = 1 },
                new CustomTestObject { Name = "Second", Value = 2 }
            };
            var expr = new ValueExpr(list);

            // Act
            var result = expr.ToString();

            // Assert
            Assert.Equal("(CustomTestObject: First (1),CustomTestObject: Second (2))", result);
        }

        /// <summary>
        /// Helper class for testing custom object ToString() behavior.
        /// </summary>
        private class CustomTestObject
        {
            public string Name { get; set; }
            public int Value { get; set; }

            public override string ToString()
            {
                return $"CustomTestObject: {Name} ({Value})";
            }
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Value property with null.
        /// </summary>
        [Fact]
        public void Constructor_WithNull_SetsValuePropertyToNull()
        {
            // Arrange & Act
            var result = new ValueExpr(null);

            // Assert
            Assert.Null(result.Value);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Value property with various primitive integer values.
        /// </summary>
        /// <param name="value">The integer value to test.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        [InlineData(int.MaxValue)]
        [InlineData(42)]
        [InlineData(-42)]
        public void Constructor_WithIntegerValues_SetsValuePropertyCorrectly(int value)
        {
            // Arrange & Act
            var result = new ValueExpr(value);

            // Assert
            Assert.Equal(value, result.Value);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Value property with various long values.
        /// </summary>
        /// <param name="value">The long value to test.</param>
        [Theory]
        [InlineData(0L)]
        [InlineData(1L)]
        [InlineData(-1L)]
        [InlineData(long.MinValue)]
        [InlineData(long.MaxValue)]
        public void Constructor_WithLongValues_SetsValuePropertyCorrectly(long value)
        {
            // Arrange & Act
            var result = new ValueExpr(value);

            // Assert
            Assert.Equal(value, result.Value);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Value property with various double values including special floating-point values.
        /// </summary>
        /// <param name="value">The double value to test.</param>
        [Theory]
        [InlineData(0.0)]
        [InlineData(1.0)]
        [InlineData(-1.0)]
        [InlineData(3.14159)]
        [InlineData(-3.14159)]
        [InlineData(double.MinValue)]
        [InlineData(double.MaxValue)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        [InlineData(double.Epsilon)]
        public void Constructor_WithDoubleValues_SetsValuePropertyCorrectly(double value)
        {
            // Arrange & Act
            var result = new ValueExpr(value);

            // Assert
            Assert.Equal(value, result.Value);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Value property with various decimal values.
        /// </summary>
        [Theory]
        [MemberData(nameof(GetDecimalTestData))]
        public void Constructor_WithDecimalValues_SetsValuePropertyCorrectly(decimal value)
        {
            // Arrange & Act
            var result = new ValueExpr(value);

            // Assert
            Assert.Equal(value, result.Value);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Value property with boolean values.
        /// </summary>
        /// <param name="value">The boolean value to test.</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Constructor_WithBooleanValues_SetsValuePropertyCorrectly(bool value)
        {
            // Arrange & Act
            var result = new ValueExpr(value);

            // Assert
            Assert.Equal(value, result.Value);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Value property with various string values including edge cases.
        /// </summary>
        /// <param name="value">The string value to test.</param>
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("  ")]
        [InlineData("\t")]
        [InlineData("\r\n")]
        [InlineData("test")]
        [InlineData("hello world")]
        [InlineData("a")]
        [InlineData("123")]
        [InlineData("!@#$%^&*()")]
        [InlineData("\0")]
        public void Constructor_WithStringValues_SetsValuePropertyCorrectly(string value)
        {
            // Arrange & Act
            var result = new ValueExpr(value);

            // Assert
            Assert.Equal(value, result.Value);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Value property with a very long string.
        /// </summary>
        [Fact]
        public void Constructor_WithVeryLongString_SetsValuePropertyCorrectly()
        {
            // Arrange
            var value = new string('a', 10000);

            // Act
            var result = new ValueExpr(value);

            // Assert
            Assert.Equal(value, result.Value);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Value property with DateTime values.
        /// </summary>
        [Theory]
        [MemberData(nameof(GetDateTimeTestData))]
        public void Constructor_WithDateTimeValues_SetsValuePropertyCorrectly(DateTime value)
        {
            // Arrange & Act
            var result = new ValueExpr(value);

            // Assert
            Assert.Equal(value, result.Value);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Value property with collection values.
        /// </summary>
        [Fact]
        public void Constructor_WithList_SetsValuePropertyCorrectly()
        {
            // Arrange
            var value = new List<int> { 1, 2, 3 };

            // Act
            var result = new ValueExpr(value);

            // Assert
            Assert.Same(value, result.Value);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Value property with an empty collection.
        /// </summary>
        [Fact]
        public void Constructor_WithEmptyList_SetsValuePropertyCorrectly()
        {
            // Arrange
            var value = new List<int>();

            // Act
            var result = new ValueExpr(value);

            // Assert
            Assert.Same(value, result.Value);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Value property with an array.
        /// </summary>
        [Fact]
        public void Constructor_WithArray_SetsValuePropertyCorrectly()
        {
            // Arrange
            var value = new int[] { 1, 2, 3 };

            // Act
            var result = new ValueExpr(value);

            // Assert
            Assert.Same(value, result.Value);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Value property with a custom object.
        /// </summary>
        [Fact]
        public void Constructor_WithCustomObject_SetsValuePropertyCorrectly()
        {
            // Arrange
            var value = new { Name = "Test", Age = 42 };

            // Act
            var result = new ValueExpr(value);

            // Assert
            Assert.Same(value, result.Value);
        }

        /// <summary>
        /// Provides test data for decimal value constructor tests.
        /// </summary>
        public static IEnumerable<object[]> GetDecimalTestData()
        {
            yield return new object[] { 0m };
            yield return new object[] { 1m };
            yield return new object[] { -1m };
            yield return new object[] { 3.14m };
            yield return new object[] { -3.14m };
            yield return new object[] { decimal.MinValue };
            yield return new object[] { decimal.MaxValue };
        }

        /// <summary>
        /// Provides test data for DateTime value constructor tests.
        /// </summary>
        public static IEnumerable<object[]> GetDateTimeTestData()
        {
            yield return new object[] { DateTime.MinValue };
            yield return new object[] { DateTime.MaxValue };
            yield return new object[] { new DateTime(2024, 1, 1) };
            yield return new object[] { new DateTime(1999, 12, 31, 23, 59, 59) };
        }

        /// <summary>
        /// Tests that the ExprType property returns the correct ExprType.Value enum value
        /// when accessed on a ValueExpr instance created with the default constructor.
        /// Expected result: ExprType.Value.
        /// </summary>
        [Fact]
        public void ExprType_DefaultConstructor_ReturnsValueExprType()
        {
            // Arrange
            var valueExpr = new ValueExpr();

            // Act
            var result = valueExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Value, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns the correct ExprType.Value enum value
        /// when accessed on a ValueExpr instance created with a parameterized constructor.
        /// Uses various input types to ensure the ExprType property is consistent.
        /// Expected result: ExprType.Value for all instances regardless of constructor parameters.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("test string")]
        [InlineData(42)]
        [InlineData(true)]
        [InlineData(3.14)]
        public void ExprType_ParameterizedConstructor_ReturnsValueExprType(object? value)
        {
            // Arrange
            var valueExpr = new ValueExpr(value);

            // Act
            var result = valueExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Value, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns the same value for multiple instances,
        /// ensuring consistency across different ValueExpr objects.
        /// Expected result: All instances return ExprType.Value.
        /// </summary>
        [Fact]
        public void ExprType_MultipleInstances_ReturnsSameValue()
        {
            // Arrange
            var instance1 = new ValueExpr();
            var instance2 = new ValueExpr("test");
            var instance3 = new ValueExpr(123);

            // Act
            var result1 = instance1.ExprType;
            var result2 = instance2.ExprType;
            var result3 = instance3.ExprType;

            // Assert
            Assert.Equal(ExprType.Value, result1);
            Assert.Equal(ExprType.Value, result2);
            Assert.Equal(ExprType.Value, result3);
            Assert.Equal(result1, result2);
            Assert.Equal(result2, result3);
        }

        /// <summary>
        /// Tests that the ExprType property value remains constant even when the Value property is modified.
        /// Expected result: ExprType.Value regardless of Value property changes.
        /// </summary>
        [Fact]
        public void ExprType_AfterModifyingValue_RemainsConstant()
        {
            // Arrange
            var valueExpr = new ValueExpr("initial");
            var initialExprType = valueExpr.ExprType;

            // Act
            valueExpr.Value = "modified";
            var afterModificationExprType = valueExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Value, initialExprType);
            Assert.Equal(ExprType.Value, afterModificationExprType);
            Assert.Equal(initialExprType, afterModificationExprType);
        }

        /// <summary>
        /// Tests that the ExprType property value remains constant even when the IsConst property is modified.
        /// Expected result: ExprType.Value regardless of IsConst property changes.
        /// </summary>
        [Fact]
        public void ExprType_AfterModifyingIsConst_RemainsConstant()
        {
            // Arrange
            var valueExpr = new ValueExpr();
            var initialExprType = valueExpr.ExprType;

            // Act
            valueExpr.IsConst = true;
            var afterModificationExprType = valueExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Value, initialExprType);
            Assert.Equal(ExprType.Value, afterModificationExprType);
            Assert.Equal(initialExprType, afterModificationExprType);
        }
    }
}

namespace LiteOrm.Tests.UnitTests
{
    /// <summary>
    /// Unit tests for the ValueExpr.Equals method.
    /// </summary>
    public partial class ValueExprEqualsTests
    {
        /// <summary>
        /// Tests that Equals returns true when comparing an instance with itself (reference equality).
        /// </summary>
        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            // Arrange
            var expr = new ValueExpr(123);

            // Act
            var result = expr.Equals(expr);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with null.
        /// </summary>
        [Fact]
        public void Equals_NullObject_ReturnsFalse()
        {
            // Arrange
            var expr = new ValueExpr(123);

            // Act
            var result = expr.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with an object of a different type.
        /// </summary>
        [Theory]
        [InlineData("test")]
        [InlineData(123)]
        [InlineData(true)]
        public void Equals_DifferentType_ReturnsFalse(object obj)
        {
            // Arrange
            var expr = new ValueExpr(123);

            // Act
            var result = expr.Equals(obj);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing two ValueExpr instances with the same integer value.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(-1)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public void Equals_SameIntegerValue_ReturnsTrue(int value)
        {
            // Arrange
            var expr1 = new ValueExpr(value);
            var expr2 = new ValueExpr(value);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing two ValueExpr instances with different integer values.
        /// </summary>
        [Fact]
        public void Equals_DifferentIntegerValues_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ValueExpr(123);
            var expr2 = new ValueExpr(456);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing two ValueExpr instances with the same string value.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("test")]
        [InlineData(" ")]
        [InlineData("  whitespace  ")]
        [InlineData("special!@#$%^&*()characters")]
        public void Equals_SameStringValue_ReturnsTrue(string value)
        {
            // Arrange
            var expr1 = new ValueExpr(value);
            var expr2 = new ValueExpr(value);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing two ValueExpr instances with different string values.
        /// </summary>
        [Fact]
        public void Equals_DifferentStringValues_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ValueExpr("test");
            var expr2 = new ValueExpr("other");

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both ValueExpr instances have null values.
        /// </summary>
        [Fact]
        public void Equals_BothNullValues_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ValueExpr(null);
            var expr2 = new ValueExpr(null);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one ValueExpr has a null value and the other doesn't.
        /// </summary>
        [Fact]
        public void Equals_OneNullOneNonNull_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ValueExpr(null);
            var expr2 = new ValueExpr(123);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing ValueExpr instances with different numeric types but same numerical value.
        /// </summary>
        [Fact]
        public void Equals_DifferentNumericTypesSameValue_ReturnsTrue()
        {
            // Arrange
            var exprInt = new ValueExpr(123);
            var exprLong = new ValueExpr(123L);

            // Act
            var result = exprInt.Equals(exprLong);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing ValueExpr instances with same double values.
        /// </summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(1.5)]
        [InlineData(-1.5)]
        [InlineData(double.MaxValue)]
        [InlineData(double.MinValue)]
        public void Equals_SameDoubleValue_ReturnsTrue(double value)
        {
            // Arrange
            var expr1 = new ValueExpr(value);
            var expr2 = new ValueExpr(value);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals handles double.NaN correctly.
        /// </summary>
        [Fact]
        public void Equals_DoubleNaN_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ValueExpr(double.NaN);
            var expr2 = new ValueExpr(double.NaN);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            // NaN != NaN per IEEE 754 standard
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing ValueExpr instances with double.PositiveInfinity.
        /// </summary>
        [Fact]
        public void Equals_DoublePositiveInfinity_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ValueExpr(double.PositiveInfinity);
            var expr2 = new ValueExpr(double.PositiveInfinity);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing ValueExpr instances with double.NegativeInfinity.
        /// </summary>
        [Fact]
        public void Equals_DoubleNegativeInfinity_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ValueExpr(double.NegativeInfinity);
            var expr2 = new ValueExpr(double.NegativeInfinity);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing ValueExpr instances with same decimal values.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(123.45)]
        [InlineData(-123.45)]
        public void Equals_SameDecimalValue_ReturnsTrue(double value)
        {
            // Arrange
            var decimalValue = (decimal)value;
            var expr1 = new ValueExpr(decimalValue);
            var expr2 = new ValueExpr(decimalValue);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing ValueExpr instances with same boolean values.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Equals_SameBooleanValue_ReturnsTrue(bool value)
        {
            // Arrange
            var expr1 = new ValueExpr(value);
            var expr2 = new ValueExpr(value);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing ValueExpr instances with different boolean values.
        /// </summary>
        [Fact]
        public void Equals_DifferentBooleanValues_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ValueExpr(true);
            var expr2 = new ValueExpr(false);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing ValueExpr instances with same DateTime values.
        /// </summary>
        [Fact]
        public void Equals_SameDateTimeValue_ReturnsTrue()
        {
            // Arrange
            var dateTime = new DateTime(2023, 1, 15, 10, 30, 45);
            var expr1 = new ValueExpr(dateTime);
            var expr2 = new ValueExpr(dateTime);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing ValueExpr instances with different DateTime values.
        /// </summary>
        [Fact]
        public void Equals_DifferentDateTimeValues_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ValueExpr(new DateTime(2023, 1, 15));
            var expr2 = new ValueExpr(new DateTime(2023, 1, 16));

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing ValueExpr instances with collections containing same elements.
        /// </summary>
        [Fact]
        public void Equals_CollectionsWithSameElements_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ValueExpr(new List<int> { 1, 2, 3 });
            var expr2 = new ValueExpr(new int[] { 1, 2, 3 });

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing ValueExpr instances with collections containing different elements.
        /// </summary>
        [Fact]
        public void Equals_CollectionsWithDifferentElements_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ValueExpr(new List<int> { 1, 2, 3 });
            var expr2 = new ValueExpr(new List<int> { 1, 2, 4 });

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing ValueExpr instances with collections of different lengths.
        /// </summary>
        [Fact]
        public void Equals_CollectionsWithDifferentLengths_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ValueExpr(new List<int> { 1, 2, 3 });
            var expr2 = new ValueExpr(new List<int> { 1, 2 });

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing ValueExpr instances with empty collections.
        /// </summary>
        [Fact]
        public void Equals_EmptyCollections_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ValueExpr(new List<int>());
            var expr2 = new ValueExpr(new int[0]);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing ValueExpr instances with collections containing single element.
        /// </summary>
        [Fact]
        public void Equals_SingleElementCollections_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ValueExpr(new List<int> { 42 });
            var expr2 = new ValueExpr(new int[] { 42 });

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing ValueExpr instances with nested collections.
        /// </summary>
        [Fact]
        public void Equals_NestedCollections_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ValueExpr(new List<List<int>> { new List<int> { 1, 2 }, new List<int> { 3, 4 } });
            var expr2 = new ValueExpr(new List<List<int>> { new List<int> { 1, 2 }, new List<int> { 3, 4 } });

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing ValueExpr instances with different nested collections.
        /// </summary>
        [Fact]
        public void Equals_DifferentNestedCollections_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ValueExpr(new List<List<int>> { new List<int> { 1, 2 }, new List<int> { 3, 4 } });
            var expr2 = new ValueExpr(new List<List<int>> { new List<int> { 1, 2 }, new List<int> { 3, 5 } });

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals handles mixed numeric types in collections correctly.
        /// </summary>
        [Fact]
        public void Equals_CollectionsWithMixedNumericTypes_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ValueExpr(new List<object> { 1, 2L, 3 });
            var expr2 = new ValueExpr(new List<object> { 1L, 2, 3L });

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing a collection ValueExpr with a non-collection ValueExpr.
        /// </summary>
        [Fact]
        public void Equals_CollectionVsNonCollection_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ValueExpr(new List<int> { 1, 2, 3 });
            var expr2 = new ValueExpr(123);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals handles very long strings correctly.
        /// </summary>
        [Fact]
        public void Equals_VeryLongStrings_ReturnsTrue()
        {
            // Arrange
            var longString = new string('a', 10000);
            var expr1 = new ValueExpr(longString);
            var expr2 = new ValueExpr(longString);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing ValueExpr instances with strings that differ by case.
        /// </summary>
        [Fact]
        public void Equals_StringsDifferByCase_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ValueExpr("Test");
            var expr2 = new ValueExpr("test");

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals handles collections with null elements correctly.
        /// </summary>
        [Fact]
        public void Equals_CollectionsWithNullElements_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ValueExpr(new List<object> { 1, null, 3 });
            var expr2 = new ValueExpr(new List<object> { 1, null, 3 });

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when collections differ in null element positions.
        /// </summary>
        [Fact]
        public void Equals_CollectionsDifferInNullPositions_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ValueExpr(new List<object> { 1, null, 3 });
            var expr2 = new ValueExpr(new List<object> { null, 1, 3 });

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }
    }
}