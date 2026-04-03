using System;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the AutoRegisterAttribute class.
    /// </summary>
    public class AutoRegisterAttributeTests
    {
        /// <summary>
        /// Tests that the default parameterless constructor initializes all properties to their expected default values.
        /// Verifies Lifetime is Singleton, Enabled is true, and other properties are null or false.
        /// </summary>
        [Fact]
        public void Constructor_Default_SetsPropertiesToDefaultValues()
        {
            // Arrange & Act
            var attribute = new AutoRegisterAttribute();

            // Assert
            Assert.Equal(Lifetime.Singleton, attribute.Lifetime);
            Assert.Null(attribute.ServiceTypes);
            Assert.True(attribute.Enabled);
            Assert.Null(attribute.Key);
            Assert.False(attribute.AutoActivate);
        }

        /// <summary>
        /// Tests that the constructor accepting a Lifetime parameter correctly sets the Lifetime property
        /// for all valid enum values.
        /// </summary>
        /// <param name="lifetime">The service lifetime to test.</param>
        [Theory]
        [InlineData(Lifetime.Singleton)]
        [InlineData(Lifetime.Scoped)]
        [InlineData(Lifetime.Transient)]
        public void AutoRegisterAttribute_WithLifetimeParameter_SetsLifetimeProperty(Lifetime lifetime)
        {
            // Arrange & Act
            var attribute = new AutoRegisterAttribute(lifetime);

            // Assert
            Assert.Equal(lifetime, attribute.Lifetime);
        }

        /// <summary>
        /// Tests that the constructor accepting a Lifetime parameter correctly initializes
        /// all other properties to their default values.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithLifetimeParameter_SetsDefaultPropertyValues()
        {
            // Arrange
            var lifetime = Lifetime.Scoped;

            // Act
            var attribute = new AutoRegisterAttribute(lifetime);

            // Assert
            Assert.Equal(lifetime, attribute.Lifetime);
            Assert.True(attribute.Enabled);
            Assert.Null(attribute.ServiceTypes);
            Assert.Null(attribute.Key);
            Assert.False(attribute.AutoActivate);
        }

        /// <summary>
        /// Tests that the constructor accepting a Lifetime parameter accepts invalid enum values
        /// (values outside the defined enum range) without throwing an exception.
        /// C# does not enforce enum bounds at runtime.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithInvalidLifetimeValue_AcceptsValue()
        {
            // Arrange
            var invalidLifetime = (Lifetime)999;

            // Act
            var attribute = new AutoRegisterAttribute(invalidLifetime);

            // Assert
            Assert.Equal(invalidLifetime, attribute.Lifetime);
        }

        /// <summary>
        /// Tests that the constructor accepting a Lifetime parameter with the minimum enum value
        /// (Singleton = 0) sets the property correctly.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithMinimumLifetimeValue_SetsLifetimeProperty()
        {
            // Arrange
            var lifetime = Lifetime.Singleton;

            // Act
            var attribute = new AutoRegisterAttribute(lifetime);

            // Assert
            Assert.Equal(Lifetime.Singleton, attribute.Lifetime);
            Assert.Equal((int)Lifetime.Singleton, (int)attribute.Lifetime);
        }

        /// <summary>
        /// Tests that the constructor accepting a Lifetime parameter with the maximum defined enum value
        /// (Transient = 2) sets the property correctly.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithMaximumLifetimeValue_SetsLifetimeProperty()
        {
            // Arrange
            var lifetime = Lifetime.Transient;

            // Act
            var attribute = new AutoRegisterAttribute(lifetime);

            // Assert
            Assert.Equal(Lifetime.Transient, attribute.Lifetime);
            Assert.Equal((int)Lifetime.Transient, (int)attribute.Lifetime);
        }

        /// <summary>
        /// Tests that the constructor correctly sets Lifetime and ServiceTypes properties
        /// when provided with a valid Lifetime enum value and a non-null serviceTypes array.
        /// </summary>
        /// <param name="lifetime">The lifetime value to test</param>
        [Theory]
        [InlineData(Lifetime.Singleton)]
        [InlineData(Lifetime.Scoped)]
        [InlineData(Lifetime.Transient)]
        public void AutoRegisterAttribute_WithValidLifetimeAndServiceTypes_SetsPropertiesCorrectly(Lifetime lifetime)
        {
            // Arrange
            var serviceTypes = new Type[] { typeof(string), typeof(int) };

            // Act
            var attribute = new AutoRegisterAttribute(lifetime, serviceTypes);

            // Assert
            Assert.Equal(lifetime, attribute.Lifetime);
            Assert.Same(serviceTypes, attribute.ServiceTypes);
            Assert.True(attribute.Enabled);
            Assert.Null(attribute.Key);
            Assert.False(attribute.AutoActivate);
        }

        /// <summary>
        /// Tests that the constructor correctly handles a null serviceTypes array parameter.
        /// Params arrays can accept null values.
        /// </summary>
        [Theory]
        [InlineData(Lifetime.Singleton)]
        [InlineData(Lifetime.Scoped)]
        [InlineData(Lifetime.Transient)]
        public void AutoRegisterAttribute_WithNullServiceTypes_SetsServiceTypesToNull(Lifetime lifetime)
        {
            // Arrange
            Type[] serviceTypes = null;

            // Act
            var attribute = new AutoRegisterAttribute(lifetime, serviceTypes);

            // Assert
            Assert.Equal(lifetime, attribute.Lifetime);
            Assert.Null(attribute.ServiceTypes);
            Assert.True(attribute.Enabled);
        }

        /// <summary>
        /// Tests that the constructor correctly handles an empty serviceTypes array
        /// when no parameters are passed to the params array.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithNoServiceTypes_CreatesEmptyArray()
        {
            // Arrange
            var lifetime = Lifetime.Scoped;

            // Act
            var attribute = new AutoRegisterAttribute(lifetime);

            // Assert
            Assert.Equal(lifetime, attribute.Lifetime);
            Assert.NotNull(attribute.ServiceTypes);
            Assert.Empty(attribute.ServiceTypes);
        }

        /// <summary>
        /// Tests that the constructor correctly handles a single Type in the serviceTypes array.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithSingleServiceType_SetsSingleElementArray()
        {
            // Arrange
            var lifetime = Lifetime.Transient;
            var serviceType = typeof(string);

            // Act
            var attribute = new AutoRegisterAttribute(lifetime, serviceType);

            // Assert
            Assert.Equal(lifetime, attribute.Lifetime);
            Assert.NotNull(attribute.ServiceTypes);
            Assert.Single(attribute.ServiceTypes);
            Assert.Equal(serviceType, attribute.ServiceTypes[0]);
        }

        /// <summary>
        /// Tests that the constructor correctly handles multiple Types in the serviceTypes array.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithMultipleServiceTypes_SetsMultipleElementArray()
        {
            // Arrange
            var lifetime = Lifetime.Singleton;
            var serviceTypes = new Type[] { typeof(string), typeof(int), typeof(double), typeof(object) };

            // Act
            var attribute = new AutoRegisterAttribute(lifetime, serviceTypes);

            // Assert
            Assert.Equal(lifetime, attribute.Lifetime);
            Assert.Same(serviceTypes, attribute.ServiceTypes);
            Assert.Equal(4, attribute.ServiceTypes.Length);
            Assert.Equal(typeof(string), attribute.ServiceTypes[0]);
            Assert.Equal(typeof(int), attribute.ServiceTypes[1]);
            Assert.Equal(typeof(double), attribute.ServiceTypes[2]);
            Assert.Equal(typeof(object), attribute.ServiceTypes[3]);
        }

        /// <summary>
        /// Tests that the constructor accepts an array containing null elements.
        /// No validation is performed on the array contents.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithNullElementsInServiceTypes_AcceptsArray()
        {
            // Arrange
            var lifetime = Lifetime.Scoped;
            var serviceTypes = new Type[] { typeof(string), null, typeof(int) };

            // Act
            var attribute = new AutoRegisterAttribute(lifetime, serviceTypes);

            // Assert
            Assert.Equal(lifetime, attribute.Lifetime);
            Assert.Same(serviceTypes, attribute.ServiceTypes);
            Assert.Equal(3, attribute.ServiceTypes.Length);
            Assert.Equal(typeof(string), attribute.ServiceTypes[0]);
            Assert.Null(attribute.ServiceTypes[1]);
            Assert.Equal(typeof(int), attribute.ServiceTypes[2]);
        }

        /// <summary>
        /// Tests that the constructor accepts an array containing duplicate types.
        /// No validation is performed to prevent duplicates.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithDuplicateServiceTypes_AcceptsArray()
        {
            // Arrange
            var lifetime = Lifetime.Transient;
            var serviceTypes = new Type[] { typeof(string), typeof(string), typeof(int), typeof(string) };

            // Act
            var attribute = new AutoRegisterAttribute(lifetime, serviceTypes);

            // Assert
            Assert.Equal(lifetime, attribute.Lifetime);
            Assert.Same(serviceTypes, attribute.ServiceTypes);
            Assert.Equal(4, attribute.ServiceTypes.Length);
        }

        /// <summary>
        /// Tests that the constructor correctly handles all edge case enum values
        /// including the minimum valid enum value.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithMinimumEnumValue_SetsLifetime()
        {
            // Arrange
            var lifetime = (Lifetime)0; // Singleton

            // Act
            var attribute = new AutoRegisterAttribute(lifetime, typeof(string));

            // Assert
            Assert.Equal(Lifetime.Singleton, attribute.Lifetime);
        }

        /// <summary>
        /// Tests that the constructor correctly handles negative enum values
        /// which are technically invalid but allowed by C#.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithNegativeEnumValue_AcceptsValue()
        {
            // Arrange
            var invalidLifetime = (Lifetime)(-1);

            // Act
            var attribute = new AutoRegisterAttribute(invalidLifetime, typeof(string));

            // Assert
            Assert.Equal(invalidLifetime, attribute.Lifetime);
        }

        /// <summary>
        /// Tests that the constructor correctly sets the Enabled property based on the enabled parameter,
        /// and ensures other properties retain their default values.
        /// </summary>
        /// <param name="enabled">Whether auto-registration should be enabled.</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AutoRegisterAttribute_WithEnabledParameter_SetsEnabledPropertyCorrectly(bool enabled)
        {
            // Arrange & Act
            var attribute = new AutoRegisterAttribute(enabled);

            // Assert
            Assert.Equal(enabled, attribute.Enabled);
            Assert.Equal(Lifetime.Singleton, attribute.Lifetime);
            Assert.Null(attribute.ServiceTypes);
            Assert.Null(attribute.Key);
            Assert.False(attribute.AutoActivate);
        }

        /// <summary>
        /// Tests that the constructor with params Type[] serviceTypes parameter correctly sets the ServiceTypes property with no arguments.
        /// Verifies that calling the constructor without arguments results in an empty array.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithNoParameters_SetsServiceTypesToEmptyArray()
        {
            // Arrange & Act
            var attribute = new AutoRegisterAttribute();

            // Assert
            Assert.NotNull(attribute.ServiceTypes);
            Assert.Empty(attribute.ServiceTypes);
        }

        /// <summary>
        /// Tests that the constructor with params Type[] serviceTypes parameter correctly handles null input.
        /// Verifies that passing null explicitly sets ServiceTypes to null.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithNullArray_SetsServiceTypesToNull()
        {
            // Arrange & Act
            var attribute = new AutoRegisterAttribute(serviceTypes: null);

            // Assert
            Assert.Null(attribute.ServiceTypes);
        }

        /// <summary>
        /// Tests that the constructor with params Type[] serviceTypes parameter correctly sets the ServiceTypes property with a single type.
        /// Verifies that the ServiceTypes property contains exactly one element matching the provided type.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithSingleType_SetsServiceTypesCorrectly()
        {
            // Arrange
            var expectedType = typeof(string);

            // Act
            var attribute = new AutoRegisterAttribute(expectedType);

            // Assert
            Assert.NotNull(attribute.ServiceTypes);
            Assert.Single(attribute.ServiceTypes);
            Assert.Equal(expectedType, attribute.ServiceTypes[0]);
        }

        /// <summary>
        /// Tests that the constructor with params Type[] serviceTypes parameter correctly sets the ServiceTypes property with multiple types.
        /// Verifies that all provided types are stored in the ServiceTypes array in the correct order.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithMultipleTypes_SetsServiceTypesCorrectly()
        {
            // Arrange
            var type1 = typeof(string);
            var type2 = typeof(int);
            var type3 = typeof(object);

            // Act
            var attribute = new AutoRegisterAttribute(type1, type2, type3);

            // Assert
            Assert.NotNull(attribute.ServiceTypes);
            Assert.Equal(3, attribute.ServiceTypes.Length);
            Assert.Equal(type1, attribute.ServiceTypes[0]);
            Assert.Equal(type2, attribute.ServiceTypes[1]);
            Assert.Equal(type3, attribute.ServiceTypes[2]);
        }

        /// <summary>
        /// Tests that the constructor with params Type[] serviceTypes parameter correctly handles an array containing null elements.
        /// Verifies that null elements within the array are preserved as-is.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithArrayContainingNullElements_SetsServiceTypesWithNulls()
        {
            // Arrange
            Type[] typesWithNull = new Type[] { typeof(string), null, typeof(int) };

            // Act
            var attribute = new AutoRegisterAttribute(typesWithNull);

            // Assert
            Assert.NotNull(attribute.ServiceTypes);
            Assert.Equal(3, attribute.ServiceTypes.Length);
            Assert.Equal(typeof(string), attribute.ServiceTypes[0]);
            Assert.Null(attribute.ServiceTypes[1]);
            Assert.Equal(typeof(int), attribute.ServiceTypes[2]);
        }

        /// <summary>
        /// Tests that the constructor with params Type[] serviceTypes parameter correctly handles duplicate types.
        /// Verifies that duplicate types are stored without deduplication.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithDuplicateTypes_StoresDuplicatesWithoutDeduplication()
        {
            // Arrange
            var duplicateType = typeof(string);

            // Act
            var attribute = new AutoRegisterAttribute(duplicateType, duplicateType, duplicateType);

            // Assert
            Assert.NotNull(attribute.ServiceTypes);
            Assert.Equal(3, attribute.ServiceTypes.Length);
            Assert.All(attribute.ServiceTypes, type => Assert.Equal(duplicateType, type));
        }

        /// <summary>
        /// Tests that the constructor with params Type[] serviceTypes parameter sets default values for all other properties.
        /// Verifies that Lifetime defaults to Singleton, Enabled defaults to true, AutoActivate defaults to false, and Key defaults to null.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithServiceTypes_SetsDefaultPropertyValues()
        {
            // Arrange & Act
            var attribute = new AutoRegisterAttribute(typeof(string));

            // Assert
            Assert.Equal(Lifetime.Singleton, attribute.Lifetime);
            Assert.True(attribute.Enabled);
            Assert.False(attribute.AutoActivate);
            Assert.Null(attribute.Key);
        }

        /// <summary>
        /// Tests that the constructor with params Type[] serviceTypes parameter correctly handles an explicit empty array.
        /// Verifies that passing an empty array explicitly results in ServiceTypes being an empty array.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithExplicitEmptyArray_SetsServiceTypesToEmptyArray()
        {
            // Arrange
            Type[] emptyArray = new Type[0];

            // Act
            var attribute = new AutoRegisterAttribute(emptyArray);

            // Assert
            Assert.NotNull(attribute.ServiceTypes);
            Assert.Empty(attribute.ServiceTypes);
            Assert.Same(emptyArray, attribute.ServiceTypes);
        }

        /// <summary>
        /// Tests that the constructor with params Type[] serviceTypes parameter correctly handles various built-in and generic types.
        /// Verifies that different type categories (value types, reference types, generic types, array types) are stored correctly.
        /// </summary>
        [Fact]
        public void AutoRegisterAttribute_WithVariousTypeCategories_SetsServiceTypesCorrectly()
        {
            // Arrange
            var valueType = typeof(int);
            var referenceType = typeof(string);
            var genericType = typeof(System.Collections.Generic.List<int>);
            var arrayType = typeof(string[]);

            // Act
            var attribute = new AutoRegisterAttribute(valueType, referenceType, genericType, arrayType);

            // Assert
            Assert.NotNull(attribute.ServiceTypes);
            Assert.Equal(4, attribute.ServiceTypes.Length);
            Assert.Equal(valueType, attribute.ServiceTypes[0]);
            Assert.Equal(referenceType, attribute.ServiceTypes[1]);
            Assert.Equal(genericType, attribute.ServiceTypes[2]);
            Assert.Equal(arrayType, attribute.ServiceTypes[3]);
        }
    }
}