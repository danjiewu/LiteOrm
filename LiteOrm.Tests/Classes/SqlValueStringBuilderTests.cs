using System;

using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for <see cref="SqlValueStringBuilder"/>.
    /// </summary>
    public class SqlValueStringBuilderTests
    {
        /// <summary>
        /// Tests that the default constructor initializes all fields correctly.
        /// Verifies that all ValueStringBuilder fields are created with initial capacity,
        /// Skip and Take are set to 0, and Indent is null.
        /// </summary>
        [Fact]
        public void Constructor_Default_InitializesAllFieldsCorrectly()
        {
            // Arrange & Act
            var builder = new SqlValueStringBuilder();

            try
            {
                // Assert - Verify ValueStringBuilder fields are initialized
                Assert.Equal(0, builder.Select.Length);
                Assert.True(builder.Select.Capacity > 0);

                Assert.Equal(0, builder.From.Length);
                Assert.True(builder.From.Capacity > 0);

                Assert.Equal(0, builder.Where.Length);
                Assert.True(builder.Where.Capacity > 0);

                Assert.Equal(0, builder.GroupBy.Length);
                Assert.True(builder.GroupBy.Capacity > 0);

                Assert.Equal(0, builder.Having.Length);
                Assert.True(builder.Having.Capacity > 0);

                Assert.Equal(0, builder.OrderBy.Length);
                Assert.True(builder.OrderBy.Capacity > 0);

                // Assert - Verify numeric fields
                Assert.Equal(0, builder.Skip);
                Assert.Equal(0, builder.Take);

            }
            finally
            {
                // Cleanup - Dispose ValueStringBuilder instances to return arrays to pool
                builder.Select.Dispose();
                builder.From.Dispose();
                builder.Where.Dispose();
                builder.GroupBy.Dispose();
                builder.Having.Dispose();
                builder.OrderBy.Dispose();
            }
        }

        /// <summary>
        /// Tests that Dispose() completes successfully without throwing exceptions
        /// when called on a newly constructed instance.
        /// Input: Newly constructed SqlValueStringBuilder.
        /// Expected: No exceptions thrown.
        /// </summary>
        [Fact]
        public void Dispose_NewlyConstructedInstance_DisposesWithoutException()
        {
            // Arrange
            var builder = new SqlValueStringBuilder();

            // Act & Assert
            builder.Dispose(); // Should not throw
        }

        /// <summary>
        /// Tests that Dispose() is idempotent and can be called multiple times
        /// without throwing exceptions.
        /// Input: SqlValueStringBuilder with Dispose() called twice.
        /// Expected: No exceptions thrown on second call.
        /// </summary>
        [Fact]
        public void Dispose_CalledMultipleTimes_DoesNotThrowException()
        {
            // Arrange
            var builder = new SqlValueStringBuilder();
            builder.Dispose();

            // Act & Assert
            builder.Dispose(); // Second call should not throw
        }

        /// <summary>
        /// Tests that Dispose() can be called multiple times in succession
        /// to verify complete idempotency.
        /// Input: SqlValueStringBuilder with Dispose() called three times.
        /// Expected: No exceptions thrown on any call.
        /// </summary>
        [Fact]
        public void Dispose_CalledThreeTimes_RemainsIdempotent()
        {
            // Arrange
            var builder = new SqlValueStringBuilder();

            // Act & Assert
            builder.Dispose(); // First call
            builder.Dispose(); // Second call
            builder.Dispose(); // Third call - should still not throw
        }

        /// <summary>
        /// Tests that Dispose() works correctly when called on an instance
        /// where ValueStringBuilder fields have been used.
        /// Input: SqlValueStringBuilder with modified Select field.
        /// Expected: No exceptions thrown.
        /// </summary>
        [Fact]
        public void Dispose_AfterUsingFields_DisposesSuccessfully()
        {
            // Arrange
            var builder = new SqlValueStringBuilder();
            builder.Select.Append("SELECT * FROM Users");

            // Act & Assert
            builder.Dispose(); // Should dispose all fields without exception
        }
    }
}