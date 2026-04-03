using System;
using System.Reflection;

using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Tests for the ForeignColumn class.
    /// </summary>
    public partial class ForeignColumnTests
    {
        /// <summary>
        /// Tests that Definition property returns the Definition from TargetColumn.Column.Definition when all properties are properly initialized.
        /// Input: ForeignColumn with properly initialized TargetColumn, Column, and Definition.
        /// Expected: Returns the expected ColumnDefinition instance.
        /// </summary>
        [Fact]
        public void Definition_WithValidTargetColumn_ReturnsTargetColumnDefinition()
        {
            // Arrange
            var propertyInfo = typeof(TestEntity).GetProperty(nameof(TestEntity.TestProperty));
            var foreignColumn = (ForeignColumn)Activator.CreateInstance(
                typeof(ForeignColumn),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { propertyInfo },
                null);

            var mockSqlColumn = new Mock<SqlColumn>(propertyInfo);
            var mockDefinition = new Mock<ColumnDefinition>(propertyInfo);
            mockSqlColumn.Setup(c => c.Definition).Returns(mockDefinition.Object);

            var columnRef = new ColumnRef(mockSqlColumn.Object);

            typeof(ForeignColumn)
                .GetProperty(nameof(ForeignColumn.TargetColumn))
                .SetValue(foreignColumn, columnRef);

            // Act
            var result = foreignColumn.Definition;

            // Assert
            Assert.NotNull(result);
            Assert.Same(mockDefinition.Object, result);
        }

        /// <summary>
        /// Tests that Definition property throws NullReferenceException when TargetColumn is null.
        /// Input: ForeignColumn with TargetColumn set to null.
        /// Expected: Throws NullReferenceException.
        /// </summary>
        [Fact]
        public void Definition_WithNullTargetColumn_ThrowsNullReferenceException()
        {
            // Arrange
            var propertyInfo = typeof(TestEntity).GetProperty(nameof(TestEntity.TestProperty));
            var foreignColumn = (ForeignColumn)Activator.CreateInstance(
                typeof(ForeignColumn),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { propertyInfo },
                null);

            typeof(ForeignColumn)
                .GetProperty(nameof(ForeignColumn.TargetColumn))
                .SetValue(foreignColumn, null);

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => foreignColumn.Definition);
        }

        /// <summary>
        /// Helper entity class used for creating PropertyInfo instances in tests.
        /// </summary>
        private class TestEntity
        {
            public string TestProperty { get; set; }
        }

        /// <summary>
        /// Tests that the Name property returns null when TargetColumn is null.
        /// </summary>
        [Fact]
        public void Name_WhenTargetColumnIsNull_ReturnsNull()
        {
            // Arrange
            PropertyInfo testProperty = typeof(TestEntity).GetProperty(nameof(TestEntity.Id))!;
            ForeignColumn foreignColumn = new ForeignColumn(testProperty);

            // Act
            string? result = foreignColumn.Name;

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that the Name property returns the TargetColumn's Name when TargetColumn is not null.
        /// </summary>
        /// <param name="expectedName">The expected name value to test.</param>
        [Theory]
        [InlineData("TestColumn")]
        [InlineData("ID")]
        [InlineData("Column_With_Underscore")]
        [InlineData("Column123")]
        public void Name_WhenTargetColumnIsNotNullWithValidName_ReturnsTargetColumnName(string expectedName)
        {
            // Arrange
            PropertyInfo testProperty = typeof(TestEntity).GetProperty(nameof(TestEntity.Id))!;
            ForeignColumn foreignColumn = new ForeignColumn(testProperty);

            Mock<SqlColumn> mockSqlColumn = new Mock<SqlColumn>(testProperty);
            mockSqlColumn.SetupGet(c => c.Name).Returns(expectedName);

            ColumnRef targetColumn = new ColumnRef(mockSqlColumn.Object);
            foreignColumn.TargetColumn = targetColumn;

            // Act
            string? result = foreignColumn.Name;

            // Assert
            Assert.Equal(expectedName, result);
        }

        /// <summary>
        /// Tests that the Name property returns null when TargetColumn's Name is null.
        /// </summary>
        [Fact]
        public void Name_WhenTargetColumnNameIsNull_ReturnsNull()
        {
            // Arrange
            PropertyInfo testProperty = typeof(TestEntity).GetProperty(nameof(TestEntity.Id))!;
            ForeignColumn foreignColumn = new ForeignColumn(testProperty);

            Mock<SqlColumn> mockSqlColumn = new Mock<SqlColumn>(testProperty);
            mockSqlColumn.SetupGet(c => c.Name).Returns((string?)null);

            ColumnRef targetColumn = new ColumnRef(mockSqlColumn.Object);
            foreignColumn.TargetColumn = targetColumn;

            // Act
            string? result = foreignColumn.Name;

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that the Name property returns an empty string when TargetColumn's Name is empty.
        /// </summary>
        [Fact]
        public void Name_WhenTargetColumnNameIsEmpty_ReturnsEmptyString()
        {
            // Arrange
            PropertyInfo testProperty = typeof(TestEntity).GetProperty(nameof(TestEntity.Id))!;
            ForeignColumn foreignColumn = new ForeignColumn(testProperty);

            Mock<SqlColumn> mockSqlColumn = new Mock<SqlColumn>(testProperty);
            mockSqlColumn.SetupGet(c => c.Name).Returns(string.Empty);

            ColumnRef targetColumn = new ColumnRef(mockSqlColumn.Object);
            foreignColumn.TargetColumn = targetColumn;

            // Act
            string? result = foreignColumn.Name;

            // Assert
            Assert.Equal(string.Empty, result);
        }

        /// <summary>
        /// Tests that the Name property returns whitespace when TargetColumn's Name is whitespace.
        /// </summary>
        /// <param name="whitespaceName">The whitespace string to test.</param>
        [Theory]
        [InlineData(" ")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void Name_WhenTargetColumnNameIsWhitespace_ReturnsWhitespace(string whitespaceName)
        {
            // Arrange
            PropertyInfo testProperty = typeof(TestEntity).GetProperty(nameof(TestEntity.Id))!;
            ForeignColumn foreignColumn = new ForeignColumn(testProperty);

            Mock<SqlColumn> mockSqlColumn = new Mock<SqlColumn>(testProperty);
            mockSqlColumn.SetupGet(c => c.Name).Returns(whitespaceName);

            ColumnRef targetColumn = new ColumnRef(mockSqlColumn.Object);
            foreignColumn.TargetColumn = targetColumn;

            // Act
            string? result = foreignColumn.Name;

            // Assert
            Assert.Equal(whitespaceName, result);
        }

        /// <summary>
        /// Tests that the Name property returns special characters when TargetColumn's Name contains special characters.
        /// </summary>
        /// <param name="specialName">The name with special characters to test.</param>
        [Theory]
        [InlineData("Column!@#$%")]
        [InlineData("Column<>?")]
        [InlineData("Column[]{};")]
        [InlineData("列名")]
        public void Name_WhenTargetColumnNameHasSpecialCharacters_ReturnsSpecialCharacters(string specialName)
        {
            // Arrange
            PropertyInfo testProperty = typeof(TestEntity).GetProperty(nameof(TestEntity.Id))!;
            ForeignColumn foreignColumn = new ForeignColumn(testProperty);

            Mock<SqlColumn> mockSqlColumn = new Mock<SqlColumn>(testProperty);
            mockSqlColumn.SetupGet(c => c.Name).Returns(specialName);

            ColumnRef targetColumn = new ColumnRef(mockSqlColumn.Object);
            foreignColumn.TargetColumn = targetColumn;

            // Act
            string? result = foreignColumn.Name;

            // Assert
            Assert.Equal(specialName, result);
        }

        /// <summary>
        /// Tests that setting the Name property does not throw and does not change the value.
        /// </summary>
        [Fact]
        public void Name_Setter_DoesNotThrowAndDoesNotChangeValue()
        {
            // Arrange
            PropertyInfo testProperty = typeof(TestEntity).GetProperty(nameof(TestEntity.Id))!;
            ForeignColumn foreignColumn = new ForeignColumn(testProperty);

            Mock<SqlColumn> mockSqlColumn = new Mock<SqlColumn>(testProperty);
            mockSqlColumn.SetupGet(c => c.Name).Returns("OriginalName");

            ColumnRef targetColumn = new ColumnRef(mockSqlColumn.Object);
            foreignColumn.TargetColumn = targetColumn;

            string? originalName = foreignColumn.Name;

            // Act
            foreignColumn.Name = "NewName";

            // Assert
            Assert.Equal(originalName, foreignColumn.Name);
        }

        /// <summary>
        /// Tests that setting the Name property to null does not throw when TargetColumn is null.
        /// </summary>
        [Fact]
        public void Name_Setter_WhenTargetColumnIsNull_DoesNotThrow()
        {
            // Arrange
            PropertyInfo testProperty = typeof(TestEntity).GetProperty(nameof(TestEntity.Id))!;
            ForeignColumn foreignColumn = new ForeignColumn(testProperty);

            // Act & Assert
            var exception = Record.Exception(() => foreignColumn.Name = "SomeName");
            Assert.Null(exception);
        }

        /// <summary>
        /// Test entity class used for creating PropertyInfo instances in tests.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
        }

        /// <summary>
        /// Tests that the ForeignColumn constructor successfully creates an instance
        /// when provided with a valid PropertyInfo from different property types.
        /// </summary>
        /// <param name="propertyName">The name of the property to get PropertyInfo from.</param>
        [Theory]
        [InlineData(nameof(TestProperties.IntProperty))]
        [InlineData(nameof(TestProperties.StringProperty))]
        [InlineData(nameof(TestProperties.NullableIntProperty))]
        [InlineData(nameof(TestProperties.DateTimeProperty))]
        [InlineData(nameof(TestProperties.ObjectProperty))]
        public void ForeignColumn_WithValidPropertyInfo_ShouldCreateInstance(string propertyName)
        {
            // 准备
            PropertyInfo propertyInfo = typeof(TestProperties).GetProperty(propertyName);

            // 执行
            var foreignColumn = new ForeignColumn(propertyInfo);

            // 断言
            Assert.NotNull(foreignColumn);
        }

        /// <summary>
        /// Tests that the ForeignColumn constructor throws ArgumentNullException
        /// when provided with a null PropertyInfo parameter.
        /// </summary>
        [Fact]
        public void ForeignColumn_WithNullPropertyInfo_ShouldThrowArgumentNullException()
        {
            // 准备
            PropertyInfo propertyInfo = null;

            // 执行 & 断言
            Assert.Throws<ArgumentNullException>(() => new ForeignColumn(propertyInfo));
        }

        /// <summary>
        /// Helper class containing various property types for testing PropertyInfo reflection.
        /// </summary>
        private class TestProperties
        {
            public int IntProperty { get; set; }
            public string StringProperty { get; set; }
            public int? NullableIntProperty { get; set; }
            public DateTime DateTimeProperty { get; set; }
            public object ObjectProperty { get; set; }
        }
    }
}