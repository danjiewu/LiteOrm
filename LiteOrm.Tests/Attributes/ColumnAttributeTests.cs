using System;
using System.Data;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class ColumnAttributeTests
    {
        /// <summary>
        /// Tests that IsColumn returns true when using the default constructor.
        /// The default constructor does not explicitly set the isColumn field, so it uses the default initialization value of true.
        /// </summary>
        [Fact]
        public void IsColumn_DefaultConstructor_ReturnsTrue()
        {
            // Arrange & Act
            var attribute = new ColumnAttribute();

            // Assert
            Assert.True(attribute.IsColumn);
        }

        /// <summary>
        /// Tests that IsColumn returns the expected value when constructed with a boolean parameter.
        /// Tests both true and false values to verify the property correctly reflects the constructor parameter.
        /// </summary>
        /// <param name="isColumnValue">The boolean value passed to the constructor.</param>
        /// <param name="expectedResult">The expected value returned by the IsColumn property.</param>
        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void IsColumn_ConstructorWithBool_ReturnsExpectedValue(bool isColumnValue, bool expectedResult)
        {
            // Arrange & Act
            var attribute = new ColumnAttribute(isColumnValue);

            // Assert
            Assert.Equal(expectedResult, attribute.IsColumn);
        }

        /// <summary>
        /// Tests that IsColumn returns true when constructed with a column name string.
        /// The string constructor internally calls the boolean constructor with true, regardless of the string value.
        /// Tests various string inputs including null, empty, whitespace, and normal strings.
        /// </summary>
        /// <param name="columnName">The column name string passed to the constructor.</param>
        [Theory]
        [InlineData("TestColumn")]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t")]
        [InlineData("Column_With_Underscores")]
        [InlineData("Column123")]
        [InlineData(null)]
        public void IsColumn_ConstructorWithColumnName_ReturnsTrue(string? columnName)
        {
            // Arrange & Act
            var attribute = new ColumnAttribute(columnName!);

            // Assert
            Assert.True(attribute.IsColumn);
        }

        /// <summary>
        /// Tests that the constructor initializes ColumnName with a valid column name
        /// and sets default values correctly, including IsColumn to true.
        /// </summary>
        [Fact]
        public void ColumnAttribute_WithValidColumnName_SetsPropertiesCorrectly()
        {
            // Arrange
            string columnName = "UserId";

            // Act
            var attribute = new ColumnAttribute(columnName);

            // Assert
            Assert.Equal("UserId", attribute.ColumnName);
            Assert.True(attribute.IsColumn);
            Assert.Equal(ColumnMode.Full, attribute.ColumnMode);
            Assert.Equal(DbType.Object, attribute.DbType);
            Assert.True(attribute.AllowNull);
        }

        /// <summary>
        /// Tests that the constructor accepts null as columnName and sets the ColumnName property to null.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\r\n")]
        public void ColumnAttribute_WithNullOrWhitespaceColumnName_AcceptsValue(string? columnName)
        {
            // Arrange & Act
            var attribute = new ColumnAttribute(columnName);

            // Assert
            Assert.Equal(columnName, attribute.ColumnName);
            Assert.True(attribute.IsColumn);
        }

        /// <summary>
        /// Tests that the constructor correctly handles column names with special characters.
        /// </summary>
        [Theory]
        [InlineData("Column-Name")]
        [InlineData("Column_Name")]
        [InlineData("Column.Name")]
        [InlineData("[ColumnName]")]
        [InlineData("Column@Name")]
        [InlineData("Column#Name")]
        [InlineData("Column$Name")]
        [InlineData("Column%Name")]
        [InlineData("Column&Name")]
        [InlineData("Column*Name")]
        [InlineData("用户ID")]
        [InlineData("用户姓名")]
        public void ColumnAttribute_WithSpecialCharactersInColumnName_AcceptsValue(string columnName)
        {
            // Arrange & Act
            var attribute = new ColumnAttribute(columnName);

            // Assert
            Assert.Equal(columnName, attribute.ColumnName);
            Assert.True(attribute.IsColumn);
        }

        /// <summary>
        /// Tests that the constructor correctly handles very long column names.
        /// </summary>
        [Fact]
        public void ColumnAttribute_WithVeryLongColumnName_AcceptsValue()
        {
            // Arrange
            string veryLongColumnName = new string('A', 10000);

            // Act
            var attribute = new ColumnAttribute(veryLongColumnName);

            // Assert
            Assert.Equal(veryLongColumnName, attribute.ColumnName);
            Assert.True(attribute.IsColumn);
        }

        /// <summary>
        /// Tests that the constructor sets IsColumn to true through constructor chaining.
        /// </summary>
        [Fact]
        public void ColumnAttribute_WithColumnName_SetsIsColumnToTrue()
        {
            // Arrange
            string columnName = "TestColumn";

            // Act
            var attribute = new ColumnAttribute(columnName);

            // Assert
            Assert.True(attribute.IsColumn);
        }

        /// <summary>
        /// Tests that the constructor initializes all default property values correctly.
        /// </summary>
        [Fact]
        public void ColumnAttribute_WithColumnName_InitializesDefaultProperties()
        {
            // Arrange
            string columnName = "TestColumn";

            // Act
            var attribute = new ColumnAttribute(columnName);

            // Assert
            Assert.Equal(ColumnMode.Full, attribute.ColumnMode);
            Assert.Equal(DbType.Object, attribute.DbType);
            Assert.True(attribute.AllowNull);
            Assert.False(attribute.IsPrimaryKey);
            Assert.False(attribute.IsIdentity);
            Assert.Equal(1, attribute.IdentityIncreasement);
            Assert.False(attribute.IsTimestamp);
            Assert.False(attribute.IsIndex);
            Assert.False(attribute.IsUnique);
            Assert.Equal(0, attribute.Length);
        }

        /// <summary>
        /// 测试 ColumnAttribute(bool) 构造函数，当传入 true 时，
        /// 应正确设置 IsColumn 属性为 true，并保留默认值。
        /// </summary>
        [Fact]
        public void ColumnAttribute_BoolConstructorWithTrue_ShouldSetIsColumnTrueAndDefaultValues()
        {
            // 准备
            bool isColumn = true;

            // 执行
            var attribute = new ColumnAttribute(isColumn);

            // 断言
            Assert.True(attribute.IsColumn);
            Assert.Equal(ColumnMode.Full, attribute.ColumnMode);
            Assert.Equal(DbType.Object, attribute.DbType);
            Assert.True(attribute.AllowNull);
        }

        /// <summary>
        /// 测试 ColumnAttribute(bool) 构造函数，当传入 false 时，
        /// 应正确设置 IsColumn 属性为 false，并保留默认值。
        /// </summary>
        [Fact]
        public void ColumnAttribute_BoolConstructorWithFalse_ShouldSetIsColumnFalseAndDefaultValues()
        {
            // 准备
            bool isColumn = false;

            // 执行
            var attribute = new ColumnAttribute(isColumn);

            // 断言
            Assert.False(attribute.IsColumn);
            Assert.Equal(ColumnMode.Full, attribute.ColumnMode);
            Assert.Equal(DbType.Object, attribute.DbType);
            Assert.True(attribute.AllowNull);
        }

        /// <summary>
        /// Tests that the parameterless constructor initializes all properties with their expected default values.
        /// Verifies that ColumnMode is set to Full, DbType is set to Object, AllowNull is set to true,
        /// and IsColumn is set to true (from field initializer).
        /// </summary>
        [Fact]
        public void ColumnAttribute_ParameterlessConstructor_InitializesPropertiesWithDefaultValues()
        {
            // Act
            var attribute = new ColumnAttribute();

            // Assert
            Assert.NotNull(attribute);
            Assert.Equal(ColumnMode.Full, attribute.ColumnMode);
            Assert.Equal(DbType.Object, attribute.DbType);
            Assert.True(attribute.AllowNull);
            Assert.True(attribute.IsColumn);
        }
    }
}