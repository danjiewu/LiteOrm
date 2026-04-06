using System;

using LiteOrm;
using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the TableJoinAttribute class.
    /// </summary>
    public class TableJoinAttributeTests
    {
        /// <summary>
        /// Tests that the constructor correctly assigns valid parameters to the corresponding properties.
        /// </summary>
        /// <param name="sourceTable">The source table name to test.</param>
        /// <param name="targetType">The target type to test.</param>
        /// <param name="foreignKeys">The foreign keys to test.</param>
        [Theory]
        [InlineData("Users", typeof(string), "UserId")]
        [InlineData("Orders", typeof(int), "OrderId,CustomerId")]
        [InlineData("Products", typeof(object), "ProductId")]
        [InlineData("Categories", typeof(TableJoinAttribute), "CategoryId")]
        public void Constructor_WithValidParameters_ShouldAssignPropertiesCorrectly(string sourceTable, Type targetType, string foreignKeys)
        {
            // Arrange & Act
            var attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Assert
            Assert.Equal(sourceTable, attribute.Source);
            Assert.Equal(targetType, attribute.TargetType);
            Assert.Equal(foreignKeys, attribute.ForeignKeys);
            Assert.Equal(TableJoinType.Left, attribute.JoinType);
            Assert.False(attribute.AutoExpand);
        }

        /// <summary>
        /// Tests that the constructor accepts null sourceTable parameter without throwing an exception.
        /// </summary>
        [Fact]
        public void Constructor_WithNullSourceTable_ShouldAcceptAndAssignNull()
        {
            // Arrange
            string? sourceTable = null;
            Type targetType = typeof(string);
            string foreignKeys = "Id";

            // Act
            var attribute = new TableJoinAttribute(sourceTable!, targetType, foreignKeys);

            // Assert
            Assert.Null(attribute.Source);
            Assert.Equal(targetType, attribute.TargetType);
            Assert.Equal(foreignKeys, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor accepts null targetType parameter without throwing an exception.
        /// </summary>
        [Fact]
        public void Constructor_WithNullTargetType_ShouldAcceptAndAssignNull()
        {
            // Arrange
            string sourceTable = "Users";
            Type? targetType = null;
            string foreignKeys = "Id";

            // Act
            var attribute = new TableJoinAttribute(sourceTable, targetType!, foreignKeys);

            // Assert
            Assert.Equal(sourceTable, attribute.Source);
            Assert.Null(attribute.TargetType);
            Assert.Equal(foreignKeys, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor accepts null foreignKeys parameter without throwing an exception.
        /// </summary>
        [Fact]
        public void Constructor_WithNullForeignKeys_ShouldAcceptAndAssignNull()
        {
            // Arrange
            string sourceTable = "Users";
            Type targetType = typeof(int);
            string? foreignKeys = null;

            // Act
            var attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys!);

            // Assert
            Assert.Equal(sourceTable, attribute.Source);
            Assert.Equal(targetType, attribute.TargetType);
            Assert.Null(attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor accepts all null parameters without throwing an exception.
        /// </summary>
        [Fact]
        public void Constructor_WithAllNullParameters_ShouldAcceptAndAssignAllNull()
        {
            // Arrange
            string? sourceTable = null;
            Type? targetType = null;
            string? foreignKeys = null;

            // Act
            var attribute = new TableJoinAttribute(sourceTable!, targetType!, foreignKeys!);

            // Assert
            Assert.Null(attribute.Source);
            Assert.Null(attribute.TargetType);
            Assert.Null(attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor accepts empty string parameters and assigns them correctly.
        /// </summary>
        /// <param name="sourceTable">The source table name to test.</param>
        /// <param name="foreignKeys">The foreign keys to test.</param>
        [Theory]
        [InlineData("", "")]
        [InlineData("", "Key")]
        [InlineData("Table", "")]
        public void Constructor_WithEmptyStrings_ShouldAcceptAndAssignEmptyStrings(string sourceTable, string foreignKeys)
        {
            // Arrange
            Type targetType = typeof(string);

            // Act
            var attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Assert
            Assert.Equal(sourceTable, attribute.Source);
            Assert.Equal(targetType, attribute.TargetType);
            Assert.Equal(foreignKeys, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor accepts whitespace-only string parameters and assigns them correctly.
        /// </summary>
        /// <param name="sourceTable">The source table name to test.</param>
        /// <param name="foreignKeys">The foreign keys to test.</param>
        [Theory]
        [InlineData("   ", "   ")]
        [InlineData("\t", "\n")]
        [InlineData(" \r\n ", " \t\r\n ")]
        public void Constructor_WithWhitespaceStrings_ShouldAcceptAndAssignWhitespace(string sourceTable, string foreignKeys)
        {
            // Arrange
            Type targetType = typeof(int);

            // Act
            var attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Assert
            Assert.Equal(sourceTable, attribute.Source);
            Assert.Equal(targetType, attribute.TargetType);
            Assert.Equal(foreignKeys, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor accepts strings with special characters and assigns them correctly.
        /// </summary>
        /// <param name="sourceTable">The source table name to test.</param>
        /// <param name="foreignKeys">The foreign keys to test.</param>
        [Theory]
        [InlineData("Table$Name", "Key#123")]
        [InlineData("Table@Name", "Key!@#")]
        [InlineData("[dbo].[Users]", "Column1,Column2")]
        [InlineData("Table-Name_123", "Foreign_Key-Id")]
        public void Constructor_WithSpecialCharacters_ShouldAcceptAndAssignCorrectly(string sourceTable, string foreignKeys)
        {
            // Arrange
            Type targetType = typeof(object);

            // Act
            var attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Assert
            Assert.Equal(sourceTable, attribute.Source);
            Assert.Equal(targetType, attribute.TargetType);
            Assert.Equal(foreignKeys, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor accepts various Type objects and assigns them correctly.
        /// </summary>
        /// <param name="targetType">The target type to test.</param>
        [Theory]
        [MemberData(nameof(GetVariousTypes))]
        public void Constructor_WithVariousTypes_ShouldAcceptAndAssignCorrectly(Type targetType)
        {
            // Arrange
            string sourceTable = "TestTable";
            string foreignKeys = "TestKey";

            // Act
            var attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Assert
            Assert.Equal(sourceTable, attribute.Source);
            Assert.Equal(targetType, attribute.TargetType);
            Assert.Equal(foreignKeys, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor initializes JoinType to its default value.
        /// </summary>
        [Fact]
        public void Constructor_Always_ShouldInitializeJoinTypeToLeft()
        {
            // Arrange
            string sourceTable = "Users";
            Type targetType = typeof(string);
            string foreignKeys = "UserId";

            // Act
            var attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Assert
            Assert.Equal(TableJoinType.Left, attribute.JoinType);
        }

        /// <summary>
        /// Tests that the constructor initializes AutoExpand to its default value.
        /// </summary>
        [Fact]
        public void Constructor_Always_ShouldInitializeAutoExpandToFalse()
        {
            // Arrange
            string sourceTable = "Users";
            Type targetType = typeof(string);
            string foreignKeys = "UserId";

            // Act
            var attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Assert
            Assert.False(attribute.AutoExpand);
        }

        /// <summary>
        /// Provides various Type objects for testing.
        /// </summary>
        public static TheoryData<Type> GetVariousTypes()
        {
            return new TheoryData<Type>
            {
                typeof(string),
                typeof(int),
                typeof(object),
                typeof(IDisposable),
                typeof(Attribute),
                typeof(TableJoinAttribute),
                typeof(int[]),
                typeof(System.Collections.Generic.List<string>)
            };
        }

        /// <summary>
        /// Tests that the constructor with Type sourceTable, Type targetType, and string foreignKeys
        /// correctly initializes the attribute with valid inputs.
        /// </summary>
        /// <param name="sourceTableType">The source table type to test.</param>
        /// <param name="targetTypeParam">The target table type to test.</param>
        /// <param name="foreignKeysValue">The foreign keys value to test.</param>
        [Theory]
        [InlineData(typeof(string), typeof(int), "Id")]
        [InlineData(typeof(DateTime), typeof(object), "Key1,Key2")]
        [InlineData(typeof(TableJoinAttribute), typeof(Exception), "CompositeKey1,CompositeKey2,CompositeKey3")]
        public void Constructor_WithValidInputs_InitializesPropertiesCorrectly(Type sourceTableType, Type targetTypeParam, string foreignKeysValue)
        {
            // Arrange & Act
            var attribute = new TableJoinAttribute(sourceTableType, targetTypeParam, foreignKeysValue);

            // Assert
            Assert.Equal(sourceTableType, attribute.Source);
            Assert.Equal(targetTypeParam, attribute.TargetType);
            Assert.Equal(foreignKeysValue, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly handles null sourceTable parameter.
        /// </summary>
        [Fact]
        public void Constructor_WithNullSourceTable_StoresNullInSourceProperty()
        {
            // Arrange
            Type? sourceTableType = null;
            Type targetTypeParam = typeof(string);
            string foreignKeysValue = "ForeignKeyId";

            // Act
            var attribute = new TableJoinAttribute(sourceTableType, targetTypeParam, foreignKeysValue);

            // Assert
            Assert.Null(attribute.Source);
            Assert.Equal(targetTypeParam, attribute.TargetType);
            Assert.Equal(foreignKeysValue, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly handles null targetType parameter.
        /// </summary>
        [Fact]
        public void Constructor_WithNullTargetType_StoresNullInTargetTypeProperty()
        {
            // Arrange
            Type sourceTableType = typeof(int);
            Type? targetTypeParam = null;
            string foreignKeysValue = "ForeignKeyId";

            // Act
            var attribute = new TableJoinAttribute(sourceTableType, targetTypeParam, foreignKeysValue);

            // Assert
            Assert.Equal(sourceTableType, attribute.Source);
            Assert.Null(attribute.TargetType);
            Assert.Equal(foreignKeysValue, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly handles null foreignKeys parameter.
        /// </summary>
        [Fact]
        public void Constructor_WithNullForeignKeys_StoresNullInForeignKeysProperty()
        {
            // Arrange
            Type sourceTableType = typeof(string);
            Type targetTypeParam = typeof(int);
            string? foreignKeysValue = null;

            // Act
            var attribute = new TableJoinAttribute(sourceTableType, targetTypeParam, foreignKeysValue);

            // Assert
            Assert.Equal(sourceTableType, attribute.Source);
            Assert.Equal(targetTypeParam, attribute.TargetType);
            Assert.Null(attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly handles empty string foreignKeys parameter.
        /// </summary>
        [Fact]
        public void Constructor_WithEmptyForeignKeys_StoresEmptyStringInForeignKeysProperty()
        {
            // Arrange
            Type sourceTableType = typeof(string);
            Type targetTypeParam = typeof(int);
            string foreignKeysValue = string.Empty;

            // Act
            var attribute = new TableJoinAttribute(sourceTableType, targetTypeParam, foreignKeysValue);

            // Assert
            Assert.Equal(sourceTableType, attribute.Source);
            Assert.Equal(targetTypeParam, attribute.TargetType);
            Assert.Equal(string.Empty, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly handles whitespace-only foreignKeys parameter.
        /// </summary>
        [Theory]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\n")]
        [InlineData("  \t\n  ")]
        public void Constructor_WithWhitespaceForeignKeys_StoresWhitespaceInForeignKeysProperty(string foreignKeysValue)
        {
            // Arrange
            Type sourceTableType = typeof(string);
            Type targetTypeParam = typeof(int);

            // Act
            var attribute = new TableJoinAttribute(sourceTableType, targetTypeParam, foreignKeysValue);

            // Assert
            Assert.Equal(sourceTableType, attribute.Source);
            Assert.Equal(targetTypeParam, attribute.TargetType);
            Assert.Equal(foreignKeysValue, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly handles all null parameters.
        /// </summary>
        [Fact]
        public void Constructor_WithAllNullParameters_StoresAllNullValues()
        {
            // Arrange
            Type? sourceTableType = null;
            Type? targetTypeParam = null;
            string? foreignKeysValue = null;

            // Act
            var attribute = new TableJoinAttribute(sourceTableType, targetTypeParam, foreignKeysValue);

            // Assert
            Assert.Null(attribute.Source);
            Assert.Null(attribute.TargetType);
            Assert.Null(attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly handles foreign keys with special characters.
        /// </summary>
        [Theory]
        [InlineData("Key@123")]
        [InlineData("Key#With$Special%Chars")]
        [InlineData("Key.With.Dots")]
        [InlineData("Key-With-Dashes")]
        [InlineData("Key_With_Underscores")]
        public void Constructor_WithSpecialCharactersInForeignKeys_StoresValueCorrectly(string foreignKeysValue)
        {
            // Arrange
            Type sourceTableType = typeof(object);
            Type targetTypeParam = typeof(string);

            // Act
            var attribute = new TableJoinAttribute(sourceTableType, targetTypeParam, foreignKeysValue);

            // Assert
            Assert.Equal(sourceTableType, attribute.Source);
            Assert.Equal(targetTypeParam, attribute.TargetType);
            Assert.Equal(foreignKeysValue, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly handles very long foreign keys string.
        /// </summary>
        [Fact]
        public void Constructor_WithVeryLongForeignKeys_StoresValueCorrectly()
        {
            // Arrange
            Type sourceTableType = typeof(int);
            Type targetTypeParam = typeof(string);
            string foreignKeysValue = new string('A', 10000);

            // Act
            var attribute = new TableJoinAttribute(sourceTableType, targetTypeParam, foreignKeysValue);

            // Assert
            Assert.Equal(sourceTableType, attribute.Source);
            Assert.Equal(targetTypeParam, attribute.TargetType);
            Assert.Equal(foreignKeysValue, attribute.ForeignKeys);
            Assert.Equal(10000, attribute.ForeignKeys.Length);
        }

        /// <summary>
        /// Tests that the constructor works with different type kinds (class, struct, interface, enum).
        /// </summary>
        [Theory]
        [InlineData(typeof(string), typeof(int))]              // class, struct
        [InlineData(typeof(IDisposable), typeof(DateTime))]    // interface, struct
        [InlineData(typeof(Exception), typeof(DayOfWeek))]     // class, enum
        [InlineData(typeof(IComparable), typeof(IFormattable))] // interface, interface
        public void Constructor_WithDifferentTypeKinds_InitializesCorrectly(Type sourceTableType, Type targetTypeParam)
        {
            // Arrange
            string foreignKeysValue = "TestKey";

            // Act
            var attribute = new TableJoinAttribute(sourceTableType, targetTypeParam, foreignKeysValue);

            // Assert
            Assert.Equal(sourceTableType, attribute.Source);
            Assert.Equal(targetTypeParam, attribute.TargetType);
            Assert.Equal(foreignKeysValue, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly stores multiple comma-separated foreign keys.
        /// </summary>
        [Theory]
        [InlineData("Key1,Key2")]
        [InlineData("Key1,Key2,Key3,Key4,Key5")]
        [InlineData("FirstKey, SecondKey")]
        [InlineData("Key1,,Key2")]
        [InlineData(",,,")]
        public void Constructor_WithMultipleForeignKeys_StoresValueCorrectly(string foreignKeysValue)
        {
            // Arrange
            Type sourceTableType = typeof(string);
            Type targetTypeParam = typeof(int);

            // Act
            var attribute = new TableJoinAttribute(sourceTableType, targetTypeParam, foreignKeysValue);

            // Assert
            Assert.Equal(sourceTableType, attribute.Source);
            Assert.Equal(targetTypeParam, attribute.TargetType);
            Assert.Equal(foreignKeysValue, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that Source property returns the correct string value when initialized with the first constructor.
        /// </summary>
        /// <param name="sourceTable">The source table string to test.</param>
        [Theory]
        [InlineData("TableA")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Table_With_Underscores")]
        [InlineData("TableWithVeryLongNameThatExceedsNormalLengthExpectations")]
        [InlineData("Table123")]
        [InlineData("table-with-dashes")]
        [InlineData("table.with.dots")]
        public void Source_WhenInitializedWithStringSourceTable_ReturnsCorrectString(string sourceTable)
        {
            // Arrange
            Type targetType = typeof(string);
            string foreignKeys = "Id";

            // Act
            TableJoinAttribute attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Assert
            Assert.Equal(sourceTable, attribute.Source);
        }

        /// <summary>
        /// Tests that Source property returns null when initialized with null string in the first constructor.
        /// </summary>
        [Fact]
        public void Source_WhenInitializedWithNullString_ReturnsNull()
        {
            // Arrange
            string? sourceTable = null;
            Type targetType = typeof(string);
            string foreignKeys = "Id";

            // Act
            TableJoinAttribute attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Assert
            Assert.Null(attribute.Source);
        }

        /// <summary>
        /// Tests that Source property returns the correct Type value when initialized with the second constructor.
        /// </summary>
        /// <param name="sourceTableType">The source table Type to test.</param>
        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(TableJoinAttribute))]
        [InlineData(typeof(object))]
        [InlineData(typeof(DateTime))]
        public void Source_WhenInitializedWithTypeSourceTable_ReturnsCorrectType(Type sourceTableType)
        {
            // Arrange
            Type targetType = typeof(string);
            string foreignKeys = "Id";

            // Act
            TableJoinAttribute attribute = new TableJoinAttribute(sourceTableType, targetType, foreignKeys);

            // Assert
            Assert.Equal(sourceTableType, attribute.Source);
        }

        /// <summary>
        /// Tests that Source property returns null when initialized with null Type in the second constructor.
        /// </summary>
        [Fact]
        public void Source_WhenInitializedWithNullType_ReturnsNull()
        {
            // Arrange
            Type? sourceTableType = null;
            Type targetType = typeof(string);
            string foreignKeys = "Id";

            // Act
            TableJoinAttribute attribute = new TableJoinAttribute(sourceTableType, targetType, foreignKeys);

            // Assert
            Assert.Null(attribute.Source);
        }

        /// <summary>
        /// Tests that Source property returns null when initialized with the third constructor (no source table parameter).
        /// </summary>
        [Fact]
        public void Source_WhenInitializedWithoutSourceTable_ReturnsNull()
        {
            // Arrange
            Type targetType = typeof(string);
            string foreignKey = "Id";

            // Act
            TableJoinAttribute attribute = new TableJoinAttribute(targetType, foreignKey);

            // Assert
            Assert.Null(attribute.Source);
        }

        /// <summary>
        /// Tests that Source property is read-only and returns the same value on multiple accesses.
        /// </summary>
        [Fact]
        public void Source_WhenAccessedMultipleTimes_ReturnsSameValue()
        {
            // Arrange
            string sourceTable = "TestTable";
            Type targetType = typeof(string);
            string foreignKeys = "Id";
            TableJoinAttribute attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Act
            object firstAccess = attribute.Source;
            object secondAccess = attribute.Source;

            // Assert
            Assert.Same(firstAccess, secondAccess);
        }

        /// <summary>
        /// Tests that Source property correctly handles special characters in string source table names.
        /// </summary>
        /// <param name="sourceTable">The source table string with special characters.</param>
        [Theory]
        [InlineData("Table@Name")]
        [InlineData("Table#Name")]
        [InlineData("Table$Name")]
        [InlineData("Table%Name")]
        [InlineData("Table Name")]
        [InlineData("Table\tName")]
        [InlineData("Table\nName")]
        [InlineData("[dbo].[Table]")]
        public void Source_WhenInitializedWithSpecialCharacters_ReturnsCorrectString(string sourceTable)
        {
            // Arrange
            Type targetType = typeof(string);
            string foreignKeys = "Id";

            // Act
            TableJoinAttribute attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Assert
            Assert.Equal(sourceTable, attribute.Source);
        }

        /// <summary>
        /// Tests that TargetType property returns the Type passed to the constructor with string sourceTable.
        /// Input: Valid Type instances passed to constructor.
        /// Expected: TargetType property returns the same Type instance.
        /// </summary>
        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(object))]
        [InlineData(typeof(IDisposable))]
        [InlineData(typeof(DateTime))]
        public void TargetType_WithStringSourceTableConstructor_ReturnsExpectedType(Type expectedType)
        {
            // Arrange
            var sourceTable = "SourceTable";
            var foreignKeys = "Id";

            // Act
            var attribute = new TableJoinAttribute(sourceTable, expectedType, foreignKeys);

            // Assert
            Assert.Same(expectedType, attribute.TargetType);
        }

        /// <summary>
        /// Tests that TargetType property returns the Type passed to the constructor with Type sourceTable.
        /// Input: Valid Type instances passed to constructor.
        /// Expected: TargetType property returns the same Type instance.
        /// </summary>
        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(object))]
        [InlineData(typeof(IDisposable))]
        [InlineData(typeof(DateTime))]
        public void TargetType_WithTypeSourceTableConstructor_ReturnsExpectedType(Type expectedType)
        {
            // Arrange
            var sourceTableType = typeof(object);
            var foreignKeys = "Id";

            // Act
            var attribute = new TableJoinAttribute(sourceTableType, expectedType, foreignKeys);

            // Assert
            Assert.Same(expectedType, attribute.TargetType);
        }

        /// <summary>
        /// Tests that TargetType property returns the Type passed to the two-parameter constructor.
        /// Input: Valid Type instances passed to constructor.
        /// Expected: TargetType property returns the same Type instance.
        /// </summary>
        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(object))]
        [InlineData(typeof(IDisposable))]
        [InlineData(typeof(DateTime))]
        public void TargetType_WithTwoParameterConstructor_ReturnsExpectedType(Type expectedType)
        {
            // Arrange
            var foreignKey = "Id";

            // Act
            var attribute = new TableJoinAttribute(expectedType, foreignKey);

            // Assert
            Assert.Same(expectedType, attribute.TargetType);
        }

        /// <summary>
        /// Tests that TargetType property returns null when null Type is passed to the constructor with string sourceTable.
        /// Input: Null Type passed to constructor.
        /// Expected: TargetType property returns null.
        /// </summary>
        [Fact]
        public void TargetType_WithStringSourceTableConstructorAndNullType_ReturnsNull()
        {
            // Arrange
            var sourceTable = "SourceTable";
            var foreignKeys = "Id";

            // Act
            var attribute = new TableJoinAttribute(sourceTable, null, foreignKeys);

            // Assert
            Assert.Null(attribute.TargetType);
        }

        /// <summary>
        /// Tests that TargetType property returns null when null Type is passed to the constructor with Type sourceTable.
        /// Input: Null Type passed to constructor.
        /// Expected: TargetType property returns null.
        /// </summary>
        [Fact]
        public void TargetType_WithTypeSourceTableConstructorAndNullType_ReturnsNull()
        {
            // Arrange
            var sourceTableType = typeof(object);
            var foreignKeys = "Id";

            // Act
            var attribute = new TableJoinAttribute(sourceTableType, null, foreignKeys);

            // Assert
            Assert.Null(attribute.TargetType);
        }

        /// <summary>
        /// Tests that TargetType property returns null when null Type is passed to the two-parameter constructor.
        /// Input: Null Type passed to constructor.
        /// Expected: TargetType property returns null.
        /// </summary>
        [Fact]
        public void TargetType_WithTwoParameterConstructorAndNullType_ReturnsNull()
        {
            // Arrange
            var foreignKey = "Id";

            // Act
            var attribute = new TableJoinAttribute(null, foreignKey);

            // Assert
            Assert.Null(attribute.TargetType);
        }

        /// <summary>
        /// Tests that TargetType property returns the correct Type for generic types.
        /// Input: Generic Type instances passed to constructor.
        /// Expected: TargetType property returns the same generic Type instance.
        /// </summary>
        [Theory]
        [InlineData(typeof(System.Collections.Generic.List<int>))]
        [InlineData(typeof(System.Collections.Generic.Dictionary<string, object>))]
        public void TargetType_WithGenericTypes_ReturnsExpectedType(Type expectedType)
        {
            // Arrange
            var foreignKey = "Id";

            // Act
            var attribute = new TableJoinAttribute(expectedType, foreignKey);

            // Assert
            Assert.Same(expectedType, attribute.TargetType);
        }

        /// <summary>
        /// Tests that TargetType property returns the correct Type for abstract types.
        /// Input: Abstract Type instances passed to constructor.
        /// Expected: TargetType property returns the same abstract Type instance.
        /// </summary>
        [Fact]
        public void TargetType_WithAbstractType_ReturnsExpectedType()
        {
            // Arrange
            var expectedType = typeof(System.IO.Stream);
            var foreignKey = "Id";

            // Act
            var attribute = new TableJoinAttribute(expectedType, foreignKey);

            // Assert
            Assert.Same(expectedType, attribute.TargetType);
        }

        /// <summary>
        /// Tests that the JoinType property returns the default value of TableJoinType.Left
        /// when a new instance is created using the constructor with string sourceTable parameter.
        /// </summary>
        [Fact]
        public void JoinType_DefaultValue_ReturnsLeft()
        {
            // Arrange & Act
            var attribute = new TableJoinAttribute("sourceTable", typeof(string), "foreignKey");

            // Assert
            Assert.Equal(TableJoinType.Left, attribute.JoinType);
        }

        /// <summary>
        /// Tests that the JoinType property returns the default value of TableJoinType.Left
        /// when a new instance is created using the constructor with Type sourceTable parameter.
        /// </summary>
        [Fact]
        public void JoinType_DefaultValueWithTypeSourceTable_ReturnsLeft()
        {
            // Arrange & Act
            var attribute = new TableJoinAttribute(typeof(object), typeof(string), "foreignKey");

            // Assert
            Assert.Equal(TableJoinType.Left, attribute.JoinType);
        }

        /// <summary>
        /// Tests that the JoinType property returns the default value of TableJoinType.Left
        /// when a new instance is created using the constructor with only targetType and foreignKey parameters.
        /// </summary>
        [Fact]
        public void JoinType_DefaultValueWithTwoParameterConstructor_ReturnsLeft()
        {
            // Arrange & Act
            var attribute = new TableJoinAttribute(typeof(string), "foreignKey");

            // Assert
            Assert.Equal(TableJoinType.Left, attribute.JoinType);
        }

        /// <summary>
        /// Tests that the JoinType property correctly sets and retrieves each defined TableJoinType enum value.
        /// </summary>
        /// <param name="joinType">The TableJoinType value to test.</param>
        [Theory]
        [InlineData(TableJoinType.Inner)]
        [InlineData(TableJoinType.Left)]
        [InlineData(TableJoinType.Right)]
        [InlineData(TableJoinType.Full)]
        [InlineData(TableJoinType.Cross)]
        public void JoinType_SetAndGet_ReturnsSetValue(TableJoinType joinType)
        {
            // Arrange
            var attribute = new TableJoinAttribute(typeof(string), "foreignKey");

            // Act
            attribute.JoinType = joinType;

            // Assert
            Assert.Equal(joinType, attribute.JoinType);
        }

        /// <summary>
        /// Tests that the JoinType property can be set to an undefined enum value (cast from int).
        /// This verifies that the property does not perform enum validation.
        /// </summary>
        [Fact]
        public void JoinType_SetUndefinedEnumValue_StoresAndReturnsValue()
        {
            // Arrange
            var attribute = new TableJoinAttribute(typeof(string), "foreignKey");
            var undefinedValue = (TableJoinType)999;

            // Act
            attribute.JoinType = undefinedValue;

            // Assert
            Assert.Equal(undefinedValue, attribute.JoinType);
        }

        /// <summary>
        /// Tests that the JoinType property can be set to the minimum integer value cast to TableJoinType.
        /// This tests the property's behavior with extreme enum values.
        /// </summary>
        [Fact]
        public void JoinType_SetMinIntValue_StoresAndReturnsValue()
        {
            // Arrange
            var attribute = new TableJoinAttribute(typeof(string), "foreignKey");
            var minValue = (TableJoinType)int.MinValue;

            // Act
            attribute.JoinType = minValue;

            // Assert
            Assert.Equal(minValue, attribute.JoinType);
        }

        /// <summary>
        /// Tests that the JoinType property can be set to the maximum integer value cast to TableJoinType.
        /// This tests the property's behavior with extreme enum values.
        /// </summary>
        [Fact]
        public void JoinType_SetMaxIntValue_StoresAndReturnsValue()
        {
            // Arrange
            var attribute = new TableJoinAttribute(typeof(string), "foreignKey");
            var maxValue = (TableJoinType)int.MaxValue;

            // Act
            attribute.JoinType = maxValue;

            // Assert
            Assert.Equal(maxValue, attribute.JoinType);
        }

        /// <summary>
        /// Tests that ForeignKeys property returns the value set in the constructor
        /// when initialized with a simple foreign key string through the three-parameter string constructor.
        /// </summary>
        /// <param name="foreignKeys">The foreign key string to test.</param>
        [Theory]
        [InlineData("UserId")]
        [InlineData("Key1,Key2")]
        [InlineData("Key1,Key2,Key3")]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("  \t\n  ")]
        [InlineData("Foreign_Key_With_Underscores")]
        [InlineData("Key-With-Dashes")]
        [InlineData("Key.With.Dots")]
        [InlineData("Key[With]Brackets")]
        [InlineData("Key1, Key2, Key3")]
        [InlineData("VeryLongForeignKeyNameThatExceedsNormalLengthToTestBoundaryConditionsAndEnsureThePropertyHandlesLongStringsCorrectly")]
        public void ForeignKeys_WhenSetViaStringSourceConstructor_ReturnsExpectedValue(string foreignKeys)
        {
            // Arrange
            var sourceTable = "SourceTable";
            var targetType = typeof(string);

            // Act
            var attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Assert
            Assert.Equal(foreignKeys, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that ForeignKeys property returns null when null is passed
        /// to the three-parameter string constructor.
        /// </summary>
        [Fact]
        public void ForeignKeys_WhenSetToNullViaStringSourceConstructor_ReturnsNull()
        {
            // Arrange
            var sourceTable = "SourceTable";
            var targetType = typeof(string);
            string? foreignKeys = null;

            // Act
            var attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Assert
            Assert.Null(attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that ForeignKeys property returns the value set in the constructor
        /// when initialized through the three-parameter Type constructor.
        /// </summary>
        /// <param name="foreignKeys">The foreign key string to test.</param>
        [Theory]
        [InlineData("UserId")]
        [InlineData("Key1,Key2")]
        [InlineData("Key1,Key2,Key3")]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("Foreign_Key_With_Underscores")]
        public void ForeignKeys_WhenSetViaTypeSourceConstructor_ReturnsExpectedValue(string foreignKeys)
        {
            // Arrange
            var sourceTable = typeof(object);
            var targetType = typeof(string);

            // Act
            var attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Assert
            Assert.Equal(foreignKeys, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that ForeignKeys property returns null when null is passed
        /// to the three-parameter Type constructor.
        /// </summary>
        [Fact]
        public void ForeignKeys_WhenSetToNullViaTypeSourceConstructor_ReturnsNull()
        {
            // Arrange
            var sourceTable = typeof(object);
            var targetType = typeof(string);
            string? foreignKeys = null;

            // Act
            var attribute = new TableJoinAttribute(sourceTable, targetType, foreignKeys);

            // Assert
            Assert.Null(attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that ForeignKeys property returns the value set in the constructor
        /// when initialized through the two-parameter constructor.
        /// </summary>
        /// <param name="foreignKey">The foreign key string to test.</param>
        [Theory]
        [InlineData("UserId")]
        [InlineData("Key1,Key2")]
        [InlineData("Key1,Key2,Key3")]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("Foreign_Key_With_Underscores")]
        public void ForeignKeys_WhenSetViaTwoParameterConstructor_ReturnsExpectedValue(string foreignKey)
        {
            // Arrange
            var targetType = typeof(string);

            // Act
            var attribute = new TableJoinAttribute(targetType, foreignKey);

            // Assert
            Assert.Equal(foreignKey, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that ForeignKeys property returns null when null is passed
        /// to the two-parameter constructor.
        /// </summary>
        [Fact]
        public void ForeignKeys_WhenSetToNullViaTwoParameterConstructor_ReturnsNull()
        {
            // Arrange
            var targetType = typeof(string);
            string? foreignKey = null;

            // Act
            var attribute = new TableJoinAttribute(targetType, foreignKey);

            // Assert
            Assert.Null(attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that ForeignKeys property returns special characters correctly
        /// when set through any constructor.
        /// </summary>
        /// <param name="foreignKeys">The foreign key string with special characters to test.</param>
        [Theory]
        [InlineData("Key@#$%")]
        [InlineData("Key\tWith\tTabs")]
        [InlineData("Key\nWith\nNewlines")]
        [InlineData("Key'With'Quotes")]
        [InlineData("Key\"With\"DoubleQuotes")]
        [InlineData("Key`With`Backticks")]
        [InlineData("Key;With;Semicolons")]
        [InlineData("🔑UnicodeKey")]
        public void ForeignKeys_WhenSetWithSpecialCharacters_ReturnsExpectedValue(string foreignKeys)
        {
            // Arrange
            var targetType = typeof(string);

            // Act
            var attribute = new TableJoinAttribute(targetType, foreignKeys);

            // Assert
            Assert.Equal(foreignKeys, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the  property correctly sets and gets various string values including edge cases.
        /// </summary>
        /// <param name="aliasName">The alias name value to test.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("TestAlias")]
        [InlineData("Alias@#$%^&*()")]
        [InlineData("VeryLongAliasNameThatExceedsNormalExpectationsAndCouldPotentiallyCauseIssuesWithBufferSizesOrMemoryAllocationIfNotHandledProperlyInTheUnderlyingImplementation")]
        [InlineData("Alias\nWith\nNewLines")]
        [InlineData("Alias\tWith\tTabs")]
        [InlineData("中文别名")]
        public void AliasName_SetAndGet_ReturnsSetValue(string? aliasName)
        {
            // Arrange
            var attribute = new TableJoinAttribute(typeof(string), "ForeignKey");

            // Act
            attribute.Alias = aliasName;
            var result = attribute.Alias;

            // Assert
            Assert.Equal(aliasName, result);
        }

        /// <summary>
        /// Tests that the  property returns null when not explicitly set.
        /// </summary>
        [Fact]
        public void AliasName_WhenNotSet_ReturnsNull()
        {
            // Arrange
            var attribute = new TableJoinAttribute(typeof(string), "ForeignKey");

            // Act
            var result = attribute.Alias;

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that the  property can be set multiple times and returns the latest value.
        /// </summary>
        [Fact]
        public void AliasName_SetMultipleTimes_ReturnsLatestValue()
        {
            // Arrange
            var attribute = new TableJoinAttribute(typeof(string), "ForeignKey");

            // Act
            attribute.Alias = "FirstAlias";
            attribute.Alias = "SecondAlias";
            attribute.Alias = "ThirdAlias";
            var result = attribute.Alias;

            // Assert
            Assert.Equal("ThirdAlias", result);
        }

        /// <summary>
        /// Tests that the  property can be set back to null after being set to a value.
        /// </summary>
        [Fact]
        public void AliasName_SetToNullAfterValue_ReturnsNull()
        {
            // Arrange
            var attribute = new TableJoinAttribute(typeof(string), "ForeignKey");
            attribute.Alias = "SomeAlias";

            // Act
            attribute.Alias = null;
            var result = attribute.Alias;

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes TargetType and ForeignKeys
        /// with valid non-null values.
        /// </summary>
        [Fact]
        public void TableJoinAttribute_WithValidTargetTypeAndForeignKey_SetsPropertiesCorrectly()
        {
            // Arrange
            Type expectedType = typeof(string);
            string expectedForeignKey = "UserId";

            // Act
            var attribute = new TableJoinAttribute(expectedType, expectedForeignKey);

            // Assert
            Assert.Equal(expectedType, attribute.TargetType);
            Assert.Equal(expectedForeignKey, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor accepts null for targetType parameter
        /// and stores it correctly.
        /// </summary>
        [Fact]
        public void TableJoinAttribute_WithNullTargetType_StoresNullValue()
        {
            // Arrange
            Type? targetType = null;
            string foreignKey = "UserId";

            // Act
            var attribute = new TableJoinAttribute(targetType, foreignKey);

            // Assert
            Assert.Null(attribute.TargetType);
            Assert.Equal(foreignKey, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor accepts null for foreignKey parameter
        /// and stores it correctly.
        /// </summary>
        [Fact]
        public void TableJoinAttribute_WithNullForeignKey_StoresNullValue()
        {
            // Arrange
            Type targetType = typeof(int);
            string? foreignKey = null;

            // Act
            var attribute = new TableJoinAttribute(targetType, foreignKey);

            // Assert
            Assert.Equal(targetType, attribute.TargetType);
            Assert.Null(attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor accepts both null parameters
        /// and stores them correctly.
        /// </summary>
        [Fact]
        public void TableJoinAttribute_WithBothParametersNull_StoresBothNullValues()
        {
            // Arrange
            Type? targetType = null;
            string? foreignKey = null;

            // Act
            var attribute = new TableJoinAttribute(targetType, foreignKey);

            // Assert
            Assert.Null(attribute.TargetType);
            Assert.Null(attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly handles empty string for foreignKey parameter.
        /// </summary>
        [Fact]
        public void TableJoinAttribute_WithEmptyForeignKey_StoresEmptyString()
        {
            // Arrange
            Type targetType = typeof(object);
            string foreignKey = string.Empty;

            // Act
            var attribute = new TableJoinAttribute(targetType, foreignKey);

            // Assert
            Assert.Equal(targetType, attribute.TargetType);
            Assert.Equal(string.Empty, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly handles whitespace-only string for foreignKey parameter.
        /// </summary>
        [Fact]
        public void TableJoinAttribute_WithWhitespaceForeignKey_StoresWhitespaceString()
        {
            // Arrange
            Type targetType = typeof(double);
            string foreignKey = "   ";

            // Act
            var attribute = new TableJoinAttribute(targetType, foreignKey);

            // Assert
            Assert.Equal(targetType, attribute.TargetType);
            Assert.Equal("   ", attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly handles composite foreign keys
        /// (comma-separated values).
        /// </summary>
        [Fact]
        public void TableJoinAttribute_WithCompositeForeignKey_StoresCommaSeparatedValue()
        {
            // Arrange
            Type targetType = typeof(DateTime);
            string foreignKey = "UserId,CompanyId";

            // Act
            var attribute = new TableJoinAttribute(targetType, foreignKey);

            // Assert
            Assert.Equal(targetType, attribute.TargetType);
            Assert.Equal("UserId,CompanyId", attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly handles foreign keys with special characters.
        /// </summary>
        [Theory]
        [InlineData("User_Id")]
        [InlineData("User-Id")]
        [InlineData("User.Id")]
        [InlineData("[UserId]")]
        [InlineData("`UserId`")]
        public void TableJoinAttribute_WithSpecialCharactersInForeignKey_StoresValue(string foreignKey)
        {
            // Arrange
            Type targetType = typeof(decimal);

            // Act
            var attribute = new TableJoinAttribute(targetType, foreignKey);

            // Assert
            Assert.Equal(targetType, attribute.TargetType);
            Assert.Equal(foreignKey, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly handles various Type values including
        /// value types, reference types, and generic types.
        /// </summary>
        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(string))]
        [InlineData(typeof(object))]
        [InlineData(typeof(DateTime))]
        [InlineData(typeof(TableJoinAttribute))]
        public void TableJoinAttribute_WithVariousTypeValues_StoresTypeCorrectly(Type targetType)
        {
            // Arrange
            string foreignKey = "TestKey";

            // Act
            var attribute = new TableJoinAttribute(targetType, foreignKey);

            // Assert
            Assert.Equal(targetType, attribute.TargetType);
            Assert.Equal(foreignKey, attribute.ForeignKeys);
        }

        /// <summary>
        /// Tests that the constructor correctly handles very long foreign key strings.
        /// </summary>
        [Fact]
        public void TableJoinAttribute_WithVeryLongForeignKey_StoresLongString()
        {
            // Arrange
            Type targetType = typeof(string);
            string foreignKey = new string('A', 10000);

            // Act
            var attribute = new TableJoinAttribute(targetType, foreignKey);

            // Assert
            Assert.Equal(targetType, attribute.TargetType);
            Assert.Equal(foreignKey, attribute.ForeignKeys);
            Assert.Equal(10000, attribute.ForeignKeys.Length);
        }
    }
}