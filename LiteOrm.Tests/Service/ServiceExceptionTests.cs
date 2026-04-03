using System;

using LiteOrm.Service;
using Xunit;

namespace LiteOrm.Service.UnitTests
{
    /// <summary>
    /// Contains unit tests for the <see cref="ServiceException"/> class.
    /// </summary>
    public class ServiceExceptionTests
    {
        /// <summary>
        /// Tests that the parameterless constructor successfully creates a new instance of ServiceException.
        /// Verifies that the instance is not null and is of the correct type.
        /// </summary>
        [Fact]
        public void ServiceException_DefaultConstructor_CreatesInstance()
        {
            // Arrange & Act
            var exception = new ServiceException();

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<ServiceException>(exception);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates an exception with a null InnerException.
        /// Expected behavior: InnerException should be null when using the parameterless constructor.
        /// </summary>
        [Fact]
        public void ServiceException_DefaultConstructor_InnerExceptionIsNull()
        {
            // Arrange & Act
            var exception = new ServiceException();

            // Assert
            Assert.Null(exception.InnerException);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates an exception with a non-null Message.
        /// Expected behavior: Message should not be null (may be empty or default system message).
        /// </summary>
        [Fact]
        public void ServiceException_DefaultConstructor_MessageIsNotNull()
        {
            // Arrange & Act
            var exception = new ServiceException();

            // Assert
            Assert.NotNull(exception.Message);
        }

        /// <summary>
        /// Tests that ServiceException inherits from System.Exception.
        /// Verifies the correct type hierarchy for exception handling.
        /// </summary>
        [Fact]
        public void ServiceException_DefaultConstructor_InheritsFromException()
        {
            // Arrange & Act
            var exception = new ServiceException();

            // Assert
            Assert.IsAssignableFrom<Exception>(exception);
        }

        /// <summary>
        /// Tests that a ServiceException created with the parameterless constructor can be thrown and caught.
        /// Verifies that the exception behaves correctly in exception handling scenarios.
        /// </summary>
        [Fact]
        public void ServiceException_DefaultConstructor_CanBeThrownAndCaught()
        {
            // Arrange
            ServiceException? caughtException = null;

            // Act
            try
            {
                throw new ServiceException();
            }
            catch (ServiceException ex)
            {
                caughtException = ex;
            }

            // Assert
            Assert.NotNull(caughtException);
            Assert.IsType<ServiceException>(caughtException);
        }

        /// <summary>
        /// Tests that a ServiceException can be caught as a base Exception type.
        /// Verifies polymorphic exception handling behavior.
        /// </summary>
        [Fact]
        public void ServiceException_DefaultConstructor_CanBeCaughtAsException()
        {
            // Arrange
            Exception? caughtException = null;

            // Act
            try
            {
                throw new ServiceException();
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Assert
            Assert.NotNull(caughtException);
            Assert.IsType<ServiceException>(caughtException);
        }

        /// <summary>
        /// Verifies that the ServiceException constructor with a message parameter
        /// correctly initializes the exception with the provided message.
        /// Tests various string edge cases including null, empty, whitespace, long strings, and special characters.
        /// </summary>
        /// <param name="message">The message to pass to the constructor.</param>
        [Theory]
        [InlineData("Service operation failed")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\n\r")]
        [InlineData("这是一个包含Unicode字符的消息: 日本語, Español, العربية")]
        [InlineData("Message with special chars: !@#$%^&*()_+-=[]{}|;':\",./<>?")]
        public void ServiceException_MessageConstructor_ShouldSetMessageCorrectly(string message)
        {
            // Arrange & Act
            var exception = new ServiceException(message);

            // Assert
            Assert.Equal(message, exception.Message);
        }

        /// <summary>
        /// Verifies that the ServiceException constructor with a message parameter
        /// correctly handles null message values without throwing an exception.
        /// </summary>
        [Fact]
        public void ServiceException_MessageConstructor_WithNullMessage_ShouldNotThrow()
        {
            // Arrange
            string? message = null;

            // Act
            var exception = new ServiceException(message!);

            // Assert
            Assert.NotNull(exception);
        }

        /// <summary>
        /// Verifies that the ServiceException constructor with a message parameter
        /// correctly handles very long message strings.
        /// </summary>
        [Fact]
        public void ServiceException_MessageConstructor_WithVeryLongMessage_ShouldSetMessageCorrectly()
        {
            // Arrange
            var longMessage = new string('A', 10000);

            // Act
            var exception = new ServiceException(longMessage);

            // Assert
            Assert.Equal(longMessage, exception.Message);
            Assert.Equal(10000, exception.Message.Length);
        }

        /// <summary>
        /// Verifies that the ServiceException constructor with a message parameter
        /// correctly preserves newlines and control characters in the message.
        /// </summary>
        [Fact]
        public void ServiceException_MessageConstructor_WithNewlinesAndControlChars_ShouldPreserveMessage()
        {
            // Arrange
            var message = "Line 1\nLine 2\r\nLine 3\tTabbed";

            // Act
            var exception = new ServiceException(message);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Contains("\n", exception.Message);
            Assert.Contains("\r\n", exception.Message);
            Assert.Contains("\t", exception.Message);
        }

        /// <summary>
        /// Tests that the constructor with message and inner exception properly initializes the exception
        /// with various message values and a null inner exception.
        /// </summary>
        /// <param name="message">The message to test.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Service error occurred")]
        [InlineData("Very long message: Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.")]
        [InlineData("Special characters: !@#$%^&*()_+-={}[]|\\:\";<>?,./~`")]
        public void ServiceException_WithMessageAndNullInner_ShouldInitializeCorrectly(string? message)
        {
            // Arrange & Act
            var exception = new ServiceException(message, null);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Null(exception.InnerException);
        }

        /// <summary>
        /// Tests that the constructor with message and inner exception properly initializes the exception
        /// with various message values and a non-null inner exception.
        /// </summary>
        /// <param name="message">The message to test.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Service error occurred")]
        [InlineData("Wrapped exception message")]
        public void ServiceException_WithMessageAndInnerException_ShouldInitializeCorrectly(string? message)
        {
            // Arrange
            var innerException = new InvalidOperationException("Inner exception message");

            // Act
            var exception = new ServiceException(message, innerException);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Same(innerException, exception.InnerException);
        }

        /// <summary>
        /// Tests that the constructor can handle a ServiceException as the inner exception,
        /// allowing for exception chaining.
        /// </summary>
        [Fact]
        public void ServiceException_WithServiceExceptionAsInner_ShouldInitializeCorrectly()
        {
            // Arrange
            var innerServiceException = new ServiceException("Inner service exception");
            var message = "Outer service exception";

            // Act
            var exception = new ServiceException(message, innerServiceException);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Same(innerServiceException, exception.InnerException);
        }

        /// <summary>
        /// Tests that the constructor properly handles multiple levels of exception nesting.
        /// </summary>
        [Fact]
        public void ServiceException_WithNestedExceptions_ShouldMaintainExceptionChain()
        {
            // Arrange
            var rootException = new ArgumentException("Root cause");
            var middleException = new InvalidOperationException("Middle exception", rootException);
            var message = "Service exception message";

            // Act
            var exception = new ServiceException(message, middleException);

            // Assert
            Assert.Equal(message, exception.Message);
            Assert.Same(middleException, exception.InnerException);
            Assert.Same(rootException, exception.InnerException.InnerException);
        }
    }
}