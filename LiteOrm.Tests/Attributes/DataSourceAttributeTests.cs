using System;

using LiteOrm;
using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class DataSourceAttributeTests
    {
        /// <summary>
        /// Tests that the constructor with connectionName parameter correctly sets the ConnectionName property
        /// for various string inputs including null, empty, whitespace, special characters, and long strings.
        /// Expected result: The ConnectionName property should be set to the exact value passed to the constructor.
        /// </summary>
        /// <param name="connectionName">The connection name to pass to the constructor.</param>
        [Theory]
        [InlineData("TestConnection")]
        [InlineData("MyDatabaseConnection")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t")]
        [InlineData("   ")]
        [InlineData("Connection_With_Underscores")]
        [InlineData("Connection-With-Dashes")]
        [InlineData("Connection.With.Dots")]
        [InlineData("Connection!@#$%^&*()")]
        [InlineData("Connection\nWith\nNewlines")]
        [InlineData("Very.Long.Connection.Name.With.Multiple.Segments.And.Dots.That.Could.Represent.A.Complex.Database.Connection.String.Or.Identifier")]
        public void Constructor_WithConnectionName_SetsConnectionNameProperty(string? connectionName)
        {
            // Arrange & Act
            var attribute = new DataSourceAttribute(connectionName!);

            // Assert
            Assert.Equal(connectionName, attribute.ConnectionName);
        }

        /// <summary>
        /// Tests that the default constructor creates a valid instance with null ConnectionName.
        /// </summary>
        [Fact]
        public void DataSourceAttribute_DefaultConstructor_CreatesInstanceWithNullConnectionName()
        {
            // Arrange & Act
            var attribute = new DataSourceAttribute();

            // Assert
            Assert.NotNull(attribute);
            Assert.Null(attribute.ConnectionName);
        }
    }
}