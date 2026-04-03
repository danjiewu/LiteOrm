using System;
using System.Collections.Generic;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for <see cref="ForeignColumnAttribute"/> class.
    /// </summary>
    public class ForeignColumnAttributeTests
    {
        /// <summary>
        /// Tests that the constructor with Type parameter correctly sets the Foreign property
        /// when provided with various valid Type values.
        /// </summary>
        /// <param name="foreignType">The Type to pass to the constructor.</param>
        [Theory]
        [MemberData(nameof(GetValidTypeTestCases))]
        public void Constructor_WithValidType_ShouldSetForeignProperty(Type foreignType)
        {
            // Arrange & Act
            var attribute = new ForeignColumnAttribute(foreignType);

            // Assert
            Assert.Same(foreignType, attribute.Foreign);
        }

        /// <summary>
        /// Tests that the constructor with Type parameter correctly handles null
        /// by setting the Foreign property to null without throwing exceptions.
        /// </summary>
        [Fact]
        public void Constructor_WithNullType_ShouldSetForeignToNull()
        {
            // Arrange
            Type? nullType = null;

            // Act
            var attribute = new ForeignColumnAttribute(nullType!);

            // Assert
            Assert.Null(attribute.Foreign);
        }

        /// <summary>
        /// Provides test cases with various valid Type values for parameterized testing.
        /// </summary>
        public static IEnumerable<object?[]> GetValidTypeTestCases()
        {
            // Regular class type
            yield return new object?[] { typeof(string) };

            // Interface type
            yield return new object?[] { typeof(IDisposable) };

            // Abstract class type
            yield return new object?[] { typeof(System.IO.Stream) };

            // Value type
            yield return new object?[] { typeof(int) };

            // Generic type definition
            yield return new object?[] { typeof(List<>) };

            // Constructed generic type
            yield return new object?[] { typeof(List<int>) };

            // Enum type
            yield return new object?[] { typeof(DayOfWeek) };

            // Array type
            yield return new object?[] { typeof(int[]) };

            // Attribute type
            yield return new object?[] { typeof(ObsoleteAttribute) };
        }

        /// <summary>
        /// Tests that the constructor with string parameter correctly assigns the foreignName to the Foreign property
        /// for various string inputs including null, empty, whitespace, normal values, and special characters.
        /// </summary>
        /// <param name="foreignName">The foreign name to test.</param>
        [Theory]
        [InlineData("ValidTableName")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Table_With_Underscore")]
        [InlineData("Table-With-Dash")]
        [InlineData("Table.With.Dots")]
        [InlineData("Table123")]
        [InlineData("表名")]
        [InlineData("!@#$%^&*()")]
        public void ForeignColumnAttribute_WithStringParameter_ShouldSetForeignProperty(string foreignName)
        {
            // Arrange & Act
            var attribute = new ForeignColumnAttribute(foreignName);

            // Assert
            Assert.Equal(foreignName, attribute.Foreign);
        }

        /// <summary>
        /// Tests that the constructor with string parameter correctly handles null input
        /// by setting the Foreign property to null.
        /// </summary>
        [Fact]
        public void ForeignColumnAttribute_WithNullString_ShouldSetForeignPropertyToNull()
        {
            // Arrange
            string? foreignName = null;

            // Act
            var attribute = new ForeignColumnAttribute(foreignName);

            // Assert
            Assert.Null(attribute.Foreign);
        }

        /// <summary>
        /// Tests that the constructor with string parameter correctly handles very long strings
        /// by preserving the entire string value in the Foreign property.
        /// </summary>
        [Fact]
        public void ForeignColumnAttribute_WithVeryLongString_ShouldSetForeignProperty()
        {
            // Arrange
            var foreignName = new string('A', 10000);

            // Act
            var attribute = new ForeignColumnAttribute(foreignName);

            // Assert
            Assert.Equal(foreignName, attribute.Foreign);
            Assert.Equal(10000, ((string)attribute.Foreign).Length);
        }

        /// <summary>
        /// Tests that the constructor with string parameter sets the Foreign property to type string
        /// and that the property value is of type string when initialized with a string parameter.
        /// </summary>
        [Fact]
        public void ForeignColumnAttribute_WithStringParameter_ShouldStoreForeignAsString()
        {
            // Arrange
            var foreignName = "TestTable";

            // Act
            var attribute = new ForeignColumnAttribute(foreignName);

            // Assert
            Assert.IsType<string>(attribute.Foreign);
        }
    }
}