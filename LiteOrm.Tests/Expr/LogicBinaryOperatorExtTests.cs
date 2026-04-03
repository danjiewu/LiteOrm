using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the LogicBinaryOperatorExt.Positive extension method.
    /// </summary>
    public class LogicBinaryOperatorExtTests
    {
        /// <summary>
        /// Tests that the Positive method correctly removes the Not flag from operators.
        /// </summary>
        /// <param name="input">The input operator value.</param>
        /// <param name="expected">The expected output after removing the Not flag.</param>
        [Theory]
        [InlineData(LogicOperator.Equal, LogicOperator.Equal)]
        [InlineData(LogicOperator.GreaterThan, LogicOperator.GreaterThan)]
        [InlineData(LogicOperator.LessThan, LogicOperator.LessThan)]
        [InlineData(LogicOperator.StartsWith, LogicOperator.StartsWith)]
        [InlineData(LogicOperator.EndsWith, LogicOperator.EndsWith)]
        [InlineData(LogicOperator.Contains, LogicOperator.Contains)]
        [InlineData(LogicOperator.Like, LogicOperator.Like)]
        [InlineData(LogicOperator.In, LogicOperator.In)]
        [InlineData(LogicOperator.RegexpLike, LogicOperator.RegexpLike)]
        [InlineData(LogicOperator.NotEqual, LogicOperator.Equal)]
        [InlineData(LogicOperator.GreaterThanOrEqual, LogicOperator.LessThan)]
        [InlineData(LogicOperator.LessThanOrEqual, LogicOperator.GreaterThan)]
        [InlineData(LogicOperator.NotStartsWith, LogicOperator.StartsWith)]
        [InlineData(LogicOperator.NotEndsWith, LogicOperator.EndsWith)]
        [InlineData(LogicOperator.NotContains, LogicOperator.Contains)]
        [InlineData(LogicOperator.NotLike, LogicOperator.Like)]
        [InlineData(LogicOperator.NotIn, LogicOperator.In)]
        [InlineData(LogicOperator.NotRegexpLike, LogicOperator.RegexpLike)]
        public void Positive_WithVariousOperators_ReturnsOperatorWithoutNotFlag(LogicOperator input, LogicOperator expected)
        {
            // Arrange - параметры переданы через InlineData

            // Act
            LogicOperator result = input.Positive();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that the Positive method returns zero when applied to the Not flag alone.
        /// </summary>
        [Fact]
        public void Positive_WithNotFlagOnly_ReturnsZero()
        {
            // Arrange
            LogicOperator input = LogicOperator.Not;

            // Act
            LogicOperator result = input.Positive();

            // Assert
            Assert.Equal((LogicOperator)0, result);
        }

        /// <summary>
        /// Tests that the Positive method handles undefined enum values by correctly removing the Not bit.
        /// </summary>
        /// <param name="undefinedValue">An undefined enum value to test.</param>
        /// <param name="expected">The expected result after removing the Not flag.</param>
        [Theory]
        [InlineData(100, 100)]
        [InlineData(164, 100)]
        [InlineData(255, 191)]
        public void Positive_WithUndefinedEnumValues_RemovesNotBit(int undefinedValue, int expected)
        {
            // Arrange
            LogicOperator input = (LogicOperator)undefinedValue;

            // Act
            LogicOperator result = input.Positive();

            // Assert
            Assert.Equal((LogicOperator)expected, result);
        }

        /// <summary>
        /// Tests that IsNot returns true when the operator has the Not flag set.
        /// </summary>
        /// <param name="oper">The operator with the Not flag.</param>
        [Theory]
        [InlineData(LogicOperator.Not)]
        [InlineData(LogicOperator.NotEqual)]
        [InlineData(LogicOperator.GreaterThanOrEqual)]
        [InlineData(LogicOperator.LessThanOrEqual)]
        [InlineData(LogicOperator.NotStartsWith)]
        [InlineData(LogicOperator.NotEndsWith)]
        [InlineData(LogicOperator.NotContains)]
        [InlineData(LogicOperator.NotLike)]
        [InlineData(LogicOperator.NotIn)]
        [InlineData(LogicOperator.NotRegexpLike)]
        public void IsNot_OperatorWithNotFlag_ReturnsTrue(LogicOperator oper)
        {
            // Arrange - parameter provided via InlineData

            // Act
            bool result = oper.IsNot();

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that IsNot returns false when the operator does not have the Not flag set.
        /// </summary>
        /// <param name="oper">The operator without the Not flag.</param>
        [Theory]
        [InlineData(LogicOperator.Equal)]
        [InlineData(LogicOperator.GreaterThan)]
        [InlineData(LogicOperator.LessThan)]
        [InlineData(LogicOperator.StartsWith)]
        [InlineData(LogicOperator.EndsWith)]
        [InlineData(LogicOperator.Contains)]
        [InlineData(LogicOperator.Like)]
        [InlineData(LogicOperator.In)]
        [InlineData(LogicOperator.RegexpLike)]
        public void IsNot_OperatorWithoutNotFlag_ReturnsFalse(LogicOperator oper)
        {
            // Arrange - parameter provided via InlineData

            // Act
            bool result = oper.IsNot();

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that IsNot correctly handles undefined enum values without the Not flag.
        /// </summary>
        [Fact]
        public void IsNot_UndefinedEnumValueWithoutNotFlag_ReturnsFalse()
        {
            // Arrange
            LogicOperator undefinedOper = (LogicOperator)100;

            // Act
            bool result = undefinedOper.IsNot();

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that IsNot correctly handles undefined enum values with the Not flag.
        /// </summary>
        [Fact]
        public void IsNot_UndefinedEnumValueWithNotFlag_ReturnsTrue()
        {
            // Arrange
            LogicOperator undefinedOper = (LogicOperator)(100 | 64);

            // Act
            bool result = undefinedOper.IsNot();

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Verifies that Opposite toggles the Not flag for positive operators (operators without the Not flag).
        /// Each positive operator should become its negated counterpart when Opposite is called.
        /// </summary>
        /// <param name="input">The input operator without the Not flag.</param>
        /// <param name="expected">The expected operator with the Not flag.</param>
        [Theory]
        [InlineData(LogicOperator.Equal, LogicOperator.NotEqual)]
        [InlineData(LogicOperator.GreaterThan, LogicOperator.LessThanOrEqual)]
        [InlineData(LogicOperator.LessThan, LogicOperator.GreaterThanOrEqual)]
        [InlineData(LogicOperator.StartsWith, LogicOperator.NotStartsWith)]
        [InlineData(LogicOperator.EndsWith, LogicOperator.NotEndsWith)]
        [InlineData(LogicOperator.Contains, LogicOperator.NotContains)]
        [InlineData(LogicOperator.Like, LogicOperator.NotLike)]
        [InlineData(LogicOperator.In, LogicOperator.NotIn)]
        [InlineData(LogicOperator.RegexpLike, LogicOperator.NotRegexpLike)]
        public void Opposite_PositiveOperator_ReturnsNegatedOperator(LogicOperator input, LogicOperator expected)
        {
            // Arrange
            // (input is provided via test parameters)

            // Act
            var result = input.Opposite();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifies that Opposite toggles the Not flag for negative operators (operators with the Not flag).
        /// Each negated operator should become its positive counterpart when Opposite is called.
        /// </summary>
        /// <param name="input">The input operator with the Not flag.</param>
        /// <param name="expected">The expected operator without the Not flag.</param>
        [Theory]
        [InlineData(LogicOperator.NotEqual, LogicOperator.Equal)]
        [InlineData(LogicOperator.LessThanOrEqual, LogicOperator.GreaterThan)]
        [InlineData(LogicOperator.GreaterThanOrEqual, LogicOperator.LessThan)]
        [InlineData(LogicOperator.NotStartsWith, LogicOperator.StartsWith)]
        [InlineData(LogicOperator.NotEndsWith, LogicOperator.EndsWith)]
        [InlineData(LogicOperator.NotContains, LogicOperator.Contains)]
        [InlineData(LogicOperator.NotLike, LogicOperator.Like)]
        [InlineData(LogicOperator.NotIn, LogicOperator.In)]
        [InlineData(LogicOperator.NotRegexpLike, LogicOperator.RegexpLike)]
        public void Opposite_NegativeOperator_ReturnsPositiveOperator(LogicOperator input, LogicOperator expected)
        {
            // Arrange
            // (input is provided via test parameters)

            // Act
            var result = input.Opposite();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifies that Opposite applied to the Not flag itself results in 0.
        /// Since Not = 64, applying XOR with 64 should yield 0.
        /// </summary>
        [Fact]
        public void Opposite_NotFlag_ReturnsZero()
        {
            // Arrange
            var input = LogicOperator.Not;

            // Act
            var result = input.Opposite();

            // Assert
            Assert.Equal((LogicOperator)0, result);
        }

        /// <summary>
        /// Verifies that calling Opposite twice on any operator returns the original operator.
        /// This tests the idempotent property of double negation: Opposite(Opposite(x)) = x.
        /// </summary>
        /// <param name="input">The input operator to test double negation on.</param>
        [Theory]
        [InlineData(LogicOperator.Equal)]
        [InlineData(LogicOperator.GreaterThan)]
        [InlineData(LogicOperator.LessThan)]
        [InlineData(LogicOperator.StartsWith)]
        [InlineData(LogicOperator.EndsWith)]
        [InlineData(LogicOperator.Contains)]
        [InlineData(LogicOperator.Like)]
        [InlineData(LogicOperator.In)]
        [InlineData(LogicOperator.RegexpLike)]
        [InlineData(LogicOperator.NotEqual)]
        [InlineData(LogicOperator.LessThanOrEqual)]
        [InlineData(LogicOperator.GreaterThanOrEqual)]
        [InlineData(LogicOperator.NotStartsWith)]
        [InlineData(LogicOperator.NotEndsWith)]
        [InlineData(LogicOperator.NotContains)]
        [InlineData(LogicOperator.NotLike)]
        [InlineData(LogicOperator.NotIn)]
        [InlineData(LogicOperator.NotRegexpLike)]
        [InlineData(LogicOperator.Not)]
        public void Opposite_CalledTwice_ReturnsOriginalOperator(LogicOperator input)
        {
            // Arrange
            // (input is provided via test parameters)

            // Act
            var result = input.Opposite().Opposite();

            // Assert
            Assert.Equal(input, result);
        }

        /// <summary>
        /// Verifies that Opposite works correctly with undefined enum values.
        /// Tests edge cases with values outside the defined enum range to ensure XOR operation is consistently applied.
        /// </summary>
        /// <param name="inputValue">The raw integer value to cast to LogicOperator.</param>
        /// <param name="expectedValue">The expected result after applying Opposite.</param>
        [Theory]
        [InlineData(100, 36)]  // 100 ^ 64 = 36
        [InlineData(200, 136)] // 200 ^ 64 = 136
        [InlineData(255, 191)] // 255 ^ 64 = 191
        [InlineData(128, 192)] // 128 ^ 64 = 192
        public void Opposite_UndefinedEnumValue_TogglesNotFlagCorrectly(int inputValue, int expectedValue)
        {
            // Arrange
            var input = (LogicOperator)inputValue;
            var expected = (LogicOperator)expectedValue;

            // Act
            var result = input.Opposite();

            // Assert
            Assert.Equal(expected, result);
        }
    }
}