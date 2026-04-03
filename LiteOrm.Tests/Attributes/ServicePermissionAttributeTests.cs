using System;

using LiteOrm;
using Xunit;

namespace LiteOrm.UnitTests
{
    public class ServicePermissionAttributeTests
    {
        /// <summary>
        /// Tests that the ServicePermissionAttribute constructor correctly sets the AllowAnonymous property
        /// to the provided parameter value for both true and false inputs.
        /// Input: Boolean value (true or false)
        /// Expected: AllowAnonymous property matches the input value
        /// </summary>
        /// <param name="allowAnonymous">The boolean value to pass to the constructor</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Constructor_WithAllowAnonymousParameter_SetsAllowAnonymousPropertyCorrectly(bool allowAnonymous)
        {
            // Arrange & Act
            var attribute = new ServicePermissionAttribute(allowAnonymous);

            // Assert
            Assert.Equal(allowAnonymous, attribute.AllowAnonymous);
        }

        /// <summary>
        /// Tests that the ServicePermissionAttribute constructor does not modify the AllowRoles property,
        /// leaving it at its default null value.
        /// Input: Boolean value (true or false)
        /// Expected: AllowRoles property remains null
        /// </summary>
        /// <param name="allowAnonymous">The boolean value to pass to the constructor</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Constructor_WithAllowAnonymousParameter_LeavesAllowRolesAsNull(bool allowAnonymous)
        {
            // Arrange & Act
            var attribute = new ServicePermissionAttribute(allowAnonymous);

            // Assert
            Assert.Null(attribute.AllowRoles);
        }

        /// <summary>
        /// Tests that the default constructor creates a valid instance with correct default property values.
        /// Verifies that AllowAnonymous is false and AllowRoles is null by default.
        /// </summary>
        [Fact]
        public void ServicePermissionAttribute_DefaultConstructor_CreatesInstanceWithDefaultValues()
        {
            // Arrange & Act
            var attribute = new ServicePermissionAttribute();

            // Assert
            Assert.NotNull(attribute);
            Assert.False(attribute.AllowAnonymous);
            Assert.Null(attribute.AllowRoles);
        }

        /// <summary>
        /// Tests that properties can be set after construction using the default constructor.
        /// Verifies that the attribute instance is fully functional and mutable.
        /// </summary>
        [Fact]
        public void ServicePermissionAttribute_DefaultConstructor_PropertiesAreMutable()
        {
            // Arrange
            var attribute = new ServicePermissionAttribute();

            // Act
            attribute.AllowAnonymous = true;
            attribute.AllowRoles = "Admin,User";

            // Assert
            Assert.True(attribute.AllowAnonymous);
            Assert.Equal("Admin,User", attribute.AllowRoles);
        }

        /// <summary>
        /// Tests that the attribute can be applied to a method and retrieved via reflection.
        /// Verifies the attribute's applicability to method targets.
        /// </summary>
        [Fact]
        public void ServicePermissionAttribute_DefaultConstructor_CanBeAppliedToMethod()
        {
            // Arrange
            var methodInfo = typeof(ServicePermissionAttributeTests).GetMethod(nameof(TestMethodWithAttribute));

            // Act
            var attributes = methodInfo?.GetCustomAttributes(typeof(ServicePermissionAttribute), false);

            // Assert
            Assert.NotNull(attributes);
            Assert.Single(attributes);
            Assert.IsType<ServicePermissionAttribute>(attributes[0]);
        }

        /// <summary>
        /// Tests that the attribute can be applied to a class and retrieved via reflection.
        /// Verifies the attribute's applicability to class targets.
        /// </summary>
        [Fact]
        public void ServicePermissionAttribute_DefaultConstructor_CanBeAppliedToClass()
        {
            // Arrange & Act
            var attributes = typeof(TestClassWithAttribute).GetCustomAttributes(typeof(ServicePermissionAttribute), false);

            // Assert
            Assert.NotNull(attributes);
            Assert.Single(attributes);
            Assert.IsType<ServicePermissionAttribute>(attributes[0]);
        }

        /// <summary>
        /// Tests that the attribute can be applied to an interface and retrieved via reflection.
        /// Verifies the attribute's applicability to interface targets.
        /// </summary>
        [Fact]
        public void ServicePermissionAttribute_DefaultConstructor_CanBeAppliedToInterface()
        {
            // Arrange & Act
            var attributes = typeof(ITestInterfaceWithAttribute).GetCustomAttributes(typeof(ServicePermissionAttribute), false);

            // Assert
            Assert.NotNull(attributes);
            Assert.Single(attributes);
            Assert.IsType<ServicePermissionAttribute>(attributes[0]);
        }

        // Helper method for testing attribute application
        [ServicePermissionAttribute]
        public void TestMethodWithAttribute()
        {
        }

        // Helper class for testing attribute application
        [ServicePermissionAttribute]
        private class TestClassWithAttribute
        {
        }

        // Helper interface for testing attribute application
        [ServicePermissionAttribute]
        private interface ITestInterfaceWithAttribute
        {
        }
    }
}