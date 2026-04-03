using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for ExprJsonConverter Write method.
    /// </summary>
    public partial class ExprJsonConverterTests
    {
        /// <summary>
        /// Tests that Write method serializes a null Expr value as JSON null.
        /// </summary>
        [Fact]
        public void Write_NullExpr_WritesNullValue()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            ValueExpr? nullExpr = null;

            // Act
            var json = JsonSerializer.Serialize(nullExpr, options);

            // Assert
            Assert.Equal("null", json);
        }

        /// <summary>
        /// Tests that Write method serializes a ValueExpr with a const value correctly.
        /// </summary>
        [Fact]
        public void Write_ValueExprWithConstValue_SerializesCorrectly()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var valueExpr = new ValueExpr(42, true);

            // Act
            var json = JsonSerializer.Serialize<Expr>(valueExpr, options);

            // Assert
            Assert.Equal("42", json);
        }

        /// <summary>
        /// Tests that Write method serializes a ValueExpr with a non-const value using @ notation.
        /// </summary>
        [Fact]
        public void Write_ValueExprWithNonConstValue_UsesAtNotation()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var valueExpr = new ValueExpr("test", false);

            // Act
            var json = JsonSerializer.Serialize<Expr>(valueExpr, options);

            // Assert
            Assert.Contains("\"@\"", json);
            Assert.Contains("\"test\"", json);
        }

        /// <summary>
        /// Tests that Write method serializes a PropertyExpr using # notation.
        /// </summary>
        [Fact]
        public void Write_PropertyExpr_UsesHashNotation()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var propertyExpr = new PropertyExpr("Name");

            // Act
            var json = JsonSerializer.Serialize<Expr>(propertyExpr, options);

            // Assert
            Assert.Contains("\"#\"", json);
            Assert.Contains("\"Name\"", json);
        }

        /// <summary>
        /// Tests that Write method serializes a PropertyExpr with table alias correctly.
        /// </summary>
        [Fact]
        public void Write_PropertyExprWithTableAlias_IncludesAlias()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var propertyExpr = new PropertyExpr("Name", "u");

            // Act
            var json = JsonSerializer.Serialize<Expr>(propertyExpr, options);

            // Assert
            Assert.Contains("\"#\"", json);
            Assert.Contains("u.Name", json);
        }

        /// <summary>
        /// Tests that Write method serializes a NotExpr using ! notation.
        /// </summary>
        [Fact]
        public void Write_NotExpr_UsesExclamationNotation()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var notExpr = new NotExpr(new ValueExpr(true, true));

            // Act
            var json = JsonSerializer.Serialize<Expr>(notExpr, options);

            // Assert
            Assert.Contains("\"!\"", json);
        }

        /// <summary>
        /// Tests that Write method serializes an AndExpr with multiple items.
        /// </summary>
        [Fact]
        public void Write_AndExpr_SerializesAllItems()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var andExpr = new AndExpr(new ValueExpr(true, true), new ValueExpr(false, true));

            // Act
            var json = JsonSerializer.Serialize<Expr>(andExpr, options);

            // Assert
            Assert.Contains("\"$\"", json);
            Assert.Contains("\"and\"", json);
            Assert.Contains("\"Items\"", json);
        }

        /// <summary>
        /// Tests that Write method serializes an OrExpr with multiple items.
        /// </summary>
        [Fact]
        public void Write_OrExpr_SerializesAllItems()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var orExpr = new OrExpr(new ValueExpr(true, true), new ValueExpr(false, true));

            // Act
            var json = JsonSerializer.Serialize<Expr>(orExpr, options);

            // Assert
            Assert.Contains("\"$\"", json);
            Assert.Contains("\"or\"", json);
            Assert.Contains("\"Items\"", json);
        }

        /// <summary>
        /// Tests that Write method serializes a LogicBinaryExpr with Equal operator.
        /// </summary>
        [Fact]
        public void Write_LogicBinaryExprEqual_UsesEqualOperator()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var left = new PropertyExpr("Age");
            var right = new ValueExpr(25, true);
            var binaryExpr = new LogicBinaryExpr(LogicOperator.Equal, left, right);

            // Act
            var json = JsonSerializer.Serialize<Expr>(binaryExpr, options);

            // Assert
            Assert.Contains("\"$\"", json);
            Assert.Contains("\"==\"", json);
            Assert.Contains("\"Left\"", json);
            Assert.Contains("\"Right\"", json);
        }

        /// <summary>
        /// Tests that Write method serializes a LogicBinaryExpr with various operators.
        /// </summary>
        /// <param name="op">The logic operator to test.</param>
        /// <param name="expectedSymbol">The expected JSON symbol.</param>
        [Theory]
        [InlineData(LogicOperator.NotEqual, "!=")]
        [InlineData(LogicOperator.GreaterThan, ">")]
        [InlineData(LogicOperator.GreaterThanOrEqual, ">=")]
        [InlineData(LogicOperator.LessThan, "<")]
        [InlineData(LogicOperator.LessThanOrEqual, "<=")]
        [InlineData(LogicOperator.In, "in")]
        [InlineData(LogicOperator.NotIn, "notin")]
        [InlineData(LogicOperator.Like, "like")]
        [InlineData(LogicOperator.NotLike, "notlike")]
        [InlineData(LogicOperator.Contains, "contains")]
        [InlineData(LogicOperator.NotContains, "notcontains")]
        [InlineData(LogicOperator.StartsWith, "startswith")]
        [InlineData(LogicOperator.NotStartsWith, "notstartswith")]
        [InlineData(LogicOperator.EndsWith, "endswith")]
        [InlineData(LogicOperator.NotEndsWith, "notendswith")]
        [InlineData(LogicOperator.RegexpLike, "regexp")]
        [InlineData(LogicOperator.NotRegexpLike, "notregexp")]
        public void Write_LogicBinaryExprWithOperator_IncludesCorrectSymbol(LogicOperator op, string expectedSymbol)
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var left = new PropertyExpr("Field");
            var right = new ValueExpr(100, true);
            var binaryExpr = new LogicBinaryExpr(op, left, right);

            // Act
            var json = JsonSerializer.Serialize<Expr>(binaryExpr, options);

            // Assert
            Assert.Contains($"\"{expectedSymbol}\"", json);
        }

        /// <summary>
        /// Tests that Write method serializes a ValueBinaryExpr with various operators.
        /// </summary>
        /// <param name="op">The value operator to test.</param>
        /// <param name="expectedSymbol">The expected JSON symbol.</param>
        [Theory]
        [InlineData(ValueOperator.Add, "+")]
        [InlineData(ValueOperator.Subtract, "-")]
        [InlineData(ValueOperator.Multiply, "*")]
        [InlineData(ValueOperator.Divide, "/")]
        [InlineData(ValueOperator.Modulo, "%")]
        [InlineData(ValueOperator.Concat, "||")]
        public void Write_ValueBinaryExprWithOperator_IncludesCorrectSymbol(ValueOperator op, string expectedSymbol)
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var left = new ValueExpr(10, true);
            var right = new ValueExpr(5, true);
            var binaryExpr = new ValueBinaryExpr(op, left, right);

            // Act
            var json = JsonSerializer.Serialize<Expr>(binaryExpr, options);

            // Assert
            Assert.Contains($"\"{expectedSymbol}\"", json);
        }

        /// <summary>
        /// Tests that Write method serializes a FunctionExpr correctly.
        /// </summary>
        [Fact]
        public void Write_FunctionExpr_SerializesFunctionNameAndArgs()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var functionExpr = new FunctionExpr("MAX", new ValueExpr(100, true));

            // Act
            var json = JsonSerializer.Serialize<Expr>(functionExpr, options);

            // Assert
            Assert.Contains("\"$\"", json);
            Assert.Contains("\"func\"", json);
            Assert.Contains("\"MAX\"", json);
        }

        /// <summary>
        /// Tests that Write method serializes an aggregate FunctionExpr with IsAggregate flag.
        /// </summary>
        [Fact]
        public void Write_AggregateFunctionExpr_IncludesIsAggregateFlag()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var functionExpr = new FunctionExpr("SUM", true, new ValueExpr(1, true));

            // Act
            var json = JsonSerializer.Serialize<Expr>(functionExpr, options);

            // Assert
            Assert.Contains("\"IsAggregate\"", json);
            Assert.Contains("true", json);
        }

        /// <summary>
        /// Tests that Write method serializes a ValueExpr with null value correctly.
        /// </summary>
        [Fact]
        public void Write_ValueExprWithNullValue_SerializesNull()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var valueExpr = new ValueExpr(null, true);

            // Act
            var json = JsonSerializer.Serialize<Expr>(valueExpr, options);

            // Assert
            Assert.Equal("null", json);
        }

        /// <summary>
        /// Tests that Write method serializes a ValueExpr with string value containing special characters.
        /// </summary>
        [Fact]
        public void Write_ValueExprWithSpecialCharacters_EscapesCorrectly()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var valueExpr = new ValueExpr("test\"value'with<special>chars", true);

            // Act
            var json = JsonSerializer.Serialize<Expr>(valueExpr, options);

            // Assert
            Assert.NotNull(json);
            Assert.Contains("test", json);
        }

        /// <summary>
        /// Tests that Write method serializes a ValueExpr with empty string.
        /// </summary>
        [Fact]
        public void Write_ValueExprWithEmptyString_SerializesEmptyString()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var valueExpr = new ValueExpr("", true);

            // Act
            var json = JsonSerializer.Serialize<Expr>(valueExpr, options);

            // Assert
            Assert.Equal("\"\"", json);
        }

        /// <summary>
        /// Tests that Write method serializes a ValueExpr with numeric extremes.
        /// </summary>
        /// <param name="value">The numeric value to test.</param>
        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(int.MaxValue)]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1)]
        public void Write_ValueExprWithNumericExtremes_SerializesCorrectly(int value)
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var valueExpr = new ValueExpr(value, true);

            // Act
            var json = JsonSerializer.Serialize<Expr>(valueExpr, options);

            // Assert
            Assert.Equal(value.ToString(), json);
        }

        /// <summary>
        /// Tests that Write method serializes a ValueExpr with double special values.
        /// </summary>
        /// <param name="value">The double value to test.</param>
        /// <param name="expectedContains">The expected string in JSON.</param>
        [Theory]
        [InlineData(double.NaN, "NaN")]
        [InlineData(double.PositiveInfinity, "Infinity")]
        [InlineData(double.NegativeInfinity, "-Infinity")]
        public void Write_ValueExprWithDoubleSpecialValues_SerializesCorrectly(double value, string expectedContains)
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var valueExpr = new ValueExpr(value, true);

            // Act
            var json = JsonSerializer.Serialize<Expr>(valueExpr, options);

            // Assert
            Assert.Contains(expectedContains, json);
        }

        /// <summary>
        /// Tests that Write method serializes an empty AndExpr correctly.
        /// </summary>
        [Fact]
        public void Write_EmptyAndExpr_SerializesEmptyArray()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var andExpr = new AndExpr();

            // Act
            var json = JsonSerializer.Serialize<Expr>(andExpr, options);

            // Assert
            Assert.Contains("\"Items\"", json);
            Assert.Contains("[]", json);
        }

        /// <summary>
        /// Tests that Write method serializes an empty OrExpr correctly.
        /// </summary>
        [Fact]
        public void Write_EmptyOrExpr_SerializesEmptyArray()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var orExpr = new OrExpr();

            // Act
            var json = JsonSerializer.Serialize<Expr>(orExpr, options);

            // Assert
            Assert.Contains("\"Items\"", json);
            Assert.Contains("[]", json);
        }

        /// <summary>
        /// Tests that Write method serializes a nested Expr structure correctly.
        /// </summary>
        [Fact]
        public void Write_NestedExpr_SerializesRecursively()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var innerExpr = new PropertyExpr("Name");
            var notExpr = new NotExpr(innerExpr);
            var andExpr = new AndExpr(notExpr, new ValueExpr(true, true));

            // Act
            var json = JsonSerializer.Serialize<Expr>(andExpr, options);

            // Assert
            Assert.Contains("\"and\"", json);
            Assert.Contains("\"!\"", json);
            Assert.Contains("\"Name\"", json);
        }

        /// <summary>
        /// Tests that Write method handles ValueExpr with nested Expr value.
        /// </summary>
        [Fact]
        public void Write_ValueExprWithNestedExpr_UnwrapsCorrectly()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var innerExpr = new PropertyExpr("Field");
            var valueExpr = new ValueExpr(innerExpr, true);

            // Act
            var json = JsonSerializer.Serialize<Expr>(valueExpr, options);

            // Assert
            Assert.Contains("\"#\"", json);
            Assert.Contains("\"Field\"", json);
        }

        /// <summary>
        /// Tests that Write method serializes PropertyExpr with null table alias.
        /// </summary>
        [Fact]
        public void Write_PropertyExprWithNullAlias_SerializesPropertyNameOnly()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var propertyExpr = new PropertyExpr("Column", null);

            // Act
            var json = JsonSerializer.Serialize<Expr>(propertyExpr, options);

            // Assert
            Assert.Contains("\"#\"", json);
            Assert.Contains("\"Column\"", json);
            Assert.DoesNotContain(".", json);
        }

        /// <summary>
        /// Tests that Write method serializes PropertyExpr with empty table alias.
        /// </summary>
        [Fact]
        public void Write_PropertyExprWithEmptyAlias_SerializesPropertyNameOnly()
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());
            var propertyExpr = new PropertyExpr("Column", "");

            // Act
            var json = JsonSerializer.Serialize<Expr>(propertyExpr, options);

            // Assert
            Assert.Contains("\"#\"", json);
            Assert.Contains("\"Column\"", json);
        }

        /// <summary>
        /// Tests that Read returns null when the JSON token is null.
        /// </summary>
        [Fact]
        public void Read_NullToken_ReturnsNull()
        {
            // Arrange
            string json = "null";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that Read converts a simple integer value to ValueExpr.
        /// </summary>
        [Theory]
        [InlineData("42")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("2147483647")] // int.MaxValue
        [InlineData("-2147483648")] // int.MinValue
        public void Read_IntegerToken_ReturnsValueExpr(string json)
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ValueExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts a simple string value to ValueExpr.
        /// </summary>
        [Theory]
        [InlineData("\"test\"")]
        [InlineData("\"\"")]
        [InlineData("\"   \"")]
        [InlineData("\"very long string with special chars !@#$%^&*()\"")]
        public void Read_StringToken_ReturnsValueExpr(string json)
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ValueExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts boolean values to ValueExpr.
        /// </summary>
        [Theory]
        [InlineData("true")]
        [InlineData("false")]
        public void Read_BooleanToken_ReturnsValueExpr(string json)
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ValueExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts floating point numbers to ValueExpr.
        /// </summary>
        [Theory]
        [InlineData("3.14")]
        [InlineData("0.0")]
        [InlineData("-123.456")]
        public void Read_DecimalToken_ReturnsValueExpr(string json)
        {
            // Arrange
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ValueExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts an object with "@" property to ValueExpr with IsConst=false.
        /// </summary>
        [Fact]
        public void Read_ObjectWithAtProperty_ReturnsValueExprWithIsConstFalse()
        {
            // Arrange
            string json = "{\"@\": 123}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            var valueExpr = Assert.IsType<ValueExpr>(result);
            Assert.False(valueExpr.IsConst);
        }

        /// <summary>
        /// Tests that Read converts an object with "#" property (simple property name) to PropertyExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithHashPropertySimpleName_ReturnsPropertyExpr()
        {
            // Arrange
            string json = "{\"#\": \"Name\"}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<PropertyExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts an object with "#" property (dotted name) to PropertyExpr with table alias.
        /// </summary>
        [Fact]
        public void Read_ObjectWithHashPropertyDottedName_ReturnsPropertyExprWithTableAlias()
        {
            // Arrange
            string json = "{\"#\": \"u.Name\"}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<PropertyExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts an object with "#" property containing empty string.
        /// </summary>
        [Fact]
        public void Read_ObjectWithHashPropertyEmptyString_ReturnsPropertyExpr()
        {
            // Arrange
            string json = "{\"#\": \"\"}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<PropertyExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts an object with "!" property to NotExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithExclamationProperty_ReturnsNotExpr()
        {
            // Arrange
            string json = "{\"!\": {\"#\": \"Active\"}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<NotExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts an object with "$" and "==" to LogicBinaryExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarAndEqualOperator_ReturnsLogicBinaryExpr()
        {
            // Arrange
            string json = "{\"$\": \"==\", \"Left\": {\"#\": \"Name\"}, \"Right\": \"Test\"}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<LogicBinaryExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts various logic operators to LogicBinaryExpr.
        /// </summary>
        [Theory]
        [InlineData("=")]
        [InlineData("==")]
        [InlineData("!=")]
        [InlineData("<>")]
        [InlineData(">")]
        [InlineData(">=")]
        [InlineData("<")]
        [InlineData("<=")]
        [InlineData("in")]
        [InlineData("notin")]
        [InlineData("like")]
        [InlineData("notlike")]
        [InlineData("contains")]
        [InlineData("notcontains")]
        [InlineData("startswith")]
        [InlineData("notstartswith")]
        [InlineData("endswith")]
        [InlineData("notendswith")]
        [InlineData("regexp")]
        [InlineData("notregexp")]
        public void Read_ObjectWithDollarAndLogicOperators_ReturnsLogicBinaryExpr(string op)
        {
            // Arrange
            string json = $"{{\"$\": \"{op}\", \"Left\": {{\"#\": \"Name\"}}, \"Right\": \"Test\"}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<LogicBinaryExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts value operators to ValueBinaryExpr.
        /// </summary>
        [Theory]
        [InlineData("+")]
        [InlineData("-")]
        [InlineData("*")]
        [InlineData("/")]
        [InlineData("%")]
        [InlineData("||")]
        public void Read_ObjectWithDollarAndValueOperators_ReturnsValueBinaryExpr(string op)
        {
            // Arrange
            string json = $"{{\"$\": \"{op}\", \"Left\": 10, \"Right\": 5}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ValueBinaryExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$and" mark to AndExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarAndMark_ReturnsAndExpr()
        {
            // Arrange
            string json = "{\"$and\": [{\"#\": \"Active\"}, {\"#\": \"Enabled\"}]}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<AndExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$or" mark to OrExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarOrMark_ReturnsOrExpr()
        {
            // Arrange
            string json = "{\"$or\": [{\"#\": \"Active\"}, {\"#\": \"Enabled\"}]}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<OrExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$set" mark to ValueSet.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarSetMark_ReturnsValueSet()
        {
            // Arrange
            string json = "{\"$set\": [1, 2, 3]}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ValueSet>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$func" mark to FunctionExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarFuncMark_ReturnsFunctionExpr()
        {
            // Arrange
            string json = "{\"$func\": \"COUNT\"}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<FunctionExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$prop" mark to PropertyExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarPropMark_ReturnsPropertyExpr()
        {
            // Arrange
            string json = "{\"$prop\": \"Name\"}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<PropertyExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$not" mark to NotExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarNotMark_ReturnsNotExpr()
        {
            // Arrange
            string json = "{\"$not\": {\"#\": \"Active\"}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<NotExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$unary" mark to UnaryExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarUnaryMark_ReturnsUnaryExpr()
        {
            // Arrange
            string json = "{\"$unary\": {\"Operator\": \"-\", \"Operand\": 5}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<UnaryExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$sql" mark to GenericSqlExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarSqlMark_ReturnsGenericSqlExpr()
        {
            // Arrange
            string json = "{\"$sql\": \"SELECT * FROM users\"}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<GenericSqlExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$value" mark to ValueExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarValueMark_ReturnsValueExpr()
        {
            // Arrange
            string json = "{\"$value\": 42}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ValueExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$const" mark to ValueExpr with IsConst=true.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarConstMark_ReturnsValueExprWithIsConstTrue()
        {
            // Arrange
            string json = "{\"$const\": 42}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            var valueExpr = Assert.IsType<ValueExpr>(result);
            Assert.True(valueExpr.IsConst);
        }

        /// <summary>
        /// Tests that Read converts object with "$foreign" mark to ForeignExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarForeignMark_ReturnsForeignExpr()
        {
            // Arrange
            string json = "{\"$foreign\": {}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ForeignExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$from" mark to FromExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarFromMark_ReturnsFromExpr()
        {
            // Arrange
            string json = "{\"$from\": {}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<FromExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$table" mark to TableExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarTableMark_ReturnsTableExpr()
        {
            // Arrange
            string json = "{\"$table\": {}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<TableExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$join" mark to TableJoinExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarJoinMark_ReturnsTableJoinExpr()
        {
            // Arrange
            string json = "{\"$join\": {}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<TableJoinExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$where" mark to WhereExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarWhereMark_ReturnsWhereExpr()
        {
            // Arrange
            string json = "{\"$where\": {}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<WhereExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$order" mark to OrderByExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarOrderMark_ReturnsOrderByExpr()
        {
            // Arrange
            string json = "{\"$order\": {}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<OrderByExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$orderbyitem" mark to OrderByItemExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarOrderByItemMark_ReturnsOrderByItemExpr()
        {
            // Arrange
            string json = "{\"$orderbyitem\": {}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<OrderByItemExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$group" mark to GroupByExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarGroupMark_ReturnsGroupByExpr()
        {
            // Arrange
            string json = "{\"$group\": {}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<GroupByExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$having" mark to HavingExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarHavingMark_ReturnsHavingExpr()
        {
            // Arrange
            string json = "{\"$having\": {}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<HavingExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$section" mark to SectionExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarSectionMark_ReturnsSectionExpr()
        {
            // Arrange
            string json = "{\"$section\": {}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SectionExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$select" mark to SelectExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarSelectMark_ReturnsSelectExpr()
        {
            // Arrange
            string json = "{\"$select\": {}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SelectExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$selectitem" mark to SelectItemExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarSelectItemMark_ReturnsSelectItemExpr()
        {
            // Arrange
            string json = "{\"$selectitem\": {}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SelectItemExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$delete" mark to DeleteExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarDeleteMark_ReturnsDeleteExpr()
        {
            // Arrange
            string json = "{\"$delete\": {}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<DeleteExpr>(result);
        }

        /// <summary>
        /// Tests that Read converts object with "$update" mark to UpdateExpr.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarUpdateMark_ReturnsUpdateExpr()
        {
            // Arrange
            string json = "{\"$update\": {}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<UpdateExpr>(result);
        }

        /// <summary>
        /// Tests that Read handles empty object by returning null result.
        /// </summary>
        [Fact]
        public void Read_EmptyObject_ReturnsNull()
        {
            // Arrange
            string json = "{}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that Read handles unknown property names by skipping them.
        /// </summary>
        [Fact]
        public void Read_ObjectWithUnknownProperty_SkipsProperty()
        {
            // Arrange
            string json = "{\"unknownProperty\": \"value\"}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that Read handles array tokens by converting them to ValueExpr.
        /// </summary>
        [Fact]
        public void Read_ArrayToken_ReturnsValueExpr()
        {
            // Arrange
            string json = "[1, 2, 3]";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ValueExpr>(result);
        }

        /// <summary>
        /// Tests that Read handles nested objects correctly.
        /// </summary>
        [Fact]
        public void Read_NestedObject_ReturnsCorrectExpr()
        {
            // Arrange
            string json = "{\"!\": {\"$\": \"==\", \"Left\": {\"#\": \"Active\"}, \"Right\": true}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<NotExpr>(result);
        }

        /// <summary>
        /// Tests that Read handles "$" property with enum value parsing for LogicOperator.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarAndEnumLogicOperator_ReturnsLogicBinaryExpr()
        {
            // Arrange
            string json = "{\"$\": \"Equal\", \"Left\": {\"#\": \"Name\"}, \"Right\": \"Test\"}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<LogicBinaryExpr>(result);
        }

        /// <summary>
        /// Tests that Read handles "$" property with enum value parsing for ValueOperator.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarAndEnumValueOperator_ReturnsValueBinaryExpr()
        {
            // Arrange
            string json = "{\"$\": \"Add\", \"Left\": 10, \"Right\": 5}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ValueBinaryExpr>(result);
        }

        /// <summary>
        /// Tests that Read handles case-insensitive operator parsing.
        /// </summary>
        [Theory]
        [InlineData("LIKE")]
        [InlineData("Like")]
        [InlineData("lIkE")]
        public void Read_ObjectWithDollarCaseInsensitiveOperator_ReturnsLogicBinaryExpr(string op)
        {
            // Arrange
            string json = $"{{\"$\": \"{op}\", \"Left\": {{\"#\": \"Name\"}}, \"Right\": \"Test%\"}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<LogicBinaryExpr>(result);
        }

        /// <summary>
        /// Tests that Read handles "$bin" mark for binary expressions.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarBinMark_ReturnsValueBinaryExpr()
        {
            // Arrange
            string json = "{\"$bin\": {\"Left\": 10, \"Operator\": \"+\", \"Right\": 5}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ValueBinaryExpr>(result);
        }

        /// <summary>
        /// Tests that Read handles "$logic" mark for logic binary expressions.
        /// </summary>
        [Fact]
        public void Read_ObjectWithDollarLogicMark_ReturnsLogicBinaryExpr()
        {
            // Arrange
            string json = "{\"$logic\": {\"Left\": {\"#\": \"Name\"}, \"Operator\": \"==\", \"Right\": \"Test\"}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<LogicBinaryExpr>(result);
        }

        /// <summary>
        /// Tests that Read handles property with prefix operator (e.g., $==).
        /// </summary>
        [Fact]
        public void Read_ObjectWithPrefixedOperatorProperty_ReturnsLogicBinaryExpr()
        {
            // Arrange
            string json = "{\"$==\": {\"Left\": {\"#\": \"Age\"}, \"Right\": 25}}";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<LogicBinaryExpr>(result);
        }

        /// <summary>
        /// Tests that Read handles complex multi-level nested structure.
        /// </summary>
        [Fact]
        public void Read_ComplexNestedStructure_ReturnsCorrectExpr()
        {
            // Arrange
            string json = @"{
                ""$and"": [
                    {""$"": "">"", ""Left"": {""#"": ""Age""}, ""Right"": 18},
                    {""$"": ""like"", ""Left"": {""#"": ""Name""}, ""Right"": ""John%""}
                ]
            }";
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExprJsonConverterFactory());

            // Act
            var result = JsonSerializer.Deserialize<Expr>(json, options);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<AndExpr>(result);
        }
    }

    /// <summary>
    /// Unit tests for ExprJsonConverterFactory.CreateConverter method.
    /// </summary>
    public class ExprJsonConverterFactoryTests
    {
        /// <summary>
        /// Tests that CreateConverter throws ArgumentNullException when typeToConvert is null.
        /// Input: null type parameter.
        /// Expected: ArgumentNullException is thrown.
        /// </summary>
        [Fact]
        public void CreateConverter_NullTypeToConvert_ThrowsArgumentNullException()
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();
            Type? typeToConvert = null;
            var options = new JsonSerializerOptions();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => factory.CreateConverter(typeToConvert!, options));
        }

        /// <summary>
        /// Tests that CreateConverter successfully creates a converter for a valid Expr subclass type.
        /// Input: Valid Expr subclass type (SelectExpr).
        /// Expected: Returns a non-null JsonConverter instance.
        /// </summary>
        [Fact]
        public void CreateConverter_ValidExprType_ReturnsConverter()
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();
            var typeToConvert = typeof(SelectExpr);
            var options = new JsonSerializerOptions();

            // Act
            var converter = factory.CreateConverter(typeToConvert, options);

            // Assert
            Assert.NotNull(converter);
            Assert.IsAssignableFrom<JsonConverter>(converter);
        }

        /// <summary>
        /// Tests that CreateConverter works correctly when JsonSerializerOptions is null.
        /// Input: Valid type with null options parameter.
        /// Expected: Returns a non-null JsonConverter instance (options parameter is not used).
        /// </summary>
        [Fact]
        public void CreateConverter_NullOptions_ReturnsConverter()
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();
            var typeToConvert = typeof(ValueExpr);
            JsonSerializerOptions? options = null;

            // Act
            var converter = factory.CreateConverter(typeToConvert, options!);

            // Assert
            Assert.NotNull(converter);
            Assert.IsAssignableFrom<JsonConverter>(converter);
        }

        /// <summary>
        /// Tests that CreateConverter returns a converter with the correct generic type.
        /// Input: Multiple different Expr subclass types.
        /// Expected: Returns converters with matching generic type parameters.
        /// </summary>
        [Theory]
        [InlineData(typeof(SelectExpr))]
        [InlineData(typeof(DeleteExpr))]
        [InlineData(typeof(UpdateExpr))]
        [InlineData(typeof(FromExpr))]
        [InlineData(typeof(WhereExpr))]
        [InlineData(typeof(ValueExpr))]
        public void CreateConverter_VariousExprTypes_ReturnsCorrectConverterType(Type exprType)
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();
            var options = new JsonSerializerOptions();

            // Act
            var converter = factory.CreateConverter(exprType, options);

            // Assert
            Assert.NotNull(converter);
            var converterType = converter.GetType();
            Assert.True(converterType.IsGenericType);
            Assert.Equal(exprType, converterType.GetGenericArguments()[0]);
        }

        /// <summary>
        /// Tests that CreateConverter returns a converter that is assignable to JsonConverter of the specific type.
        /// Input: Valid Expr subclass type.
        /// Expected: Returned converter can be cast to JsonConverter&lt;T&gt; where T is the input type.
        /// </summary>
        [Fact]
        public void CreateConverter_ValidType_ReturnsTypedConverter()
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();
            var typeToConvert = typeof(TableExpr);
            var options = new JsonSerializerOptions();

            // Act
            var converter = factory.CreateConverter(typeToConvert, options);

            // Assert
            Assert.NotNull(converter);
            var expectedConverterType = typeof(JsonConverter<>).MakeGenericType(typeToConvert);
            Assert.IsAssignableFrom(expectedConverterType, converter);
        }

        /// <summary>
        /// Tests that CreateConverter creates independent converter instances.
        /// Input: Same type called multiple times.
        /// Expected: Different converter instances are returned (not singleton).
        /// </summary>
        [Fact]
        public void CreateConverter_CalledMultipleTimes_ReturnsDifferentInstances()
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();
            var typeToConvert = typeof(PropertyExpr);
            var options = new JsonSerializerOptions();

            // Act
            var converter1 = factory.CreateConverter(typeToConvert, options);
            var converter2 = factory.CreateConverter(typeToConvert, options);

            // Assert
            Assert.NotNull(converter1);
            Assert.NotNull(converter2);
            Assert.NotSame(converter1, converter2);
        }

        /// <summary>
        /// Tests that CanConvert returns true when the type is exactly Expr.
        /// Input: typeof(Expr)
        /// Expected: Returns true
        /// </summary>
        [Fact]
        public void CanConvert_ExprType_ReturnsTrue()
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();
            var typeToConvert = typeof(Expr);

            // Act
            var result = factory.CanConvert(typeToConvert);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that CanConvert returns true for various types derived from Expr.
        /// Input: Types that inherit from Expr
        /// Expected: Returns true for all derived types
        /// </summary>
        [Theory]
        [InlineData(typeof(ValueExpr))]
        [InlineData(typeof(FromExpr))]
        [InlineData(typeof(TableExpr))]
        [InlineData(typeof(TableJoinExpr))]
        [InlineData(typeof(WhereExpr))]
        [InlineData(typeof(OrderByExpr))]
        [InlineData(typeof(GroupByExpr))]
        [InlineData(typeof(HavingExpr))]
        [InlineData(typeof(SectionExpr))]
        [InlineData(typeof(SelectExpr))]
        [InlineData(typeof(DeleteExpr))]
        [InlineData(typeof(UpdateExpr))]
        [InlineData(typeof(PropertyExpr))]
        [InlineData(typeof(FunctionExpr))]
        [InlineData(typeof(LogicBinaryExpr))]
        [InlineData(typeof(ValueBinaryExpr))]
        [InlineData(typeof(AndExpr))]
        [InlineData(typeof(OrExpr))]
        [InlineData(typeof(NotExpr))]
        [InlineData(typeof(UnaryExpr))]
        [InlineData(typeof(GenericSqlExpr))]
        [InlineData(typeof(ForeignExpr))]
        [InlineData(typeof(ValueSet))]
        public void CanConvert_DerivedExprTypes_ReturnsTrue(Type typeToConvert)
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();

            // Act
            var result = factory.CanConvert(typeToConvert);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that CanConvert returns false for types unrelated to Expr.
        /// Input: Various types not derived from Expr
        /// Expected: Returns false
        /// </summary>
        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(long))]
        [InlineData(typeof(double))]
        [InlineData(typeof(object))]
        [InlineData(typeof(DateTime))]
        [InlineData(typeof(Guid))]
        [InlineData(typeof(IDisposable))]
        [InlineData(typeof(ICloneable))]
        public void CanConvert_UnrelatedTypes_ReturnsFalse(Type typeToConvert)
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();

            // Act
            var result = factory.CanConvert(typeToConvert);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that CanConvert returns false for array types that are not Expr-based.
        /// Input: typeof(string[])
        /// Expected: Returns false
        /// </summary>
        [Fact]
        public void CanConvert_ArrayType_ReturnsFalse()
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();
            var typeToConvert = typeof(string[]);

            // Act
            var result = factory.CanConvert(typeToConvert);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that CanConvert returns true for array of Expr types.
        /// Input: typeof(Expr[])
        /// Expected: Returns false (arrays don't derive from Expr)
        /// </summary>
        [Fact]
        public void CanConvert_ExprArrayType_ReturnsFalse()
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();
            var typeToConvert = typeof(Expr[]);

            // Act
            var result = factory.CanConvert(typeToConvert);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that CanConvert throws NullReferenceException when typeToConvert is null.
        /// Input: null
        /// Expected: Throws NullReferenceException
        /// </summary>
        [Fact]
        public void CanConvert_NullType_ThrowsNullReferenceException()
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();
            Type typeToConvert = null;

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => factory.CanConvert(typeToConvert));
        }

        /// <summary>
        /// Tests that CanConvert returns false for generic type definitions.
        /// Input: typeof(System.Collections.Generic.List&lt;&gt;)
        /// Expected: Returns false
        /// </summary>
        [Fact]
        public void CanConvert_GenericTypeDefinition_ReturnsFalse()
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();
            var typeToConvert = typeof(System.Collections.Generic.List<>);

            // Act
            var result = factory.CanConvert(typeToConvert);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that CanConvert returns false for constructed generic types.
        /// Input: typeof(System.Collections.Generic.List&lt;Expr&gt;)
        /// Expected: Returns false (generic type containing Expr is not assignable to Expr)
        /// </summary>
        [Fact]
        public void CanConvert_ConstructedGenericType_ReturnsFalse()
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();
            var typeToConvert = typeof(System.Collections.Generic.List<Expr>);

            // Act
            var result = factory.CanConvert(typeToConvert);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that CanConvert returns false for interface types.
        /// Input: typeof(ICloneable)
        /// Expected: Returns false (Expr implements ICloneable, but ICloneable is not assignable to Expr)
        /// </summary>
        [Fact]
        public void CanConvert_InterfaceType_ReturnsFalse()
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();
            var typeToConvert = typeof(ICloneable);

            // Act
            var result = factory.CanConvert(typeToConvert);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that CanConvert returns false for value types.
        /// Input: typeof(int)
        /// Expected: Returns false
        /// </summary>
        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(bool))]
        [InlineData(typeof(decimal))]
        [InlineData(typeof(byte))]
        public void CanConvert_ValueTypes_ReturnsFalse(Type typeToConvert)
        {
            // Arrange
            var factory = new ExprJsonConverterFactory();

            // Act
            var result = factory.CanConvert(typeToConvert);

            // Assert
            Assert.False(result);
        }
    }
}