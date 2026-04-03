using System;

using LiteOrm;
using Xunit;

namespace LiteOrm.UnitTests
{
    /// <summary>
    /// Unit tests for the ServiceAttribute class.
    /// </summary>
    public class ServiceAttributeTests
    {
        /// <summary>
        /// Tests that the ServiceAttribute constructor correctly sets the IsService property
        /// when provided with a boolean value, and verifies that the Name property remains null.
        /// </summary>
        /// <param name="isService">The value to pass to the constructor.</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ServiceAttribute_BoolConstructor_SetsIsServiceCorrectly(bool isService)
        {
            // Arrange & Act
            var attribute = new ServiceAttribute(isService);

            // Assert
            Assert.Equal(isService, attribute.IsService);
            Assert.Null(attribute.Name);
        }

        /// <summary>
        /// Tests that the default constructor sets IsService to true.
        /// </summary>
        [Fact]
        public void ServiceAttribute_DefaultConstructor_SetsIsServiceToTrue()
        {
            // Arrange & Act
            var attribute = new ServiceAttribute();

            // Assert
            Assert.True(attribute.IsService);
        }

        /// <summary>
        /// Tests that the default constructor leaves Name property uninitialized (null).
        /// </summary>
        [Fact]
        public void ServiceAttribute_DefaultConstructor_NameRemainsNull()
        {
            // Arrange & Act
            var attribute = new ServiceAttribute();

            // Assert
            Assert.Null(attribute.Name);
        }
    }
}