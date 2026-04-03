using System;

using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the ColumnRef class.
    /// </summary>
    public partial class ColumnRefTests
    {
        /// <summary>
        /// Tests that GetHashCode returns consistent hash code when called multiple times on the same instance.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsSameValue()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (object)typeof(string).GetProperty("Length"));
            mockColumn.Setup(c => c.GetHashCode()).Returns(123);
            var columnRef = new ColumnRef(mockColumn.Object);

            // Act
            int hashCode1 = columnRef.GetHashCode();
            int hashCode2 = columnRef.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns correct hash code when Table is null and Column is non-null.
        /// </summary>
        [Fact]
        public void GetHashCode_WithNullTableAndNonNullColumn_ReturnsCorrectHashCode()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (object)typeof(string).GetProperty("Length"));
            mockColumn.Setup(c => c.GetHashCode()).Returns(456);
            var columnRef = new ColumnRef(mockColumn.Object);

            // Act
            int hashCode = columnRef.GetHashCode();

            // Assert
            int expectedHashCode = ((0 * 31) ^ 456);
            Assert.Equal(expectedHashCode, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns correct hash code when both Table and Column are non-null.
        /// </summary>
        [Fact]
        public void GetHashCode_WithNonNullTableAndColumn_ReturnsCorrectHashCode()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (object)typeof(string).GetProperty("Length"));
            mockColumn.Setup(c => c.GetHashCode()).Returns(789);
            var mockTableDefinition = new Mock<TableDefinition>(MockBehavior.Strict, typeof(object));
            var mockTable = new Mock<TableRef>(MockBehavior.Strict, mockTableDefinition.Object);
            mockTable.Setup(t => t.GetHashCode()).Returns(321);
            var columnRef = new ColumnRef(mockTable.Object, mockColumn.Object);

            // Act
            int hashCode = columnRef.GetHashCode();

            // Assert
            int expectedHashCode = unchecked(((321 * 31) ^ 789));
            Assert.Equal(expectedHashCode, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns equal hash codes for equal ColumnRef objects.
        /// </summary>
        [Fact]
        public void GetHashCode_ForEqualObjects_ReturnsEqualHashCodes()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (object)typeof(string).GetProperty("Length"));
            mockColumn.Setup(c => c.GetHashCode()).Returns(555);
            mockColumn.Setup(c => c.Equals(It.IsAny<object>())).Returns(true);
            var mockTableDefinition = new Mock<TableDefinition>(MockBehavior.Strict, typeof(object));
            var mockTable = new Mock<TableRef>(MockBehavior.Strict, mockTableDefinition.Object);
            mockTable.Setup(t => t.GetHashCode()).Returns(666);
            mockTable.Setup(t => t.Equals(It.IsAny<object>())).Returns(true);
            var columnRef1 = new ColumnRef(mockTable.Object, mockColumn.Object);
            var columnRef2 = new ColumnRef(mockTable.Object, mockColumn.Object);

            // Act
            int hashCode1 = columnRef1.GetHashCode();
            int hashCode2 = columnRef2.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode handles integer overflow correctly using unchecked arithmetic.
        /// </summary>
        [Fact]
        public void GetHashCode_WithLargeHashCodes_HandlesOverflowCorrectly()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (object)typeof(string).GetProperty("Length"));
            mockColumn.Setup(c => c.GetHashCode()).Returns(int.MaxValue);
            var mockTableDefinition = new Mock<TableDefinition>(MockBehavior.Strict, typeof(object));
            var mockTable = new Mock<TableRef>(MockBehavior.Strict, mockTableDefinition.Object);
            mockTable.Setup(t => t.GetHashCode()).Returns(int.MaxValue);
            var columnRef = new ColumnRef(mockTable.Object, mockColumn.Object);

            // Act
            int hashCode = columnRef.GetHashCode();

            // Assert
            int expectedHashCode = unchecked(((int.MaxValue * 31) ^ int.MaxValue));
            Assert.Equal(expectedHashCode, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes for ColumnRef objects with different Columns.
        /// </summary>
        [Fact]
        public void GetHashCode_WithDifferentColumns_ReturnsDifferentHashCodes()
        {
            // Arrange
            var mockColumn1 = new Mock<SqlColumn>(MockBehavior.Strict, (object)typeof(string).GetProperty("Length"));
            mockColumn1.Setup(c => c.GetHashCode()).Returns(100);
            var mockColumn2 = new Mock<SqlColumn>(MockBehavior.Strict, (object)typeof(string).GetProperty("Length"));
            mockColumn2.Setup(c => c.GetHashCode()).Returns(200);
            var columnRef1 = new ColumnRef(mockColumn1.Object);
            var columnRef2 = new ColumnRef(mockColumn2.Object);

            // Act
            int hashCode1 = columnRef1.GetHashCode();
            int hashCode2 = columnRef2.GetHashCode();

            // Assert
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes for ColumnRef objects with different Tables.
        /// </summary>
        [Fact]
        public void GetHashCode_WithDifferentTables_ReturnsDifferentHashCodes()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (object)typeof(string).GetProperty("Length"));
            mockColumn.Setup(c => c.GetHashCode()).Returns(500);
            var mockTableDefinition1 = new Mock<TableDefinition>(MockBehavior.Strict, typeof(object));
            var mockTable1 = new Mock<TableRef>(MockBehavior.Strict, mockTableDefinition1.Object);
            mockTable1.Setup(t => t.GetHashCode()).Returns(111);
            var mockTableDefinition2 = new Mock<TableDefinition>(MockBehavior.Strict, typeof(object));
            var mockTable2 = new Mock<TableRef>(MockBehavior.Strict, mockTableDefinition2.Object);
            mockTable2.Setup(t => t.GetHashCode()).Returns(222);
            var columnRef1 = new ColumnRef(mockTable1.Object, mockColumn.Object);
            var columnRef2 = new ColumnRef(mockTable2.Object, mockColumn.Object);

            // Act
            int hashCode1 = columnRef1.GetHashCode();
            int hashCode2 = columnRef2.GetHashCode();

            // Assert
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode correctly handles zero hash codes from both Table and Column.
        /// </summary>
        [Fact]
        public void GetHashCode_WithZeroHashCodes_ReturnsZero()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (object)typeof(string).GetProperty("Length"));
            mockColumn.Setup(c => c.GetHashCode()).Returns(0);
            var mockTableDefinition = new Mock<TableDefinition>(MockBehavior.Strict, typeof(object));
            var mockTable = new Mock<TableRef>(MockBehavior.Strict, mockTableDefinition.Object);
            mockTable.Setup(t => t.GetHashCode()).Returns(0);
            var columnRef = new ColumnRef(mockTable.Object, mockColumn.Object);

            // Act
            int hashCode = columnRef.GetHashCode();

            // Assert
            Assert.Equal(0, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode correctly handles negative hash codes.
        /// </summary>
        [Fact]
        public void GetHashCode_WithNegativeHashCodes_ReturnsCorrectValue()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (object)typeof(string).GetProperty("Length"));
            mockColumn.Setup(c => c.GetHashCode()).Returns(-123);
            var mockTableDefinition = new Mock<TableDefinition>(MockBehavior.Strict, typeof(object));
            var mockTable = new Mock<TableRef>(MockBehavior.Strict, mockTableDefinition.Object);
            mockTable.Setup(t => t.GetHashCode()).Returns(-456);
            var columnRef = new ColumnRef(mockTable.Object, mockColumn.Object);

            // Act
            int hashCode = columnRef.GetHashCode();

            // Assert
            int expectedHashCode = unchecked(((-456 * 31) ^ -123));
            Assert.Equal(expectedHashCode, hashCode);
        }

        /// <summary>
        /// Tests that the constructor with table and column parameters correctly initializes
        /// all properties when provided with valid inputs.
        /// </summary>
        [Fact]
        public void ColumnRef_WithTableAndColumn_InitializesCorrectly()
        {
            // Arrange
            var mockTable = new Mock<TableRef>(Mock.Of<TableDefinition>());
            var mockColumn = new Mock<SqlColumn>(Mock.Of<System.Reflection.PropertyInfo>());
            mockColumn.SetupGet(c => c.Name).Returns("TestColumn");

            // Act
            var columnRef = new ColumnRef(mockTable.Object, mockColumn.Object);

            // Assert
            Assert.NotNull(columnRef);
            Assert.Equal("TestColumn", columnRef.Name);
            Assert.Same(mockColumn.Object, columnRef.Column);
            Assert.Same(mockTable.Object, columnRef.Table);
        }

        /// <summary>
        /// Tests that the constructor throws NullReferenceException when the column parameter is null,
        /// as it attempts to access column.Name.
        /// </summary>
        [Fact]
        public void ColumnRef_WithNullColumn_ThrowsNullReferenceException()
        {
            // Arrange
            var mockTable = new Mock<TableRef>(Mock.Of<TableDefinition>());

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => new ColumnRef(mockTable.Object, null!));
        }

        /// <summary>
        /// Tests that the constructor accepts a null table parameter and stores it,
        /// as there is no validation preventing null table assignment.
        /// </summary>
        [Fact]
        public void ColumnRef_WithNullTable_StoresNullTable()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(Mock.Of<System.Reflection.PropertyInfo>());
            mockColumn.SetupGet(c => c.Name).Returns("TestColumn");

            // Act
            var columnRef = new ColumnRef(null!, mockColumn.Object);

            // Assert
            Assert.NotNull(columnRef);
            Assert.Equal("TestColumn", columnRef.Name);
            Assert.Same(mockColumn.Object, columnRef.Column);
            Assert.Null(columnRef.Table);
        }

        /// <summary>
        /// Tests that the constructor correctly handles a column with a null Name property,
        /// storing null as the ColumnRef's Name.
        /// </summary>
        [Fact]
        public void ColumnRef_WithColumnNameNull_StoresNullName()
        {
            // Arrange
            var mockTable = new Mock<TableRef>(Mock.Of<TableDefinition>());
            var mockColumn = new Mock<SqlColumn>(Mock.Of<System.Reflection.PropertyInfo>());
            mockColumn.SetupGet(c => c.Name).Returns((string?)null);

            // Act
            var columnRef = new ColumnRef(mockTable.Object, mockColumn.Object);

            // Assert
            Assert.NotNull(columnRef);
            Assert.Null(columnRef.Name);
            Assert.Same(mockColumn.Object, columnRef.Column);
            Assert.Same(mockTable.Object, columnRef.Table);
        }

        /// <summary>
        /// Tests that the constructor correctly handles various edge case string values for the column name,
        /// including empty strings, whitespace, and special characters.
        /// </summary>
        /// <param name="columnName">The column name to test.</param>
        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("  \t\n")]
        [InlineData("Column-With-Special!@#$%^&*()Characters")]
        [InlineData("很长的列名")]
        [InlineData("Column\nWith\nNewlines")]
        [InlineData("Column\u0000WithNull")]
        public void ColumnRef_WithVariousColumnNames_StoresCorrectly(string columnName)
        {
            // Arrange
            var mockTable = new Mock<TableRef>(Mock.Of<TableDefinition>());
            var mockColumn = new Mock<SqlColumn>(Mock.Of<System.Reflection.PropertyInfo>());
            mockColumn.SetupGet(c => c.Name).Returns(columnName);

            // Act
            var columnRef = new ColumnRef(mockTable.Object, mockColumn.Object);

            // Assert
            Assert.NotNull(columnRef);
            Assert.Equal(columnName, columnRef.Name);
            Assert.Same(mockColumn.Object, columnRef.Column);
            Assert.Same(mockTable.Object, columnRef.Table);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes Name and Column properties
        /// when provided with a valid SqlColumn instance.
        /// </summary>
        [Theory]
        [InlineData("ValidColumnName")]
        [InlineData("Column_With_Underscore")]
        [InlineData("Column123")]
        [InlineData("A")]
        [InlineData("VeryLongColumnNameThatExceedsTypicalLengthButIsStillValidAccordingToMostDatabaseStandards")]
        public void Constructor_WithValidColumn_SetsNameAndColumnCorrectly(string columnName)
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (System.Reflection.PropertyInfo)null!);
            mockColumn.Setup(c => c.Name).Returns(columnName);

            // Act
            var columnRef = new ColumnRef(mockColumn.Object);

            // Assert
            Assert.Equal(columnName, columnRef.Name);
            Assert.Same(mockColumn.Object, columnRef.Column);
        }

        /// <summary>
        /// Tests that the constructor correctly handles SqlColumn with empty string Name.
        /// Edge case: Empty string as column name.
        /// Expected: The ColumnRef.Name should be set to empty string.
        /// </summary>
        [Fact]
        public void Constructor_WithEmptyStringColumnName_SetsNameToEmptyString()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (System.Reflection.PropertyInfo)null!);
            mockColumn.Setup(c => c.Name).Returns(string.Empty);

            // Act
            var columnRef = new ColumnRef(mockColumn.Object);

            // Assert
            Assert.Equal(string.Empty, columnRef.Name);
            Assert.Same(mockColumn.Object, columnRef.Column);
        }

        /// <summary>
        /// Tests that the constructor correctly handles SqlColumn with whitespace-only Name.
        /// Edge case: Whitespace-only string as column name.
        /// Expected: The ColumnRef.Name should be set to the whitespace string.
        /// </summary>
        [Theory]
        [InlineData(" ")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void Constructor_WithWhitespaceColumnName_SetsNameToWhitespace(string whitespaceName)
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (System.Reflection.PropertyInfo)null!);
            mockColumn.Setup(c => c.Name).Returns(whitespaceName);

            // Act
            var columnRef = new ColumnRef(mockColumn.Object);

            // Assert
            Assert.Equal(whitespaceName, columnRef.Name);
            Assert.Same(mockColumn.Object, columnRef.Column);
        }

        /// <summary>
        /// Tests that the constructor correctly handles SqlColumn with special characters in Name.
        /// Edge case: Special characters as column name.
        /// Expected: The ColumnRef.Name should be set to the special characters string.
        /// </summary>
        [Theory]
        [InlineData("Column@Name")]
        [InlineData("Column#123")]
        [InlineData("Column$Value")]
        [InlineData("Column%Percent")]
        [InlineData("Column&And")]
        [InlineData("Column-With-Dashes")]
        [InlineData("Column.With.Dots")]
        public void Constructor_WithSpecialCharactersInColumnName_SetsNameCorrectly(string specialName)
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (System.Reflection.PropertyInfo)null!);
            mockColumn.Setup(c => c.Name).Returns(specialName);

            // Act
            var columnRef = new ColumnRef(mockColumn.Object);

            // Assert
            Assert.Equal(specialName, columnRef.Name);
            Assert.Same(mockColumn.Object, columnRef.Column);
        }

        /// <summary>
        /// Tests that the constructor throws NullReferenceException when column parameter is null.
        /// Edge case: Null column parameter.
        /// Expected: NullReferenceException should be thrown when accessing column.Name.
        /// </summary>
        [Fact]
        public void Constructor_WithNullColumn_ThrowsNullReferenceException()
        {
            // Arrange
            SqlColumn? nullColumn = null;

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => new ColumnRef(nullColumn!));
        }

        /// <summary>
        /// Tests that Equals returns true when comparing an object to itself (same reference).
        /// </summary>
        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.Setup(c => c.Name).Returns("TestColumn");
            var columnRef = new ColumnRef(mockColumn.Object);

            // Act
            var result = columnRef.Equals(columnRef);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing to a null object.
        /// </summary>
        [Fact]
        public void Equals_NullObject_ReturnsFalse()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.Setup(c => c.Name).Returns("TestColumn");
            var columnRef = new ColumnRef(mockColumn.Object);

            // Act
            var result = columnRef.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing to objects of different types.
        /// </summary>
        /// <param name="otherObject">The object of different type to compare against.</param>
        [Theory]
        [InlineData("string")]
        [InlineData(42)]
        [InlineData(true)]
        public void Equals_DifferentType_ReturnsFalse(object otherObject)
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.Setup(c => c.Name).Returns("TestColumn");
            var columnRef = new ColumnRef(mockColumn.Object);

            // Act
            var result = columnRef.Equals(otherObject);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when two ColumnRef instances have equal Table and Column properties.
        /// </summary>
        [Fact]
        public void Equals_EqualTableAndColumn_ReturnsTrue()
        {
            // Arrange
            var mockTable = new Mock<TableRef>();
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.Setup(c => c.Name).Returns("TestColumn");

            var columnRef1 = new ColumnRef(mockTable.Object, mockColumn.Object);
            var columnRef2 = new ColumnRef(mockTable.Object, mockColumn.Object);

            // Act
            var result = columnRef1.Equals(columnRef2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when Column properties differ.
        /// </summary>
        [Fact]
        public void Equals_DifferentColumn_ReturnsFalse()
        {
            // Arrange
            var mockTable = new Mock<TableRef>();
            var mockColumn1 = new Mock<SqlColumn>();
            mockColumn1.Setup(c => c.Name).Returns("Column1");
            var mockColumn2 = new Mock<SqlColumn>();
            mockColumn2.Setup(c => c.Name).Returns("Column2");

            var columnRef1 = new ColumnRef(mockTable.Object, mockColumn1.Object);
            var columnRef2 = new ColumnRef(mockTable.Object, mockColumn2.Object);

            // Act
            var result = columnRef1.Equals(columnRef2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when Table properties differ.
        /// </summary>
        [Fact]
        public void Equals_DifferentTable_ReturnsFalse()
        {
            // Arrange
            var mockTable1 = new Mock<TableRef>();
            var mockTable2 = new Mock<TableRef>();
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.Setup(c => c.Name).Returns("TestColumn");

            var columnRef1 = new ColumnRef(mockTable1.Object, mockColumn.Object);
            var columnRef2 = new ColumnRef(mockTable2.Object, mockColumn.Object);

            // Act
            var result = columnRef1.Equals(columnRef2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when both Table and Column properties differ.
        /// </summary>
        [Fact]
        public void Equals_DifferentTableAndColumn_ReturnsFalse()
        {
            // Arrange
            var mockTable1 = new Mock<TableRef>();
            var mockTable2 = new Mock<TableRef>();
            var mockColumn1 = new Mock<SqlColumn>();
            mockColumn1.Setup(c => c.Name).Returns("Column1");
            var mockColumn2 = new Mock<SqlColumn>();
            mockColumn2.Setup(c => c.Name).Returns("Column2");

            var columnRef1 = new ColumnRef(mockTable1.Object, mockColumn1.Object);
            var columnRef2 = new ColumnRef(mockTable2.Object, mockColumn2.Object);

            // Act
            var result = columnRef1.Equals(columnRef2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both ColumnRef instances have null Table and same Column.
        /// </summary>
        [Fact]
        public void Equals_BothNullTableSameColumn_ReturnsTrue()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.Setup(c => c.Name).Returns("TestColumn");

            var columnRef1 = new ColumnRef(mockColumn.Object);
            var columnRef2 = new ColumnRef(mockColumn.Object);

            // Act
            var result = columnRef1.Equals(columnRef2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one ColumnRef has null Table and the other has non-null Table.
        /// </summary>
        [Fact]
        public void Equals_OneNullTableOtherNonNull_ReturnsFalse()
        {
            // Arrange
            var mockTable = new Mock<TableRef>();
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.Setup(c => c.Name).Returns("TestColumn");

            var columnRef1 = new ColumnRef(mockColumn.Object);
            var columnRef2 = new ColumnRef(mockTable.Object, mockColumn.Object);

            // Act
            var result = columnRef1.Equals(columnRef2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when the other ColumnRef has null Table while current has non-null Table.
        /// </summary>
        [Fact]
        public void Equals_CurrentNonNullTableOtherNull_ReturnsFalse()
        {
            // Arrange
            var mockTable = new Mock<TableRef>();
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.Setup(c => c.Name).Returns("TestColumn");

            var columnRef1 = new ColumnRef(mockTable.Object, mockColumn.Object);
            var columnRef2 = new ColumnRef(mockColumn.Object);

            // Act
            var result = columnRef1.Equals(columnRef2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that the Table property returns null when ColumnRef is created 
        /// with the single-parameter constructor.
        /// </summary>
        [Fact]
        public void Table_WhenCreatedWithoutTable_ReturnsNull()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.Setup(c => c.Name).Returns("TestColumn");

            // Act
            var columnRef = new ColumnRef(mockColumn.Object);

            // Assert
            Assert.Null(columnRef.Table);
        }

        /// <summary>
        /// Tests that the Table property returns the correct TableRef when ColumnRef 
        /// is created with the two-parameter constructor.
        /// </summary>
        [Fact]
        public void Table_WhenCreatedWithTable_ReturnsTable()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(t => t.Name).Returns("TestTable");
            mockTableDefinition.Setup(t => t.Columns).Returns(new System.Collections.Generic.List<SqlColumn>());

            var mockTable = new Mock<TableRef>(mockTableDefinition.Object);
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.Setup(c => c.Name).Returns("TestColumn");

            // Act
            var columnRef = new ColumnRef(mockTable.Object, mockColumn.Object);

            // Assert
            Assert.Same(mockTable.Object, columnRef.Table);
        }

        /// <summary>
        /// Tests that the Table property can be set to a valid TableRef value 
        /// using the internal setter and the value is correctly retrieved.
        /// </summary>
        [Fact]
        public void Table_WhenSetToValidValue_ReturnsSetValue()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.Setup(c => c.Name).Returns("TestColumn");
            var columnRef = new ColumnRef(mockColumn.Object);

            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(t => t.Name).Returns("TestTable");
            mockTableDefinition.Setup(t => t.Columns).Returns(new System.Collections.Generic.List<SqlColumn>());

            var mockTable = new Mock<TableRef>(mockTableDefinition.Object);

            // Act
            columnRef.Table = mockTable.Object;

            // Assert
            Assert.Same(mockTable.Object, columnRef.Table);
        }

        /// <summary>
        /// Tests that the Table property can be set to null using the internal setter.
        /// </summary>
        [Fact]
        public void Table_WhenSetToNull_ReturnsNull()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(t => t.Name).Returns("TestTable");
            mockTableDefinition.Setup(t => t.Columns).Returns(new System.Collections.Generic.List<SqlColumn>());

            var mockTable = new Mock<TableRef>(mockTableDefinition.Object);
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.Setup(c => c.Name).Returns("TestColumn");

            var columnRef = new ColumnRef(mockTable.Object, mockColumn.Object);

            // Act
            columnRef.Table = null;

            // Assert
            Assert.Null(columnRef.Table);
        }

        /// <summary>
        /// Tests that the Table property can be updated multiple times and 
        /// each update is correctly reflected.
        /// </summary>
        [Fact]
        public void Table_WhenSetMultipleTimes_ReturnsLastSetValue()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.Setup(c => c.Name).Returns("TestColumn");
            var columnRef = new ColumnRef(mockColumn.Object);

            var mockTableDefinition1 = new Mock<TableDefinition>();
            mockTableDefinition1.Setup(t => t.Name).Returns("TestTable1");
            mockTableDefinition1.Setup(t => t.Columns).Returns(new System.Collections.Generic.List<SqlColumn>());
            var mockTable1 = new Mock<TableRef>(mockTableDefinition1.Object);

            var mockTableDefinition2 = new Mock<TableDefinition>();
            mockTableDefinition2.Setup(t => t.Name).Returns("TestTable2");
            mockTableDefinition2.Setup(t => t.Columns).Returns(new System.Collections.Generic.List<SqlColumn>());
            var mockTable2 = new Mock<TableRef>(mockTableDefinition2.Object);

            // Act
            columnRef.Table = mockTable1.Object;
            var firstValue = columnRef.Table;
            columnRef.Table = mockTable2.Object;
            var secondValue = columnRef.Table;

            // Assert
            Assert.Same(mockTable1.Object, firstValue);
            Assert.Same(mockTable2.Object, secondValue);
        }

        /// <summary>
        /// Tests that the Column property returns the same SqlColumn instance that was passed to the constructor with single parameter.
        /// </summary>
        [Fact]
        public void Column_WhenInitializedWithSingleParameterConstructor_ReturnsSameSqlColumnInstance()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (object)null);
            mockColumn.SetupGet(c => c.Name).Returns("TestColumn");
            var column = mockColumn.Object;
            var columnRef = new ColumnRef(column);

            // Act
            var result = columnRef.Column;

            // Assert
            Assert.Same(column, result);
        }

        /// <summary>
        /// Tests that the Column property returns the same SqlColumn instance that was passed to the constructor with table and column parameters.
        /// </summary>
        [Fact]
        public void Column_WhenInitializedWithTwoParameterConstructor_ReturnsSameSqlColumnInstance()
        {
            // Arrange
            var mockTable = new Mock<TableRef>(MockBehavior.Strict, (object)null);
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (object)null);
            mockColumn.SetupGet(c => c.Name).Returns("TestColumn");
            var table = mockTable.Object;
            var column = mockColumn.Object;
            var columnRef = new ColumnRef(table, column);

            // Act
            var result = columnRef.Column;

            // Assert
            Assert.Same(column, result);
        }

        /// <summary>
        /// Tests that the Column property consistently returns the same instance across multiple accesses.
        /// </summary>
        [Fact]
        public void Column_WhenAccessedMultipleTimes_ReturnsSameInstance()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, (object)null);
            mockColumn.SetupGet(c => c.Name).Returns("TestColumn");
            var column = mockColumn.Object;
            var columnRef = new ColumnRef(column);

            // Act
            var result1 = columnRef.Column;
            var result2 = columnRef.Column;
            var result3 = columnRef.Column;

            // Assert
            Assert.Same(result1, result2);
            Assert.Same(result2, result3);
            Assert.Same(column, result1);
        }
    }
}