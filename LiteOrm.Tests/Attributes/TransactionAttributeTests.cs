using System;
using System.Data;

using LiteOrm.Service;
using Xunit;

namespace LiteOrm.Service.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="TransactionAttribute"/> class.
    /// </summary>
    public class TransactionAttributeTests
    {
        /// <summary>
        /// Tests that the constructor with isTransaction parameter correctly sets the IsTransaction property
        /// and maintains the default IsolationLevel value of ReadCommitted.
        /// </summary>
        /// <param name="isTransaction">The value to pass to the constructor.</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Constructor_WithIsTransactionParameter_SetsIsTransactionPropertyAndMaintainsDefaultIsolationLevel(bool isTransaction)
        {
            // Arrange & Act
            var attribute = new TransactionAttribute(isTransaction);

            // Assert
            Assert.Equal(isTransaction, attribute.IsTransaction);
            Assert.Equal(IsolationLevel.ReadCommitted, attribute.IsolationLevel);
        }

        /// <summary>
        /// Tests that the parameterless constructor initializes IsTransaction to true and IsolationLevel to ReadCommitted.
        /// </summary>
        [Fact]
        public void TransactionAttribute_DefaultConstructor_InitializesPropertiesToDefaultValues()
        {
            // Arrange & Act
            var attribute = new TransactionAttribute();

            // Assert
            Assert.True(attribute.IsTransaction);
            Assert.Equal(IsolationLevel.ReadCommitted, attribute.IsolationLevel);
        }
    }
}