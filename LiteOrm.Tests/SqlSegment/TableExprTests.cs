using System;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the TableExpr class Clone method.
    /// </summary>
    public partial class TableExprTests
    {
        /// <summary>
        /// Tests that Clone creates a new instance that is not the same reference as the original.
        /// </summary>
        [Fact]
        public void Clone_ReturnsNewInstance_NotSameReference()
        {
            // Arrange
            var original = new TableExpr(typeof(string));

            // Act
            var cloned = original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
        }

        /// <summary>
        /// Tests that Clone copies the Type property correctly.
        /// </summary>
        [Fact]
        public void Clone_CopiesTypeProperty_Correctly()
        {
            // Arrange
            var original = new TableExpr(typeof(int));

            // Act
            var cloned = (TableExpr)original.Clone();

            // Assert
            Assert.Equal(original.Type, cloned.Type);
        }

        /// <summary>
        /// Tests that Clone handles null Type property correctly.
        /// </summary>
        [Fact]
        public void Clone_WithNullType_CopiesNull()
        {
            // Arrange
            var original = new TableExpr { Type = null };

            // Act
            var cloned = (TableExpr)original.Clone();

            // Assert
            Assert.Null(cloned.Type);
        }

        /// <summary>
        /// Tests that Clone copies the Alias property correctly.
        /// </summary>
        [Fact]
        public void Clone_CopiesAliasProperty_Correctly()
        {
            // Arrange
            var original = new TableExpr(typeof(string)) { Alias = "t" };

            // Act
            var cloned = (TableExpr)original.Clone();

            // Assert
            Assert.Equal(original.Alias, cloned.Alias);
        }

        /// <summary>
        /// Tests that Clone handles null Alias property correctly.
        /// </summary>
        [Fact]
        public void Clone_WithNullAlias_CopiesNull()
        {
            // Arrange
            var original = new TableExpr(typeof(string)) { Alias = null };

            // Act
            var cloned = (TableExpr)original.Clone();

            // Assert
            Assert.Null(cloned.Alias);
        }

        /// <summary>
        /// Tests that Clone handles null TableArgs property correctly.
        /// </summary>
        [Fact]
        public void Clone_WithNullTableArgs_CopiesNull()
        {
            // Arrange
            var original = new TableExpr(typeof(string)) { TableArgs = null };

            // Act
            var cloned = (TableExpr)original.Clone();

            // Assert
            Assert.Null(cloned.TableArgs);
        }

        /// <summary>
        /// Tests that Clone handles empty TableArgs array correctly.
        /// </summary>
        [Fact]
        public void Clone_WithEmptyTableArgs_CopiesEmptyArray()
        {
            // Arrange
            var original = new TableExpr(typeof(string)) { TableArgs = Array.Empty<string>() };

            // Act
            var cloned = (TableExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned.TableArgs);
            Assert.Empty(cloned.TableArgs);
            Assert.NotSame(original.TableArgs, cloned.TableArgs);
        }

        /// <summary>
        /// Tests that Clone creates a deep copy of TableArgs with single element.
        /// </summary>
        [Fact]
        public void Clone_WithSingleElementTableArgs_CreatesDeepCopy()
        {
            // Arrange
            var original = new TableExpr(typeof(string)) { TableArgs = new[] { "arg1" } };

            // Act
            var cloned = (TableExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned.TableArgs);
            Assert.Single(cloned.TableArgs);
            Assert.Equal("arg1", cloned.TableArgs[0]);
            Assert.NotSame(original.TableArgs, cloned.TableArgs);
        }

        /// <summary>
        /// Tests that Clone creates a deep copy of TableArgs with multiple elements.
        /// </summary>
        [Fact]
        public void Clone_WithMultipleElementTableArgs_CreatesDeepCopy()
        {
            // Arrange
            var original = new TableExpr(typeof(string)) { TableArgs = new[] { "2024", "01", "table" } };

            // Act
            var cloned = (TableExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned.TableArgs);
            Assert.Equal(3, cloned.TableArgs.Length);
            Assert.Equal(original.TableArgs, cloned.TableArgs);
            Assert.NotSame(original.TableArgs, cloned.TableArgs);
        }

        /// <summary>
        /// Tests that modifying cloned TableArgs does not affect the original.
        /// </summary>
        [Fact]
        public void Clone_ModifyingClonedTableArgs_DoesNotAffectOriginal()
        {
            // Arrange
            var original = new TableExpr(typeof(string)) { TableArgs = new[] { "original" } };
            var cloned = (TableExpr)original.Clone();

            // Act
            cloned.TableArgs[0] = "modified";

            // Assert
            Assert.Equal("original", original.TableArgs[0]);
            Assert.Equal("modified", cloned.TableArgs[0]);
        }

        /// <summary>
        /// Tests that Clone copies all properties correctly when all are set.
        /// </summary>
        [Fact]
        public void Clone_WithAllPropertiesSet_CopiesAllCorrectly()
        {
            // Arrange
            var original = new TableExpr(typeof(int))
            {
                Alias = "u",
                TableArgs = new[] { "2024", "01" }
            };

            // Act
            var cloned = (TableExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.Equal(original.Type, cloned.Type);
            Assert.Equal(original.Alias, cloned.Alias);
            Assert.Equal(original.TableArgs, cloned.TableArgs);
            Assert.NotSame(original.TableArgs, cloned.TableArgs);
        }

        /// <summary>
        /// Tests that Clone creates an instance that equals the original using Equals method.
        /// </summary>
        [Fact]
        public void Clone_CreatesEqualInstance_EqualsReturnsTrue()
        {
            // Arrange
            var original = new TableExpr(typeof(string))
            {
                Alias = "t",
                TableArgs = new[] { "2024", "01" }
            };

            // Act
            var cloned = (TableExpr)original.Clone();

            // Assert
            Assert.True(original.Equals(cloned));
            Assert.True(cloned.Equals(original));
        }

        /// <summary>
        /// Tests that Clone returns correct ExprType for cloned instance.
        /// </summary>
        [Fact]
        public void Clone_ReturnsCorrectExprType()
        {
            // Arrange
            var original = new TableExpr(typeof(string));

            // Act
            var cloned = (TableExpr)original.Clone();

            // Assert
            Assert.Equal(ExprType.Table, cloned.ExprType);
        }

        /// <summary>
        /// Tests that Clone with default constructor and no properties set works correctly.
        /// </summary>
        [Fact]
        public void Clone_WithDefaultConstructor_CopiesCorrectly()
        {
            // Arrange
            var original = new TableExpr();

            // Act
            var cloned = (TableExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.Null(cloned.Type);
            Assert.Null(cloned.Alias);
            Assert.Null(cloned.TableArgs);
        }

        /// <summary>
        /// Tests that setting TableArgs to null does not throw an exception
        /// and allows retrieval of the null value.
        /// </summary>
        [Fact]
        public void TableArgs_SetNull_DoesNotThrowAndReturnsNull()
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act
            tableExpr.TableArgs = null;

            // Assert
            Assert.Null(tableExpr.TableArgs);
        }

        /// <summary>
        /// Tests that setting TableArgs to an empty array does not throw an exception
        /// and allows retrieval of the empty array.
        /// </summary>
        [Fact]
        public void TableArgs_SetEmptyArray_DoesNotThrowAndReturnsEmptyArray()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var emptyArray = new string[0];

            // Act
            tableExpr.TableArgs = emptyArray;

            // Assert
            Assert.Same(emptyArray, tableExpr.TableArgs);
        }

        /// <summary>
        /// Tests that setting TableArgs with valid SQL names does not throw an exception
        /// and correctly stores the values.
        /// </summary>
        /// <param name="args">The array of valid SQL names to test.</param>
        [Theory]
        [InlineData(new[] { "table1" })]
        [InlineData(new[] { "Table_Name" })]
        [InlineData(new[] { "table123" })]
        [InlineData(new[] { "_underscore" })]
        [InlineData(new[] { "UPPERCASE" })]
        [InlineData(new[] { "table1", "table2", "table3" })]
        [InlineData(new[] { "a", "b", "c123", "xyz_" })]
        public void TableArgs_SetValidNames_DoesNotThrowAndReturnsValues(string[] args)
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act
            tableExpr.TableArgs = args;

            // Assert
            Assert.Same(args, tableExpr.TableArgs);
        }

        /// <summary>
        /// Tests that setting TableArgs with invalid SQL names throws an ArgumentException
        /// with the correct parameter name and message.
        /// </summary>
        /// <param name="invalidName">The invalid SQL name to test.</param>
        [Theory]
        [InlineData("table-name")]
        [InlineData("table.name")]
        [InlineData("table name")]
        [InlineData("table@name")]
        [InlineData("table#name")]
        [InlineData("table$name")]
        [InlineData("table%name")]
        [InlineData("table&name")]
        [InlineData("table*name")]
        [InlineData("table(name")]
        [InlineData("table)name")]
        [InlineData("table+name")]
        [InlineData("table=name")]
        [InlineData("table[name")]
        [InlineData("table]name")]
        [InlineData("table{name")]
        [InlineData("table}name")]
        [InlineData("table|name")]
        [InlineData("table\\name")]
        [InlineData("table/name")]
        [InlineData("table:name")]
        [InlineData("table;name")]
        [InlineData("table\"name")]
        [InlineData("table'name")]
        [InlineData("table<name")]
        [InlineData("table>name")]
        [InlineData("table,name")]
        [InlineData("table?name")]
        [InlineData("table!name")]
        [InlineData("table~name")]
        [InlineData("table`name")]
        public void TableArgs_SetInvalidName_ThrowsArgumentException(string invalidName)
        {
            // Arrange
            var tableExpr = new TableExpr();
            var args = new[] { invalidName };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => tableExpr.TableArgs = args);
            Assert.Equal("TableArgs", exception.ParamName);
            Assert.Contains("contains invalid characters", exception.Message);
            Assert.Contains(invalidName, exception.Message);
        }

        /// <summary>
        /// Tests that setting TableArgs with an array containing both valid and invalid names
        /// throws an ArgumentException for the first invalid name encountered.
        /// </summary>
        [Fact]
        public void TableArgs_SetMixedValidAndInvalidNames_ThrowsArgumentExceptionForInvalidName()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var args = new[] { "validTable", "invalid-table", "anotherValid" };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => tableExpr.TableArgs = args);
            Assert.Equal("TableArgs", exception.ParamName);
            Assert.Contains("invalid-table", exception.Message);
        }

        /// <summary>
        /// Tests that setting TableArgs with an array containing null elements does not throw an exception
        /// because null values are not validated.
        /// </summary>
        [Fact]
        public void TableArgs_SetArrayWithNullElements_DoesNotThrow()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var args = new string[] { "validTable", null, "anotherValid" };

            // Act
            tableExpr.TableArgs = args;

            // Assert
            Assert.Same(args, tableExpr.TableArgs);
            Assert.Null(tableExpr.TableArgs[1]);
        }

        /// <summary>
        /// Tests that setting TableArgs with an array containing empty strings does not throw an exception
        /// because empty strings are not validated.
        /// </summary>
        [Fact]
        public void TableArgs_SetArrayWithEmptyStrings_DoesNotThrow()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var args = new[] { "validTable", "", "anotherValid" };

            // Act
            tableExpr.TableArgs = args;

            // Assert
            Assert.Same(args, tableExpr.TableArgs);
            Assert.Equal("", tableExpr.TableArgs[1]);
        }

        /// <summary>
        /// Tests that setting TableArgs with an array containing whitespace-only strings
        /// throws an ArgumentException because whitespace-only strings do not match the valid name regex.
        /// </summary>
        [Theory]
        [InlineData(" ")]
        [InlineData("  ")]
        [InlineData("\t")]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void TableArgs_SetWhitespaceOnlyString_ThrowsArgumentException(string whitespaceString)
        {
            // Arrange
            var tableExpr = new TableExpr();
            var args = new[] { whitespaceString };

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => tableExpr.TableArgs = args);
            Assert.Equal("TableArgs", exception.ParamName);
        }

        /// <summary>
        /// Tests that setting TableArgs multiple times correctly updates the stored value.
        /// </summary>
        [Fact]
        public void TableArgs_SetMultipleTimes_UpdatesValueCorrectly()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var firstArgs = new[] { "table1" };
            var secondArgs = new[] { "table2", "table3" };

            // Act
            tableExpr.TableArgs = firstArgs;
            var firstResult = tableExpr.TableArgs;
            tableExpr.TableArgs = secondArgs;
            var secondResult = tableExpr.TableArgs;

            // Assert
            Assert.Same(firstArgs, firstResult);
            Assert.Same(secondArgs, secondResult);
        }

        /// <summary>
        /// Tests that setting TableArgs with a very long valid name does not throw an exception.
        /// </summary>
        [Fact]
        public void TableArgs_SetVeryLongValidName_DoesNotThrow()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var longValidName = new string('a', 10000);
            var args = new[] { longValidName };

            // Act
            tableExpr.TableArgs = args;

            // Assert
            Assert.Same(args, tableExpr.TableArgs);
            Assert.Equal(longValidName, tableExpr.TableArgs[0]);
        }

        /// <summary>
        /// Tests that setting TableArgs with names containing numbers and underscores
        /// in various positions does not throw an exception.
        /// </summary>
        [Theory]
        [InlineData("123")]
        [InlineData("_")]
        [InlineData("___")]
        [InlineData("123abc")]
        [InlineData("abc123")]
        [InlineData("_abc_123_")]
        [InlineData("a1b2c3")]
        public void TableArgs_SetNamesWithNumbersAndUnderscores_DoesNotThrow(string validName)
        {
            // Arrange
            var tableExpr = new TableExpr();
            var args = new[] { validName };

            // Act
            tableExpr.TableArgs = args;

            // Assert
            Assert.Same(args, tableExpr.TableArgs);
        }

        /// <summary>
        /// Tests that the getter returns the same reference that was set.
        /// </summary>
        [Fact]
        public void TableArgs_GetAfterSet_ReturnsSameReference()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var args = new[] { "table1", "table2" };

            // Act
            tableExpr.TableArgs = args;
            var result = tableExpr.TableArgs;

            // Assert
            Assert.Same(args, result);
        }

        /// <summary>
        /// Tests that getting TableArgs before setting it returns null (default value).
        /// </summary>
        [Fact]
        public void TableArgs_GetBeforeSet_ReturnsNull()
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act
            var result = tableExpr.TableArgs;

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that setting TableArgs to null after setting it to a non-null value
        /// correctly updates to null.
        /// </summary>
        [Fact]
        public void TableArgs_SetToNullAfterNonNull_UpdatesToNull()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var args = new[] { "table1" };

            // Act
            tableExpr.TableArgs = args;
            tableExpr.TableArgs = null;

            // Assert
            Assert.Null(tableExpr.TableArgs);
        }

        /// <summary>
        /// Tests that the Alias property getter returns the value set by the setter.
        /// </summary>
        /// <param name="alias">The alias value to set and retrieve.</param>
        [Theory]
        [InlineData("validAlias")]
        [InlineData("_underscore")]
        [InlineData("table123")]
        [InlineData("my_table_123")]
        [InlineData("a")]
        [InlineData("_")]
        [InlineData("")]
        [InlineData(null)]
        public void Alias_SetValidValue_ReturnsSetValue(string? alias)
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act
            tableExpr.Alias = alias;

            // Assert
            Assert.Equal(alias, tableExpr.Alias);
        }

        /// <summary>
        /// Tests that the Alias property setter allows null value.
        /// </summary>
        [Fact]
        public void Alias_SetNull_DoesNotThrow()
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act & Assert
            var exception = Record.Exception(() => tableExpr.Alias = null);
            Assert.Null(exception);
        }

        /// <summary>
        /// Tests that the Alias property setter allows empty string value.
        /// </summary>
        [Fact]
        public void Alias_SetEmptyString_DoesNotThrow()
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act & Assert
            var exception = Record.Exception(() => tableExpr.Alias = string.Empty);
            Assert.Null(exception);
        }

        /// <summary>
        /// Tests that the Alias property setter allows valid SQL names containing only letters.
        /// </summary>
        [Theory]
        [InlineData("a")]
        [InlineData("A")]
        [InlineData("table")]
        [InlineData("TableName")]
        [InlineData("UPPERCASE")]
        public void Alias_SetValidLettersOnlyName_DoesNotThrow(string alias)
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act & Assert
            var exception = Record.Exception(() => tableExpr.Alias = alias);
            Assert.Null(exception);
            Assert.Equal(alias, tableExpr.Alias);
        }

        /// <summary>
        /// Tests that the Alias property setter allows valid SQL names containing underscores.
        /// </summary>
        [Theory]
        [InlineData("_")]
        [InlineData("_table")]
        [InlineData("table_")]
        [InlineData("my_table")]
        [InlineData("__double__")]
        public void Alias_SetValidNameWithUnderscores_DoesNotThrow(string alias)
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act & Assert
            var exception = Record.Exception(() => tableExpr.Alias = alias);
            Assert.Null(exception);
            Assert.Equal(alias, tableExpr.Alias);
        }

        /// <summary>
        /// Tests that the Alias property setter allows valid SQL names containing numbers.
        /// </summary>
        [Theory]
        [InlineData("table1")]
        [InlineData("t123")]
        [InlineData("123")]
        [InlineData("0")]
        public void Alias_SetValidNameWithNumbers_DoesNotThrow(string alias)
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act & Assert
            var exception = Record.Exception(() => tableExpr.Alias = alias);
            Assert.Null(exception);
            Assert.Equal(alias, tableExpr.Alias);
        }

        /// <summary>
        /// Tests that the Alias property setter allows valid SQL names with mixed alphanumeric and underscores.
        /// </summary>
        [Theory]
        [InlineData("my_table_123")]
        [InlineData("_Table123_")]
        [InlineData("A1_B2_C3")]
        public void Alias_SetValidMixedName_DoesNotThrow(string alias)
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act & Assert
            var exception = Record.Exception(() => tableExpr.Alias = alias);
            Assert.Null(exception);
            Assert.Equal(alias, tableExpr.Alias);
        }

        /// <summary>
        /// Tests that the Alias property setter throws ArgumentException when the value contains spaces.
        /// </summary>
        [Theory]
        [InlineData(" ")]
        [InlineData("  ")]
        [InlineData("my table")]
        [InlineData(" table")]
        [InlineData("table ")]
        [InlineData("my table name")]
        public void Alias_SetValueWithSpaces_ThrowsArgumentException(string alias)
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => tableExpr.Alias = alias);
            Assert.Equal("Alias", exception.ParamName);
            Assert.Contains("invalid characters", exception.Message);
            Assert.Contains(alias, exception.Message);
        }

        /// <summary>
        /// Tests that the Alias property setter throws ArgumentException when the value contains special characters.
        /// </summary>
        [Theory]
        [InlineData("table!")]
        [InlineData("my-table")]
        [InlineData("table@name")]
        [InlineData("table.name")]
        [InlineData("table$")]
        [InlineData("table%")]
        [InlineData("table&")]
        [InlineData("table*")]
        [InlineData("table+")]
        [InlineData("table=")]
        [InlineData("table[")]
        [InlineData("table]")]
        [InlineData("table{")]
        [InlineData("table}")]
        [InlineData("table(")]
        [InlineData("table)")]
        [InlineData("table<")]
        [InlineData("table>")]
        [InlineData("table?")]
        [InlineData("table/")]
        [InlineData("table\\")]
        [InlineData("table|")]
        [InlineData("table:")]
        [InlineData("table;")]
        [InlineData("table'")]
        [InlineData("table\"")]
        [InlineData("table,")]
        public void Alias_SetValueWithSpecialCharacters_ThrowsArgumentException(string alias)
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => tableExpr.Alias = alias);
            Assert.Equal("Alias", exception.ParamName);
            Assert.Contains("invalid characters", exception.Message);
        }

        /// <summary>
        /// Tests that the Alias property setter throws ArgumentException when the value contains tab characters.
        /// </summary>
        [Fact]
        public void Alias_SetValueWithTab_ThrowsArgumentException()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var alias = "table\tname";

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => tableExpr.Alias = alias);
            Assert.Equal("Alias", exception.ParamName);
        }

        /// <summary>
        /// Tests that the Alias property setter throws ArgumentException when the value contains newline characters.
        /// </summary>
        [Fact]
        public void Alias_SetValueWithNewline_ThrowsArgumentException()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var alias = "table\nname";

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => tableExpr.Alias = alias);
            Assert.Equal("Alias", exception.ParamName);
        }

        /// <summary>
        /// Tests that the Alias property setter throws ArgumentException when the value contains Unicode characters.
        /// </summary>
        [Theory]
        [InlineData("table中文")]
        [InlineData("tableÄ")]
        [InlineData("table©")]
        [InlineData("table™")]
        public void Alias_SetValueWithUnicodeCharacters_ThrowsArgumentException(string alias)
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => tableExpr.Alias = alias);
            Assert.Equal("Alias", exception.ParamName);
        }

        /// <summary>
        /// Tests that the Alias property setter allows very long valid SQL names.
        /// </summary>
        [Fact]
        public void Alias_SetVeryLongValidName_DoesNotThrow()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var alias = new string('a', 10000);

            // Act & Assert
            var exception = Record.Exception(() => tableExpr.Alias = alias);
            Assert.Null(exception);
            Assert.Equal(alias, tableExpr.Alias);
        }

        /// <summary>
        /// Tests that the Alias property setter throws ArgumentException for very long invalid SQL names.
        /// </summary>
        [Fact]
        public void Alias_SetVeryLongInvalidName_ThrowsArgumentException()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var alias = new string('a', 1000) + "!";

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => tableExpr.Alias = alias);
            Assert.Equal("Alias", exception.ParamName);
        }

        /// <summary>
        /// Tests that setting the Alias property multiple times updates the value correctly.
        /// </summary>
        [Fact]
        public void Alias_SetMultipleTimes_UpdatesValue()
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act
            tableExpr.Alias = "first";
            Assert.Equal("first", tableExpr.Alias);

            tableExpr.Alias = "second";
            Assert.Equal("second", tableExpr.Alias);

            tableExpr.Alias = null;
            Assert.Null(tableExpr.Alias);

            tableExpr.Alias = "third";
            Assert.Equal("third", tableExpr.Alias);
        }

        /// <summary>
        /// Tests that the Alias property getter returns null when not set.
        /// </summary>
        [Fact]
        public void Alias_NotSet_ReturnsNull()
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act
            var alias = tableExpr.Alias;

            // Assert
            Assert.Null(alias);
        }

        /// <summary>
        /// Tests that the exception message contains the expected information for invalid input.
        /// </summary>
        [Fact]
        public void Alias_SetInvalidValue_ExceptionContainsExpectedMessage()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var invalidAlias = "invalid-alias";

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => tableExpr.Alias = invalidAlias);
            Assert.Equal("Alias", exception.ParamName);
            Assert.Contains("invalid characters", exception.Message);
            Assert.Contains("only letters, numbers, and underscores are allowed", exception.Message);
            Assert.Contains(invalidAlias, exception.Message);
        }

        /// <summary>
        /// Tests that GetHashCode returns consistent hash code when called multiple times on the same instance.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsSameValue()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(string)) { Alias = "t", TableArgs = new[] { "2024", "01" } };

            // Act
            var hash1 = tableExpr.GetHashCode();
            var hash2 = tableExpr.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns equal hash codes for equal TableExpr instances.
        /// </summary>
        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHashCode()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { Alias = "t", TableArgs = new[] { "2024", "01" } };
            var tableExpr2 = new TableExpr(typeof(int)) { Alias = "t", TableArgs = new[] { "2024", "01" } };

            // Act
            var hash1 = tableExpr1.GetHashCode();
            var hash2 = tableExpr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes when Type property differs.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentTypes_ReturnsDifferentHashCodes()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(string));
            var tableExpr2 = new TableExpr(typeof(int));

            // Act
            var hash1 = tableExpr1.GetHashCode();
            var hash2 = tableExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes when Alias property differs.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentAliases_ReturnsDifferentHashCodes()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(string)) { Alias = "t1" };
            var tableExpr2 = new TableExpr(typeof(string)) { Alias = "t2" };

            // Act
            var hash1 = tableExpr1.GetHashCode();
            var hash2 = tableExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes when TableArgs property differs.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentTableArgs_ReturnsDifferentHashCodes()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(string)) { TableArgs = new[] { "2024", "01" } };
            var tableExpr2 = new TableExpr(typeof(string)) { TableArgs = new[] { "2024", "02" } };

            // Act
            var hash1 = tableExpr1.GetHashCode();
            var hash2 = tableExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles null Type property correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_NullType_ReturnsValidHashCode()
        {
            // Arrange
            var tableExpr = new TableExpr { Type = null };

            // Act
            var hash = tableExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that GetHashCode handles null Alias property correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_NullAlias_ReturnsValidHashCode()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(string)) { Alias = null };

            // Act
            var hash = tableExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that GetHashCode handles null TableArgs property correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_NullTableArgs_ReturnsValidHashCode()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(string)) { TableArgs = null };

            // Act
            var hash = tableExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that GetHashCode handles empty TableArgs array correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_EmptyTableArgs_ReturnsValidHashCode()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(string)) { TableArgs = Array.Empty<string>() };

            // Act
            var hash = tableExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that GetHashCode returns same hash code for null and empty TableArgs.
        /// </summary>
        [Fact]
        public void GetHashCode_NullVsEmptyTableArgs_ReturnsSameHashCode()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(string)) { TableArgs = null };
            var tableExpr2 = new TableExpr(typeof(string)) { TableArgs = Array.Empty<string>() };

            // Act
            var hash1 = tableExpr1.GetHashCode();
            var hash2 = tableExpr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles all properties being null correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_AllPropertiesNull_ReturnsValidHashCode()
        {
            // Arrange
            var tableExpr = new TableExpr { Type = null, Alias = null, TableArgs = null };

            // Act
            var hash = tableExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that GetHashCode returns same hash for instances with all null properties.
        /// </summary>
        [Fact]
        public void GetHashCode_BothInstancesWithAllNullProperties_ReturnsSameHashCode()
        {
            // Arrange
            var tableExpr1 = new TableExpr { Type = null, Alias = null, TableArgs = null };
            var tableExpr2 = new TableExpr { Type = null, Alias = null, TableArgs = null };

            // Act
            var hash1 = tableExpr1.GetHashCode();
            var hash2 = tableExpr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles single element TableArgs correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_SingleElementTableArgs_ReturnsValidHashCode()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(string)) { TableArgs = new[] { "2024" } };

            // Act
            var hash = tableExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes for different TableArgs lengths.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentTableArgsLengths_ReturnsDifferentHashCodes()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(string)) { TableArgs = new[] { "2024" } };
            var tableExpr2 = new TableExpr(typeof(string)) { TableArgs = new[] { "2024", "01" } };

            // Act
            var hash1 = tableExpr1.GetHashCode();
            var hash2 = tableExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles empty string in Alias correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_EmptyStringAlias_ReturnsValidHashCode()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(string)) { Alias = "" };

            // Act
            var hash = tableExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that GetHashCode handles long Alias strings correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_LongAlias_ReturnsValidHashCode()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(string)) { Alias = new string('a', 1000) };

            // Act
            var hash = tableExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that GetHashCode handles TableArgs with multiple elements correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_MultipleTableArgs_ReturnsValidHashCode()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(string)) { TableArgs = new[] { "2024", "01", "15", "extra" } };

            // Act
            var hash = tableExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that GetHashCode returns equal hash codes for instances with equal TableArgs order.
        /// </summary>
        [Fact]
        public void GetHashCode_SameTableArgsOrder_ReturnsSameHashCode()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(string)) { TableArgs = new[] { "2024", "01", "15" } };
            var tableExpr2 = new TableExpr(typeof(string)) { TableArgs = new[] { "2024", "01", "15" } };

            // Act
            var hash1 = tableExpr1.GetHashCode();
            var hash2 = tableExpr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes for TableArgs with different order.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentTableArgsOrder_ReturnsDifferentHashCodes()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(string)) { TableArgs = new[] { "2024", "01" } };
            var tableExpr2 = new TableExpr(typeof(string)) { TableArgs = new[] { "01", "2024" } };

            // Act
            var hash1 = tableExpr1.GetHashCode();
            var hash2 = tableExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles combination of all properties set correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_AllPropertiesSet_ReturnsValidHashCode()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(double))
            {
                Alias = "myTable",
                TableArgs = new[] { "2024", "Q1", "Data" }
            };

            // Act
            var hash = tableExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that GetHashCode with only Type set returns valid hash code.
        /// </summary>
        [Fact]
        public void GetHashCode_OnlyTypeSet_ReturnsValidHashCode()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(decimal));

            // Act
            var hash = tableExpr.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that the ExprType property returns ExprType.Table when using the default constructor.
        /// </summary>
        [Fact]
        public void ExprType_WithDefaultConstructor_ReturnsTable()
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act
            var result = tableExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Table, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns ExprType.Table when initialized with a Type parameter.
        /// </summary>
        [Fact]
        public void ExprType_WithTypeParameter_ReturnsTable()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(string));

            // Act
            var result = tableExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Table, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns ExprType.Table when initialized with a null Type.
        /// </summary>
        [Fact]
        public void ExprType_WithNullType_ReturnsTable()
        {
            // Arrange
            var tableExpr = new TableExpr(null);

            // Act
            var result = tableExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Table, result);
        }

        /// <summary>
        /// Tests that the ExprType property consistently returns ExprType.Table across multiple accesses.
        /// </summary>
        [Fact]
        public void ExprType_MultipleAccesses_ReturnsTableConsistently()
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act
            var result1 = tableExpr.ExprType;
            var result2 = tableExpr.ExprType;
            var result3 = tableExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Table, result1);
            Assert.Equal(ExprType.Table, result2);
            Assert.Equal(ExprType.Table, result3);
            Assert.Equal(result1, result2);
            Assert.Equal(result2, result3);
        }

        /// <summary>
        /// Tests that ToString returns an empty string when Type is null.
        /// </summary>
        [Fact]
        public void ToString_WhenTypeIsNull_ReturnsEmptyString()
        {
            // Arrange
            var tableExpr = new TableExpr();

            // Act
            var result = tableExpr.ToString();

            // Assert
            Assert.Equal(string.Empty, result);
        }

        /// <summary>
        /// Tests that ToString returns the Type.Name when Type is set to various valid types.
        /// </summary>
        /// <param name="type">The type to test</param>
        /// <param name="expectedName">The expected name returned by ToString</param>
        [Theory]
        [InlineData(typeof(string), "String")]
        [InlineData(typeof(int), "Int32")]
        [InlineData(typeof(TableExpr), "TableExpr")]
        [InlineData(typeof(DateTime), "DateTime")]
        [InlineData(typeof(object), "Object")]
        public void ToString_WhenTypeIsSet_ReturnsTypeName(Type type, string expectedName)
        {
            // Arrange
            var tableExpr = new TableExpr(type);

            // Act
            var result = tableExpr.ToString();

            // Assert
            Assert.Equal(expectedName, result);
        }

        /// <summary>
        /// Tests that ToString returns the Type.Name for generic types.
        /// </summary>
        [Fact]
        public void ToString_WhenTypeIsGeneric_ReturnsGenericTypeName()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(System.Collections.Generic.List<int>));

            // Act
            var result = tableExpr.ToString();

            // Assert
            Assert.Equal("List`1", result);
        }

        /// <summary>
        /// Tests that ToString returns the Type.Name for array types.
        /// </summary>
        [Fact]
        public void ToString_WhenTypeIsArray_ReturnsArrayTypeName()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(int[]));

            // Act
            var result = tableExpr.ToString();

            // Assert
            Assert.Equal("Int32[]", result);
        }

        /// <summary>
        /// Tests that ToString returns empty string when Type is explicitly set to null after construction.
        /// </summary>
        [Fact]
        public void ToString_WhenTypeSetToNullAfterConstruction_ReturnsEmptyString()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(string))
            {
                Type = null
            };

            // Act
            var result = tableExpr.ToString();

            // Assert
            Assert.Equal(string.Empty, result);
        }

        /// <summary>
        /// Tests that Equals returns false when the parameter is null.
        /// </summary>
        [Fact]
        public void Equals_NullObject_ReturnsFalse()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(string));

            // Act
            var result = tableExpr.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when the parameter is a different type.
        /// </summary>
        [Theory]
        [InlineData("string")]
        [InlineData(42)]
        [InlineData(3.14)]
        public void Equals_DifferentType_ReturnsFalse(object obj)
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(string));

            // Act
            var result = tableExpr.Equals(obj);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing a TableExpr with itself (reflexive property).
        /// </summary>
        [Fact]
        public void Equals_SameInstance_ReturnsTrue()
        {
            // Arrange
            var tableExpr = new TableExpr(typeof(string));

            // Act
            var result = tableExpr.Equals(tableExpr);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both TableExpr instances have the same Type.
        /// </summary>
        [Fact]
        public void Equals_SameType_ReturnsTrue()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int));
            var tableExpr2 = new TableExpr(typeof(int));

            // Act
            var result = tableExpr1.Equals(tableExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when TableExpr instances have different Types.
        /// </summary>
        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int));
            var tableExpr2 = new TableExpr(typeof(string));

            // Act
            var result = tableExpr1.Equals(tableExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both TableExpr instances have null Type.
        /// </summary>
        [Fact]
        public void Equals_BothTypeNull_ReturnsTrue()
        {
            // Arrange
            var tableExpr1 = new TableExpr();
            var tableExpr2 = new TableExpr();

            // Act
            var result = tableExpr1.Equals(tableExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one Type is null and the other is not.
        /// </summary>
        [Fact]
        public void Equals_OneTypeNull_ReturnsFalse()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int));
            var tableExpr2 = new TableExpr();

            // Act
            var result1 = tableExpr1.Equals(tableExpr2);
            var result2 = tableExpr2.Equals(tableExpr1);

            // Assert
            Assert.False(result1);
            Assert.False(result2);
        }

        /// <summary>
        /// Tests that Equals returns true when both TableExpr instances have the same Alias.
        /// </summary>
        [Fact]
        public void Equals_SameAlias_ReturnsTrue()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { Alias = "t1" };
            var tableExpr2 = new TableExpr(typeof(int)) { Alias = "t1" };

            // Act
            var result = tableExpr1.Equals(tableExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when TableExpr instances have different Alias values.
        /// </summary>
        [Fact]
        public void Equals_DifferentAlias_ReturnsFalse()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { Alias = "t1" };
            var tableExpr2 = new TableExpr(typeof(int)) { Alias = "t2" };

            // Act
            var result = tableExpr1.Equals(tableExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both TableExpr instances have null Alias.
        /// </summary>
        [Fact]
        public void Equals_BothAliasNull_ReturnsTrue()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int));
            var tableExpr2 = new TableExpr(typeof(int));

            // Act
            var result = tableExpr1.Equals(tableExpr2);

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
            var tableExpr1 = new TableExpr(typeof(int)) { Alias = "t1" };
            var tableExpr2 = new TableExpr(typeof(int));

            // Act
            var result1 = tableExpr1.Equals(tableExpr2);
            var result2 = tableExpr2.Equals(tableExpr1);

            // Assert
            Assert.False(result1);
            Assert.False(result2);
        }

        /// <summary>
        /// Tests that Equals returns false when one Alias is empty string and the other is null.
        /// </summary>
        [Fact]
        public void Equals_EmptyAliasVsNull_ReturnsFalse()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { Alias = "" };
            var tableExpr2 = new TableExpr(typeof(int));

            // Act
            var result1 = tableExpr1.Equals(tableExpr2);
            var result2 = tableExpr2.Equals(tableExpr1);

            // Assert
            Assert.False(result1);
            Assert.False(result2);
        }

        /// <summary>
        /// Tests that Equals returns true when both TableExpr instances have the same TableArgs.
        /// </summary>
        [Fact]
        public void Equals_SameTableArgs_ReturnsTrue()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { TableArgs = new[] { "2024", "01" } };
            var tableExpr2 = new TableExpr(typeof(int)) { TableArgs = new[] { "2024", "01" } };

            // Act
            var result = tableExpr1.Equals(tableExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when TableExpr instances have different TableArgs.
        /// </summary>
        [Fact]
        public void Equals_DifferentTableArgs_ReturnsFalse()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { TableArgs = new[] { "2024", "01" } };
            var tableExpr2 = new TableExpr(typeof(int)) { TableArgs = new[] { "2024", "02" } };

            // Act
            var result = tableExpr1.Equals(tableExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both TableExpr instances have null TableArgs.
        /// </summary>
        [Fact]
        public void Equals_BothTableArgsNull_ReturnsTrue()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int));
            var tableExpr2 = new TableExpr(typeof(int));

            // Act
            var result = tableExpr1.Equals(tableExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both TableExpr instances have empty TableArgs arrays.
        /// </summary>
        [Fact]
        public void Equals_BothTableArgsEmpty_ReturnsTrue()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { TableArgs = new string[0] };
            var tableExpr2 = new TableExpr(typeof(int)) { TableArgs = new string[0] };

            // Act
            var result = tableExpr1.Equals(tableExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when one TableArgs is null and the other is empty (per ArrayEquals logic).
        /// </summary>
        [Fact]
        public void Equals_OneTableArgsNullOneEmpty_ReturnsTrue()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { TableArgs = null };
            var tableExpr2 = new TableExpr(typeof(int)) { TableArgs = new string[0] };

            // Act
            var result1 = tableExpr1.Equals(tableExpr2);
            var result2 = tableExpr2.Equals(tableExpr1);

            // Assert
            Assert.True(result1);
            Assert.True(result2);
        }

        /// <summary>
        /// Tests that Equals returns false when one TableArgs is null/empty and the other has items.
        /// </summary>
        [Fact]
        public void Equals_OneTableArgsNullOneWithItems_ReturnsFalse()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { TableArgs = null };
            var tableExpr2 = new TableExpr(typeof(int)) { TableArgs = new[] { "2024" } };

            // Act
            var result1 = tableExpr1.Equals(tableExpr2);
            var result2 = tableExpr2.Equals(tableExpr1);

            // Assert
            Assert.False(result1);
            Assert.False(result2);
        }

        /// <summary>
        /// Tests that Equals returns false when TableArgs have same items but different order.
        /// </summary>
        [Fact]
        public void Equals_TableArgsDifferentOrder_ReturnsFalse()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { TableArgs = new[] { "01", "2024" } };
            var tableExpr2 = new TableExpr(typeof(int)) { TableArgs = new[] { "2024", "01" } };

            // Act
            var result = tableExpr1.Equals(tableExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when TableArgs have single matching item.
        /// </summary>
        [Fact]
        public void Equals_TableArgsSingleItem_ReturnsTrue()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { TableArgs = new[] { "2024" } };
            var tableExpr2 = new TableExpr(typeof(int)) { TableArgs = new[] { "2024" } };

            // Act
            var result = tableExpr1.Equals(tableExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when TableArgs have different lengths.
        /// </summary>
        [Fact]
        public void Equals_TableArgsDifferentLengths_ReturnsFalse()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { TableArgs = new[] { "2024" } };
            var tableExpr2 = new TableExpr(typeof(int)) { TableArgs = new[] { "2024", "01" } };

            // Act
            var result = tableExpr1.Equals(tableExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when all properties (Type, Alias, TableArgs) are equal.
        /// </summary>
        [Fact]
        public void Equals_AllPropertiesEqual_ReturnsTrue()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { Alias = "t1", TableArgs = new[] { "2024", "01" } };
            var tableExpr2 = new TableExpr(typeof(int)) { Alias = "t1", TableArgs = new[] { "2024", "01" } };

            // Act
            var result = tableExpr1.Equals(tableExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when multiple properties differ.
        /// </summary>
        [Fact]
        public void Equals_MultiplePropertiesDifferent_ReturnsFalse()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { Alias = "t1", TableArgs = new[] { "2024", "01" } };
            var tableExpr2 = new TableExpr(typeof(string)) { Alias = "t2", TableArgs = new[] { "2024", "02" } };

            // Act
            var result = tableExpr1.Equals(tableExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals handles complex types for Type property.
        /// </summary>
        [Fact]
        public void Equals_ComplexTypes_ReturnsCorrectResult()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(System.Collections.Generic.List<int>));
            var tableExpr2 = new TableExpr(typeof(System.Collections.Generic.List<int>));
            var tableExpr3 = new TableExpr(typeof(System.Collections.Generic.List<string>));

            // Act
            var result1 = tableExpr1.Equals(tableExpr2);
            var result2 = tableExpr1.Equals(tableExpr3);

            // Assert
            Assert.True(result1);
            Assert.False(result2);
        }

        /// <summary>
        /// Tests that Equals is symmetric (a.Equals(b) == b.Equals(a)).
        /// </summary>
        [Fact]
        public void Equals_SymmetricProperty_ReturnsConsistentResults()
        {
            // Arrange
            var tableExpr1 = new TableExpr(typeof(int)) { Alias = "t1", TableArgs = new[] { "2024" } };
            var tableExpr2 = new TableExpr(typeof(int)) { Alias = "t1", TableArgs = new[] { "2024" } };

            // Act
            var result1 = tableExpr1.Equals(tableExpr2);
            var result2 = tableExpr2.Equals(tableExpr1);

            // Assert
            Assert.Equal(result1, result2);
            Assert.True(result1);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes the Type property with the provided objectType parameter.
        /// </summary>
        /// <param name="objectType">The type to pass to the constructor.</param>
        /// <param name="expectedTypeName">The expected name of the type, or null if objectType is null.</param>
        [Theory]
        [InlineData(typeof(string), "String")]
        [InlineData(typeof(int), "Int32")]
        [InlineData(typeof(object), "Object")]
        public void Constructor_WithValidType_SetsTypeProperty(Type objectType, string expectedTypeName)
        {
            // Arrange & Act
            var tableExpr = new TableExpr(objectType);

            // Assert
            Assert.NotNull(tableExpr.Type);
            Assert.Equal(objectType, tableExpr.Type);
            Assert.Equal(expectedTypeName, tableExpr.Type.Name);
        }

        /// <summary>
        /// Tests that the constructor accepts null as the objectType parameter and sets Type property to null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullType_SetsTypePropertyToNull()
        {
            // Arrange
            Type objectType = null;

            // Act
            var tableExpr = new TableExpr(objectType);

            // Assert
            Assert.Null(tableExpr.Type);
        }

        /// <summary>
        /// Tests that the constructor correctly handles various type kinds including value types, interfaces, and generic types.
        /// </summary>
        /// <param name="objectType">The type to pass to the constructor.</param>
        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(IDisposable))]
        [InlineData(typeof(Action<string>))]
        [InlineData(typeof(Nullable<int>))]
        public void Constructor_WithDifferentTypeKinds_SetsTypePropertyCorrectly(Type objectType)
        {
            // Arrange & Act
            var tableExpr = new TableExpr(objectType);

            // Assert
            Assert.Equal(objectType, tableExpr.Type);
        }

        /// <summary>
        /// Tests the default parameterless constructor of TableExpr.
        /// Verifies that all properties are initialized to their expected default values.
        /// </summary>
        [Fact]
        public void TableExpr_DefaultConstructor_InitializesPropertiesToDefaultValues()
        {
            // Arrange & Act
            var tableExpr = new TableExpr();

            // Assert
            Assert.Null(tableExpr.Type);
            Assert.Null(tableExpr.Alias);
            Assert.Null(tableExpr.TableArgs);
            Assert.Equal(ExprType.Table, tableExpr.ExprType);
            Assert.Equal(string.Empty, tableExpr.ToString());
        }

        /// <summary>
        /// Tests that two instances created with the default constructor are considered equal.
        /// Verifies the Equals method works correctly for default instances.
        /// </summary>
        [Fact]
        public void TableExpr_DefaultConstructor_TwoInstancesAreEqual()
        {
            // Arrange & Act
            var tableExpr1 = new TableExpr();
            var tableExpr2 = new TableExpr();

            // Assert
            Assert.True(tableExpr1.Equals(tableExpr2));
            Assert.Equal(tableExpr1.GetHashCode(), tableExpr2.GetHashCode());
        }

        /// <summary>
        /// Tests that Clone method works correctly for an instance created with the default constructor.
        /// Verifies that the cloned instance is equal to the original but is a different instance.
        /// </summary>
        [Fact]
        public void TableExpr_DefaultConstructor_CloneCreatesEqualInstance()
        {
            // Arrange
            var original = new TableExpr();

            // Act
            var cloned = (TableExpr)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.True(original.Equals(cloned));
            Assert.Null(cloned.Type);
            Assert.Null(cloned.Alias);
            Assert.Null(cloned.TableArgs);
        }

        /// <summary>
        /// Tests that properties can be set after using the default constructor.
        /// Verifies that the default constructor allows subsequent property initialization.
        /// </summary>
        [Fact]
        public void TableExpr_DefaultConstructor_AllowsPropertyInitialization()
        {
            // Arrange
            var tableExpr = new TableExpr();
            var testType = typeof(string);

            // Act
            tableExpr.Type = testType;
            tableExpr.Alias = "alias1";
            tableExpr.TableArgs = new[] { "arg1", "arg2" };

            // Assert
            Assert.Equal(testType, tableExpr.Type);
            Assert.Equal("alias1", tableExpr.Alias);
            Assert.Equal(new[] { "arg1", "arg2" }, tableExpr.TableArgs);
        }
    }
}