using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

#nullable enable
using Moq;
using Xunit;

namespace LiteOrm.Tests.UnitTests
{
    /// <summary>
    /// Tests for PropertyAccessorExtension.GetValueFast
    /// </summary>
    public class PropertyAccessorExtensionTests
    {
        /// <summary>
        /// The test helper type with instance properties used by tests.
        /// This helper is declared inside the test class to avoid polluting the top-level test namespace.
        /// </summary>
        private class Holder
        {
            public int Number { get; set; }
            public string? Text { get; set; }
        }

        private class OtherHolder
        {
            public double Number { get; set; }
        }

        /// <summary>
        /// Verifies that GetValueFast throws ArgumentNullException when the provided PropertyInfo is null.
        /// Input: property = null, instance = new object().
        /// Expected: ArgumentNullException with ParamName 'property'.
        /// </summary>
        [Fact]
        public void GetValueFast_PropertyNull_ThrowsArgumentNullException()
        {
            // Arrange
            PropertyInfo? property = null;
            var instance = new object();

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => PropertyAccessorExtension.GetValueFast(property!, instance));
            Assert.Equal("property", ex.ParamName);
        }

        /// <summary>
        /// Verifies that GetValueFast returns null when the instance is null.
        /// Input: a valid PropertyInfo for an instance property and instance = null.
        /// Expected: result is null.
        /// </summary>
        [Fact]
        public void GetValueFast_InstanceNull_ReturnsNull()
        {
            // Arrange
            PropertyInfo property = typeof(Holder).GetProperty(nameof(Holder.Text)) ?? throw new Exception("Property not found");

            // Act
            var result = property.GetValueFast(null);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Verifies GetValueFast returns exact integer values for an int property across edge numeric values.
        /// Inputs: int.MinValue, 0, int.MaxValue.
        /// Expected: returned boxed int equals input value.
        /// </summary>
        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(0)]
        [InlineData(int.MaxValue)]
        public void GetValueFast_IntProperty_ReturnsExpectedValue(int input)
        {
            // Arrange
            var holder = new Holder { Number = input };
            PropertyInfo property = typeof(Holder).GetProperty(nameof(Holder.Number)) ?? throw new Exception("Property not found");

            // Act
            var result = property.GetValueFast(holder);

            // Assert
            Assert.IsType<int>(result);
            Assert.Equal(input, (int)result!);
        }

        /// <summary>
        /// Provides a variety of string values including empty, whitespace, long string, special/control characters, and null.
        /// Each case is set on Holder.Text and the getter is expected to return the same value.
        /// </summary>
        public static IEnumerable<object?[]> StringValues()
        {
            yield return new object?[] { string.Empty };
            yield return new object?[] { "   " };
            yield return new object?[] { new string('A', 1000) };
            yield return new object?[] { "\0\n\t\u2603" };
            yield return new object?[] { null };
        }

        /// <summary>
        /// Verifies GetValueFast returns expected string values for a string property.
        /// Inputs: empty, whitespace, long, special/control characters, and null.
        /// Expected: returned value equals the set value (including null).
        /// </summary>
        [Theory]
        [MemberData(nameof(StringValues))]
        public void GetValueFast_StringProperty_ReturnsExpectedValue(string? input)
        {
            // Arrange
            var holder = new Holder { Text = input };
            PropertyInfo property = typeof(Holder).GetProperty(nameof(Holder.Text)) ?? throw new Exception("Property not found");

            // Act
            var result = property.GetValueFast(holder);

            // Assert
            if (input is null)
            {
                Assert.Null(result);
            }
            else
            {
                Assert.IsType<string>(result);
                Assert.Equal(input, (string)result!);
            }
        }

        /// <summary>
        /// Verifies GetValueFast throws InvalidCastException when the provided instance is not compatible with the property's declaring type.
        /// Input: PropertyInfo for Holder.Number and an instance of OtherHolder.
        /// Expected: InvalidCastException is thrown due to invalid cast in generated delegate.
        /// </summary>
        [Fact]
        public void GetValueFast_InstanceTypeMismatch_ThrowsInvalidCastException()
        {
            // Arrange
            PropertyInfo property = typeof(Holder).GetProperty(nameof(Holder.Number)) ?? throw new Exception("Property not found");
            var wrongInstance = new OtherHolder();

            // Act & Assert
            Assert.Throws<InvalidCastException>(() => property.GetValueFast(wrongInstance));
        }

        /// <summary>
        /// Simple model used for testing SetValueFast against properties with different types.
        /// Nested inside the test class as required.
        /// </summary>
        private class Model
        {
            public int IntProp { get; set; }
            public string StrProp { get; set; }
            public int? NullableIntProp { get; set; }
        }

