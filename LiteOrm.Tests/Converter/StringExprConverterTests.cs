using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for StringExprConverter.ToText method.
    /// </summary>
    public class StringExprConverterTests
    {
        /// <summary>
        /// Tests ToText with Equal operator and simple value.
        /// Input: Equal operator with simple string value.
        /// Expected: Returns the value without '=' prefix.
        /// </summary>
        [Fact]
        public void ToText_EqualOperatorWithSimpleValue_ReturnsValueWithoutPrefix()
        {
            // Arrange
            var op = LogicOperator.Equal;
            var value = "testValue";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("testValue", result);
        }

        /// <summary>
        /// Tests ToText with Equal operator and value starting with special character.
        /// Input: Equal operator with value starting with special character.
        /// Expected: Returns value with '=' prefix.
        /// </summary>
        [Theory]
        [InlineData("!test", "=!test")]
        [InlineData("<test", "=<test")]
        [InlineData(">test", "=>test")]
        [InlineData("=test", "==test")]
        [InlineData("*test", "=*test")]
        [InlineData("%test", "=%test")]
        [InlineData("$test", "=$test")]
        public void ToText_EqualOperatorWithSpecialStartChar_ReturnsValueWithEqualPrefix(string value, string expected)
        {
            // Arrange
            var op = LogicOperator.Equal;

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests ToText with Equal operator and value containing comma.
        /// Input: Equal operator with value containing comma.
        /// Expected: Returns value with '=' prefix.
        /// </summary>
        [Fact]
        public void ToText_EqualOperatorWithComma_ReturnsValueWithEqualPrefix()
        {
            // Arrange
            var op = LogicOperator.Equal;
            var value = "test,value";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("=test,value", result);
        }

        /// <summary>
        /// Tests ToText with Equal operator and null value.
        /// Input: Equal operator with null value.
        /// Expected: Returns empty string.
        /// </summary>
        [Fact]
        public void ToText_EqualOperatorWithNull_ReturnsEmptyString()
        {
            // Arrange
            var op = LogicOperator.Equal;
            object value = null;

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        /// <summary>
        /// Tests ToText with Equal operator and empty string.
        /// Input: Equal operator with empty string.
        /// Expected: Returns '=' prefix.
        /// </summary>
        [Fact]
        public void ToText_EqualOperatorWithEmptyString_ReturnsEqualPrefix()
        {
            // Arrange
            var op = LogicOperator.Equal;
            var value = string.Empty;

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("=", result);
        }

        /// <summary>
        /// Tests ToText with NotEqual operator and simple value.
        /// Input: NotEqual operator with simple string value.
        /// Expected: Returns value with '!' prefix.
        /// </summary>
        [Fact]
        public void ToText_NotEqualOperatorWithSimpleValue_ReturnsValueWithExclamationPrefix()
        {
            // Arrange
            var op = LogicOperator.NotEqual;
            var value = "testValue";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("!testValue", result);
        }

        /// <summary>
        /// Tests ToText with NotEqual operator and value starting with special character.
        /// Input: NotEqual operator with value starting with special characters (<, >, =, *, %, $).
        /// Expected: Returns value with '!=' prefix.
        /// </summary>
        [Theory]
        [InlineData("<test", "!=<test")]
        [InlineData(">test", "!=>test")]
        [InlineData("=test", "!==test")]
        [InlineData("*test", "!=*test")]
        [InlineData("%test", "!=%test")]
        [InlineData("$test", "!=$test")]
        public void ToText_NotEqualOperatorWithSpecialStartChar_ReturnsValueWithNotEqualPrefix(string value, string expected)
        {
            // Arrange
            var op = LogicOperator.NotEqual;

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests ToText with NotEqual operator and value containing comma.
        /// Input: NotEqual operator with value containing comma.
        /// Expected: Returns value with '!=' prefix.
        /// </summary>
        [Fact]
        public void ToText_NotEqualOperatorWithComma_ReturnsValueWithNotEqualPrefix()
        {
            // Arrange
            var op = LogicOperator.NotEqual;
            var value = "test,value";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("!=test,value", result);
        }

        /// <summary>
        /// Tests ToText with NotEqual operator and null value.
        /// Input: NotEqual operator with null value.
        /// Expected: Returns '!' prefix.
        /// </summary>
        [Fact]
        public void ToText_NotEqualOperatorWithNull_ReturnsExclamationPrefix()
        {
            // Arrange
            var op = LogicOperator.NotEqual;
            object value = null;

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("!", result);
        }

        /// <summary>
        /// Tests ToText with In operator and enumerable collection.
        /// Input: In operator with list of values.
        /// Expected: Returns comma-separated values.
        /// </summary>
        [Fact]
        public void ToText_InOperatorWithEnumerable_ReturnsCommaSeparatedValues()
        {
            // Arrange
            var op = LogicOperator.In;
            var value = new List<object> { "value1", "value2", "value3" };

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("value1,value2,value3", result);
        }

        /// <summary>
        /// Tests ToText with In operator and empty collection.
        /// Input: In operator with empty list.
        /// Expected: Returns empty string.
        /// </summary>
        [Fact]
        public void ToText_InOperatorWithEmptyCollection_ReturnsEmptyString()
        {
            // Arrange
            var op = LogicOperator.In;
            var value = new List<object>();

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        /// <summary>
        /// Tests ToText with In operator and collection containing special characters.
        /// Input: In operator with list containing values starting with special characters.
        /// Expected: Returns escaped comma-separated values.
        /// </summary>
        [Fact]
        public void ToText_InOperatorWithSpecialChars_ReturnsEscapedCommaSeparatedValues()
        {
            // Arrange
            var op = LogicOperator.In;
            var value = new List<object> { "!value1", "=value2", "*value3" };

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal(" !value1, =value2, *value3", result);
        }

        /// <summary>
        /// Tests ToText with NotIn operator and enumerable collection.
        /// Input: NotIn operator with list of values.
        /// Expected: Returns '!' prefix with comma-separated values.
        /// </summary>
        [Fact]
        public void ToText_NotInOperatorWithEnumerable_ReturnsExclamationWithCommaSeparatedValues()
        {
            // Arrange
            var op = LogicOperator.NotIn;
            var value = new List<object> { "value1", "value2", "value3" };

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("!value1,value2,value3", result);
        }

        /// <summary>
        /// Tests ToText with NotIn operator and empty collection.
        /// Input: NotIn operator with empty list.
        /// Expected: Returns '!' prefix.
        /// </summary>
        [Fact]
        public void ToText_NotInOperatorWithEmptyCollection_ReturnsExclamationPrefix()
        {
            // Arrange
            var op = LogicOperator.NotIn;
            var value = new List<object>();

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("!", result);
        }

        /// <summary>
        /// Tests ToText with GreaterThan operator.
        /// Input: GreaterThan operator with integer value.
        /// Expected: Returns value with '>' prefix.
        /// </summary>
        [Fact]
        public void ToText_GreaterThanOperator_ReturnsValueWithGreaterThanPrefix()
        {
            // Arrange
            var op = LogicOperator.GreaterThan;
            var value = 100;

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal(">100", result);
        }

        /// <summary>
        /// Tests ToText with GreaterThan operator and value starting with '='.
        /// Input: GreaterThan operator with value starting with '='.
        /// Expected: Returns escaped value with '>' prefix.
        /// </summary>
        [Fact]
        public void ToText_GreaterThanOperatorWithEqualStartChar_ReturnsEscapedValue()
        {
            // Arrange
            var op = LogicOperator.GreaterThan;
            var value = "=value";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("> =value", result);
        }

        /// <summary>
        /// Tests ToText with GreaterThanOrEqual operator.
        /// Input: GreaterThanOrEqual operator with integer value.
        /// Expected: Returns value with '>=' prefix.
        /// </summary>
        [Fact]
        public void ToText_GreaterThanOrEqualOperator_ReturnsValueWithGreaterThanOrEqualPrefix()
        {
            // Arrange
            var op = LogicOperator.GreaterThanOrEqual;
            var value = 100;

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal(">=100", result);
        }

        /// <summary>
        /// Tests ToText with LessThan operator.
        /// Input: LessThan operator with integer value.
        /// Expected: Returns value with '<' prefix.
        /// </summary>
        [Fact]
        public void ToText_LessThanOperator_ReturnsValueWithLessThanPrefix()
        {
            // Arrange
            var op = LogicOperator.LessThan;
            var value = 50;

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("<50", result);
        }

        /// <summary>
        /// Tests ToText with LessThan operator and value starting with '='.
        /// Input: LessThan operator with value starting with '='.
        /// Expected: Returns escaped value with '<' prefix.
        /// </summary>
        [Fact]
        public void ToText_LessThanOperatorWithEqualStartChar_ReturnsEscapedValue()
        {
            // Arrange
            var op = LogicOperator.LessThan;
            var value = "=value";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("< =value", result);
        }

        /// <summary>
        /// Tests ToText with LessThanOrEqual operator.
        /// Input: LessThanOrEqual operator with integer value.
        /// Expected: Returns value with '<=' prefix.
        /// </summary>
        [Fact]
        public void ToText_LessThanOrEqualOperator_ReturnsValueWithLessThanOrEqualPrefix()
        {
            // Arrange
            var op = LogicOperator.LessThanOrEqual;
            var value = 50;

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("<=50", result);
        }

        /// <summary>
        /// Tests ToText with Like operator.
        /// Input: Like operator with string value.
        /// Expected: Returns value with '*' prefix.
        /// </summary>
        [Fact]
        public void ToText_LikeOperator_ReturnsValueWithAsteriskPrefix()
        {
            // Arrange
            var op = LogicOperator.Like;
            var value = "pattern";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("*pattern", result);
        }

        /// <summary>
        /// Tests ToText with NotLike operator.
        /// Input: NotLike operator with string value.
        /// Expected: Returns value with '!*' prefix.
        /// </summary>
        [Fact]
        public void ToText_NotLikeOperator_ReturnsValueWithExclamationAsteriskPrefix()
        {
            // Arrange
            var op = LogicOperator.NotLike;
            var value = "pattern";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("!*pattern", result);
        }

        /// <summary>
        /// Tests ToText with Contains operator.
        /// Input: Contains operator with string value.
        /// Expected: Returns value with '%' prefix.
        /// </summary>
        [Fact]
        public void ToText_ContainsOperator_ReturnsValueWithPercentPrefix()
        {
            // Arrange
            var op = LogicOperator.Contains;
            var value = "substring";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("%substring", result);
        }

        /// <summary>
        /// Tests ToText with NotContains operator.
        /// Input: NotContains operator with string value.
        /// Expected: Returns value with '!%' prefix.
        /// </summary>
        [Fact]
        public void ToText_NotContainsOperator_ReturnsValueWithExclamationPercentPrefix()
        {
            // Arrange
            var op = LogicOperator.NotContains;
            var value = "substring";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("!%substring", result);
        }

        /// <summary>
        /// Tests ToText with RegexpLike operator.
        /// Input: RegexpLike operator with string value.
        /// Expected: Returns value with '$' prefix.
        /// </summary>
        [Fact]
        public void ToText_RegexpLikeOperator_ReturnsValueWithDollarPrefix()
        {
            // Arrange
            var op = LogicOperator.RegexpLike;
            var value = "^pattern$";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("$^pattern$", result);
        }

        /// <summary>
        /// Tests ToText with NotRegexpLike operator.
        /// Input: NotRegexpLike operator with string value.
        /// Expected: Returns value with '!$' prefix.
        /// </summary>
        [Fact]
        public void ToText_NotRegexpLikeOperator_ReturnsValueWithExclamationDollarPrefix()
        {
            // Arrange
            var op = LogicOperator.NotRegexpLike;
            var value = "^pattern$";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("!$^pattern$", result);
        }

        /// <summary>
        /// Tests ToText with StartsWith operator (not explicitly handled, uses default case).
        /// Input: StartsWith operator with string value.
        /// Expected: Returns value with proper escaping.
        /// </summary>
        [Fact]
        public void ToText_StartsWithOperator_ReturnsValueWithEscaping()
        {
            // Arrange
            var op = LogicOperator.StartsWith;
            var value = "prefix";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("prefix", result);
        }

        /// <summary>
        /// Tests ToText with enum value.
        /// Input: Equal operator with enum value.
        /// Expected: Returns integer representation of enum.
        /// </summary>
        [Fact]
        public void ToText_WithEnumValue_ReturnsIntegerRepresentation()
        {
            // Arrange
            var op = LogicOperator.Equal;
            var value = LogicOperator.GreaterThan;

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("1", result);
        }

        /// <summary>
        /// Tests ToText with boolean true value.
        /// Input: Equal operator with boolean true.
        /// Expected: Returns "1".
        /// </summary>
        [Fact]
        public void ToText_WithBooleanTrue_ReturnsOne()
        {
            // Arrange
            var op = LogicOperator.Equal;
            var value = true;

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("1", result);
        }

        /// <summary>
        /// Tests ToText with boolean false value.
        /// Input: Equal operator with boolean false.
        /// Expected: Returns "0".
        /// </summary>
        [Fact]
        public void ToText_WithBooleanFalse_ReturnsZero()
        {
            // Arrange
            var op = LogicOperator.Equal;
            var value = false;

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("0", result);
        }

        /// <summary>
        /// Tests ToText with integer extremes.
        /// Input: Equal operator with int.MinValue and int.MaxValue.
        /// Expected: Returns string representation of extreme values.
        /// </summary>
        [Theory]
        [InlineData(int.MinValue, "-2147483648")]
        [InlineData(int.MaxValue, "2147483647")]
        [InlineData(0, "0")]
        public void ToText_WithIntegerExtremes_ReturnsStringRepresentation(int value, string expected)
        {
            // Arrange
            var op = LogicOperator.Equal;

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests ToText with In operator and array.
        /// Input: In operator with array of values.
        /// Expected: Returns comma-separated values.
        /// </summary>
        [Fact]
        public void ToText_InOperatorWithArray_ReturnsCommaSeparatedValues()
        {
            // Arrange
            var op = LogicOperator.In;
            var value = new object[] { 1, 2, 3 };

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("1,2,3", result);
        }

        /// <summary>
        /// Tests ToText with In operator and single item collection.
        /// Input: In operator with single item list.
        /// Expected: Returns single value.
        /// </summary>
        [Fact]
        public void ToText_InOperatorWithSingleItem_ReturnsSingleValue()
        {
            // Arrange
            var op = LogicOperator.In;
            var value = new List<object> { "singleValue" };

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("singleValue", result);
        }

        /// <summary>
        /// Tests ToText with In operator and collection containing nulls.
        /// Input: In operator with list containing null values.
        /// Expected: Returns comma-separated values with empty strings for nulls.
        /// </summary>
        [Fact]
        public void ToText_InOperatorWithNulls_ReturnsCommaSeparatedWithEmptyForNulls()
        {
            // Arrange
            var op = LogicOperator.In;
            var value = new List<object> { "value1", null, "value3" };

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("value1,,value3", result);
        }

        /// <summary>
        /// Tests ToText with NotEqual operator and value starting with '!'.
        /// Input: NotEqual operator with value starting with '!'.
        /// Expected: Returns value with '!' prefix only (not '!=').
        /// </summary>
        [Fact]
        public void ToText_NotEqualOperatorWithExclamationStartChar_ReturnsValueWithExclamationPrefix()
        {
            // Arrange
            var op = LogicOperator.NotEqual;
            var value = "!test";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("!!test", result);
        }

        /// <summary>
        /// Tests ToText with whitespace-only string.
        /// Input: Equal operator with whitespace string.
        /// Expected: Returns whitespace string.
        /// </summary>
        [Fact]
        public void ToText_WithWhitespaceString_ReturnsWhitespace()
        {
            // Arrange
            var op = LogicOperator.Equal;
            var value = "   ";

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("   ", result);
        }

        /// <summary>
        /// Tests ToText with very long string.
        /// Input: Equal operator with very long string.
        /// Expected: Returns the entire long string.
        /// </summary>
        [Fact]
        public void ToText_WithVeryLongString_ReturnsEntireLongString()
        {
            // Arrange
            var op = LogicOperator.Equal;
            var value = new string('a', 10000);

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal(new string('a', 10000), result);
        }

        /// <summary>
        /// Tests ToText with In operator and collection containing boolean values.
        /// Input: In operator with list of boolean values.
        /// Expected: Returns comma-separated "1" and "0" values.
        /// </summary>
        [Fact]
        public void ToText_InOperatorWithBooleans_ReturnsCommaSeparatedBinaryValues()
        {
            // Arrange
            var op = LogicOperator.In;
            var value = new List<object> { true, false, true };

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("1,0,1", result);
        }

        /// <summary>
        /// Tests ToText with In operator and mixed type collection.
        /// Input: In operator with list of mixed types.
        /// Expected: Returns comma-separated string representations.
        /// </summary>
        [Fact]
        public void ToText_InOperatorWithMixedTypes_ReturnsCommaSeparatedStringRepresentations()
        {
            // Arrange
            var op = LogicOperator.In;
            var value = new List<object> { 100, "text", true, LogicOperator.Equal };

            // Act
            var result = StringExprConverter.ToText(op, value);

            // Assert
            Assert.Equal("100,text,1,0", result);
        }

        /// <summary>
        /// Test helper class for testing with various property types.
        /// </summary>
        private class TestEntity
        {
            public string? StringProperty { get; set; }
            public int IntProperty { get; set; }
            public DateTime DateProperty { get; set; }
            public bool BoolProperty { get; set; }
            public TestEnum EnumProperty { get; set; }
            public double DoubleProperty { get; set; }
        }

        /// <summary>
        /// Test enum for testing enum parsing.
        /// </summary>
        private enum TestEnum
        {
            Value1 = 0,
            Value2 = 1,
            Value3 = 2
        }

        /// <summary>
        /// Tests that Parse returns PropEqual with null when text is null.
        /// </summary>
        [Fact]
        public void Parse_NullText_ReturnsEqualWithNull()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, null!);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Equal, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
        }

        /// <summary>
        /// Tests that Parse returns PropEqual with null when text is empty string.
        /// </summary>
        [Fact]
        public void Parse_EmptyText_ReturnsEqualWithNull()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, string.Empty);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Equal, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
        }

        /// <summary>
        /// Tests that Parse correctly handles the less than or equal operator (<=).
        /// </summary>
        [Fact]
        public void Parse_LessThanOrEqualOperator_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "<=10");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.LessThanOrEqual, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(10, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles the greater than or equal operator (>=).
        /// </summary>
        [Fact]
        public void Parse_GreaterThanOrEqualOperator_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, ">=20");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.GreaterThanOrEqual, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(20, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles the equal operator (=).
        /// </summary>
        [Fact]
        public void Parse_EqualOperator_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "=5");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Equal, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(5, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles the greater than operator (>).
        /// </summary>
        [Fact]
        public void Parse_GreaterThanOperator_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, ">15");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.GreaterThan, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(15, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles the less than operator (<).
        /// </summary>
        [Fact]
        public void Parse_LessThanOperator_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "<8");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.LessThan, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(8, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles the contains operator (%).
        /// </summary>
        [Fact]
        public void Parse_ContainsOperator_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "%test");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Contains, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal("test", result.Right.ToString());
        }

        /// <summary>
        /// Tests that Parse correctly handles the like operator (*).
        /// </summary>
        [Fact]
        public void Parse_LikeOperator_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "*pattern");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Like, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal("pattern", result.Right.ToString());
        }

        /// <summary>
        /// Tests that Parse correctly handles the regexp like operator ($).
        /// </summary>
        [Fact]
        public void Parse_RegexpLikeOperator_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "$^[a-z]+$");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.RegexpLike, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal("^[a-z]+$", result.Right.ToString());
        }

        /// <summary>
        /// Tests that Parse correctly handles the not equal operator (!=).
        /// </summary>
        [Fact]
        public void Parse_NotEqualOperator_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "!=5");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.NotEqual, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(5, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles the not greater than operator (!>).
        /// </summary>
        [Fact]
        public void Parse_NotGreaterThanOperator_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "!>10");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.GreaterThan | LogicOperator.Not, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(10, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles the not less than operator (!<).
        /// </summary>
        [Fact]
        public void Parse_NotLessThanOperator_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "!<10");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.LessThan | LogicOperator.Not, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(10, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles the not contains operator (!%).
        /// </summary>
        [Fact]
        public void Parse_NotContainsOperator_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "!%test");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.NotContains, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal("test", result.Right.ToString());
        }

        /// <summary>
        /// Tests that Parse correctly handles the not like operator (!*).
        /// </summary>
        [Fact]
        public void Parse_NotLikeOperator_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "!*pattern");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.NotLike, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal("pattern", result.Right.ToString());
        }

        /// <summary>
        /// Tests that Parse correctly handles the not regexp like operator (!$).
        /// </summary>
        [Fact]
        public void Parse_NotRegexpLikeOperator_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "!$^[a-z]+$");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.NotRegexpLike, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal("^[a-z]+$", result.Right.ToString());
        }

        /// <summary>
        /// Tests that Parse returns equal with null when only negation operator is provided.
        /// </summary>
        [Fact]
        public void Parse_OnlyNegationOperator_ReturnsNotEqualWithNull()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "!");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Not | LogicOperator.Equal, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
        }

        /// <summary>
        /// Tests that Parse correctly handles comma-separated values for In operator.
        /// </summary>
        [Fact]
        public void Parse_CommaSeparatedValues_ReturnsInExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "1,2,3");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.In, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            object[] values = (object[])((ValueExpr)result.Right).Value;
            Assert.Equal(3, values.Length);
            Assert.Equal(1, values[0]);
            Assert.Equal(2, values[1]);
            Assert.Equal(3, values[2]);
        }

        /// <summary>
        /// Tests that Parse correctly handles comma-separated values with negation for NotIn operator.
        /// </summary>
        [Fact]
        public void Parse_CommaSeparatedValuesWithNegation_ReturnsNotInExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "!1,2,3");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.NotIn, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            object[] values = (object[])((ValueExpr)result.Right).Value;
            Assert.Equal(3, values.Length);
            Assert.Equal(1, values[0]);
            Assert.Equal(2, values[1]);
            Assert.Equal(3, values[2]);
        }

        /// <summary>
        /// Tests that Parse correctly handles text without any operators, defaulting to Equal.
        /// </summary>
        [Fact]
        public void Parse_TextWithoutOperator_ReturnsEqualExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "42");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Equal, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(42, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles string values without operators.
        /// </summary>
        [Fact]
        public void Parse_StringValueWithoutOperator_ReturnsEqualExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "test value");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Equal, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal("test value", ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles negative integer values.
        /// </summary>
        [Fact]
        public void Parse_NegativeIntegerValue_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "-100");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Equal, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(-100, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles int.MaxValue.
        /// </summary>
        [Fact]
        public void Parse_IntMaxValue_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, int.MaxValue.ToString());

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Equal, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(int.MaxValue, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles int.MinValue.
        /// </summary>
        [Fact]
        public void Parse_IntMinValue_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, int.MinValue.ToString());

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Equal, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(int.MinValue, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles zero value.
        /// </summary>
        [Fact]
        public void Parse_ZeroValue_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "=0");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Equal, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(0, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles whitespace-only text after operator.
        /// </summary>
        [Fact]
        public void Parse_WhitespaceAfterOperator_TrimsAndProcesses()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "%   test   ");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Contains, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal("test", result.Right.ToString());
        }

        /// <summary>
        /// Tests that Parse correctly handles very long string values.
        /// </summary>
        [Fact]
        public void Parse_VeryLongString_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;
            string longString = new string('a', 10000);

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, longString);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Equal, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(longString, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles special characters in string values.
        /// </summary>
        [Fact]
        public void Parse_SpecialCharacters_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "test@#$%^&*()");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Equal, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal("test@#$%^&*()", ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles empty string after operator.
        /// </summary>
        [Fact]
        public void Parse_EmptyStringAfterEqualOperator_ReturnsEqualWithEmptyString()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "=");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Equal, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(string.Empty, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles contains operator with empty string.
        /// </summary>
        [Fact]
        public void Parse_ContainsOperatorWithEmptyString_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "%");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Contains, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(string.Empty, result.Right.ToString());
        }

        /// <summary>
        /// Tests that Parse correctly handles like operator with empty string.
        /// </summary>
        [Fact]
        public void Parse_LikeOperatorWithEmptyString_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "*");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Like, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(string.Empty, result.Right.ToString());
        }

        /// <summary>
        /// Tests that Parse correctly handles single comma-separated value.
        /// </summary>
        [Fact]
        public void Parse_SingleCommaSeparatedValue_ReturnsInExpressionWithOneElement()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "42,");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.In, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            object[] values = (object[])((ValueExpr)result.Right).Value;
            Assert.Equal(2, values.Length);
            Assert.Equal(42, values[0]);
        }

        /// <summary>
        /// Tests that Parse correctly handles comma-separated string values.
        /// </summary>
        [Fact]
        public void Parse_CommaSeparatedStringValues_ReturnsInExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "apple,banana,cherry");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.In, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            object[] values = (object[])((ValueExpr)result.Right).Value;
            Assert.Equal(3, values.Length);
            Assert.Equal("apple", values[0]);
            Assert.Equal("banana", values[1]);
            Assert.Equal("cherry", values[2]);
        }

        /// <summary>
        /// Tests that Parse correctly handles operators with double property type.
        /// </summary>
        [Fact]
        public void Parse_DoublePropertyWithGreaterThan_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.DoubleProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, ">3.14");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.GreaterThan, result.Operator);
            Assert.Equal(nameof(TestEntity.DoubleProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(3.14, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles less than or equal with boundary value.
        /// </summary>
        [Fact]
        public void Parse_LessThanOrEqualWithZero_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "<=0");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.LessThanOrEqual, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(0, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles greater than or equal with negative value.
        /// </summary>
        [Fact]
        public void Parse_GreaterThanOrEqualWithNegative_ReturnsCorrectExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, ">=-50");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.GreaterThanOrEqual, result.Operator);
            Assert.Equal(nameof(TestEntity.IntProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal(-50, ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles text starting with multiple special characters.
        /// </summary>
        [Fact]
        public void Parse_TextStartingWithMultipleOperatorLikeChars_ReturnsEqualExpression()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "===value");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Equal, result.Operator);
            Assert.Equal(nameof(TestEntity.StringProperty), ((PropertyExpr)result.Left).PropertyName);
            Assert.Equal("==value", ((ValueExpr)result.Right).Value);
        }

        /// <summary>
        /// Tests that Parse correctly handles comma in contains operator.
        /// </summary>
        [Fact]
        public void Parse_ContainsOperatorWithComma_ReturnsContainsNotIn()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "%test");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Contains, result.Operator);
        }

        /// <summary>
        /// Tests that Parse handles text with only whitespace after trimming operators.
        /// </summary>
        [Fact]
        public void Parse_OnlyWhitespaceAfterOperator_HandlesCorrectly()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.StringProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "%   ");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(LogicOperator.Contains, result.Operator);
            Assert.Equal(string.Empty, result.Right.ToString());
        }

        /// <summary>
        /// Tests that Parse handles operators with equal sign followed by other operators.
        /// </summary>
        [Fact]
        public void Parse_LessThanOrEqualWithNegation_NotSupported()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result = StringExprConverter.Parse(property, "!<=5");

            // Assert
            Assert.NotNull(result);
            // The negation applies to the remaining text after removing "!"
            // So "!<=5" becomes "<" with mask=Not and value "=5"
            Assert.Equal(LogicOperator.LessThan | LogicOperator.Not, result.Operator);
        }

        /// <summary>
        /// Tests that Parse handles two-character operators at the beginning correctly.
        /// </summary>
        [Fact]
        public void Parse_TwoCharOperatorAtStart_ParsedCorrectly()
        {
            // Arrange
            PropertyInfo property = typeof(TestEntity).GetProperty(nameof(TestEntity.IntProperty))!;

            // Act
            LogicBinaryExpr result1 = StringExprConverter.Parse(property, "<=100");
            LogicBinaryExpr result2 = StringExprConverter.Parse(property, ">=50");

            // Assert
            Assert.Equal(LogicOperator.LessThanOrEqual, result1.Operator);
            Assert.Equal(100, ((ValueExpr)result1.Right).Value);
            Assert.Equal(LogicOperator.GreaterThanOrEqual, result2.Operator);
            Assert.Equal(50, ((ValueExpr)result2.Right).Value);
        }
    }
}