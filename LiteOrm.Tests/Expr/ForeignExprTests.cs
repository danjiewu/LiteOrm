using System;
using System.Linq;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for <see cref="ForeignExpr"/> class.
    /// </summary>
    public sealed partial class ForeignExprTests
    {
        /// <summary>
        /// Verifies that TableArgs property is initialized to an empty array by default.
        /// Tests the default value behavior when no value has been set.
        /// Expected: Returns an empty string array.
        /// </summary>
        [Fact]
        public void TableArgs_DefaultValue_ReturnsEmptyArray()
        {
            // Arrange & Act
            var foreignExpr = new ForeignExpr();

            // Assert
            Assert.NotNull(foreignExpr.TableArgs);
            Assert.Empty(foreignExpr.TableArgs);
        }

        /// <summary>
        /// Verifies that TableArgs property accepts and returns null value.
        /// Tests the behavior when explicitly setting null.
        /// Expected: Getter returns null after setting null.
        /// </summary>
        [Fact]
        public void TableArgs_SetNull_ReturnsNull()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();

            // Act
            foreignExpr.TableArgs = null;

            // Assert
            Assert.Null(foreignExpr.TableArgs);
        }

        /// <summary>
        /// Verifies that TableArgs property accepts and returns an empty array.
        /// Tests the behavior when setting an empty array.
        /// Expected: Getter returns the empty array.
        /// </summary>
        [Fact]
        public void TableArgs_SetEmptyArray_ReturnsEmptyArray()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();
            var emptyArray = new string[0];

            // Act
            foreignExpr.TableArgs = emptyArray;

            // Assert
            Assert.NotNull(foreignExpr.TableArgs);
            Assert.Empty(foreignExpr.TableArgs);
        }

        /// <summary>
        /// Verifies that TableArgs property accepts valid SQL names containing letters, numbers, and underscores.
        /// Tests various valid SQL name patterns.
        /// Expected: Property accepts the values without throwing exceptions.
        /// </summary>
        [Theory]
        [InlineData(new[] { "table1" })]
        [InlineData(new[] { "Column_Name" })]
        [InlineData(new[] { "field123" })]
        [InlineData(new[] { "_privateField" })]
        [InlineData(new[] { "UPPERCASE" })]
        [InlineData(new[] { "lowercase" })]
        [InlineData(new[] { "Mixed_Case_123" })]
        [InlineData(new[] { "table1", "table2", "table3" })]
        [InlineData(new[] { "a", "b_c", "d123" })]
        public void TableArgs_SetValidNames_AcceptsValues(string[] validNames)
        {
            // Arrange
            var foreignExpr = new ForeignExpr();

            // Act
            foreignExpr.TableArgs = validNames;

            // Assert
            Assert.Equal(validNames, foreignExpr.TableArgs);
        }

        /// <summary>
        /// Verifies that TableArgs property accepts arrays containing null elements.
        /// Tests the behavior when array contains null strings.
        /// Expected: Null elements are allowed (they pass validation).
        /// </summary>
        [Fact]
        public void TableArgs_SetArrayWithNullElement_AcceptsValue()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();
            var arrayWithNull = new string[] { "valid", null, "table" };

            // Act
            foreignExpr.TableArgs = arrayWithNull;

            // Assert
            Assert.Equal(arrayWithNull, foreignExpr.TableArgs);
        }

        /// <summary>
        /// Verifies that TableArgs property accepts arrays containing empty string elements.
        /// Tests the behavior when array contains empty strings.
        /// Expected: Empty strings are allowed (they pass validation).
        /// </summary>
        [Fact]
        public void TableArgs_SetArrayWithEmptyStringElement_AcceptsValue()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();
            var arrayWithEmpty = new string[] { "valid", "", "table" };

            // Act
            foreignExpr.TableArgs = arrayWithEmpty;

            // Assert
            Assert.Equal(arrayWithEmpty, foreignExpr.TableArgs);
        }

        /// <summary>
        /// Verifies that TableArgs property throws ArgumentException for invalid SQL names with spaces.
        /// Tests the validation logic against names containing spaces.
        /// Expected: Throws ArgumentException with appropriate message.
        /// </summary>
        [Fact]
        public void TableArgs_SetNameWithSpaces_ThrowsArgumentException()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();
            var invalidNames = new[] { "invalid name" };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => foreignExpr.TableArgs = invalidNames);
            Assert.Equal("TableArgs", exception.ParamName);
            Assert.Contains("invalid name", exception.Message);
            Assert.Contains("invalid characters", exception.Message);
            Assert.Contains("letters, numbers, and underscores", exception.Message);
        }

        /// <summary>
        /// Verifies that TableArgs property throws ArgumentException for invalid SQL names with hyphens.
        /// Tests the validation logic against names containing hyphens.
        /// Expected: Throws ArgumentException with appropriate message.
        /// </summary>
        [Fact]
        public void TableArgs_SetNameWithHyphen_ThrowsArgumentException()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();
            var invalidNames = new[] { "invalid-name" };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => foreignExpr.TableArgs = invalidNames);
            Assert.Equal("TableArgs", exception.ParamName);
            Assert.Contains("invalid-name", exception.Message);
        }

        /// <summary>
        /// Verifies that TableArgs property throws ArgumentException for invalid SQL names with special characters.
        /// Tests the validation logic against various special characters that are not allowed in SQL names.
        /// Expected: Throws ArgumentException for each invalid character.
        /// </summary>
        [Theory]
        [InlineData("name@domain")]
        [InlineData("name$special")]
        [InlineData("name%percent")]
        [InlineData("name&ampersand")]
        [InlineData("name*asterisk")]
        [InlineData("name(parens)")]
        [InlineData("name.dot")]
        [InlineData("name,comma")]
        [InlineData("name;semicolon")]
        [InlineData("name:colon")]
        [InlineData("name'quote")]
        [InlineData("name\"doublequote")]
        [InlineData("name[bracket]")]
        [InlineData("name{brace}")]
        [InlineData("name+plus")]
        [InlineData("name=equals")]
        [InlineData("name!exclaim")]
        [InlineData("name?question")]
        [InlineData("name#hash")]
        [InlineData("name\\backslash")]
        [InlineData("name/slash")]
        public void TableArgs_SetNameWithSpecialCharacters_ThrowsArgumentException(string invalidName)
        {
            // Arrange
            var foreignExpr = new ForeignExpr();
            var invalidNames = new[] { invalidName };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => foreignExpr.TableArgs = invalidNames);
            Assert.Equal("TableArgs", exception.ParamName);
            Assert.Contains(invalidName, exception.Message);
        }

        /// <summary>
        /// Verifies that TableArgs property throws ArgumentException on the first invalid name in a mixed array.
        /// Tests the behavior when array contains both valid and invalid names.
        /// Expected: Throws ArgumentException when encountering the first invalid name.
        /// </summary>
        [Fact]
        public void TableArgs_SetMixedValidInvalidNames_ThrowsOnFirstInvalid()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();
            var mixedNames = new[] { "valid1", "invalid-name", "valid2" };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => foreignExpr.TableArgs = mixedNames);
            Assert.Equal("TableArgs", exception.ParamName);
            Assert.Contains("invalid-name", exception.Message);
        }

        /// <summary>
        /// Verifies that TableArgs property does not mutate when an invalid value is attempted to be set.
        /// Tests that the property retains its previous value after a failed validation.
        /// Expected: Property value remains unchanged after exception.
        /// </summary>
        [Fact]
        public void TableArgs_SetInvalidValue_DoesNotMutateProperty()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();
            var validNames = new[] { "valid1", "valid2" };
            foreignExpr.TableArgs = validNames;
            var invalidNames = new[] { "invalid-name" };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => foreignExpr.TableArgs = invalidNames);
            Assert.Equal(validNames, foreignExpr.TableArgs);
        }

        /// <summary>
        /// Verifies that TableArgs property accepts very long valid SQL names.
        /// Tests the behavior with long strings that are still valid SQL names.
        /// Expected: Accepts long valid names without throwing.
        /// </summary>
        [Fact]
        public void TableArgs_SetVeryLongValidName_AcceptsValue()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();
            var longValidName = new string('a', 1000) + "_" + new string('1', 1000);
            var names = new[] { longValidName };

            // Act
            foreignExpr.TableArgs = names;

            // Assert
            Assert.Single(foreignExpr.TableArgs);
            Assert.Equal(longValidName, foreignExpr.TableArgs[0]);
        }

        /// <summary>
        /// Verifies that TableArgs property accepts arrays with only null and empty elements.
        /// Tests edge case where all elements are null or empty.
        /// Expected: Accepts the array (null and empty are allowed).
        /// </summary>
        [Fact]
        public void TableArgs_SetArrayWithOnlyNullAndEmpty_AcceptsValue()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();
            var arrayWithNullAndEmpty = new string[] { null, "", null, "" };

            // Act
            foreignExpr.TableArgs = arrayWithNullAndEmpty;

            // Assert
            Assert.Equal(4, foreignExpr.TableArgs.Length);
            Assert.Equal(arrayWithNullAndEmpty, foreignExpr.TableArgs);
        }

        /// <summary>
        /// Verifies that TableArgs property can be set multiple times with different valid values.
        /// Tests the behavior when property is updated multiple times.
        /// Expected: Each assignment correctly updates the property value.
        /// </summary>
        [Fact]
        public void TableArgs_SetMultipleTimes_UpdatesValue()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();
            var firstValue = new[] { "first" };
            var secondValue = new[] { "second", "third" };
            var thirdValue = new string[0];

            // Act & Assert
            foreignExpr.TableArgs = firstValue;
            Assert.Equal(firstValue, foreignExpr.TableArgs);

            foreignExpr.TableArgs = secondValue;
            Assert.Equal(secondValue, foreignExpr.TableArgs);

            foreignExpr.TableArgs = thirdValue;
            Assert.Equal(thirdValue, foreignExpr.TableArgs);
        }

        /// <summary>
        /// Tests that the default constructor initializes all properties to their expected default values.
        /// </summary>
        [Fact]
        public void Constructor_Default_InitializesPropertiesToDefaultValues()
        {
            // Act
            var foreignExpr = new ForeignExpr();

            // Assert
            Assert.Null(foreignExpr.Foreign);
            Assert.Null(foreignExpr.InnerExpr);
            Assert.Null(foreignExpr.Alias);
            Assert.NotNull(foreignExpr.TableArgs);
            Assert.Empty(foreignExpr.TableArgs);
            Assert.False(foreignExpr.AutoRelated);
        }

        /// <summary>
        /// Tests that the default constructor creates an instance with the correct ExprType.
        /// </summary>
        [Fact]
        public void Constructor_Default_SetsExprTypeToForeign()
        {
            // Act
            var foreignExpr = new ForeignExpr();

            // Assert
            Assert.Equal(ExprType.Foreign, foreignExpr.ExprType);
        }

        /// <summary>
        /// Tests that ToString works correctly for a default-constructed instance with null values.
        /// </summary>
        [Fact]
        public void Constructor_Default_ToStringReturnsExpectedFormat()
        {
            // Act
            var foreignExpr = new ForeignExpr();
            var result = foreignExpr.ToString();

            // Assert
            Assert.Equal("{:}", result);
        }

        /// <summary>
        /// Tests that two default-constructed instances are equal.
        /// </summary>
        [Fact]
        public void Constructor_Default_TwoInstancesAreEqual()
        {
            // Arrange
            var foreignExpr1 = new ForeignExpr();
            var foreignExpr2 = new ForeignExpr();

            // Assert
            Assert.True(foreignExpr1.Equals(foreignExpr2));
            Assert.Equal(foreignExpr1.GetHashCode(), foreignExpr2.GetHashCode());
        }

        /// <summary>
        /// Tests that Clone works correctly for a default-constructed instance.
        /// </summary>
        [Fact]
        public void Constructor_Default_CloneCreatesEqualInstance()
        {
            // Arrange
            var original = new ForeignExpr();

            // Act
            var cloned = (ForeignExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.True(original.Equals(cloned));
            Assert.Equal(original.GetHashCode(), cloned.GetHashCode());
            Assert.Null(cloned.Foreign);
            Assert.Null(cloned.InnerExpr);
            Assert.Null(cloned.Alias);
            Assert.NotNull(cloned.TableArgs);
            Assert.Empty(cloned.TableArgs);
            Assert.False(cloned.AutoRelated);
        }

        /// <summary>
        /// Tests that properties can be set after default construction.
        /// </summary>
        [Fact]
        public void Constructor_Default_PropertiesCanBeSetAfterConstruction()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();
            var testType = typeof(string);
            var testExpr = new ForeignExpr();
            var testAlias = "TestAlias";
            var testTableArgs = new[] { "arg1", "arg2" };

            // Act
            foreignExpr.Foreign = testType;
            foreignExpr.InnerExpr = testExpr;
            foreignExpr.Alias = testAlias;
            foreignExpr.TableArgs = testTableArgs;
            foreignExpr.AutoRelated = true;

            // Assert
            Assert.Equal(testType, foreignExpr.Foreign);
            Assert.Equal(testExpr, foreignExpr.InnerExpr);
            Assert.Equal(testAlias, foreignExpr.Alias);
            Assert.Equal(testTableArgs, foreignExpr.TableArgs);
            Assert.True(foreignExpr.AutoRelated);
        }

        /// <summary>
        /// Tests that GetHashCode does not throw for a default-constructed instance.
        /// </summary>
        [Fact]
        public void Constructor_Default_GetHashCodeDoesNotThrow()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();

            // Act & Assert
            var hashCode = foreignExpr.GetHashCode();
            Assert.IsType<int>(hashCode);
        }

        /// <summary>
        /// Tests that a default-constructed instance does not equal null.
        /// </summary>
        [Fact]
        public void Constructor_Default_DoesNotEqualNull()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();

            // Act & Assert
            Assert.False(foreignExpr.Equals(null));
        }

        /// <summary>
        /// Tests that a default-constructed instance does not equal an object of a different type.
        /// </summary>
        [Fact]
        public void Constructor_Default_DoesNotEqualDifferentType()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();
            var otherObject = new object();

            // Act & Assert
            Assert.False(foreignExpr.Equals(otherObject));
        }

        /// <summary>
        /// Tests that TableArgs can be set to null after default construction.
        /// </summary>
        [Fact]
        public void Constructor_Default_TableArgsCanBeSetToNull()
        {
            // Arrange
            var foreignExpr = new ForeignExpr();

            // Act
            foreignExpr.TableArgs = null;

            // Assert
            Assert.Null(foreignExpr.TableArgs);
        }

        /// <summary>
        /// Tests that two default-constructed instances with modified TableArgs to null are equal.
        /// </summary>
        [Fact]
        public void Constructor_Default_InstancesWithNullTableArgsAreEqual()
        {
            // Arrange
            var foreignExpr1 = new ForeignExpr();
            var foreignExpr2 = new ForeignExpr();
            foreignExpr1.TableArgs = null;
            foreignExpr2.TableArgs = null;

            // Assert
            Assert.True(foreignExpr1.Equals(foreignExpr2));
            Assert.Equal(foreignExpr1.GetHashCode(), foreignExpr2.GetHashCode());
        }

        /// <summary>
        /// Tests that Alias property allows null and can be retrieved correctly.
        /// </summary>
        [Fact]
        public void Alias_SetNull_ReturnsNull()
        {
            // Arrange
            var expr = new ForeignExpr();

            // Act
            expr.Alias = null;

            // Assert
            Assert.Null(expr.Alias);
        }

        /// <summary>
        /// Tests that Alias property allows empty string and can be retrieved correctly.
        /// </summary>
        [Fact]
        public void Alias_SetEmptyString_ReturnsEmptyString()
        {
            // Arrange
            var expr = new ForeignExpr();

            // Act
            expr.Alias = string.Empty;

            // Assert
            Assert.Equal(string.Empty, expr.Alias);
        }

        /// <summary>
        /// Tests that Alias property accepts valid SQL names containing only letters, numbers, and underscores.
        /// </summary>
        /// <param name="validAlias">The valid alias value to test.</param>
        [Theory]
        [InlineData("alias")]
        [InlineData("Alias")]
        [InlineData("ALIAS")]
        [InlineData("alias1")]
        [InlineData("Alias123")]
        [InlineData("my_alias")]
        [InlineData("_alias")]
        [InlineData("__alias__")]
        [InlineData("alias_")]
        [InlineData("a1b2c3")]
        [InlineData("_")]
        [InlineData("a")]
        [InlineData("A")]
        [InlineData("_1")]
        [InlineData("CamelCaseAlias")]
        [InlineData("snake_case_alias")]
        [InlineData("UPPER_CASE_ALIAS")]
        [InlineData("MixedCase_123_Alias")]
        public void Alias_SetValidSqlName_ReturnsSetValue(string validAlias)
        {
            // Arrange
            var expr = new ForeignExpr();

            // Act
            expr.Alias = validAlias;

            // Assert
            Assert.Equal(validAlias, expr.Alias);
        }

        /// <summary>
        /// Tests that Alias property throws ArgumentException when set to whitespace-only strings.
        /// </summary>
        /// <param name="invalidAlias">The invalid alias value containing only whitespace.</param>
        [Theory]
        [InlineData(" ")]
        [InlineData("  ")]
        [InlineData("\t")]
        [InlineData("\n")]
        [InlineData("\r")]
        [InlineData("\r\n")]
        [InlineData("   \t   ")]
        public void Alias_SetWhitespaceOnly_ThrowsArgumentException(string invalidAlias)
        {
            // Arrange
            var expr = new ForeignExpr();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => expr.Alias = invalidAlias);
            Assert.Equal("Alias", exception.ParamName);
            Assert.Contains("invalid characters", exception.Message);
        }

        /// <summary>
        /// Tests that Alias property throws ArgumentException when set to names with spaces.
        /// </summary>
        /// <param name="invalidAlias">The invalid alias value containing spaces.</param>
        [Theory]
        [InlineData("my alias")]
        [InlineData(" alias")]
        [InlineData("alias ")]
        [InlineData("my alias table")]
        [InlineData("a b")]
        public void Alias_SetNameWithSpaces_ThrowsArgumentException(string invalidAlias)
        {
            // Arrange
            var expr = new ForeignExpr();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => expr.Alias = invalidAlias);
            Assert.Equal("Alias", exception.ParamName);
            Assert.Contains("invalid characters", exception.Message);
        }

        /// <summary>
        /// Tests that Alias property throws ArgumentException when set to names with special characters.
        /// </summary>
        /// <param name="invalidAlias">The invalid alias value containing special characters.</param>
        [Theory]
        [InlineData("alias!")]
        [InlineData("alias@")]
        [InlineData("alias#")]
        [InlineData("alias$")]
        [InlineData("alias%")]
        [InlineData("alias^")]
        [InlineData("alias&")]
        [InlineData("alias*")]
        [InlineData("alias(")]
        [InlineData("alias)")]
        [InlineData("alias-")]
        [InlineData("alias+")]
        [InlineData("alias=")]
        [InlineData("alias{")]
        [InlineData("alias}")]
        [InlineData("alias[")]
        [InlineData("alias]")]
        [InlineData("alias|")]
        [InlineData("alias\\")]
        [InlineData("alias/")]
        [InlineData("alias:")]
        [InlineData("alias;")]
        [InlineData("alias\"")]
        [InlineData("alias'")]
        [InlineData("alias<")]
        [InlineData("alias>")]
        [InlineData("alias,")]
        [InlineData("alias.")]
        [InlineData("alias?")]
        [InlineData("alias~")]
        [InlineData("alias`")]
        public void Alias_SetNameWithSpecialCharacters_ThrowsArgumentException(string invalidAlias)
        {
            // Arrange
            var expr = new ForeignExpr();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => expr.Alias = invalidAlias);
            Assert.Equal("Alias", exception.ParamName);
            Assert.Contains("invalid characters", exception.Message);
        }

        /// <summary>
        /// Tests that Alias property throws ArgumentException when set to SQL injection attempts or dangerous patterns.
        /// </summary>
        /// <param name="invalidAlias">The invalid alias value with potentially dangerous SQL patterns.</param>
        [Theory]
        [InlineData("alias'; DROP TABLE users--")]
        [InlineData("alias--")]
        [InlineData("alias/*")]
        [InlineData("alias*/")]
        [InlineData("1=1")]
        [InlineData("OR 1=1")]
        public void Alias_SetSqlInjectionPattern_ThrowsArgumentException(string invalidAlias)
        {
            // Arrange
            var expr = new ForeignExpr();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => expr.Alias = invalidAlias);
            Assert.Equal("Alias", exception.ParamName);
            Assert.Contains("invalid characters", exception.Message);
        }

        /// <summary>
        /// Tests that Alias property throws ArgumentException when set to Unicode or non-ASCII characters.
        /// </summary>
        /// <param name="invalidAlias">The invalid alias value containing Unicode or non-ASCII characters.</param>
        [Theory]
        [InlineData("aliás")]
        [InlineData("aliäs")]
        [InlineData("别名")]
        [InlineData("псевдоним")]
        [InlineData("alias™")]
        [InlineData("alias©")]
        [InlineData("alias€")]
        public void Alias_SetUnicodeCharacters_ThrowsArgumentException(string invalidAlias)
        {
            // Arrange
            var expr = new ForeignExpr();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => expr.Alias = invalidAlias);
            Assert.Equal("Alias", exception.ParamName);
            Assert.Contains("invalid characters", exception.Message);
        }

        /// <summary>
        /// Tests that Alias property maintains its value after being set multiple times.
        /// </summary>
        [Fact]
        public void Alias_SetMultipleTimes_ReturnsLatestValue()
        {
            // Arrange
            var expr = new ForeignExpr();

            // Act
            expr.Alias = "first_alias";
            expr.Alias = "second_alias";
            expr.Alias = "third_alias";

            // Assert
            Assert.Equal("third_alias", expr.Alias);
        }

        /// <summary>
        /// Tests that Alias property can be set from valid value to null.
        /// </summary>
        [Fact]
        public void Alias_SetValidThenNull_ReturnsNull()
        {
            // Arrange
            var expr = new ForeignExpr();
            expr.Alias = "valid_alias";

            // Act
            expr.Alias = null;

            // Assert
            Assert.Null(expr.Alias);
        }

        /// <summary>
        /// Tests that Alias property can be set from null to valid value.
        /// </summary>
        [Fact]
        public void Alias_SetNullThenValid_ReturnsValidValue()
        {
            // Arrange
            var expr = new ForeignExpr();
            expr.Alias = null;

            // Act
            expr.Alias = "valid_alias";

            // Assert
            Assert.Equal("valid_alias", expr.Alias);
        }

        /// <summary>
        /// Tests that Alias property can be set from valid value to empty string.
        /// </summary>
        [Fact]
        public void Alias_SetValidThenEmpty_ReturnsEmpty()
        {
            // Arrange
            var expr = new ForeignExpr();
            expr.Alias = "valid_alias";

            // Act
            expr.Alias = string.Empty;

            // Assert
            Assert.Equal(string.Empty, expr.Alias);
        }

        /// <summary>
        /// Tests that Alias property throws ArgumentException for very long string with invalid characters.
        /// </summary>
        [Fact]
        public void Alias_SetVeryLongStringWithInvalidCharacters_ThrowsArgumentException()
        {
            // Arrange
            var expr = new ForeignExpr();
            var invalidAlias = new string('a', 1000) + "!";

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => expr.Alias = invalidAlias);
            Assert.Equal("Alias", exception.ParamName);
            Assert.Contains("invalid characters", exception.Message);
        }

        /// <summary>
        /// Tests that Alias property accepts very long valid SQL name.
        /// </summary>
        [Fact]
        public void Alias_SetVeryLongValidSqlName_ReturnsSetValue()
        {
            // Arrange
            var expr = new ForeignExpr();
            var validAlias = new string('a', 10000);

            // Act
            expr.Alias = validAlias;

            // Assert
            Assert.Equal(validAlias, expr.Alias);
        }

        /// <summary>
        /// Tests that Clone creates a deep copy with all properties populated.
        /// Verifies that the clone has the same property values and is equal to the original.
        /// </summary>
        [Fact]
        public void Clone_AllPropertiesPopulated_CreatesDeepCopyWithSameValues()
        {
            // Arrange
            var innerExpr = new ForeignExpr(typeof(string), "innerAlias", null);
            var original = new ForeignExpr(typeof(int), "testAlias", innerExpr, "arg1", "arg2")
            {
                AutoRelated = true
            };

            // Act
            var clone = (ForeignExpr)original.Clone();

            // Assert
            Assert.NotNull(clone);
            Assert.Equal(original.Foreign, clone.Foreign);
            Assert.Equal(original.Alias, clone.Alias);
            Assert.Equal(original.AutoRelated, clone.AutoRelated);
            Assert.NotNull(clone.TableArgs);
            Assert.Equal(original.TableArgs.Length, clone.TableArgs.Length);
            Assert.Equal(original.TableArgs, clone.TableArgs);
            Assert.NotNull(clone.InnerExpr);
            Assert.True(original.Equals(clone));
        }

        /// <summary>
        /// Tests that Clone creates a separate instance, not the same reference.
        /// Verifies that the cloned object is not the same instance as the original.
        /// </summary>
        [Fact]
        public void Clone_Always_ReturnsDifferentInstance()
        {
            // Arrange
            var original = new ForeignExpr(typeof(string), "alias", null);

            // Act
            var clone = (ForeignExpr)original.Clone();

            // Assert
            Assert.NotSame(original, clone);
        }

        /// <summary>
        /// Tests that Clone handles null InnerExpr correctly.
        /// Verifies that a null InnerExpr is properly cloned as null.
        /// </summary>
        [Fact]
        public void Clone_NullInnerExpr_ClonesCorrectly()
        {
            // Arrange
            var original = new ForeignExpr(typeof(double))
            {
                Alias = "testAlias",
                AutoRelated = false,
                TableArgs = new[] { "arg1" }
            };

            // Act
            var clone = (ForeignExpr)original.Clone();

            // Assert
            Assert.Null(clone.InnerExpr);
            Assert.Equal(original.Foreign, clone.Foreign);
            Assert.Equal(original.Alias, clone.Alias);
            Assert.Equal(original.AutoRelated, clone.AutoRelated);
            Assert.Equal(original.TableArgs, clone.TableArgs);
            Assert.True(original.Equals(clone));
        }

        /// <summary>
        /// Tests that Clone handles null TableArgs correctly.
        /// Verifies that a null TableArgs is properly cloned as null.
        /// </summary>
        [Fact]
        public void Clone_NullTableArgs_ClonesCorrectly()
        {
            // Arrange
            var innerExpr = new ForeignExpr(typeof(string));
            var original = new ForeignExpr(typeof(int), innerExpr)
            {
                Alias = "alias",
                AutoRelated = true,
                TableArgs = null
            };

            // Act
            var clone = (ForeignExpr)original.Clone();

            // Assert
            Assert.Null(clone.TableArgs);
            Assert.Equal(original.Foreign, clone.Foreign);
            Assert.Equal(original.Alias, clone.Alias);
            Assert.Equal(original.AutoRelated, clone.AutoRelated);
            Assert.NotNull(clone.InnerExpr);
            Assert.True(original.Equals(clone));
        }

        /// <summary>
        /// Tests that Clone handles both null InnerExpr and null TableArgs correctly.
        /// Verifies that both null properties are properly cloned as null.
        /// </summary>
        [Fact]
        public void Clone_NullInnerExprAndTableArgs_ClonesCorrectly()
        {
            // Arrange
            var original = new ForeignExpr(typeof(long))
            {
                Alias = "testAlias",
                AutoRelated = false,
                InnerExpr = null,
                TableArgs = null
            };

            // Act
            var clone = (ForeignExpr)original.Clone();

            // Assert
            Assert.Null(clone.InnerExpr);
            Assert.Null(clone.TableArgs);
            Assert.Equal(original.Foreign, clone.Foreign);
            Assert.Equal(original.Alias, clone.Alias);
            Assert.Equal(original.AutoRelated, clone.AutoRelated);
            Assert.True(original.Equals(clone));
        }

        /// <summary>
        /// Tests that Clone handles empty TableArgs array correctly.
        /// Verifies that an empty array is properly cloned.
        /// </summary>
        [Fact]
        public void Clone_EmptyTableArgs_ClonesCorrectly()
        {
            // Arrange
            var original = new ForeignExpr(typeof(byte))
            {
                TableArgs = new string[0],
                AutoRelated = true
            };

            // Act
            var clone = (ForeignExpr)original.Clone();

            // Assert
            Assert.NotNull(clone.TableArgs);
            Assert.Empty(clone.TableArgs);
            Assert.True(original.Equals(clone));
        }

        /// <summary>
        /// Tests that Clone creates a deep copy of TableArgs array.
        /// Verifies that modifying the cloned TableArgs does not affect the original.
        /// </summary>
        [Fact]
        public void Clone_TableArgs_CreatesDeepCopy()
        {
            // Arrange
            var original = new ForeignExpr(typeof(string))
            {
                TableArgs = new[] { "arg1", "arg2", "arg3" }
            };

            // Act
            var clone = (ForeignExpr)original.Clone();
            clone.TableArgs[0] = "modified";

            // Assert
            Assert.NotSame(original.TableArgs, clone.TableArgs);
            Assert.Equal("arg1", original.TableArgs[0]);
            Assert.Equal("modified", clone.TableArgs[0]);
        }

        /// <summary>
        /// Tests that Clone creates a deep copy of InnerExpr.
        /// Verifies that the cloned InnerExpr is a separate instance.
        /// </summary>
        [Fact]
        public void Clone_InnerExpr_CreatesDeepCopy()
        {
            // Arrange
            var innerExpr = new ForeignExpr(typeof(char), "innerAlias", null);
            var original = new ForeignExpr(typeof(decimal), innerExpr);

            // Act
            var clone = (ForeignExpr)original.Clone();

            // Assert
            Assert.NotNull(clone.InnerExpr);
            Assert.NotSame(original.InnerExpr, clone.InnerExpr);
            Assert.True(original.InnerExpr.Equals(clone.InnerExpr));
        }

        /// <summary>
        /// Tests that Clone with default constructor values produces correct clone.
        /// Verifies that cloning a newly constructed ForeignExpr works correctly.
        /// </summary>
        [Fact]
        public void Clone_DefaultConstructor_ClonesCorrectly()
        {
            // Arrange
            var original = new ForeignExpr();

            // Act
            var clone = (ForeignExpr)original.Clone();

            // Assert
            Assert.NotSame(original, clone);
            Assert.True(original.Equals(clone));
        }

        /// <summary>
        /// Tests that Clone preserves Foreign type reference correctly.
        /// Verifies that different Type instances are handled correctly.
        /// </summary>
        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(object))]
        [InlineData(typeof(ForeignExpr))]
        public void Clone_VariousForeignTypes_PreservesTypeReference(Type foreignType)
        {
            // Arrange
            var original = new ForeignExpr(foreignType);

            // Act
            var clone = (ForeignExpr)original.Clone();

            // Assert
            Assert.Equal(foreignType, clone.Foreign);
            Assert.Same(original.Foreign, clone.Foreign);
        }

        /// <summary>
        /// Tests that Clone preserves AutoRelated flag correctly.
        /// Verifies that both true and false values are properly cloned.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Clone_AutoRelatedFlag_PreservesValue(bool autoRelated)
        {
            // Arrange
            var original = new ForeignExpr(typeof(bool))
            {
                AutoRelated = autoRelated
            };

            // Act
            var clone = (ForeignExpr)original.Clone();

            // Assert
            Assert.Equal(autoRelated, clone.AutoRelated);
        }

        /// <summary>
        /// Tests that Clone handles multiple table arguments correctly.
        /// Verifies that all elements in TableArgs are properly cloned.
        /// </summary>
        [Fact]
        public void Clone_MultipleTableArgs_ClonesAllElements()
        {
            // Arrange
            var original = new ForeignExpr(typeof(float))
            {
                TableArgs = new[] { "2024", "user", "data", "table" }
            };

            // Act
            var clone = (ForeignExpr)original.Clone();

            // Assert
            Assert.Equal(4, clone.TableArgs.Length);
            for (int i = 0; i < original.TableArgs.Length; i++)
            {
                Assert.Equal(original.TableArgs[i], clone.TableArgs[i]);
            }
        }

        /// <summary>
        /// Tests that Clone preserves nested InnerExpr structure.
        /// Verifies that deeply nested ForeignExpr instances are properly cloned.
        /// </summary>
        [Fact]
        public void Clone_NestedInnerExpr_PreservesStructure()
        {
            // Arrange
            var level3 = new ForeignExpr(typeof(byte));
            var level2 = new ForeignExpr(typeof(short), level3);
            var level1 = new ForeignExpr(typeof(int), level2);

            // Act
            var clone = (ForeignExpr)level1.Clone();

            // Assert
            Assert.NotSame(level1, clone);
            Assert.NotSame(level1.InnerExpr, clone.InnerExpr);
            var clonedLevel2 = (ForeignExpr)clone.InnerExpr;
            Assert.NotNull(clonedLevel2);
            Assert.NotSame(level2.InnerExpr, clonedLevel2.InnerExpr);
            Assert.True(level1.Equals(clone));
        }

        /// <summary>
        /// Tests that Clone handles Alias property correctly.
        /// Verifies that various Alias values are properly cloned.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("a")]
        [InlineData("testAlias")]
        [InlineData("VeryLongAliasNameForTesting")]
        public void Clone_VariousAliasValues_PreservesValue(string? alias)
        {
            // Arrange
            var original = new ForeignExpr(typeof(object))
            {
                Alias = alias
            };

            // Act
            var clone = (ForeignExpr)original.Clone();

            // Assert
            Assert.Equal(alias, clone.Alias);
        }

        /// <summary>
        /// Tests that GetHashCode returns the same hash code for two instances with identical property values.
        /// </summary>
        [Fact]
        public void GetHashCode_IdenticalInstances_ReturnsSameHashCode()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(string), "alias1", Expr.Prop("Name") == "Test")
            {
                AutoRelated = true,
                TableArgs = new[] { "arg1", "arg2" }
            };
            var expr2 = new ForeignExpr(typeof(string), "alias1", Expr.Prop("Name") == "Test")
            {
                AutoRelated = true,
                TableArgs = new[] { "arg1", "arg2" }
            };

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode produces consistent hash codes for instances with different Foreign types.
        /// </summary>
        /// <param name="foreignType1">First foreign type.</param>
        /// <param name="foreignType2">Second foreign type.</param>
        /// <param name="shouldBeEqual">Whether hash codes should be equal.</param>
        [Theory]
        [InlineData(typeof(string), typeof(string), true)]
        [InlineData(typeof(string), typeof(int), false)]
        [InlineData(typeof(object), typeof(string), false)]
        public void GetHashCode_DifferentForeignTypes_ProducesConsistentHashCodes(System.Type foreignType1, System.Type foreignType2, bool shouldBeEqual)
        {
            // Arrange
            var expr1 = new ForeignExpr(foreignType1, Expr.Prop("Name") == "Test");
            var expr2 = new ForeignExpr(foreignType2, Expr.Prop("Name") == "Test");

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            if (shouldBeEqual)
            {
                Assert.Equal(hash1, hash2);
            }
            else
            {
                Assert.NotEqual(hash1, hash2);
            }
        }

        /// <summary>
        /// Tests that GetHashCode handles null Foreign type correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_NullForeignType_DoesNotThrow()
        {
            // Arrange
            var expr = new ForeignExpr { Foreign = null };

            // Act
            int hash = expr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hash);
        }

        /// <summary>
        /// Tests that GetHashCode produces different hash codes for different Alias values.
        /// </summary>
        /// <param name="alias1">First alias value.</param>
        /// <param name="alias2">Second alias value.</param>
        /// <param name="shouldBeEqual">Whether hash codes should be equal.</param>
        [Theory]
        [InlineData(null, null, true)]
        [InlineData("alias1", "alias1", true)]
        [InlineData("alias1", "alias2", false)]
        [InlineData(null, "alias1", false)]
        [InlineData("", "", true)]
        public void GetHashCode_DifferentAliasValues_ProducesConsistentHashCodes(string alias1, string alias2, bool shouldBeEqual)
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(string), alias1, Expr.Prop("Name") == "Test");
            var expr2 = new ForeignExpr(typeof(string), alias2, Expr.Prop("Name") == "Test");

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            if (shouldBeEqual)
            {
                Assert.Equal(hash1, hash2);
            }
            else
            {
                Assert.NotEqual(hash1, hash2);
            }
        }

        /// <summary>
        /// Tests that GetHashCode produces different hash codes based on AutoRelated property.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentAutoRelatedValues_ProducesDifferentHashCodes()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(string), Expr.Prop("Name") == "Test") { AutoRelated = true };
            var expr2 = new ForeignExpr(typeof(string), Expr.Prop("Name") == "Test") { AutoRelated = false };

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles null InnerExpr correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_NullInnerExpr_DoesNotThrow()
        {
            // Arrange
            var expr = new ForeignExpr(typeof(string)) { InnerExpr = null };

            // Act
            int hash = expr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hash);
        }

        /// <summary>
        /// Tests that GetHashCode produces different hash codes for different InnerExpr values.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentInnerExpr_ProducesDifferentHashCodes()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(string), Expr.Prop("Name") == "Test1");
            var expr2 = new ForeignExpr(typeof(string), Expr.Prop("Name") == "Test2");

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles various TableArgs configurations correctly.
        /// </summary>
        /// <param name="tableArgs1">First TableArgs array.</param>
        /// <param name="tableArgs2">Second TableArgs array.</param>
        /// <param name="shouldBeEqual">Whether hash codes should be equal.</param>
        [Theory]
        [MemberData(nameof(GetTableArgsTestData))]
        public void GetHashCode_DifferentTableArgs_ProducesConsistentHashCodes(string[] tableArgs1, string[] tableArgs2, bool shouldBeEqual)
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(string), Expr.Prop("Name") == "Test") { TableArgs = tableArgs1 };
            var expr2 = new ForeignExpr(typeof(string), Expr.Prop("Name") == "Test") { TableArgs = tableArgs2 };

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            if (shouldBeEqual)
            {
                Assert.Equal(hash1, hash2);
            }
            else
            {
                Assert.NotEqual(hash1, hash2);
            }
        }

        /// <summary>
        /// Tests that GetHashCode handles TableArgs with null elements correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_TableArgsWithNullElements_DoesNotThrow()
        {
            // Arrange
            var expr = new ForeignExpr(typeof(string), Expr.Prop("Name") == "Test")
            {
                TableArgs = new string[] { "arg1", null, "arg3" }
            };

            // Act
            int hash = expr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hash);
        }

        /// <summary>
        /// Tests that GetHashCode is consistent across multiple calls for the same instance.
        /// </summary>
        [Fact]
        public void GetHashCode_MultipleCallsSameInstance_ReturnsConsistentValue()
        {
            // Arrange
            var expr = new ForeignExpr(typeof(string), "alias", Expr.Prop("Name") == "Test")
            {
                AutoRelated = true,
                TableArgs = new[] { "arg1", "arg2" }
            };

            // Act
            int hash1 = expr.GetHashCode();
            int hash2 = expr.GetHashCode();
            int hash3 = expr.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
            Assert.Equal(hash2, hash3);
        }

        /// <summary>
        /// Tests that GetHashCode handles all properties being null or default correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_AllPropertiesDefault_DoesNotThrow()
        {
            // Arrange
            var expr = new ForeignExpr();

            // Act
            int hash = expr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hash);
        }

        /// <summary>
        /// Tests that GetHashCode produces different hash codes when TableArgs order differs.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentTableArgsOrder_ProducesDifferentHashCodes()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(string), Expr.Prop("Name") == "Test")
            {
                TableArgs = new[] { "arg1", "arg2" }
            };
            var expr2 = new ForeignExpr(typeof(string), Expr.Prop("Name") == "Test")
            {
                TableArgs = new[] { "arg2", "arg1" }
            };

            // Act
            int hash1 = expr1.GetHashCode();
            int hash2 = expr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Provides test data for TableArgs scenarios.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<object[]> GetTableArgsTestData()
        {
            yield return new object[] { null, null, true };
            yield return new object[] { new string[0], new string[0], true };
            yield return new object[] { new[] { "arg1" }, new[] { "arg1" }, true };
            yield return new object[] { new[] { "arg1", "arg2" }, new[] { "arg1", "arg2" }, true };
            yield return new object[] { null, new string[0], false };
            yield return new object[] { new[] { "arg1" }, new[] { "arg2" }, false };
            yield return new object[] { new[] { "arg1" }, new[] { "arg1", "arg2" }, false };
            yield return new object[] { new[] { "arg1", "arg2" }, new[] { "arg1", "arg2", "arg3" }, false };
        }

        /// <summary>
        /// Tests that Equals returns true when comparing a ForeignExpr instance with itself (reference equality).
        /// </summary>
        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            // Arrange
            var expr = new ForeignExpr(typeof(string), new LogicExpr());

            // Act
            var result = expr.Equals(expr);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with null.
        /// </summary>
        [Fact]
        public void Equals_NullObject_ReturnsFalse()
        {
            // Arrange
            var expr = new ForeignExpr(typeof(string));

            // Act
            var result = expr.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with an object of a different type.
        /// </summary>
        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            // Arrange
            var expr = new ForeignExpr(typeof(string));
            var otherObject = "not a ForeignExpr";

            // Act
            var result = expr.Equals(otherObject);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when all properties are equal.
        /// </summary>
        [Fact]
        public void Equals_AllPropertiesEqual_ReturnsTrue()
        {
            // Arrange
            var innerExpr = new LogicExpr();
            var expr1 = new ForeignExpr(typeof(int), "alias1", innerExpr, "arg1", "arg2")
            {
                AutoRelated = true
            };
            var expr2 = new ForeignExpr(typeof(int), "alias1", innerExpr, "arg1", "arg2")
            {
                AutoRelated = true
            };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when Foreign types are different.
        /// </summary>
        [Fact]
        public void Equals_DifferentForeignType_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int));
            var expr2 = new ForeignExpr(typeof(string));

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both Foreign properties are null.
        /// </summary>
        [Fact]
        public void Equals_BothForeignNull_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ForeignExpr { Foreign = null };
            var expr2 = new ForeignExpr { Foreign = null };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one Foreign is null and the other is not.
        /// </summary>
        [Fact]
        public void Equals_OneForeignNull_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ForeignExpr { Foreign = null };
            var expr2 = new ForeignExpr(typeof(string));

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when Alias values are different.
        /// </summary>
        [Fact]
        public void Equals_DifferentAlias_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int), "alias1", new LogicExpr());
            var expr2 = new ForeignExpr(typeof(int), "alias2", new LogicExpr());

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both Alias properties are null.
        /// </summary>
        [Fact]
        public void Equals_BothAliasNull_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int)) { Alias = null };
            var expr2 = new ForeignExpr(typeof(int)) { Alias = null };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one Alias is null and the other is not.
        /// </summary>
        [Fact]
        public void Equals_OneAliasNull_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int)) { Alias = null };
            var expr2 = new ForeignExpr(typeof(int), "alias", new LogicExpr());

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both Alias properties are empty strings.
        /// </summary>
        [Fact]
        public void Equals_BothAliasEmpty_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int), string.Empty, new LogicExpr());
            var expr2 = new ForeignExpr(typeof(int), string.Empty, new LogicExpr());

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when AutoRelated values are different.
        /// </summary>
        [Fact]
        public void Equals_DifferentAutoRelated_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int)) { AutoRelated = true };
            var expr2 = new ForeignExpr(typeof(int)) { AutoRelated = false };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both AutoRelated are true.
        /// </summary>
        [Fact]
        public void Equals_BothAutoRelatedTrue_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int)) { AutoRelated = true };
            var expr2 = new ForeignExpr(typeof(int)) { AutoRelated = true };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both AutoRelated are false.
        /// </summary>
        [Fact]
        public void Equals_BothAutoRelatedFalse_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int)) { AutoRelated = false };
            var expr2 = new ForeignExpr(typeof(int)) { AutoRelated = false };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when InnerExpr values are different.
        /// </summary>
        [Fact]
        public void Equals_DifferentInnerExpr_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int), new LogicExpr());
            var expr2 = new ForeignExpr(typeof(int), new LogicExpr());

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both InnerExpr are the same instance.
        /// </summary>
        [Fact]
        public void Equals_SameInnerExprInstance_ReturnsTrue()
        {
            // Arrange
            var innerExpr = new LogicExpr();
            var expr1 = new ForeignExpr(typeof(int), innerExpr);
            var expr2 = new ForeignExpr(typeof(int), innerExpr);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both InnerExpr are null.
        /// </summary>
        [Fact]
        public void Equals_BothInnerExprNull_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int)) { InnerExpr = null };
            var expr2 = new ForeignExpr(typeof(int)) { InnerExpr = null };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one InnerExpr is null and the other is not.
        /// </summary>
        [Fact]
        public void Equals_OneInnerExprNull_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int)) { InnerExpr = null };
            var expr2 = new ForeignExpr(typeof(int), new LogicExpr());

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both TableArgs are null.
        /// </summary>
        [Fact]
        public void Equals_BothTableArgsNull_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int)) { TableArgs = null };
            var expr2 = new ForeignExpr(typeof(int)) { TableArgs = null };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one TableArgs is null and the other is an empty array.
        /// </summary>
        [Fact]
        public void Equals_OneTableArgsNullOtherEmpty_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int)) { TableArgs = null };
            var expr2 = new ForeignExpr(typeof(int)) { TableArgs = new string[0] };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one TableArgs is null and the other is non-empty.
        /// </summary>
        [Fact]
        public void Equals_OneTableArgsNull_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int)) { TableArgs = null };
            var expr2 = new ForeignExpr(typeof(int), new LogicExpr(), "arg1");

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both TableArgs are empty arrays.
        /// </summary>
        [Fact]
        public void Equals_BothTableArgsEmpty_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int)) { TableArgs = new string[0] };
            var expr2 = new ForeignExpr(typeof(int)) { TableArgs = new string[0] };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both TableArgs contain the same single element.
        /// </summary>
        [Fact]
        public void Equals_SameSingleTableArg_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int), new LogicExpr(), "arg1");
            var expr2 = new ForeignExpr(typeof(int), new LogicExpr(), "arg1");

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both TableArgs contain the same multiple elements.
        /// </summary>
        [Fact]
        public void Equals_SameMultipleTableArgs_ReturnsTrue()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int), new LogicExpr(), "arg1", "arg2", "arg3");
            var expr2 = new ForeignExpr(typeof(int), new LogicExpr(), "arg1", "arg2", "arg3");

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when TableArgs have different lengths.
        /// </summary>
        [Fact]
        public void Equals_DifferentTableArgsLength_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int), new LogicExpr(), "arg1");
            var expr2 = new ForeignExpr(typeof(int), new LogicExpr(), "arg1", "arg2");

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when TableArgs have different values.
        /// </summary>
        [Fact]
        public void Equals_DifferentTableArgsValues_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int), new LogicExpr(), "arg1", "arg2");
            var expr2 = new ForeignExpr(typeof(int), new LogicExpr(), "arg1", "arg3");

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when TableArgs have same elements in different order.
        /// </summary>
        [Fact]
        public void Equals_TableArgsDifferentOrder_ReturnsFalse()
        {
            // Arrange
            var expr1 = new ForeignExpr(typeof(int), new LogicExpr(), "arg1", "arg2");
            var expr2 = new ForeignExpr(typeof(int), new LogicExpr(), "arg2", "arg1");

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals is symmetric (a.Equals(b) == b.Equals(a)).
        /// </summary>
        [Fact]
        public void Equals_Symmetry_BothDirectionsMatch()
        {
            // Arrange
            var innerExpr = new LogicExpr();
            var expr1 = new ForeignExpr(typeof(int), "alias", innerExpr, "arg1");
            var expr2 = new ForeignExpr(typeof(int), "alias", innerExpr, "arg1");

            // Act
            var result1 = expr1.Equals(expr2);
            var result2 = expr2.Equals(expr1);

            // Assert
            Assert.True(result1);
            Assert.True(result2);
        }

        /// <summary>
        /// Tests that Equals handles complex scenario with all properties set and equal.
        /// </summary>
        [Fact]
        public void Equals_ComplexEqualScenario_ReturnsTrue()
        {
            // Arrange
            var innerExpr = new LogicExpr();
            var expr1 = new ForeignExpr(typeof(decimal), "myAlias", innerExpr, "2024", "Q1", "region")
            {
                AutoRelated = true
            };
            var expr2 = new ForeignExpr(typeof(decimal), "myAlias", innerExpr, "2024", "Q1", "region")
            {
                AutoRelated = true
            };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false for complex scenario with one property different.
        /// </summary>
        [Fact]
        public void Equals_ComplexScenarioOneDifference_ReturnsFalse()
        {
            // Arrange
            var innerExpr = new LogicExpr();
            var expr1 = new ForeignExpr(typeof(decimal), "myAlias", innerExpr, "2024", "Q1")
            {
                AutoRelated = true
            };
            var expr2 = new ForeignExpr(typeof(decimal), "myAlias", innerExpr, "2024", "Q2")
            {
                AutoRelated = true
            };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that the constructor correctly assigns all parameters to their respective properties.
        /// Validates the happy path with valid non-null values for all parameters.
        /// </summary>
        [Fact]
        public void Constructor_WithAllValidParameters_AssignsPropertiesCorrectly()
        {
            // Arrange
            var foreign = typeof(string);
            var alias = "TestAlias";
            var expr = Expr.Prop("Name") == "Test";
            var tableArgs = new[] { "2024", "Table1" };

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Equal(alias, result.Alias);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Equal(tableArgs, result.TableArgs);
            Assert.False(result.AutoRelated);
        }

        /// <summary>
        /// Tests constructor with null foreign parameter.
        /// Verifies that null Type is accepted and assigned correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithNullForeign_AssignsNullToForeignProperty()
        {
            // Arrange
            Type? foreign = null;
            var alias = "TestAlias";
            var expr = Expr.Prop("Id") > 0;
            var tableArgs = new[] { "arg1" };

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.Null(result.Foreign);
            Assert.Equal(alias, result.Alias);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Equal(tableArgs, result.TableArgs);
        }

        /// <summary>
        /// Tests constructor with null alias parameter.
        /// Verifies that null alias is accepted and assigned correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithNullAlias_AssignsNullToAliasProperty()
        {
            // Arrange
            var foreign = typeof(int);
            string? alias = null;
            var expr = Expr.Prop("Status") == "Active";
            var tableArgs = new[] { "2024" };

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Null(result.Alias);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Equal(tableArgs, result.TableArgs);
        }

        /// <summary>
        /// Tests constructor with null expr parameter.
        /// Verifies that null LogicExpr is accepted and assigned correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithNullExpr_AssignsNullToInnerExprProperty()
        {
            // Arrange
            var foreign = typeof(DateTime);
            var alias = "dt";
            LogicExpr? expr = null;
            var tableArgs = new[] { "arg" };

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Equal(alias, result.Alias);
            Assert.Null(result.InnerExpr);
            Assert.Equal(tableArgs, result.TableArgs);
        }

        /// <summary>
        /// Tests constructor with null tableArgs parameter.
        /// Verifies that null array is accepted and assigned correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithNullTableArgs_AssignsNullToTableArgsProperty()
        {
            // Arrange
            var foreign = typeof(object);
            var alias = "obj";
            var expr = Expr.Prop("Value") != null;
            string[]? tableArgs = null;

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Equal(alias, result.Alias);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Null(result.TableArgs);
        }

        /// <summary>
        /// Tests constructor with empty tableArgs array.
        /// Verifies that empty array is correctly assigned.
        /// </summary>
        [Fact]
        public void Constructor_WithEmptyTableArgs_AssignsEmptyArrayToTableArgsProperty()
        {
            // Arrange
            var foreign = typeof(string);
            var alias = "str";
            var expr = Expr.Prop("Length") > 5;
            var tableArgs = new string[0];

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Equal(alias, result.Alias);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Empty(result.TableArgs);
        }

        /// <summary>
        /// Tests constructor with single element in tableArgs.
        /// Verifies that single-element array is correctly assigned.
        /// </summary>
        [Fact]
        public void Constructor_WithSingleTableArg_AssignsSingleElementArrayCorrectly()
        {
            // Arrange
            var foreign = typeof(bool);
            var alias = "b";
            var expr = Expr.Prop("IsActive") == true;
            var tableArgs = new[] { "SingleArg" };

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Equal(alias, result.Alias);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Single(result.TableArgs);
            Assert.Equal("SingleArg", result.TableArgs[0]);
        }

        /// <summary>
        /// Tests constructor with multiple elements in tableArgs.
        /// Verifies that multi-element array is correctly assigned with all elements preserved.
        /// </summary>
        [Fact]
        public void Constructor_WithMultipleTableArgs_AssignsMultipleElementsCorrectly()
        {
            // Arrange
            var foreign = typeof(double);
            var alias = "dbl";
            var expr = Expr.Prop("Price") >= 100.0;
            var tableArgs = new[] { "2024", "Q1", "Sales", "Region1" };

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Equal(alias, result.Alias);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Equal(4, result.TableArgs.Length);
            Assert.Equal(new[] { "2024", "Q1", "Sales", "Region1" }, result.TableArgs);
        }

        /// <summary>
        /// Tests constructor with all null parameters.
        /// Verifies that all null values are accepted and assigned correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithAllNullParameters_AssignsAllNullValues()
        {
            // Arrange
            Type? foreign = null;
            string? alias = null;
            LogicExpr? expr = null;
            string[]? tableArgs = null;

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.Null(result.Foreign);
            Assert.Null(result.Alias);
            Assert.Null(result.InnerExpr);
            Assert.Null(result.TableArgs);
        }

        /// <summary>
        /// Tests constructor with empty string alias.
        /// Verifies that empty string alias is accepted or throws appropriate exception.
        /// </summary>
        [Fact]
        public void Constructor_WithEmptyStringAlias_AssignsEmptyStringToAlias()
        {
            // Arrange
            var foreign = typeof(int);
            var alias = "";
            var expr = Expr.Prop("Id") == 1;
            var tableArgs = new[] { "arg" };

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Equal("", result.Alias);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Equal(tableArgs, result.TableArgs);
        }

        /// <summary>
        /// Tests constructor with various valid generic and complex types.
        /// Verifies that different Type instances are correctly assigned.
        /// </summary>
        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(DateTime))]
        [InlineData(typeof(object))]
        public void Constructor_WithVariousValidTypes_AssignsTypesCorrectly(Type foreign)
        {
            // Arrange
            var alias = "alias";
            var expr = Expr.Prop("Field") != null;
            var tableArgs = new[] { "arg1" };

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Equal(alias, result.Alias);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Equal(tableArgs, result.TableArgs);
        }

        /// <summary>
        /// Tests constructor with tableArgs containing numeric strings.
        /// Verifies that numeric strings in tableArgs are correctly assigned.
        /// </summary>
        [Fact]
        public void Constructor_WithNumericTableArgs_AssignsNumericStringsCorrectly()
        {
            // Arrange
            var foreign = typeof(decimal);
            var alias = "dec";
            var expr = Expr.Prop("Amount") > 0;
            var tableArgs = new[] { "2024", "2025", "1", "999" };

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.Equal(tableArgs, result.TableArgs);
            Assert.Equal(4, result.TableArgs.Length);
        }

        /// <summary>
        /// Tests constructor with alphanumeric alias.
        /// Verifies that alphanumeric alias strings are correctly assigned.
        /// </summary>
        [Theory]
        [InlineData("alias123")]
        [InlineData("t1")]
        [InlineData("TableA")]
        [InlineData("_underscore")]
        public void Constructor_WithAlphanumericAlias_AssignsAliasCorrectly(string alias)
        {
            // Arrange
            var foreign = typeof(long);
            var expr = Expr.Prop("Key") == "value";
            var tableArgs = new[] { "arg" };

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.Equal(alias, result.Alias);
            Assert.Equal(foreign, result.Foreign);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Equal(tableArgs, result.TableArgs);
        }

        /// <summary>
        /// Tests that ExprType property returns Foreign.
        /// Verifies the expression type is correctly set to Foreign.
        /// </summary>
        [Fact]
        public void Constructor_SetsExprTypeToForeign()
        {
            // Arrange
            var foreign = typeof(string);
            var alias = "s";
            var expr = Expr.Prop("Name") == "test";
            var tableArgs = new[] { "arg" };

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.Equal(ExprType.Foreign, result.ExprType);
        }

        /// <summary>
        /// Tests that AutoRelated property defaults to false when not explicitly set.
        /// Verifies default value of AutoRelated is false after construction.
        /// </summary>
        [Fact]
        public void Constructor_DefaultAutoRelatedToFalse()
        {
            // Arrange
            var foreign = typeof(int);
            var alias = "i";
            var expr = Expr.Prop("Value") > 0;
            var tableArgs = new[] { "2024" };

            // Act
            var result = new ForeignExpr(foreign, alias, expr, tableArgs);

            // Assert
            Assert.False(result.AutoRelated);
        }

        /// <summary>
        /// Tests the constructor ForeignExpr(Type, string, LogicExpr) with valid inputs.
        /// Verifies that all properties are correctly assigned.
        /// </summary>
        [Fact]
        public void Constructor_ValidInputs_SetsPropertiesCorrectly()
        {
            // Arrange
            var foreign = typeof(string);
            var alias = "TestAlias";
            var expr = Expr.Prop("Name") == "Test";

            // Act
            var result = new ForeignExpr(foreign, alias, expr);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Equal(alias, result.Alias);
            Assert.Equal(expr, result.InnerExpr);
            Assert.False(result.AutoRelated);
            Assert.Empty(result.TableArgs);
        }

        /// <summary>
        /// Tests the constructor with null foreign parameter.
        /// Verifies that null Type is accepted and stored correctly.
        /// </summary>
        [Fact]
        public void Constructor_NullForeign_AcceptsNullType()
        {
            // Arrange
            Type? foreign = null;
            var alias = "TestAlias";
            var expr = Expr.Prop("Name") == "Test";

            // Act
            var result = new ForeignExpr(foreign, alias, expr);

            // Assert
            Assert.Null(result.Foreign);
            Assert.Equal(alias, result.Alias);
            Assert.Equal(expr, result.InnerExpr);
        }

        /// <summary>
        /// Tests the constructor with null alias parameter.
        /// Verifies that null alias is accepted (as per validation rules).
        /// </summary>
        [Fact]
        public void Constructor_NullAlias_AcceptsNullAlias()
        {
            // Arrange
            var foreign = typeof(int);
            string? alias = null;
            var expr = Expr.Prop("Name") == "Test";

            // Act
            var result = new ForeignExpr(foreign, alias, expr);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Null(result.Alias);
            Assert.Equal(expr, result.InnerExpr);
        }

        /// <summary>
        /// Tests the constructor with null expr parameter.
        /// Verifies that null LogicExpr is accepted and stored correctly.
        /// </summary>
        [Fact]
        public void Constructor_NullExpr_AcceptsNullExpr()
        {
            // Arrange
            var foreign = typeof(double);
            var alias = "TestAlias";
            LogicExpr? expr = null;

            // Act
            var result = new ForeignExpr(foreign, alias, expr);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Equal(alias, result.Alias);
            Assert.Null(result.InnerExpr);
        }

        /// <summary>
        /// Tests the constructor with all null parameters.
        /// Verifies that all nulls are accepted.
        /// </summary>
        [Fact]
        public void Constructor_AllNullParameters_AcceptsAllNulls()
        {
            // Arrange
            Type? foreign = null;
            string? alias = null;
            LogicExpr? expr = null;

            // Act
            var result = new ForeignExpr(foreign, alias, expr);

            // Assert
            Assert.Null(result.Foreign);
            Assert.Null(result.Alias);
            Assert.Null(result.InnerExpr);
        }

        /// <summary>
        /// Tests the constructor with empty string alias.
        /// Verifies that empty string is accepted (as per validation rules).
        /// </summary>
        [Fact]
        public void Constructor_EmptyStringAlias_AcceptsEmptyString()
        {
            // Arrange
            var foreign = typeof(string);
            var alias = "";
            var expr = Expr.Prop("Id") > 10;

            // Act
            var result = new ForeignExpr(foreign, alias, expr);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Equal("", result.Alias);
            Assert.Equal(expr, result.InnerExpr);
        }

        /// <summary>
        /// Tests the constructor with valid alphanumeric alias.
        /// Verifies that aliases with letters, numbers, and underscores are accepted.
        /// </summary>
        [Theory]
        [InlineData("alias123")]
        [InlineData("_alias")]
        [InlineData("Alias_123")]
        [InlineData("A")]
        [InlineData("_")]
        [InlineData("alias_with_underscores")]
        public void Constructor_ValidAlphanumericAlias_AcceptsAlias(string alias)
        {
            // Arrange
            var foreign = typeof(object);
            var expr = Expr.Prop("Status") == "Active";

            // Act
            var result = new ForeignExpr(foreign, alias, expr);

            // Assert
            Assert.Equal(alias, result.Alias);
        }

        /// <summary>
        /// Tests the constructor with invalid alias containing special characters.
        /// Verifies that ArgumentException is thrown for invalid SQL names.
        /// </summary>
        [Theory]
        [InlineData("alias-name")]
        [InlineData("alias name")]
        [InlineData("alias@name")]
        [InlineData("alias.name")]
        [InlineData("alias$name")]
        [InlineData("alias#name")]
        [InlineData("alias!")]
        [InlineData("alias%")]
        [InlineData("alias&name")]
        public void Constructor_InvalidAliasWithSpecialCharacters_ThrowsArgumentException(string invalidAlias)
        {
            // Arrange
            var foreign = typeof(int);
            var expr = Expr.Prop("Value") > 0;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new ForeignExpr(foreign, invalidAlias, expr));
            Assert.Contains("invalid characters", exception.Message);
            Assert.Contains("only letters, numbers, and underscores are allowed", exception.Message);
            Assert.Equal("Alias", exception.ParamName);
        }

        /// <summary>
        /// Tests the constructor with whitespace-only alias.
        /// Verifies that whitespace is treated as invalid.
        /// </summary>
        [Theory]
        [InlineData(" ")]
        [InlineData("  ")]
        [InlineData("\t")]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void Constructor_WhitespaceOnlyAlias_ThrowsArgumentException(string whitespaceAlias)
        {
            // Arrange
            var foreign = typeof(string);
            var expr = Expr.Prop("Name") == "Test";

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new ForeignExpr(foreign, whitespaceAlias, expr));
            Assert.Contains("invalid characters", exception.Message);
            Assert.Equal("Alias", exception.ParamName);
        }

        /// <summary>
        /// Tests the constructor with very long valid alias.
        /// Verifies that long strings are accepted if they contain only valid characters.
        /// </summary>
        [Fact]
        public void Constructor_VeryLongValidAlias_AcceptsLongAlias()
        {
            // Arrange
            var foreign = typeof(decimal);
            var longAlias = new string('a', 1000);
            var expr = Expr.Prop("Amount") > 100;

            // Act
            var result = new ForeignExpr(foreign, longAlias, expr);

            // Assert
            Assert.Equal(longAlias, result.Alias);
        }

        /// <summary>
        /// Tests the constructor with different Type instances.
        /// Verifies that various types are correctly stored.
        /// </summary>
        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(object))]
        [InlineData(typeof(DateTime))]
        [InlineData(typeof(decimal))]
        public void Constructor_DifferentTypes_StoresTypeCorrectly(Type type)
        {
            // Arrange
            var alias = "TestAlias";
            var expr = Expr.Prop("Id") == 1;

            // Act
            var result = new ForeignExpr(type, alias, expr);

            // Assert
            Assert.Equal(type, result.Foreign);
        }

        /// <summary>
        /// Tests the constructor with different LogicExpr instances.
        /// Verifies that various expression types are correctly stored.
        /// </summary>
        [Fact]
        public void Constructor_DifferentLogicExpressions_StoresExpressionCorrectly()
        {
            // Arrange
            var foreign = typeof(string);
            var alias = "TestAlias";
            var expr1 = Expr.Prop("Name") == "Test";
            var expr2 = Expr.Prop("Id") > 100;
            var expr3 = (Expr.Prop("Active") == true) & (Expr.Prop("Count") < 10);

            // Act
            var result1 = new ForeignExpr(foreign, alias, expr1);
            var result2 = new ForeignExpr(foreign, alias, expr2);
            var result3 = new ForeignExpr(foreign, alias, expr3);

            // Assert
            Assert.Equal(expr1, result1.InnerExpr);
            Assert.Equal(expr2, result2.InnerExpr);
            Assert.Equal(expr3, result3.InnerExpr);
        }

        /// <summary>
        /// Tests equality of two instances created with identical parameters.
        /// Verifies that Equals and GetHashCode work correctly for instances created via this constructor.
        /// </summary>
        [Fact]
        public void Constructor_IdenticalParameters_CreatesEqualInstances()
        {
            // Arrange
            var foreign = typeof(int);
            var alias = "TestAlias";
            var expr = Expr.Prop("Name") == "Test";

            // Act
            var result1 = new ForeignExpr(foreign, alias, expr);
            var result2 = new ForeignExpr(foreign, alias, expr);

            // Assert
            Assert.True(result1.Equals(result2));
            Assert.Equal(result1.GetHashCode(), result2.GetHashCode());
        }

        /// <summary>
        /// Tests inequality of instances created with different parameters.
        /// Verifies that instances with different values are not equal.
        /// </summary>
        [Fact]
        public void Constructor_DifferentParameters_CreatesUnequalInstances()
        {
            // Arrange
            var foreign1 = typeof(int);
            var foreign2 = typeof(string);
            var alias1 = "Alias1";
            var alias2 = "Alias2";
            var expr1 = Expr.Prop("Name") == "Test";
            var expr2 = Expr.Prop("Id") > 10;

            // Act
            var result1 = new ForeignExpr(foreign1, alias1, expr1);
            var result2 = new ForeignExpr(foreign2, alias1, expr1);
            var result3 = new ForeignExpr(foreign1, alias2, expr1);
            var result4 = new ForeignExpr(foreign1, alias1, expr2);

            // Assert
            Assert.False(result1.Equals(result2)); // Different foreign
            Assert.False(result1.Equals(result3)); // Different alias
            Assert.False(result1.Equals(result4)); // Different expr
        }

        /// <summary>
        /// Tests that ExprType property returns correct value.
        /// Verifies that instances created via this constructor have ExprType.Foreign.
        /// </summary>
        [Fact]
        public void Constructor_CreatedInstance_HasCorrectExprType()
        {
            // Arrange
            var foreign = typeof(object);
            var alias = "TestAlias";
            var expr = Expr.Prop("Value") == 42;

            // Act
            var result = new ForeignExpr(foreign, alias, expr);

            // Assert
            Assert.Equal(ExprType.Foreign, result.ExprType);
        }

        /// <summary>
        /// Tests that TableArgs is initialized to empty array.
        /// Verifies that the constructor does not set TableArgs and it remains at default value.
        /// </summary>
        [Fact]
        public void Constructor_DefaultTableArgs_IsEmptyArray()
        {
            // Arrange
            var foreign = typeof(string);
            var alias = "TestAlias";
            var expr = Expr.Prop("Name") == "Test";

            // Act
            var result = new ForeignExpr(foreign, alias, expr);

            // Assert
            Assert.NotNull(result.TableArgs);
            Assert.Empty(result.TableArgs);
        }

        /// <summary>
        /// Tests that AutoRelated is initialized to false.
        /// Verifies that the constructor does not set AutoRelated and it remains at default value.
        /// </summary>
        [Fact]
        public void Constructor_DefaultAutoRelated_IsFalse()
        {
            // Arrange
            var foreign = typeof(decimal);
            var alias = "TestAlias";
            var expr = Expr.Prop("Amount") > 1000;

            // Act
            var result = new ForeignExpr(foreign, alias, expr);

            // Assert
            Assert.False(result.AutoRelated);
        }

        /// <summary>
        /// Tests ToString method on instance created via this constructor.
        /// Verifies that ToString produces expected format.
        /// </summary>
        [Fact]
        public void Constructor_ToString_ProducesExpectedFormat()
        {
            // Arrange
            var foreign = typeof(int);
            var alias = "TestAlias";
            var expr = Expr.Prop("Name") == "Test";

            // Act
            var result = new ForeignExpr(foreign, alias, expr);
            var toString = result.ToString();

            // Assert
            Assert.Contains("Int32", toString);
            Assert.Contains("Name", toString);
        }

        /// <summary>
        /// Tests Clone method on instance created via this constructor.
        /// Verifies that cloned instance is equal but not the same reference.
        /// </summary>
        [Fact]
        public void Constructor_Clone_ProducesEqualButDifferentInstance()
        {
            // Arrange
            var foreign = typeof(string);
            var alias = "TestAlias";
            var expr = Expr.Prop("Name") == "Test";
            var original = new ForeignExpr(foreign, alias, expr);

            // Act
            var cloned = (ForeignExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.True(original.Equals(cloned));
            Assert.Equal(original.Foreign, cloned.Foreign);
            Assert.Equal(original.Alias, cloned.Alias);
            Assert.Equal(original.InnerExpr, cloned.InnerExpr);
        }

        /// <summary>
        /// Tests that the constructor with foreign, expr, and tableArgs parameters correctly sets all properties.
        /// Input: Valid Type, valid LogicExpr, and valid string array.
        /// Expected: All properties are set to the provided values.
        /// </summary>
        [Fact]
        public void Constructor_WithValidParameters_SetsPropertiesCorrectly()
        {
            // Arrange
            var foreignType = typeof(string);
            var expr = Expr.Prop("Name") == "Test";
            var tableArgs = new[] { "2024", "table1" };

            // Act
            var result = new ForeignExpr(foreignType, expr, tableArgs);

            // Assert
            Assert.Equal(foreignType, result.Foreign);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Equal(tableArgs, result.TableArgs);
        }

        /// <summary>
        /// Tests that the constructor accepts null for the foreign parameter.
        /// Input: Null Type, valid LogicExpr, valid string array.
        /// Expected: Foreign property is null, other properties are set correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithNullForeign_AllowsNull()
        {
            // Arrange
            var expr = Expr.Prop("Name") == "Test";
            var tableArgs = new[] { "2024" };

            // Act
            var result = new ForeignExpr(null, expr, tableArgs);

            // Assert
            Assert.Null(result.Foreign);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Equal(tableArgs, result.TableArgs);
        }

        /// <summary>
        /// Tests that the constructor accepts null for the expr parameter.
        /// Input: Valid Type, null LogicExpr, valid string array.
        /// Expected: InnerExpr property is null, other properties are set correctly.
        /// </summary>
        [Fact]
        public void Constructor_WithNullExpr_AllowsNull()
        {
            // Arrange
            var foreignType = typeof(string);
            var tableArgs = new[] { "2024" };

            // Act
            var result = new ForeignExpr(foreignType, null, tableArgs);

            // Assert
            Assert.Equal(foreignType, result.Foreign);
            Assert.Null(result.InnerExpr);
            Assert.Equal(tableArgs, result.TableArgs);
        }

        /// <summary>
        /// Tests that the constructor accepts null for the tableArgs parameter.
        /// Input: Valid Type, valid LogicExpr, null tableArgs.
        /// Expected: TableArgs property is null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullTableArgs_AllowsNull()
        {
            // Arrange
            var foreignType = typeof(string);
            var expr = Expr.Prop("Name") == "Test";

            // Act
            var result = new ForeignExpr(foreignType, expr, null);

            // Assert
            Assert.Equal(foreignType, result.Foreign);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Null(result.TableArgs);
        }

        /// <summary>
        /// Tests that the constructor accepts all null parameters.
        /// Input: All parameters are null.
        /// Expected: All properties are null.
        /// </summary>
        [Fact]
        public void Constructor_WithAllNullParameters_AllowsAllNull()
        {
            // Arrange & Act
            var result = new ForeignExpr(null, null, null);

            // Assert
            Assert.Null(result.Foreign);
            Assert.Null(result.InnerExpr);
            Assert.Null(result.TableArgs);
        }

        /// <summary>
        /// Tests that the constructor accepts an empty tableArgs array.
        /// Input: Valid Type, valid LogicExpr, empty string array.
        /// Expected: TableArgs property is set to an empty array.
        /// </summary>
        [Fact]
        public void Constructor_WithEmptyTableArgs_SetsEmptyArray()
        {
            // Arrange
            var foreignType = typeof(string);
            var expr = Expr.Prop("Name") == "Test";
            var tableArgs = new string[0];

            // Act
            var result = new ForeignExpr(foreignType, expr, tableArgs);

            // Assert
            Assert.Equal(foreignType, result.Foreign);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Empty(result.TableArgs);
        }

        /// <summary>
        /// Tests that the constructor accepts a single element in tableArgs.
        /// Input: Valid Type, valid LogicExpr, single-element string array.
        /// Expected: TableArgs property contains the single element.
        /// </summary>
        [Fact]
        public void Constructor_WithSingleTableArg_SetsSingleElement()
        {
            // Arrange
            var foreignType = typeof(string);
            var expr = Expr.Prop("Name") == "Test";
            var tableArgs = new[] { "2024" };

            // Act
            var result = new ForeignExpr(foreignType, expr, tableArgs);

            // Assert
            Assert.Equal(foreignType, result.Foreign);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Single(result.TableArgs);
            Assert.Equal("2024", result.TableArgs[0]);
        }

        /// <summary>
        /// Tests that the constructor accepts multiple elements in tableArgs.
        /// Input: Valid Type, valid LogicExpr, multi-element string array.
        /// Expected: TableArgs property contains all elements in the correct order.
        /// </summary>
        [Fact]
        public void Constructor_WithMultipleTableArgs_SetsAllElements()
        {
            // Arrange
            var foreignType = typeof(string);
            var expr = Expr.Prop("Name") == "Test";
            var tableArgs = new[] { "2024", "table1", "partition_A" };

            // Act
            var result = new ForeignExpr(foreignType, expr, tableArgs);

            // Assert
            Assert.Equal(foreignType, result.Foreign);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Equal(3, result.TableArgs.Length);
            Assert.Equal("2024", result.TableArgs[0]);
            Assert.Equal("table1", result.TableArgs[1]);
            Assert.Equal("partition_A", result.TableArgs[2]);
        }

        /// <summary>
        /// Tests that the constructor allows null elements within the tableArgs array.
        /// Input: Valid Type, valid LogicExpr, string array containing null elements.
        /// Expected: TableArgs property is set with null elements preserved.
        /// </summary>
        [Fact]
        public void Constructor_WithNullElementsInTableArgs_AllowsNullElements()
        {
            // Arrange
            var foreignType = typeof(string);
            var expr = Expr.Prop("Name") == "Test";
            var tableArgs = new string[] { "2024", null, "table1" };

            // Act
            var result = new ForeignExpr(foreignType, expr, tableArgs);

            // Assert
            Assert.Equal(foreignType, result.Foreign);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Equal(3, result.TableArgs.Length);
            Assert.Equal("2024", result.TableArgs[0]);
            Assert.Null(result.TableArgs[1]);
            Assert.Equal("table1", result.TableArgs[2]);
        }

        /// <summary>
        /// Tests that the constructor allows empty string elements within the tableArgs array.
        /// Input: Valid Type, valid LogicExpr, string array containing empty strings.
        /// Expected: TableArgs property is set with empty strings preserved.
        /// </summary>
        [Fact]
        public void Constructor_WithEmptyStringElementsInTableArgs_AllowsEmptyStrings()
        {
            // Arrange
            var foreignType = typeof(string);
            var expr = Expr.Prop("Name") == "Test";
            var tableArgs = new[] { "", "2024", "" };

            // Act
            var result = new ForeignExpr(foreignType, expr, tableArgs);

            // Assert
            Assert.Equal(foreignType, result.Foreign);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Equal(3, result.TableArgs.Length);
            Assert.Equal("", result.TableArgs[0]);
            Assert.Equal("2024", result.TableArgs[1]);
            Assert.Equal("", result.TableArgs[2]);
        }

        /// <summary>
        /// Tests that the constructor throws ArgumentException when tableArgs contains invalid SQL names.
        /// Input: Valid Type, valid LogicExpr, string array with invalid SQL name (contains special characters).
        /// Expected: ArgumentException is thrown with appropriate message.
        /// </summary>
        [Theory]
        [InlineData("table-name")]
        [InlineData("table name")]
        [InlineData("table.name")]
        [InlineData("table@name")]
        [InlineData("table#name")]
        [InlineData("table$name")]
        [InlineData("table%name")]
        [InlineData("table&name")]
        [InlineData("table*name")]
        [InlineData("table+name")]
        [InlineData("table=name")]
        [InlineData("table;name")]
        [InlineData("table,name")]
        public void Constructor_WithInvalidSqlNameInTableArgs_ThrowsArgumentException(string invalidName)
        {
            // Arrange
            var foreignType = typeof(string);
            var expr = Expr.Prop("Name") == "Test";
            var tableArgs = new[] { "valid_name", invalidName };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new ForeignExpr(foreignType, expr, tableArgs));
            Assert.Equal("TableArgs", exception.ParamName);
            Assert.Contains("invalid characters", exception.Message);
        }

        /// <summary>
        /// Tests that the constructor accepts valid SQL names in tableArgs (letters, numbers, underscores).
        /// Input: Valid Type, valid LogicExpr, string array with various valid SQL names.
        /// Expected: TableArgs property is set correctly without exceptions.
        /// </summary>
        [Theory]
        [InlineData("table_name")]
        [InlineData("Table123")]
        [InlineData("_table")]
        [InlineData("TABLE")]
        [InlineData("a")]
        [InlineData("_")]
        [InlineData("table_name_123")]
        [InlineData("Table_Name_2024")]
        public void Constructor_WithValidSqlNamesInTableArgs_SetsTableArgsCorrectly(string validName)
        {
            // Arrange
            var foreignType = typeof(string);
            var expr = Expr.Prop("Name") == "Test";
            var tableArgs = new[] { validName };

            // Act
            var result = new ForeignExpr(foreignType, expr, tableArgs);

            // Assert
            Assert.Equal(foreignType, result.Foreign);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Single(result.TableArgs);
            Assert.Equal(validName, result.TableArgs[0]);
        }

        /// <summary>
        /// Tests that the constructor using params keyword works with individual string arguments.
        /// Input: Valid Type, valid LogicExpr, individual string arguments.
        /// Expected: TableArgs property contains all provided arguments.
        /// </summary>
        [Fact]
        public void Constructor_WithParamsArguments_SetsAllArguments()
        {
            // Arrange
            var foreignType = typeof(string);
            var expr = Expr.Prop("Name") == "Test";

            // Act
            var result = new ForeignExpr(foreignType, expr, "2024", "table1", "partition_A");

            // Assert
            Assert.Equal(foreignType, result.Foreign);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Equal(3, result.TableArgs.Length);
            Assert.Equal("2024", result.TableArgs[0]);
            Assert.Equal("table1", result.TableArgs[1]);
            Assert.Equal("partition_A", result.TableArgs[2]);
        }

        /// <summary>
        /// Tests that the constructor using params keyword works with no arguments (empty params).
        /// Input: Valid Type, valid LogicExpr, no additional arguments.
        /// Expected: TableArgs property is set to an empty array.
        /// </summary>
        [Fact]
        public void Constructor_WithNoParamsArguments_SetsEmptyArray()
        {
            // Arrange
            var foreignType = typeof(string);
            var expr = Expr.Prop("Name") == "Test";

            // Act
            var result = new ForeignExpr(foreignType, expr);

            // Assert
            Assert.Equal(foreignType, result.Foreign);
            Assert.Equal(expr, result.InnerExpr);
            Assert.Empty(result.TableArgs);
        }

        /// <summary>
        /// Tests that the constructor ForeignExpr(Type, LogicExpr) correctly assigns both parameters
        /// when both are valid non-null values.
        /// Expected: Foreign and InnerExpr properties should be set to the provided values.
        /// </summary>
        [Fact]
        public void Constructor_WithValidForeignAndExpr_AssignsProperties()
        {
            // Arrange
            Type foreign = typeof(string);
            LogicExpr expr = new ForeignExpr(); // Using ForeignExpr itself as a valid LogicExpr

            // Act
            ForeignExpr result = new ForeignExpr(foreign, expr);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Equal(expr, result.InnerExpr);
            Assert.False(result.AutoRelated); // Default value
        }

        /// <summary>
        /// Tests that the constructor ForeignExpr(Type, LogicExpr) accepts null for both parameters.
        /// Expected: Both Foreign and InnerExpr properties should be null.
        /// </summary>
        [Fact]
        public void Constructor_WithBothParametersNull_AssignsBothPropertiesToNull()
        {
            // Arrange
            Type foreign = null;
            LogicExpr expr = null;

            // Act
            ForeignExpr result = new ForeignExpr(foreign, expr);

            // Assert
            Assert.Null(result.Foreign);
            Assert.Null(result.InnerExpr);
        }

        /// <summary>
        /// Tests the constructor with various Type objects including class, struct, interface, abstract class, and enum types.
        /// Expected: The Foreign property should be correctly assigned for all type categories.
        /// </summary>
        [Theory]
        [InlineData(typeof(object))]        // Class type
        [InlineData(typeof(int))]           // Struct type
        [InlineData(typeof(IDisposable))]   // Interface type
        [InlineData(typeof(Array))]         // Abstract class type
        [InlineData(typeof(DayOfWeek))]     // Enum type
        public void Constructor_WithVariousTypeObjects_AssignsForeignPropertyCorrectly(Type foreign)
        {
            // Arrange
            LogicExpr expr = new ForeignExpr();

            // Act
            ForeignExpr result = new ForeignExpr(foreign, expr);

            // Assert
            Assert.Equal(foreign, result.Foreign);
            Assert.Equal(expr, result.InnerExpr);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Foreign property with a valid Type.
        /// Input: Valid Type object.
        /// Expected: Foreign property is set to the provided Type, other properties have default values.
        /// </summary>
        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(object))]
        [InlineData(typeof(ForeignExpr))]
        public void Constructor_WithValidType_SetsForeignProperty(Type foreignType)
        {
            // Arrange & Act
            var foreignExpr = new ForeignExpr(foreignType);

            // Assert
            Assert.Equal(foreignType, foreignExpr.Foreign);
            Assert.Null(foreignExpr.InnerExpr);
            Assert.Null(foreignExpr.Alias);
            Assert.NotNull(foreignExpr.TableArgs);
            Assert.Empty(foreignExpr.TableArgs);
            Assert.False(foreignExpr.AutoRelated);
            Assert.Equal(ExprType.Foreign, foreignExpr.ExprType);
        }

        /// <summary>
        /// Tests that the constructor accepts null as the foreign parameter without throwing.
        /// Input: null Type.
        /// Expected: Foreign property is set to null, no exception is thrown.
        /// </summary>
        [Fact]
        public void Constructor_WithNullType_AcceptsNullAndSetsForeignToNull()
        {
            // Arrange & Act
            var foreignExpr = new ForeignExpr(null);

            // Assert
            Assert.Null(foreignExpr.Foreign);
            Assert.Null(foreignExpr.InnerExpr);
            Assert.Null(foreignExpr.Alias);
            Assert.NotNull(foreignExpr.TableArgs);
            Assert.Empty(foreignExpr.TableArgs);
            Assert.False(foreignExpr.AutoRelated);
            Assert.Equal(ExprType.Foreign, foreignExpr.ExprType);
        }

        /// <summary>
        /// Tests that different instances created with the same Type are not reference-equal but have equal Foreign properties.
        /// Input: Multiple constructor calls with the same Type.
        /// Expected: Different instances with equal Foreign properties.
        /// </summary>
        [Fact]
        public void Constructor_WithSameType_CreatesDistinctInstancesWithEqualForeignProperty()
        {
            // Arrange
            var type = typeof(string);

            // Act
            var foreignExpr1 = new ForeignExpr(type);
            var foreignExpr2 = new ForeignExpr(type);

            // Assert
            Assert.NotSame(foreignExpr1, foreignExpr2);
            Assert.Equal(foreignExpr1.Foreign, foreignExpr2.Foreign);
        }

        /// <summary>
        /// Tests that the constructor works with generic type parameters.
        /// Input: Generic Type objects.
        /// Expected: Foreign property is correctly set to the generic Type.
        /// </summary>
        [Fact]
        public void Constructor_WithGenericType_SetsForeignPropertyCorrectly()
        {
            // Arrange
            var genericType = typeof(System.Collections.Generic.List<int>);

            // Act
            var foreignExpr = new ForeignExpr(genericType);

            // Assert
            Assert.Equal(genericType, foreignExpr.Foreign);
            Assert.True(foreignExpr.Foreign.IsGenericType);
        }

        /// <summary>
        /// Tests that the constructor works with nested types.
        /// Input: Nested Type object.
        /// Expected: Foreign property is correctly set to the nested Type.
        /// </summary>
        [Fact]
        public void Constructor_WithNestedType_SetsForeignPropertyCorrectly()
        {
            // Arrange
            var nestedType = typeof(System.Environment.SpecialFolder);

            // Act
            var foreignExpr = new ForeignExpr(nestedType);

            // Assert
            Assert.Equal(nestedType, foreignExpr.Foreign);
            Assert.True(foreignExpr.Foreign.IsNested);
        }

        /// <summary>
        /// Tests that the constructor works with array types.
        /// Input: Array Type object.
        /// Expected: Foreign property is correctly set to the array Type.
        /// </summary>
        [Fact]
        public void Constructor_WithArrayType_SetsForeignPropertyCorrectly()
        {
            // Arrange
            var arrayType = typeof(int[]);

            // Act
            var foreignExpr = new ForeignExpr(arrayType);

            // Assert
            Assert.Equal(arrayType, foreignExpr.Foreign);
            Assert.True(foreignExpr.Foreign.IsArray);
        }

        /// <summary>
        /// Tests that the constructor works with interface types.
        /// Input: Interface Type object.
        /// Expected: Foreign property is correctly set to the interface Type.
        /// </summary>
        [Fact]
        public void Constructor_WithInterfaceType_SetsForeignPropertyCorrectly()
        {
            // Arrange
            var interfaceType = typeof(IDisposable);

            // Act
            var foreignExpr = new ForeignExpr(interfaceType);

            // Assert
            Assert.Equal(interfaceType, foreignExpr.Foreign);
            Assert.True(foreignExpr.Foreign.IsInterface);
        }

        /// <summary>
        /// Tests that the constructor works with abstract class types.
        /// Input: Abstract class Type object.
        /// Expected: Foreign property is correctly set to the abstract class Type.
        /// </summary>
        [Fact]
        public void Constructor_WithAbstractType_SetsForeignPropertyCorrectly()
        {
            // Arrange
            var abstractType = typeof(System.IO.Stream);

            // Act
            var foreignExpr = new ForeignExpr(abstractType);

            // Assert
            Assert.Equal(abstractType, foreignExpr.Foreign);
            Assert.True(foreignExpr.Foreign.IsAbstract);
        }

        /// <summary>
        /// Tests that the ExprType property returns the correct enum value (ExprType.Foreign)
        /// for a default constructed instance.
        /// </summary>
        [Fact]
        public void ExprType_DefaultConstructor_ReturnsForeign()
        {
            // Arrange
            var expr = new ForeignExpr();

            // Act
            var result = expr.ExprType;

            // Assert
            Assert.Equal(ExprType.Foreign, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns the correct enum value (ExprType.Foreign)
        /// for an instance constructed with a foreign type parameter.
        /// </summary>
        [Fact]
        public void ExprType_ConstructedWithForeignType_ReturnsForeign()
        {
            // Arrange
            var expr = new ForeignExpr(typeof(string));

            // Act
            var result = expr.ExprType;

            // Assert
            Assert.Equal(ExprType.Foreign, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns the correct enum value (ExprType.Foreign)
        /// for an instance constructed with a foreign type and inner expression.
        /// </summary>
        [Fact]
        public void ExprType_ConstructedWithForeignTypeAndExpression_ReturnsForeign()
        {
            // Arrange
            var innerExpr = new AndExpr();
            var expr = new ForeignExpr(typeof(int), innerExpr);

            // Act
            var result = expr.ExprType;

            // Assert
            Assert.Equal(ExprType.Foreign, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns the correct enum value (ExprType.Foreign)
        /// for an instance constructed with a foreign type, expression, and table args.
        /// </summary>
        [Fact]
        public void ExprType_ConstructedWithAllParameters_ReturnsForeign()
        {
            // Arrange
            var innerExpr = new AndExpr();
            var expr = new ForeignExpr(typeof(double), innerExpr, "arg1", "arg2");

            // Act
            var result = expr.ExprType;

            // Assert
            Assert.Equal(ExprType.Foreign, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns the correct enum value (ExprType.Foreign)
        /// for an instance constructed with a foreign type, alias, and expression.
        /// </summary>
        [Fact]
        public void ExprType_ConstructedWithAlias_ReturnsForeign()
        {
            // Arrange
            var innerExpr = new AndExpr();
            var expr = new ForeignExpr(typeof(object), "alias", innerExpr);

            // Act
            var result = expr.ExprType;

            // Assert
            Assert.Equal(ExprType.Foreign, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns the correct enum value (ExprType.Foreign)
        /// for an instance constructed with a foreign type, alias, expression, and table args.
        /// </summary>
        [Fact]
        public void ExprType_ConstructedWithAliasAndTableArgs_ReturnsForeign()
        {
            // Arrange
            var innerExpr = new AndExpr();
            var expr = new ForeignExpr(typeof(DateTime), "testAlias", innerExpr, "table1", "table2");

            // Act
            var result = expr.ExprType;

            // Assert
            Assert.Equal(ExprType.Foreign, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns a consistent value across multiple accesses.
        /// </summary>
        [Fact]
        public void ExprType_MultipleAccess_ReturnsConsistentValue()
        {
            // Arrange
            var expr = new ForeignExpr();

            // Act
            var result1 = expr.ExprType;
            var result2 = expr.ExprType;
            var result3 = expr.ExprType;

            // Assert
            Assert.Equal(ExprType.Foreign, result1);
            Assert.Equal(ExprType.Foreign, result2);
            Assert.Equal(ExprType.Foreign, result3);
            Assert.Equal(result1, result2);
            Assert.Equal(result2, result3);
        }

        /// <summary>
        /// Tests that the ExprType property returns the Foreign value even when properties are modified.
        /// </summary>
        [Fact]
        public void ExprType_AfterPropertyModification_StillReturnsForeign()
        {
            // Arrange
            var expr = new ForeignExpr
            {
                Foreign = typeof(string),
                InnerExpr = new AndExpr(),
                Alias = "modified",
                AutoRelated = true,
                TableArgs = new[] { "arg1", "arg2" }
            };

            // Act
            var result = expr.ExprType;

            // Assert
            Assert.Equal(ExprType.Foreign, result);
        }
    }
}

namespace LiteOrm.Tests.UnitTests
{
    /// <summary>
    /// Unit tests for the ToString method of ForeignExpr class.
    /// </summary>
    public partial class ForeignExprToStringTests
    {
        /// <summary>
        /// Tests that ToString returns the correct format when Foreign type is set and InnerExpr is null.
        /// Input: Foreign = typeof(TestDepartment), Alias = null, InnerExpr = null
        /// Expected: "{TestDepartment:}"
        /// </summary>
        [Fact]
        public void ToString_WithForeignTypeAndNullInnerExpr_ReturnsFormattedStringWithTypeName()
        {
            // Arrange
            var foreignExpr = new ForeignExpr(typeof(TestDepartment), null);

            // Act
            var result = foreignExpr.ToString();

            // Assert
            Assert.Equal("{TestDepartment:}", result);
        }

        /// <summary>
        /// Tests that ToString returns the correct format when Alias is set and Foreign is null.
        /// Input: Foreign = null, Alias = "T1", InnerExpr = null
        /// Expected: "{T1:}"
        /// </summary>
        [Fact]
        public void ToString_WithAliasAndNullForeign_ReturnsFormattedStringWithAlias()
        {
            // Arrange
            var foreignExpr = new ForeignExpr { Alias = "T1", InnerExpr = null };

            // Act
            var result = foreignExpr.ToString();

            // Assert
            Assert.Equal("{T1:}", result);
        }

        /// <summary>
        /// Tests that ToString returns the correct format when both Foreign and Alias are null.
        /// Input: Foreign = null, Alias = null, InnerExpr = null
        /// Expected: "{:}"
        /// </summary>
        [Fact]
        public void ToString_WithBothForeignAndAliasNull_ReturnsFormattedStringWithEmptyName()
        {
            // Arrange
            var foreignExpr = new ForeignExpr { InnerExpr = null };

            // Act
            var result = foreignExpr.ToString();

            // Assert
            Assert.Equal("{:}", result);
        }

        /// <summary>
        /// Tests that ToString returns the correct format when Foreign type is set with InnerExpr.
        /// Input: Foreign = typeof(TestDepartment), InnerExpr = Expr.Prop("Name") == "IT"
        /// Expected: String starting with "{TestDepartment:" and ending with "}" containing InnerExpr.ToString()
        /// </summary>
        [Fact]
        public void ToString_WithForeignTypeAndInnerExpr_ReturnsFormattedStringWithBothParts()
        {
            // Arrange
            var innerExpr = Expr.Prop("Name") == "IT";
            var foreignExpr = new ForeignExpr(typeof(TestDepartment), innerExpr);

            // Act
            var result = foreignExpr.ToString();

            // Assert
            Assert.StartsWith("{TestDepartment:", result);
            Assert.EndsWith("}", result);
            Assert.Contains(innerExpr.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString returns the correct format when Alias is set with InnerExpr.
        /// Input: Foreign = null, Alias = "T1", InnerExpr = Expr.Prop("Status") == 1
        /// Expected: String starting with "{T1:" and ending with "}" containing InnerExpr.ToString()
        /// </summary>
        [Fact]
        public void ToString_WithAliasAndInnerExpr_ReturnsFormattedStringWithBothParts()
        {
            // Arrange
            var innerExpr = Expr.Prop("Status") == 1;
            var foreignExpr = new ForeignExpr { Alias = "T1", InnerExpr = innerExpr };

            // Act
            var result = foreignExpr.ToString();

            // Assert
            Assert.StartsWith("{T1:", result);
            Assert.EndsWith("}", result);
            Assert.Contains(innerExpr.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString prefers Foreign.Name over Alias when both are set.
        /// Input: Foreign = typeof(TestUser), Alias = "T1", InnerExpr = null
        /// Expected: "{TestUser:}" (Foreign.Name takes precedence)
        /// </summary>
        [Fact]
        public void ToString_WithBothForeignAndAlias_PrefersForeignName()
        {
            // Arrange
            var foreignExpr = new ForeignExpr(typeof(TestUser), null) { Alias = "T1" };

            // Act
            var result = foreignExpr.ToString();

            // Assert
            Assert.Equal("{TestUser:}", result);
        }

        /// <summary>
        /// Tests that ToString handles empty string Alias correctly.
        /// Input: Foreign = null, Alias = "", InnerExpr = null
        /// Expected: "{:}"
        /// </summary>
        [Fact]
        public void ToString_WithEmptyStringAlias_ReturnsFormattedStringWithEmptyName()
        {
            // Arrange
            var foreignExpr = new ForeignExpr { Alias = "", InnerExpr = null };

            // Act
            var result = foreignExpr.ToString();

            // Assert
            Assert.Equal("{:}", result);
        }

        /// <summary>
        /// Tests that ToString handles various built-in Type values correctly.
        /// Input: Foreign = typeof(string), Alias = null, InnerExpr = null
        /// Expected: "{String:}"
        /// </summary>
        [Fact]
        public void ToString_WithBuiltInType_ReturnsFormattedStringWithBuiltInTypeName()
        {
            // Arrange
            var foreignExpr = new ForeignExpr(typeof(string), null);

            // Act
            var result = foreignExpr.ToString();

            // Assert
            Assert.Equal("{String:}", result);
        }

        /// <summary>
        /// Tests that ToString handles generic types correctly.
        /// Input: Foreign = typeof(System.Collections.Generic.List&lt;int&gt;), Alias = null, InnerExpr = null
        /// Expected: String containing the generic type name
        /// </summary>
        [Fact]
        public void ToString_WithGenericType_ReturnsFormattedStringWithGenericTypeName()
        {
            // Arrange
            var foreignExpr = new ForeignExpr(typeof(System.Collections.Generic.List<int>), null);

            // Act
            var result = foreignExpr.ToString();

            // Assert
            Assert.StartsWith("{List`1:", result);
            Assert.EndsWith("}", result);
        }

        /// <summary>
        /// Tests that ToString handles array types correctly.
        /// Input: Foreign = typeof(int[]), Alias = null, InnerExpr = null
        /// Expected: "{Int32[]:}"
        /// </summary>
        [Fact]
        public void ToString_WithArrayType_ReturnsFormattedStringWithArrayTypeName()
        {
            // Arrange
            var foreignExpr = new ForeignExpr(typeof(int[]), null);

            // Act
            var result = foreignExpr.ToString();

            // Assert
            Assert.Equal("{Int32[]:}", result);
        }

        /// <summary>
        /// Tests that ToString handles complex nested expressions correctly.
        /// Input: Foreign = typeof(TestDepartment), InnerExpr = (Expr.Prop("Name") == "IT") &amp; (Expr.Prop("Active") == true)
        /// Expected: String containing both the type name and the complex expression
        /// </summary>
        [Fact]
        public void ToString_WithComplexInnerExpr_ReturnsFormattedStringWithComplexExpression()
        {
            // Arrange
            var innerExpr = (Expr.Prop("Name") == "IT") & (Expr.Prop("Active") == true);
            var foreignExpr = new ForeignExpr(typeof(TestDepartment), innerExpr);

            // Act
            var result = foreignExpr.ToString();

            // Assert
            Assert.StartsWith("{TestDepartment:", result);
            Assert.EndsWith("}", result);
            Assert.Contains(innerExpr.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString with all properties set returns the correct format.
        /// Input: Foreign = typeof(TestUser), Alias = "U1", InnerExpr = Expr.Prop("Id") > 10, AutoRelated = true
        /// Expected: String using Foreign.Name (ignoring Alias) and containing InnerExpr
        /// </summary>
        [Fact]
        public void ToString_WithAllPropertiesSet_PrefersForeignNameAndIncludesInnerExpr()
        {
            // Arrange
            var innerExpr = Expr.Prop("Id") > 10;
            var foreignExpr = new ForeignExpr(typeof(TestUser), "U1", innerExpr) { AutoRelated = true };

            // Act
            var result = foreignExpr.ToString();

            // Assert
            Assert.StartsWith("{TestUser:", result);
            Assert.EndsWith("}", result);
            Assert.Contains(innerExpr.ToString(), result);
        }
    }
}