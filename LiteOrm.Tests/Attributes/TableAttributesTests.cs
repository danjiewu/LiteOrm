using System;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="TableAttribute"/> class.
    /// </summary>
    public class TableAttributeTests
    {
        /// <summary>
        /// Tests that the parameterless constructor creates a valid instance with default property values.
        /// Verifies that TableName and DataSource properties are initialized to null.
        /// </summary>
        [Fact]
        public void TableAttribute_ParameterlessConstructor_CreatesInstanceWithNullProperties()
        {
            // 准备 & 执行
            var attribute = new TableAttribute();

            // 断言
            Assert.NotNull(attribute);
            Assert.Null(attribute.TableName);
            Assert.Null(attribute.DataSource);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates an instance that inherits from Attribute.
        /// Verifies the type hierarchy is correct.
        /// </summary>
        [Fact]
        public void TableAttribute_ParameterlessConstructor_CreatesAttributeInstance()
        {
            // 准备 & 执行
            var attribute = new TableAttribute();

            // 断言
            Assert.IsAssignableFrom<Attribute>(attribute);
        }

        /// <summary>
        /// Tests that the constructor with tableName parameter correctly sets the TableName property
        /// with various string values including normal, empty, whitespace, special characters, and very long strings.
        /// </summary>
        /// <param name="tableName">The table name to pass to the constructor.</param>
        [Theory]
        [InlineData("Users")]
        [InlineData("OrderDetails")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Table_With_Underscores")]
        [InlineData("Table-With-Dashes")]
        [InlineData("Table.With.Dots")]
        [InlineData("表名")]
        [InlineData("TableWithVeryLongNameThatExceedsNormalLengthToTestBoundaryConditionsAndEnsureTheAttributeHandlesItCorrectlyWithoutAnyIssuesOrTruncation")]
        [InlineData("Table\nWith\nNewlines")]
        [InlineData("Table\tWith\tTabs")]
        [InlineData("!@#$%^&*()")]
        public void TableAttribute_WithTableName_SetsTableNameProperty(string tableName)
        {
            // Arrange & Act
            var attribute = new TableAttribute(tableName);

            // Assert
            Assert.Equal(tableName, attribute.TableName);
        }

        /// <summary>
        /// Tests that the constructor with null tableName parameter correctly sets the TableName property to null.
        /// Validates that the constructor accepts null values without throwing exceptions.
        /// </summary>
        [Fact]
        public void TableAttribute_WithNullTableName_SetsTableNamePropertyToNull()
        {
            // Arrange
            string tableName = null;

            // Act
            var attribute = new TableAttribute(tableName);

            // Assert
            Assert.Null(attribute.TableName);
        }
    }
}