        /// <summary>
        /// Verifies that when the 'property' argument is null an ArgumentNullException is thrown.
        /// Input: property = null, instance = new Model().
        /// Expected: ArgumentNullException for parameter 'property'.
        /// </summary>
        [Fact]
        public void SetValueFast_PropertyNull_ThrowsArgumentNullException()
        {
            // Arrange
            PropertyInfo? property = null;
            var instance = new Model();

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => PropertyAccessorExtension.SetValueFast(property!, instance, 1));
            Assert.Equal("property", ex.ParamName);
        }

        /// <summary>
        /// Verifies that when the 'instance' argument is null an ArgumentNullException is thrown.
        /// Input: property = typeof(Model).GetProperty("IntProp"), instance = null.
        /// Expected: ArgumentNullException for parameter 'instance'.
        /// </summary>
        [Fact]
        public void SetValueFast_InstanceNull_ThrowsArgumentNullException()
        {
            // Arrange
            PropertyInfo property = typeof(Model).GetProperty(nameof(Model.IntProp))!;
            Model? instance = null;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => PropertyAccessorExtension.SetValueFast(property, instance!, 5));
            Assert.Equal("instance", ex.ParamName);
        }

        /// <summary>
        /// Parameterized test validating successful assignments for multiple property types and values.
        /// Tests boxed values and null assignment for reference and nullable properties.
        /// Inputs:
        /// - propertyName: the name of the property on Model
        /// - initialValue: initial value assigned to the property before calling SetValueFast
        /// - valueToSet: boxed value passed to SetValueFast
        /// - expectedAfter: expected property value after SetValueFast
        /// Expected: property value equals expectedAfter and no exceptions thrown.
        /// </summary>
        [Theory]
        [MemberData(nameof(ValidSetCases))]
        public void SetValueFast_AssignsValues_Correctly(string propertyName, object? initialValue, object? valueToSet, object? expectedAfter)
        {
            // Arrange
            var model = new Model();
            PropertyInfo property = typeof(Model).GetProperty(propertyName)!;

            // Initialize to initialValue using reflection to ensure a clean start
            property.SetValue(model, initialValue);

            // Act
            PropertyAccessorExtension.SetValueFast(property, model, valueToSet);

            // Assert
            object? actual = property.GetValue(model);
            Assert.Equal(expectedAfter, actual);
        }

        public static IEnumerable<object[]> ValidSetCases()
        {
            // IntProp: boxed int assignment
            yield return new object[] { nameof(Model.IntProp), 1, (object)42, (object)42 };

            // StrProp: set to null (reference type)
            yield return new object[] { nameof(Model.StrProp), "initial", null, null };

            // NullableIntProp: set from null to boxed int
            yield return new object[] { nameof(Model.NullableIntProp), null, (object)5, (object)5 };
        }

        /// <summary>
        /// Verifies that providing a value of an incompatible runtime type for a non-nullable value type property
        /// results in an exception being thrown by the compiled setter.
        /// Input: setting IntProp (int) with a string value.
        /// Expected: some exception thrown due to invalid cast/conversion.
        /// Note: the exact exception type can vary depending on runtime conversion behavior; we assert that an exception occurs.
        /// </summary>
        [Fact]
        public void SetValueFast_WrongValueType_ThrowsException()
        {
            // Arrange
            var model = new Model();
            PropertyInfo property = typeof(Model).GetProperty(nameof(Model.IntProp))!;

            // Act & Assert: We assert that some exception is thrown when setting a string to an int property.
            Assert.ThrowsAny<Exception>(() => PropertyAccessorExtension.SetValueFast(property, model, "not-an-int"));
        }

        /// <summary>
        /// Ensures repeated invocations (which exercise the internal setter cache) correctly update values
        /// and do not throw on subsequent calls.
        /// Input: call SetValueFast twice with different boxed ints.
        /// Expected: final value is the most recently set value and no exceptions thrown.
        /// </summary>
        [Fact]
        public void SetValueFast_RepeatedCalls_UpdatesValueWithoutThrowing()
        {
            // Arrange
            var model = new Model { IntProp = 0 };
            PropertyInfo property = typeof(Model).GetProperty(nameof(Model.IntProp))!;

            // Act & Assert - first call
            PropertyAccessorExtension.SetValueFast(property, model, (object)100);
            Assert.Equal(100, model.IntProp);

            // Act & Assert - second call (should reuse cached setter)
            PropertyAccessorExtension.SetValueFast(property, model, (object)200);
            Assert.Equal(200, model.IntProp);
        }
    }
}