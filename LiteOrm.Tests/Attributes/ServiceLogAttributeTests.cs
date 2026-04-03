#nullable enable
using LiteOrm;
using System;
using Xunit;


namespace LiteOrm.UnitTests
{
    /// <summary>
    /// Tests for LiteOrm.ServiceLogAttribute constructor behavior.
    /// </summary>
    public class ServiceLogAttributeTests
    {
        /// <summary>
        /// Verifies that the parameterless constructor sets the LogLevel to Information
        /// and the LogFormat to Full by default.
        /// Input conditions: constructing ServiceLogAttribute with no parameters.
        /// Expected result: LogLevel equals ServiceLogLevel.Information and LogFormat equals LogFormat.Full.
        /// </summary>
        [Fact]
        public void ServiceLogAttribute_Ctor_DefaultsSet()
        {
            // Arrange
            // (no setup required)

            // Act
            var attr = new ServiceLogAttribute();

            // Assert
            Assert.Equal(ServiceLogLevel.Information, attr.LogLevel);
            Assert.Equal(LogFormat.Full, attr.LogFormat);
        }
    }
}