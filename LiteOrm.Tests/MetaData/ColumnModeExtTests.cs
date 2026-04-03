using System;

using LiteOrm;
using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Tests for <see cref="ColumnModeExt"/> extension methods.
    /// </summary>
    public class ColumnModeExtTests
    {
        /// <summary>
        /// Tests that CanUpdate returns true when the ColumnMode has the Update flag set.
        /// </summary>
        /// <param name="mode">The column mode with Update flag set.</param>
        [Theory]
        [InlineData(ColumnMode.Update)]
        [InlineData(ColumnMode.Write)]
        [InlineData(ColumnMode.Full)]
        [InlineData(ColumnMode.Read | ColumnMode.Update)]
        public void CanUpdate_WithUpdateFlag_ReturnsTrue(ColumnMode mode)
        {
            // Act
            bool result = mode.CanUpdate();

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that CanUpdate returns false when the ColumnMode does not have the Update flag set.
        /// </summary>
        /// <param name="mode">The column mode without Update flag set.</param>
        [Theory]
        [InlineData(ColumnMode.None)]
        [InlineData(ColumnMode.Read)]
        [InlineData(ColumnMode.Insert)]
        [InlineData(ColumnMode.Final)]
        public void CanUpdate_WithoutUpdateFlag_ReturnsFalse(ColumnMode mode)
        {
            // Act
            bool result = mode.CanUpdate();

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that CanUpdate returns true for undefined enum values when the Update bit (2) is set.
        /// </summary>
        /// <param name="modeValue">The integer value representing an undefined ColumnMode with Update bit set.</param>
        [Theory]
        [InlineData(2 | 8)]
        [InlineData(2 | 16)]
        [InlineData(2 | 128)]
        public void CanUpdate_WithUndefinedEnumValueHavingUpdateBit_ReturnsTrue(int modeValue)
        {
            // Arrange
            ColumnMode mode = (ColumnMode)modeValue;

            // Act
            bool result = mode.CanUpdate();

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that CanUpdate returns false for undefined enum values when the Update bit (2) is not set.
        /// </summary>
        /// <param name="modeValue">The integer value representing an undefined ColumnMode without Update bit set.</param>
        [Theory]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(128)]
        [InlineData(253)]
        public void CanUpdate_WithUndefinedEnumValueWithoutUpdateBit_ReturnsFalse(int modeValue)
        {
            // Arrange
            ColumnMode mode = (ColumnMode)modeValue;

            // Act
            bool result = mode.CanUpdate();

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that CanUpdate returns false for negative undefined enum values without Update bit set.
        /// </summary>
        [Fact]
        public void CanUpdate_WithNegativeEnumValueWithoutUpdateBit_ReturnsFalse()
        {
            // Arrange
            ColumnMode mode = (ColumnMode)(-4);

            // Act
            bool result = mode.CanUpdate();

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that CanUpdate returns true for negative undefined enum values with Update bit set.
        /// </summary>
        [Fact]
        public void CanUpdate_WithNegativeEnumValueHavingUpdateBit_ReturnsTrue()
        {
            // Arrange
            ColumnMode mode = (ColumnMode)(-2);

            // Act
            bool result = mode.CanUpdate();

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that CanInsert returns true when the ColumnMode has the Insert flag set.
        /// Validates all enum values and combinations that include the Insert flag.
        /// </summary>
        /// <param name="mode">The column mode to test</param>
        [Theory]
        [InlineData(ColumnMode.Insert)]
        [InlineData(ColumnMode.Write)]
        [InlineData(ColumnMode.Final)]
        [InlineData(ColumnMode.Full)]
        public void CanInsert_ModeWithInsertFlag_ReturnsTrue(ColumnMode mode)
        {
            // Act
            bool result = mode.CanInsert();

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that CanInsert returns false when the ColumnMode does not have the Insert flag set.
        /// Validates enum values without the Insert flag including None, Read, and Update.
        /// </summary>
        /// <param name="mode">The column mode to test</param>
        [Theory]
        [InlineData(ColumnMode.None)]
        [InlineData(ColumnMode.Read)]
        [InlineData(ColumnMode.Update)]
        public void CanInsert_ModeWithoutInsertFlag_ReturnsFalse(ColumnMode mode)
        {
            // Act
            bool result = mode.CanInsert();

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that CanInsert returns false for undefined enum values that don't contain the Insert flag.
        /// Validates behavior with edge case values outside the defined enum range.
        /// </summary>
        /// <param name="value">The integer value to cast to ColumnMode</param>
        [Theory]
        [InlineData(8)]
        [InlineData(16)]
        [InlineData(3)]
        [InlineData(-8)]
        public void CanInsert_UndefinedEnumValueWithoutInsertFlag_ReturnsFalse(int value)
        {
            // Arrange
            ColumnMode mode = (ColumnMode)value;

            // Act
            bool result = mode.CanInsert();

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that CanInsert returns true for undefined enum values that contain the Insert flag (bit 2).
        /// Validates behavior with edge case values outside the defined enum range but with Insert bit set.
        /// </summary>
        /// <param name="value">The integer value to cast to ColumnMode (must have bit 2 set)</param>
        [Theory]
        [InlineData(12)]
        [InlineData(20)]
        [InlineData(28)]
        public void CanInsert_UndefinedEnumValueWithInsertFlag_ReturnsTrue(int value)
        {
            // Arrange
            ColumnMode mode = (ColumnMode)value;

            // Act
            bool result = mode.CanInsert();

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that CanRead returns false when the Read flag is not set
        /// </summary>
        /// <param name="mode">The column mode to test</param>
        [Theory]
        [InlineData(ColumnMode.None)]
        [InlineData(ColumnMode.Update)]
        [InlineData(ColumnMode.Insert)]
        [InlineData(ColumnMode.Write)]
        public void CanRead_ModeWithoutReadFlag_ReturnsFalse(ColumnMode mode)
        {
            // Act
            bool result = mode.CanRead();

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that CanRead returns true when the Read flag is set
        /// </summary>
        /// <param name="mode">The column mode to test</param>
        [Theory]
        [InlineData(ColumnMode.Read)]
        [InlineData(ColumnMode.Final)]
        [InlineData(ColumnMode.Full)]
        public void CanRead_ModeWithReadFlag_ReturnsTrue(ColumnMode mode)
        {
            // Act
            bool result = mode.CanRead();

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that CanRead returns true when Read flag is combined with other flags using bitwise OR
        /// </summary>
        [Fact]
        public void CanRead_CustomCombinationWithReadFlag_ReturnsTrue()
        {
            // Arrange
            ColumnMode mode = ColumnMode.Read | ColumnMode.Update | ColumnMode.Insert;

            // Act
            bool result = mode.CanRead();

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that CanRead returns false when Read flag is not in a custom combination
        /// </summary>
        [Fact]
        public void CanRead_CustomCombinationWithoutReadFlag_ReturnsFalse()
        {
            // Arrange
            ColumnMode mode = ColumnMode.Update | ColumnMode.Insert;

            // Act
            bool result = mode.CanRead();

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that CanRead handles undefined enum values correctly
        /// </summary>
        /// <param name="value">The integer value to cast to ColumnMode</param>
        /// <param name="expectedResult">The expected result</param>
        [Theory]
        [InlineData(1, true)]
        [InlineData(0, false)]
        [InlineData(8, false)]
        [InlineData(9, true)]
        [InlineData(-1, true)]
        [InlineData(int.MaxValue, true)]
        [InlineData(int.MinValue, false)]
        public void CanRead_UndefinedEnumValue_ReturnsExpectedResult(int value, bool expectedResult)
        {
            // Arrange
            ColumnMode mode = (ColumnMode)value;

            // Act
            bool result = mode.CanRead();

            // Assert
            Assert.Equal(expectedResult, result);
        }
    }
}