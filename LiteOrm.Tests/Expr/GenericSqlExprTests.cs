using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using LiteOrm;
using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the GenericSqlExpr class.
    /// </summary>
    public partial class GenericSqlExprTests
    {
        #region Constructor Tests

        /// <summary>
        /// Tests that the constructor with key parameter correctly assigns the key to the Key property
        /// for various valid string inputs including edge cases.
        /// </summary>
        /// <param name="key">The key value to test.</param>
        [Theory]
        [InlineData("TestKey")]
        [InlineData("Key123")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Test@Key#123")]
        [InlineData("Key.With.Dots")]
        [InlineData("KeyWithUnicode_日本語")]
        [InlineData("a")]
        [InlineData("VeryLongKeyValueWithManyCharactersThatExceedsNormalLengthToTestBoundaryConditionsForStringHandling1234567890")]
        public void Constructor_WithKeyParameter_SetsKeyPropertyCorrectly(string? key)
        {
            // Arrange & Act
            var expr = new GenericSqlExpr(key!);

            // Assert
            Assert.Equal(key, expr.Key);
        }

        /// <summary>
        /// Tests that the constructor with null key parameter correctly assigns null to the Key property.
        /// The source code does not use nullable reference types, so null is a valid input.
        /// </summary>
        [Fact]
        public void Constructor_WithNullKey_SetsKeyPropertyToNull()
        {
            // Arrange
            string? key = null;

            // Act
            var expr = new GenericSqlExpr(key!);

            // Assert
            Assert.Null(expr.Key);
        }

        /// <summary>
        /// Tests that the constructor with key parameter creates a valid object instance
        /// and does not throw any exceptions for valid inputs.
        /// </summary>
        [Fact]
        public void Constructor_WithKeyParameter_CreatesValidInstance()
        {
            // Arrange
            var key = "ValidKey";

            // Act
            var expr = new GenericSqlExpr(key);

            // Assert
            Assert.NotNull(expr);
            Assert.IsType<GenericSqlExpr>(expr);
            Assert.Equal(key, expr.Key);
        }

        /// <summary>
        /// Tests that the constructor with key parameter correctly handles special control characters.
        /// </summary>
        [Theory]
        [InlineData("\n")]
        [InlineData("\t")]
        [InlineData("\r\n")]
        [InlineData("Key\nWith\nNewlines")]
        [InlineData("Key\tWith\tTabs")]
        public void Constructor_WithKeyContainingControlCharacters_SetsKeyPropertyCorrectly(string key)
        {
            // Arrange & Act
            var expr = new GenericSqlExpr(key);

            // Assert
            Assert.Equal(key, expr.Key);
        }

        #endregion

        /// <summary>
        /// Tests that Register throws ArgumentNullException when key parameter is null.
        /// </summary>
        [Fact]
        public void Register_NullKey_ThrowsArgumentNullException()
        {
            // Arrange
            string? key = null;
            SqlGenerateHandler func = (ctx, builder, pms, arg) => "SQL";

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => GenericSqlExpr.Register(key!, func));
            Assert.Equal("key", exception.ParamName);
        }

        /// <summary>
        /// Tests that Register throws ArgumentNullException when func parameter is null.
        /// </summary>
        [Fact]
        public void Register_NullFunc_ThrowsArgumentNullException()
        {
            // Arrange
            string key = "TestKey_NullFunc";
            SqlGenerateHandler? func = null;

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => GenericSqlExpr.Register(key, func!));
            Assert.Equal("func", exception.ParamName);
        }

        /// <summary>
        /// Tests that Register successfully registers a new key and returns GenericSqlExpr with correct Key property.
        /// </summary>
        [Fact]
        public void Register_NewKey_ReturnsGenericSqlExprWithCorrectKey()
        {
            // Arrange
            string key = "TestKey_NewKey_" + Guid.NewGuid().ToString();
            SqlGenerateHandler func = (ctx, builder, pms, arg) => "SQL";

            // Act
            var result = GenericSqlExpr.Register(key, func);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(key, result.Key);
        }

        /// <summary>
        /// Tests that Register with isValue=true successfully registers the expression.
        /// </summary>
        [Fact]
        public void Register_WithIsValueTrue_ReturnsGenericSqlExpr()
        {
            // Arrange
            string key = "TestKey_IsValueTrue_" + Guid.NewGuid().ToString();
            SqlGenerateHandler func = (ctx, builder, pms, arg) => "SQL";

            // Act
            var result = GenericSqlExpr.Register(key, func, isValue: true);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(key, result.Key);
        }

        /// <summary>
        /// Tests that Register with isValue=false successfully registers the expression.
        /// </summary>
        [Fact]
        public void Register_WithIsValueFalse_ReturnsGenericSqlExpr()
        {
            // Arrange
            string key = "TestKey_IsValueFalse_" + Guid.NewGuid().ToString();
            SqlGenerateHandler func = (ctx, builder, pms, arg) => "SQL";

            // Act
            var result = GenericSqlExpr.Register(key, func, isValue: false);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(key, result.Key);
        }

        /// <summary>
        /// Tests that registering the same key twice returns GenericSqlExpr without throwing exception.
        /// </summary>
        [Fact]
        public void Register_DuplicateKey_ReturnsGenericSqlExprWithoutException()
        {
            // Arrange
            string key = "TestKey_Duplicate_" + Guid.NewGuid().ToString();
            SqlGenerateHandler func1 = (ctx, builder, pms, arg) => "SQL1";
            SqlGenerateHandler func2 = (ctx, builder, pms, arg) => "SQL2";

            // Act
            var result1 = GenericSqlExpr.Register(key, func1, isValue: true);
            var result2 = GenericSqlExpr.Register(key, func2, isValue: false);

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.Equal(key, result1.Key);
            Assert.Equal(key, result2.Key);
        }

        /// <summary>
        /// Tests that Register handles empty string key correctly.
        /// </summary>
        [Fact]
        public void Register_EmptyStringKey_ReturnsGenericSqlExpr()
        {
            // Arrange
            string key = string.Empty;
            SqlGenerateHandler func = (ctx, builder, pms, arg) => "SQL";

            // Act
            var result = GenericSqlExpr.Register(key, func);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(key, result.Key);
        }

        /// <summary>
        /// Tests that Register handles whitespace-only key correctly.
        /// </summary>
        [Fact]
        public void Register_WhitespaceKey_ReturnsGenericSqlExpr()
        {
            // Arrange
            string key = "   \t\n\r   ";
            SqlGenerateHandler func = (ctx, builder, pms, arg) => "SQL";

            // Act
            var result = GenericSqlExpr.Register(key, func);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(key, result.Key);
        }

        /// <summary>
        /// Tests that Register handles very long key correctly.
        /// </summary>
        [Fact]
        public void Register_VeryLongKey_ReturnsGenericSqlExpr()
        {
            // Arrange
            string key = new string('A', 10000);
            SqlGenerateHandler func = (ctx, builder, pms, arg) => "SQL";

            // Act
            var result = GenericSqlExpr.Register(key, func);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(key, result.Key);
        }

        /// <summary>
        /// Tests that Register handles keys with special characters correctly.
        /// </summary>
        [Theory]
        [InlineData("Key!@#$%^&*()")]
        [InlineData("Key<>?:\"{}|")]
        [InlineData("Key\0\t\n\r")]
        [InlineData("KeyWith中文字符")]
        [InlineData("KeyWith😀Emoji")]
        public void Register_SpecialCharactersInKey_ReturnsGenericSqlExpr(string key)
        {
            // Arrange
            SqlGenerateHandler func = (ctx, builder, pms, arg) => "SQL";

            // Act
            var result = GenericSqlExpr.Register(key, func);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(key, result.Key);
        }

        /// <summary>
        /// Tests that registered handler can be invoked via GenerateSql method after registration.
        /// </summary>
        [Fact]
        public void Register_RegisteredHandler_CanBeInvokedViaGenerateSql()
        {
            // Arrange
            string key = "TestKey_Handler_" + Guid.NewGuid().ToString();
            string expectedSql = "CUSTOM SQL";
            SqlGenerateHandler func = (ctx, builder, pms, arg) => expectedSql;

            // Act
            var result = GenericSqlExpr.Register(key, func);
            var generatedSql = result.GenerateSql(null!, null!, new List<KeyValuePair<string, object>>());

            // Assert
            Assert.Equal(expectedSql, generatedSql);
        }

        /// <summary>
        /// Tests that Register with different keys creates independent registry entries.
        /// </summary>
        [Fact]
        public void Register_DifferentKeys_CreatesIndependentEntries()
        {
            // Arrange
            string key1 = "TestKey_Independent1_" + Guid.NewGuid().ToString();
            string key2 = "TestKey_Independent2_" + Guid.NewGuid().ToString();
            SqlGenerateHandler func1 = (ctx, builder, pms, arg) => "SQL1";
            SqlGenerateHandler func2 = (ctx, builder, pms, arg) => "SQL2";

            // Act
            var result1 = GenericSqlExpr.Register(key1, func1);
            var result2 = GenericSqlExpr.Register(key2, func2);

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.Equal(key1, result1.Key);
            Assert.Equal(key2, result2.Key);
            Assert.NotEqual(result1.Key, result2.Key);
        }

        /// <summary>
        /// Tests that GenerateSql returns null when Key is null.
        /// </summary>
        [Fact]
        public void GenerateSql_KeyIsNull_ReturnsNull()
        {
            // Arrange
            var expr = new GenericSqlExpr { Key = null, Arg = "test" };
            var context = new SqlBuildContext();
            var mockSqlBuilder = new Mock<ISqlBuilder>();
            var outputParams = new List<KeyValuePair<string, object>>();

            // Act
            var result = expr.GenerateSql(context, mockSqlBuilder.Object, outputParams);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GenerateSql returns null when Key is an empty string.
        /// </summary>
        [Fact]
        public void GenerateSql_KeyIsEmpty_ReturnsNull()
        {
            // Arrange
            var expr = new GenericSqlExpr { Key = string.Empty, Arg = "test" };
            var context = new SqlBuildContext();
            var mockSqlBuilder = new Mock<ISqlBuilder>();
            var outputParams = new List<KeyValuePair<string, object>>();

            // Act
            var result = expr.GenerateSql(context, mockSqlBuilder.Object, outputParams);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GenerateSql returns null when Key is whitespace only.
        /// </summary>
        [Fact]
        public void GenerateSql_KeyIsWhitespace_ReturnsNull()
        {
            // Arrange
            var expr = new GenericSqlExpr { Key = "   ", Arg = "test" };
            var context = new SqlBuildContext();
            var mockSqlBuilder = new Mock<ISqlBuilder>();
            var outputParams = new List<KeyValuePair<string, object>>();

            // Act
            var result = expr.GenerateSql(context, mockSqlBuilder.Object, outputParams);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GenerateSql throws KeyNotFoundException when Key is not registered.
        /// </summary>
        [Fact]
        public void GenerateSql_KeyNotRegistered_ThrowsKeyNotFoundException()
        {
            // Arrange
            var expr = new GenericSqlExpr { Key = "NonExistentKey_" + Guid.NewGuid().ToString(), Arg = null };
            var context = new SqlBuildContext();
            var mockSqlBuilder = new Mock<ISqlBuilder>();
            var outputParams = new List<KeyValuePair<string, object>>();

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => expr.GenerateSql(context, mockSqlBuilder.Object, outputParams));
        }

        /// <summary>
        /// Tests that GenerateSql invokes the registered handler and returns its result.
        /// </summary>
        [Fact]
        public void GenerateSql_RegisteredHandler_ReturnsHandlerResult()
        {
            // Arrange
            var key = "TestKey_" + Guid.NewGuid().ToString();
            var expectedResult = "SELECT * FROM TestTable";
            GenericSqlExpr.Register(key, (ctx, builder, pms, arg) => expectedResult);
            var expr = new GenericSqlExpr { Key = key, Arg = null };
            var context = new SqlBuildContext();
            var mockSqlBuilder = new Mock<ISqlBuilder>();
            var outputParams = new List<KeyValuePair<string, object>>();

            // Act
            var result = expr.GenerateSql(context, mockSqlBuilder.Object, outputParams);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        /// <summary>
        /// Tests that GenerateSql passes the correct parameters to the registered handler.
        /// </summary>
        [Fact]
        public void GenerateSql_RegisteredHandler_PassesCorrectParameters()
        {
            // Arrange
            var key = "TestKey_" + Guid.NewGuid().ToString();
            SqlBuildContext capturedContext = null;
            ISqlBuilder capturedBuilder = null;
            ICollection<KeyValuePair<string, object>> capturedParams = null;
            object capturedArg = null;

            GenericSqlExpr.Register(key, (ctx, builder, pms, arg) =>
            {
                capturedContext = ctx;
                capturedBuilder = builder;
                capturedParams = pms;
                capturedArg = arg;
                return "SQL";
            });

            var expr = new GenericSqlExpr { Key = key, Arg = "TestArg" };
            var context = new SqlBuildContext();
            var mockSqlBuilder = new Mock<ISqlBuilder>();
            var outputParams = new List<KeyValuePair<string, object>>();

            // Act
            expr.GenerateSql(context, mockSqlBuilder.Object, outputParams);

            // Assert
            Assert.Same(context, capturedContext);
            Assert.Same(mockSqlBuilder.Object, capturedBuilder);
            Assert.Same(outputParams, capturedParams);
            Assert.Equal("TestArg", capturedArg);
        }

        /// <summary>
        /// Tests that GenerateSql passes different Arg values to the handler correctly.
        /// </summary>
        /// <param name="argValue">The Arg value to test.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("StringArg")]
        [InlineData(123)]
        public void GenerateSql_DifferentArgValues_PassesCorrectArg(object argValue)
        {
            // Arrange
            var key = "TestKey_" + Guid.NewGuid().ToString();
            object capturedArg = new object(); // Initialize with different object

            GenericSqlExpr.Register(key, (ctx, builder, pms, arg) =>
            {
                capturedArg = arg;
                return "SQL";
            });

            var expr = new GenericSqlExpr { Key = key, Arg = argValue };
            var context = new SqlBuildContext();
            var mockSqlBuilder = new Mock<ISqlBuilder>();
            var outputParams = new List<KeyValuePair<string, object>>();

            // Act
            expr.GenerateSql(context, mockSqlBuilder.Object, outputParams);

            // Assert
            Assert.Equal(argValue, capturedArg);
        }

        /// <summary>
        /// Tests that GenerateSql returns null when the registered handler returns null.
        /// </summary>
        [Fact]
        public void GenerateSql_HandlerReturnsNull_ReturnsNull()
        {
            // Arrange
            var key = "TestKey_" + Guid.NewGuid().ToString();
            GenericSqlExpr.Register(key, (ctx, builder, pms, arg) => null);
            var expr = new GenericSqlExpr { Key = key, Arg = null };
            var context = new SqlBuildContext();
            var mockSqlBuilder = new Mock<ISqlBuilder>();
            var outputParams = new List<KeyValuePair<string, object>>();

            // Act
            var result = expr.GenerateSql(context, mockSqlBuilder.Object, outputParams);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GenerateSql allows the handler to modify the outputParams collection.
        /// </summary>
        [Fact]
        public void GenerateSql_HandlerModifiesOutputParams_OutputParamsContainsAddedItems()
        {
            // Arrange
            var key = "TestKey_" + Guid.NewGuid().ToString();
            GenericSqlExpr.Register(key, (ctx, builder, pms, arg) =>
            {
                pms.Add(new KeyValuePair<string, object>("@param1", "value1"));
                pms.Add(new KeyValuePair<string, object>("@param2", 42));
                return "SQL";
            });

            var expr = new GenericSqlExpr { Key = key, Arg = null };
            var context = new SqlBuildContext();
            var mockSqlBuilder = new Mock<ISqlBuilder>();
            var outputParams = new List<KeyValuePair<string, object>>();

            // Act
            expr.GenerateSql(context, mockSqlBuilder.Object, outputParams);

            // Assert
            Assert.Equal(2, outputParams.Count);
            Assert.Contains(new KeyValuePair<string, object>("@param1", "value1"), outputParams);
            Assert.Contains(new KeyValuePair<string, object>("@param2", 42), outputParams);
        }

        /// <summary>
        /// Tests that GenerateSql returns an empty string when the handler returns an empty string.
        /// </summary>
        [Fact]
        public void GenerateSql_HandlerReturnsEmptyString_ReturnsEmptyString()
        {
            // Arrange
            var key = "TestKey_" + Guid.NewGuid().ToString();
            GenericSqlExpr.Register(key, (ctx, builder, pms, arg) => string.Empty);
            var expr = new GenericSqlExpr { Key = key, Arg = null };
            var context = new SqlBuildContext();
            var mockSqlBuilder = new Mock<ISqlBuilder>();
            var outputParams = new List<KeyValuePair<string, object>>();

            // Act
            var result = expr.GenerateSql(context, mockSqlBuilder.Object, outputParams);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        /// <summary>
        /// Tests that GenerateSql works correctly with complex Arg objects.
        /// </summary>
        [Fact]
        public void GenerateSql_ComplexArgObject_PassesCorrectArg()
        {
            // Arrange
            var key = "TestKey_" + Guid.NewGuid().ToString();
            var complexArg = new { Name = "Test", Value = 123 };
            object capturedArg = null;

            GenericSqlExpr.Register(key, (ctx, builder, pms, arg) =>
            {
                capturedArg = arg;
                return "SQL";
            });

            var expr = new GenericSqlExpr { Key = key, Arg = complexArg };
            var context = new SqlBuildContext();
            var mockSqlBuilder = new Mock<ISqlBuilder>();
            var outputParams = new List<KeyValuePair<string, object>>();

            // Act
            expr.GenerateSql(context, mockSqlBuilder.Object, outputParams);

            // Assert
            Assert.Same(complexArg, capturedArg);
        }

        /// <summary>
        /// Tests that ToString returns the expected formatted string for various key values.
        /// Input: Various key values including null, empty, whitespace, special characters, and long strings.
        /// Expected: "[Sql:{key}]" format for each input, with null treated as empty string.
        /// </summary>
        [Theory]
        [InlineData("TestKey", "[Sql:TestKey]")]
        [InlineData(null, "[Sql:]")]
        [InlineData("", "[Sql:]")]
        [InlineData("   ", "[Sql:   ]")]
        [InlineData("\t", "[Sql:\t]")]
        [InlineData("Test@#$%^&*()Key", "[Sql:Test@#$%^&*()Key]")]
        [InlineData("Key With Spaces", "[Sql:Key With Spaces]")]
        [InlineData("Key\nWith\nNewlines", "[Sql:Key\nWith\nNewlines]")]
        [InlineData("Key\r\nWith\r\nCRLF", "[Sql:Key\r\nWith\r\nCRLF]")]
        [InlineData("Unicode_键_مفتاح", "[Sql:Unicode_键_مفتاح]")]
        [InlineData("Key:With:Colons", "[Sql:Key:With:Colons]")]
        [InlineData("[Key]", "[Sql:[Key]]")]
        [InlineData("Very_Long_Key_String_That_Contains_Many_Characters_To_Test_Handling_Of_Longer_Inputs_Without_Issues", "[Sql:Very_Long_Key_String_That_Contains_Many_Characters_To_Test_Handling_Of_Longer_Inputs_Without_Issues]")]
        public void ToString_WithVariousKeys_ReturnsExpectedFormattedString(string key, string expected)
        {
            // Arrange
            var expr = new GenericSqlExpr { Key = key };

            // Act
            string result = expr.ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that Get throws ArgumentNullException when key is null.
        /// </summary>
        [Fact]
        public void Get_NullKey_ThrowsArgumentNullException()
        {
            // Arrange
            string? key = null;

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => GenericSqlExpr.Get(key!));
            Assert.Equal("key", exception.ParamName);
        }

        /// <summary>
        /// Tests that Get returns a GenericSqlExpr instance when the key exists in the registry.
        /// </summary>
        [Fact]
        public void Get_ExistingKey_ReturnsGenericSqlExpr()
        {
            // Arrange
            string key = "Get_ExistingKey_Test";
            GenericSqlExpr.Register(key, (ctx, builder, pms, arg) => "TEST SQL");

            // Act
            GenericSqlExpr result = GenericSqlExpr.Get(key);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(key, result.Key);
            Assert.Equal(ExprType.GenericSql, result.ExprType);
        }

        /// <summary>
        /// Tests that Get throws KeyNotFoundException when the key does not exist in the registry.
        /// </summary>
        [Fact]
        public void Get_NonExistingKey_ThrowsKeyNotFoundException()
        {
            // Arrange
            string key = "Get_NonExistingKey_UniqueTest_" + Guid.NewGuid().ToString();

            // Act & Assert
            var exception = Assert.Throws<KeyNotFoundException>(() => GenericSqlExpr.Get(key));
            Assert.Contains(key, exception.Message);
            Assert.Contains("was not found in the registry", exception.Message);
        }

        /// <summary>
        /// Tests that Get throws KeyNotFoundException when the key is an empty string and not registered.
        /// </summary>
        [Fact]
        public void Get_EmptyStringKeyNotRegistered_ThrowsKeyNotFoundException()
        {
            // Arrange
            string key = string.Empty;

            // Act & Assert
            var exception = Assert.Throws<KeyNotFoundException>(() => GenericSqlExpr.Get(key));
            Assert.Contains("was not found in the registry", exception.Message);
        }

        /// <summary>
        /// Tests that Get returns a GenericSqlExpr when the key is an empty string and is registered.
        /// </summary>
        [Fact]
        public void Get_EmptyStringKeyRegistered_ReturnsGenericSqlExpr()
        {
            // Arrange
            string key = string.Empty;
            GenericSqlExpr.Register(key, (ctx, builder, pms, arg) => "EMPTY KEY SQL");

            // Act
            GenericSqlExpr result = GenericSqlExpr.Get(key);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(key, result.Key);
        }

        /// <summary>
        /// Tests that Get throws KeyNotFoundException when the key is whitespace-only and not registered.
        /// </summary>
        [Fact]
        public void Get_WhitespaceKeyNotRegistered_ThrowsKeyNotFoundException()
        {
            // Arrange
            string key = "   ";

            // Act & Assert
            var exception = Assert.Throws<KeyNotFoundException>(() => GenericSqlExpr.Get(key));
            Assert.Contains("was not found in the registry", exception.Message);
        }

        /// <summary>
        /// Tests that Get returns a GenericSqlExpr when the key contains special characters and is registered.
        /// </summary>
        [Theory]
        [InlineData("key!@#$%^&*()")]
        [InlineData("key_with_unicode_中文")]
        [InlineData("key\twith\ttabs")]
        [InlineData("key\nwith\nnewlines")]
        public void Get_SpecialCharacterKeys_ReturnsGenericSqlExpr(string key)
        {
            // Arrange
            GenericSqlExpr.Register(key, (ctx, builder, pms, arg) => "SPECIAL CHAR SQL");

            // Act
            GenericSqlExpr result = GenericSqlExpr.Get(key);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(key, result.Key);
        }

        /// <summary>
        /// Tests that Get returns a GenericSqlExpr when the key is very long and is registered.
        /// </summary>
        [Fact]
        public void Get_VeryLongKey_ReturnsGenericSqlExpr()
        {
            // Arrange
            string key = new string('A', 10000);
            GenericSqlExpr.Register(key, (ctx, builder, pms, arg) => "LONG KEY SQL");

            // Act
            GenericSqlExpr result = GenericSqlExpr.Get(key);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(key, result.Key);
        }

        /// <summary>
        /// Tests that Get returns different instances for the same key.
        /// </summary>
        [Fact]
        public void Get_SameKey_ReturnsDifferentInstances()
        {
            // Arrange
            string key = "Get_SameKey_DifferentInstances_Test";
            GenericSqlExpr.Register(key, (ctx, builder, pms, arg) => "TEST SQL");

            // Act
            GenericSqlExpr result1 = GenericSqlExpr.Get(key);
            GenericSqlExpr result2 = GenericSqlExpr.Get(key);

            // Assert
            Assert.NotSame(result1, result2);
            Assert.Equal(result1.Key, result2.Key);
        }

        /// <summary>
        /// Tests that the default constructor creates a valid GenericSqlExpr instance
        /// with all properties initialized to their default values (null).
        /// </summary>
        [Fact]
        public void GenericSqlExpr_DefaultConstructor_CreatesInstanceWithNullProperties()
        {
            // Act
            var expr = new GenericSqlExpr();

            // Assert
            Assert.NotNull(expr);
            Assert.Null(expr.Key);
            Assert.Null(expr.Arg);
        }

        /// <summary>
        /// Tests that the default constructor creates an instance with correct ExprType.
        /// Expected: ExprType.GenericSql
        /// </summary>
        [Fact]
        public void GenericSqlExpr_DefaultConstructor_ReturnsCorrectExprType()
        {
            // Act
            var expr = new GenericSqlExpr();

            // Assert
            Assert.Equal(ExprType.GenericSql, expr.ExprType);
        }

        /// <summary>
        /// Tests that ToString() works correctly when Key is null (default constructor).
        /// Expected: Returns "[Sql:]" with empty key.
        /// </summary>
        [Fact]
        public void GenericSqlExpr_DefaultConstructor_ToStringReturnsExpectedFormat()
        {
            // Act
            var expr = new GenericSqlExpr();

            // Assert
            Assert.Equal("[Sql:]", expr.ToString());
        }

        /// <summary>
        /// Tests that GetHashCode() works correctly for default-constructed instance.
        /// Expected: No exception is thrown and hash code is computed.
        /// </summary>
        [Fact]
        public void GenericSqlExpr_DefaultConstructor_GetHashCodeDoesNotThrow()
        {
            // Act
            var expr = new GenericSqlExpr();
            var hashCode = expr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests that two default-constructed instances are equal.
        /// Expected: Both have null Key and Arg, so they should be equal.
        /// </summary>
        [Fact]
        public void GenericSqlExpr_DefaultConstructor_TwoInstancesAreEqual()
        {
            // Arrange
            var expr1 = new GenericSqlExpr();
            var expr2 = new GenericSqlExpr();

            // Assert
            Assert.True(expr1.Equals(expr2));
            Assert.Equal(expr1.GetHashCode(), expr2.GetHashCode());
        }

        /// <summary>
        /// Tests that GenerateSql() handles null Key gracefully (via null SqlHandler).
        /// Expected: Returns null without throwing exception.
        /// </summary>
        [Fact]
        public void GenericSqlExpr_DefaultConstructor_GenerateSqlReturnsNull()
        {
            // Arrange
            var expr = new GenericSqlExpr();
            var mockContext = new Mock<SqlBuildContext>();
            var mockSqlBuilder = new Mock<ISqlBuilder>();
            var outputParams = new List<KeyValuePair<string, object>>();

            // Act
            var result = expr.GenerateSql(mockContext.Object, mockSqlBuilder.Object, outputParams);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that Clone() works correctly for default-constructed instance.
        /// Expected: Returns a new instance with same null Key and Arg values.
        /// </summary>
        [Fact]
        public void GenericSqlExpr_DefaultConstructor_CloneCreatesEqualInstance()
        {
            // Arrange
            var expr = new GenericSqlExpr();

            // Act
            var cloned = (GenericSqlExpr)expr.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(expr, cloned);
            Assert.Null(cloned.Key);
            Assert.Null(cloned.Arg);
            Assert.True(expr.Equals(cloned));
        }

        /// <summary>
        /// Tests that default-constructed instance does not equal null.
        /// Expected: Equals(null) returns false.
        /// </summary>
        [Fact]
        public void GenericSqlExpr_DefaultConstructor_NotEqualToNull()
        {
            // Arrange
            var expr = new GenericSqlExpr();

            // Act & Assert
            Assert.False(expr.Equals(null));
        }

        /// <summary>
        /// Tests that default-constructed instance does not equal object of different type.
        /// Expected: Equals(differentTypeObject) returns false.
        /// </summary>
        [Fact]
        public void GenericSqlExpr_DefaultConstructor_NotEqualToDifferentType()
        {
            // Arrange
            var expr = new GenericSqlExpr();
            var differentObject = new object();

            // Act & Assert
            Assert.False(expr.Equals(differentObject));
        }

        /// <summary>
        /// Tests that properties can be set after default construction.
        /// Expected: Properties are mutable and can be assigned values.
        /// </summary>
        [Fact]
        public void GenericSqlExpr_DefaultConstructor_PropertiesAreMutable()
        {
            // Arrange
            var expr = new GenericSqlExpr();
            var testKey = "TestKey";
            var testArg = new object();

            // Act
            expr.Key = testKey;
            expr.Arg = testArg;

            // Assert
            Assert.Equal(testKey, expr.Key);
            Assert.Same(testArg, expr.Arg);
        }

        /// <summary>
        /// Verifies that GetHashCode returns consistent hash codes for objects with identical Key and Arg values.
        /// </summary>
        [Fact]
        public void GetHashCode_SameKeyAndArg_ReturnsSameHashCode()
        {
            // Arrange
            GenericSqlExpr.Register("TestKey1", (context, builder, outputParams, arg) => "SQL");
            var expr1 = new GenericSqlExpr("TestKey1") { Arg = 123 };
            var expr2 = new GenericSqlExpr("TestKey1") { Arg = 123 };

            // Act
            int hashCode1 = expr1.GetHashCode();
            int hashCode2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Verifies that GetHashCode returns different hash codes for objects with different Key values.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentKey_ReturnsDifferentHashCode()
        {
            // Arrange
            GenericSqlExpr.Register("TestKey2", (context, builder, outputParams, arg) => "SQL");
            GenericSqlExpr.Register("TestKey3", (context, builder, outputParams, arg) => "SQL");
            var expr1 = new GenericSqlExpr("TestKey2") { Arg = 123 };
            var expr2 = new GenericSqlExpr("TestKey3") { Arg = 123 };

            // Act
            int hashCode1 = expr1.GetHashCode();
            int hashCode2 = expr2.GetHashCode();

            // Assert
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Verifies that GetHashCode returns different hash codes for objects with different Arg values.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentArg_ReturnsDifferentHashCode()
        {
            // Arrange
            GenericSqlExpr.Register("TestKey4", (context, builder, outputParams, arg) => "SQL");
            var expr1 = new GenericSqlExpr("TestKey4") { Arg = 123 };
            var expr2 = new GenericSqlExpr("TestKey4") { Arg = 456 };

            // Act
            int hashCode1 = expr1.GetHashCode();
            int hashCode2 = expr2.GetHashCode();

            // Assert
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Verifies that GetHashCode handles null Key properly and returns a consistent hash code.
        /// </summary>
        [Fact]
        public void GetHashCode_NullKey_ReturnsConsistentHashCode()
        {
            // Arrange
            var expr1 = new GenericSqlExpr { Key = null, Arg = 123 };
            var expr2 = new GenericSqlExpr { Key = null, Arg = 123 };

            // Act
            int hashCode1 = expr1.GetHashCode();
            int hashCode2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Verifies that GetHashCode handles null Arg properly and returns a consistent hash code.
        /// </summary>
        [Fact]
        public void GetHashCode_NullArg_ReturnsConsistentHashCode()
        {
            // Arrange
            GenericSqlExpr.Register("TestKey5", (context, builder, outputParams, arg) => "SQL");
            var expr1 = new GenericSqlExpr("TestKey5") { Arg = null };
            var expr2 = new GenericSqlExpr("TestKey5") { Arg = null };

            // Act
            int hashCode1 = expr1.GetHashCode();
            int hashCode2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Verifies that GetHashCode handles both null Key and null Arg properly and returns a consistent hash code.
        /// </summary>
        [Fact]
        public void GetHashCode_BothNullKeyAndArg_ReturnsConsistentHashCode()
        {
            // Arrange
            var expr1 = new GenericSqlExpr { Key = null, Arg = null };
            var expr2 = new GenericSqlExpr { Key = null, Arg = null };

            // Act
            int hashCode1 = expr1.GetHashCode();
            int hashCode2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Verifies that GetHashCode returns the same hash code when called multiple times on the same object.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsConsistentValue()
        {
            // Arrange
            GenericSqlExpr.Register("TestKey6", (context, builder, outputParams, arg) => "SQL");
            var expr = new GenericSqlExpr("TestKey6") { Arg = "test" };

            // Act
            int hashCode1 = expr.GetHashCode();
            int hashCode2 = expr.GetHashCode();
            int hashCode3 = expr.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
            Assert.Equal(hashCode2, hashCode3);
        }

        /// <summary>
        /// Verifies that GetHashCode respects the equality contract: equal objects must have equal hash codes.
        /// </summary>
        [Fact]
        public void GetHashCode_EqualObjects_ReturnsSameHashCode()
        {
            // Arrange
            GenericSqlExpr.Register("TestKey7", (context, builder, outputParams, arg) => "SQL");
            var expr1 = new GenericSqlExpr("TestKey7") { Arg = 999 };
            var expr2 = new GenericSqlExpr("TestKey7") { Arg = 999 };

            // Act
            bool areEqual = expr1.Equals(expr2);
            int hashCode1 = expr1.GetHashCode();
            int hashCode2 = expr2.GetHashCode();

            // Assert
            Assert.True(areEqual);
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Verifies that GetHashCode produces different hash codes for different combinations of Key and Arg.
        /// Tests various combinations including null values, empty strings, numeric values, and string values.
        /// </summary>
        /// <param name="key1">First object's key value.</param>
        /// <param name="arg1">First object's arg value.</param>
        /// <param name="key2">Second object's key value.</param>
        /// <param name="arg2">Second object's arg value.</param>
        /// <param name="expectEqual">Whether the hash codes are expected to be equal.</param>
        [Theory]
        [InlineData("Key1", 100, "Key1", 100, true)]
        [InlineData("Key1", 100, "Key1", 200, false)]
        [InlineData("Key1", 100, "Key2", 100, false)]
        [InlineData("Key1", 100, "Key2", 200, false)]
        [InlineData(null, 100, null, 100, true)]
        [InlineData(null, null, null, null, true)]
        [InlineData("", null, "", null, true)]
        [InlineData("KeyA", "ValueA", "KeyA", "ValueA", true)]
        [InlineData("KeyA", "ValueA", "KeyA", "ValueB", false)]
        [InlineData("KeyA", "ValueA", "KeyB", "ValueA", false)]
        [InlineData("", "", "", "", true)]
        [InlineData(" ", " ", " ", " ", true)]
        public void GetHashCode_VariousKeyAndArgCombinations_ReturnsExpectedHashCodeEquality(
            string key1, object arg1, string key2, object arg2, bool expectEqual)
        {
            // Arrange
            if (key1 != null)
            {
                GenericSqlExpr.Register(key1, (context, builder, outputParams, arg) => "SQL");
            }
            if (key2 != null && key2 != key1)
            {
                GenericSqlExpr.Register(key2, (context, builder, outputParams, arg) => "SQL");
            }

            var expr1 = new GenericSqlExpr { Key = key1, Arg = arg1 };
            var expr2 = new GenericSqlExpr { Key = key2, Arg = arg2 };

            // Act
            int hashCode1 = expr1.GetHashCode();
            int hashCode2 = expr2.GetHashCode();

            // Assert
            if (expectEqual)
            {
                Assert.Equal(hashCode1, hashCode2);
            }
            else
            {
                Assert.NotEqual(hashCode1, hashCode2);
            }
        }

        /// <summary>
        /// Verifies that GetHashCode handles different object types for Arg property.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentArgTypes_ProducesDistinctHashCodes()
        {
            // Arrange
            GenericSqlExpr.Register("TestKey8", (context, builder, outputParams, arg) => "SQL");
            var exprInt = new GenericSqlExpr("TestKey8") { Arg = 100 };
            var exprString = new GenericSqlExpr("TestKey8") { Arg = "100" };
            var exprDouble = new GenericSqlExpr("TestKey8") { Arg = 100.0 };

            // Act
            int hashCodeInt = exprInt.GetHashCode();
            int hashCodeString = exprString.GetHashCode();
            int hashCodeDouble = exprDouble.GetHashCode();

            // Assert
            Assert.NotEqual(hashCodeInt, hashCodeString);
            Assert.NotEqual(hashCodeInt, hashCodeDouble);
            Assert.NotEqual(hashCodeString, hashCodeDouble);
        }

        /// <summary>
        /// Verifies that GetHashCode handles empty string Key value correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_EmptyStringKey_ReturnsConsistentHashCode()
        {
            // Arrange
            GenericSqlExpr.Register("", (context, builder, outputParams, arg) => "SQL");
            var expr1 = new GenericSqlExpr("") { Arg = 123 };
            var expr2 = new GenericSqlExpr("") { Arg = 123 };

            // Act
            int hashCode1 = expr1.GetHashCode();
            int hashCode2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Verifies that GetHashCode handles whitespace-only Key value correctly.
        /// </summary>
        [Fact]
        public void GetHashCode_WhitespaceKey_ReturnsConsistentHashCode()
        {
            // Arrange
            GenericSqlExpr.Register("   ", (context, builder, outputParams, arg) => "SQL");
            var expr1 = new GenericSqlExpr("   ") { Arg = 123 };
            var expr2 = new GenericSqlExpr("   ") { Arg = 123 };

            // Act
            int hashCode1 = expr1.GetHashCode();
            int hashCode2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Verifies that GetHashCode produces different hash codes for special string Key values.
        /// </summary>
        [Fact]
        public void GetHashCode_SpecialStringKeys_ProducesDifferentHashCodes()
        {
            // Arrange
            GenericSqlExpr.Register("", (context, builder, outputParams, arg) => "SQL");
            GenericSqlExpr.Register(" ", (context, builder, outputParams, arg) => "SQL");
            GenericSqlExpr.Register("\t", (context, builder, outputParams, arg) => "SQL");
            GenericSqlExpr.Register("\n", (context, builder, outputParams, arg) => "SQL");

            var exprEmpty = new GenericSqlExpr("") { Arg = null };
            var exprSpace = new GenericSqlExpr(" ") { Arg = null };
            var exprTab = new GenericSqlExpr("\t") { Arg = null };
            var exprNewline = new GenericSqlExpr("\n") { Arg = null };

            // Act
            int hashCodeEmpty = exprEmpty.GetHashCode();
            int hashCodeSpace = exprSpace.GetHashCode();
            int hashCodeTab = exprTab.GetHashCode();
            int hashCodeNewline = exprNewline.GetHashCode();

            // Assert
            Assert.NotEqual(hashCodeEmpty, hashCodeSpace);
            Assert.NotEqual(hashCodeEmpty, hashCodeTab);
            Assert.NotEqual(hashCodeEmpty, hashCodeNewline);
            Assert.NotEqual(hashCodeSpace, hashCodeTab);
            Assert.NotEqual(hashCodeSpace, hashCodeNewline);
            Assert.NotEqual(hashCodeTab, hashCodeNewline);
        }

        /// <summary>
        /// Verifies that GetHashCode handles complex objects as Arg values.
        /// </summary>
        [Fact]
        public void GetHashCode_ComplexObjectArg_ReturnsConsistentHashCode()
        {
            // Arrange
            GenericSqlExpr.Register("TestKey9", (context, builder, outputParams, arg) => "SQL");
            var complexObj1 = new { Name = "Test", Value = 123 };
            var complexObj2 = new { Name = "Test", Value = 123 };
            var expr1 = new GenericSqlExpr("TestKey9") { Arg = complexObj1 };
            var expr2 = new GenericSqlExpr("TestKey9") { Arg = complexObj1 };

            // Act
            int hashCode1 = expr1.GetHashCode();
            int hashCode2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Verifies that GetHashCode produces different hash codes when Arg is different reference types with different hash codes.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentReferenceTypeArgs_ProducesDifferentHashCodes()
        {
            // Arrange
            GenericSqlExpr.Register("TestKey10", (context, builder, outputParams, arg) => "SQL");
            var obj1 = new object();
            var obj2 = new object();
            var expr1 = new GenericSqlExpr("TestKey10") { Arg = obj1 };
            var expr2 = new GenericSqlExpr("TestKey10") { Arg = obj2 };

            // Act
            int hashCode1 = expr1.GetHashCode();
            int hashCode2 = expr2.GetHashCode();

            // Assert
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that Clone creates a new instance that is not the same reference as the original.
        /// </summary>
        /// <param name="key">The key value for the GenericSqlExpr.</param>
        /// <param name="arg">The arg value for the GenericSqlExpr.</param>
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("TestKey", null)]
        [InlineData("TestKey", 123)]
        [InlineData(null, "StringArg")]
        [InlineData("MyKey", 456)]
        public void Clone_VariousKeyAndArgValues_CreatesNewInstance(string? key, object? arg)
        {
            // Arrange
            var original = new GenericSqlExpr(key) { Arg = arg };

            // Act
            var cloned = original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
        }

        /// <summary>
        /// Tests that Clone preserves the Key property value.
        /// </summary>
        /// <param name="key">The key value to test.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("TestKey")]
        [InlineData("AnotherKey")]
        [InlineData("Key_With_Underscores")]
        [InlineData("VeryLongKeyNameThatExceedsNormalLength123456789012345678901234567890")]
        public void Clone_VariousKeyValues_PreservesKeyProperty(string? key)
        {
            // Arrange
            var original = new GenericSqlExpr(key);

            // Act
            var cloned = (GenericSqlExpr)original.Clone();

            // Assert
            Assert.Equal(original.Key, cloned.Key);
        }

        /// <summary>
        /// Tests that Clone preserves the Arg property reference (shallow copy).
        /// </summary>
        /// <param name="arg">The arg value to test.</param>
        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        [InlineData(123)]
        [InlineData(-456)]
        [InlineData("StringValue")]
        [InlineData("")]
        public void Clone_VariousArgValues_PreservesArgProperty(object? arg)
        {
            // Arrange
            var original = new GenericSqlExpr("TestKey") { Arg = arg };

            // Act
            var cloned = (GenericSqlExpr)original.Clone();

            // Assert
            Assert.Equal(original.Arg, cloned.Arg);
        }

        /// <summary>
        /// Tests that Clone creates an object that is equal to the original based on Equals implementation.
        /// </summary>
        [Fact]
        public void Clone_WithKeyAndArg_CreatesEqualObject()
        {
            // Arrange
            var original = new GenericSqlExpr("TestKey") { Arg = 123 };

            // Act
            var cloned = original.Clone();

            // Assert
            Assert.True(original.Equals(cloned));
            Assert.True(cloned.Equals(original));
        }

        /// <summary>
        /// Tests that Clone with null Key creates an equal object.
        /// </summary>
        [Fact]
        public void Clone_WithNullKey_CreatesEqualObject()
        {
            // Arrange
            var original = new GenericSqlExpr(null) { Arg = "test" };

            // Act
            var cloned = original.Clone();

            // Assert
            Assert.True(original.Equals(cloned));
        }

        /// <summary>
        /// Tests that Clone with null Arg creates an equal object.
        /// </summary>
        [Fact]
        public void Clone_WithNullArg_CreatesEqualObject()
        {
            // Arrange
            var original = new GenericSqlExpr("Key") { Arg = null };

            // Act
            var cloned = original.Clone();

            // Assert
            Assert.True(original.Equals(cloned));
        }

        /// <summary>
        /// Tests that Clone with both null Key and null Arg creates an equal object.
        /// </summary>
        [Fact]
        public void Clone_WithNullKeyAndNullArg_CreatesEqualObject()
        {
            // Arrange
            var original = new GenericSqlExpr(null) { Arg = null };

            // Act
            var cloned = original.Clone();

            // Assert
            Assert.True(original.Equals(cloned));
        }

        /// <summary>
        /// Tests that Clone returns an instance of GenericSqlExpr type.
        /// </summary>
        [Fact]
        public void Clone_ReturnsCorrectType()
        {
            // Arrange
            var original = new GenericSqlExpr("TestKey");

            // Act
            var cloned = original.Clone();

            // Assert
            Assert.IsType<GenericSqlExpr>(cloned);
            Assert.IsAssignableFrom<Expr>(cloned);
        }

        /// <summary>
        /// Tests that Clone performs shallow copy for reference type Arg property.
        /// Modifying a mutable reference type in the original should affect the clone.
        /// </summary>
        [Fact]
        public void Clone_WithReferenceTypeArg_PerformsShallowCopy()
        {
            // Arrange
            var mutableArg = new List<int> { 1, 2, 3 };
            var original = new GenericSqlExpr("TestKey") { Arg = mutableArg };

            // Act
            var cloned = (GenericSqlExpr)original.Clone();

            // Assert
            Assert.Same(original.Arg, cloned.Arg);

            // Modify the mutable object through original
            mutableArg.Add(4);

            // Verify the clone's Arg is affected (same reference)
            Assert.Equal(4, ((List<int>)cloned.Arg!).Count);
        }

        /// <summary>
        /// Tests that Clone preserves extreme integer values in Arg property.
        /// </summary>
        /// <param name="arg">The extreme integer value to test.</param>
        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(int.MaxValue)]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(1)]
        public void Clone_WithExtremeIntegerArgValues_PreservesValue(int arg)
        {
            // Arrange
            var original = new GenericSqlExpr("Key") { Arg = arg };

            // Act
            var cloned = (GenericSqlExpr)original.Clone();

            // Assert
            Assert.Equal(arg, cloned.Arg);
        }

        /// <summary>
        /// Tests that Clone preserves special double values in Arg property.
        /// </summary>
        /// <param name="arg">The special double value to test.</param>
        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        [InlineData(double.MinValue)]
        [InlineData(double.MaxValue)]
        [InlineData(0.0)]
        [InlineData(-0.0)]
        public void Clone_WithSpecialDoubleArgValues_PreservesValue(double arg)
        {
            // Arrange
            var original = new GenericSqlExpr("Key") { Arg = arg };

            // Act
            var cloned = (GenericSqlExpr)original.Clone();

            // Assert
            if (double.IsNaN(arg))
            {
                Assert.True(double.IsNaN((double)cloned.Arg!));
            }
            else
            {
                Assert.Equal(arg, cloned.Arg);
            }
        }

        /// <summary>
        /// Tests that Clone preserves GetHashCode value equality.
        /// </summary>
        [Fact]
        public void Clone_PreservesHashCode()
        {
            // Arrange
            var original = new GenericSqlExpr("TestKey") { Arg = 123 };

            // Act
            var cloned = original.Clone();

            // Assert
            Assert.Equal(original.GetHashCode(), cloned.GetHashCode());
        }

        /// <summary>
        /// Tests that Clone with empty string Key preserves the value.
        /// </summary>
        [Fact]
        public void Clone_WithEmptyStringKey_PreservesEmptyString()
        {
            // Arrange
            var original = new GenericSqlExpr("") { Arg = null };

            // Act
            var cloned = (GenericSqlExpr)original.Clone();

            // Assert
            Assert.Equal(string.Empty, cloned.Key);
            Assert.NotNull(cloned.Key);
        }

        /// <summary>
        /// Tests that Clone with whitespace-only Key preserves the exact value.
        /// </summary>
        [Fact]
        public void Clone_WithWhitespaceKey_PreservesWhitespace()
        {
            // Arrange
            var whitespaceKey = "   ";
            var original = new GenericSqlExpr(whitespaceKey);

            // Act
            var cloned = (GenericSqlExpr)original.Clone();

            // Assert
            Assert.Equal(whitespaceKey, cloned.Key);
        }

        /// <summary>
        /// Tests that the ExprType property returns GenericSql.
        /// </summary>
        [Fact]
        public void ExprType_Always_ReturnsGenericSql()
        {
            // Arrange
            var expr = new GenericSqlExpr();

            // Act
            var result = expr.ExprType;

            // Assert
            Assert.Equal(ExprType.GenericSql, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns GenericSql when instance is created with a key.
        /// </summary>
        [Fact]
        public void ExprType_WhenCreatedWithKey_ReturnsGenericSql()
        {
            // Arrange
            var expr = new GenericSqlExpr("testKey");

            // Act
            var result = expr.ExprType;

            // Assert
            Assert.Equal(ExprType.GenericSql, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns GenericSql when Arg property is set.
        /// </summary>
        [Fact]
        public void ExprType_WhenArgIsSet_ReturnsGenericSql()
        {
            // Arrange
            var expr = new GenericSqlExpr { Arg = new object() };

            // Act
            var result = expr.ExprType;

            // Assert
            Assert.Equal(ExprType.GenericSql, result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with null.
        /// </summary>
        [Fact]
        public void Equals_NullObject_ReturnsFalse()
        {
            // Arrange
            var expr = new GenericSqlExpr("TestKey") { Arg = 123 };

            // Act
            var result = expr.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with an object of a different type.
        /// </summary>
        [Theory]
        [InlineData("string value")]
        [InlineData(123)]
        public void Equals_DifferentType_ReturnsFalse(object obj)
        {
            // Arrange
            var expr = new GenericSqlExpr("TestKey") { Arg = 123 };

            // Act
            var result = expr.Equals(obj);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing two GenericSqlExpr instances with the same Key and Arg.
        /// </summary>
        [Theory]
        [MemberData(nameof(GetEqualInstancesData))]
        public void Equals_SameKeyAndArg_ReturnsTrue(GenericSqlExpr expr1, GenericSqlExpr expr2)
        {
            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing two GenericSqlExpr instances with different Keys.
        /// </summary>
        [Theory]
        [MemberData(nameof(GetDifferentKeyData))]
        public void Equals_DifferentKey_ReturnsFalse(GenericSqlExpr expr1, GenericSqlExpr expr2)
        {
            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing two GenericSqlExpr instances with different Args.
        /// </summary>
        [Theory]
        [MemberData(nameof(GetDifferentArgData))]
        public void Equals_DifferentArg_ReturnsFalse(GenericSqlExpr expr1, GenericSqlExpr expr2)
        {
            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null Keys and same Arg.
        /// </summary>
        [Fact]
        public void Equals_BothKeysNull_SameArg_ReturnsTrue()
        {
            // Arrange
            var expr1 = new GenericSqlExpr { Arg = 123 };
            var expr2 = new GenericSqlExpr { Arg = 123 };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one Key is null and the other is not.
        /// </summary>
        [Fact]
        public void Equals_OneKeyNull_ReturnsFalse()
        {
            // Arrange
            var expr1 = new GenericSqlExpr("Key1") { Arg = 123 };
            var expr2 = new GenericSqlExpr { Arg = 123 };

            // Act
            var result1 = expr1.Equals(expr2);
            var result2 = expr2.Equals(expr1);

            // Assert
            Assert.False(result1);
            Assert.False(result2);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null Args and same Key.
        /// </summary>
        [Fact]
        public void Equals_BothArgsNull_SameKey_ReturnsTrue()
        {
            // Arrange
            var expr1 = new GenericSqlExpr("TestKey");
            var expr2 = new GenericSqlExpr("TestKey");

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one Arg is null and the other is not.
        /// </summary>
        [Fact]
        public void Equals_OneArgNull_ReturnsFalse()
        {
            // Arrange
            var expr1 = new GenericSqlExpr("TestKey") { Arg = 123 };
            var expr2 = new GenericSqlExpr("TestKey");

            // Act
            var result1 = expr1.Equals(expr2);
            var result2 = expr2.Equals(expr1);

            // Assert
            Assert.False(result1);
            Assert.False(result2);
        }

        /// <summary>
        /// Tests that Equals returns true when both Key and Arg are null.
        /// </summary>
        [Fact]
        public void Equals_BothKeyAndArgNull_ReturnsTrue()
        {
            // Arrange
            var expr1 = new GenericSqlExpr();
            var expr2 = new GenericSqlExpr();

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals handles edge case string values for Key property correctly.
        /// </summary>
        [Theory]
        [InlineData("", "")]
        [InlineData("   ", "   ")]
        [InlineData("Key with spaces", "Key with spaces")]
        [InlineData("Key\twith\ttabs", "Key\twith\ttabs")]
        [InlineData("Key\nwith\nnewlines", "Key\nwith\nnewlines")]
        public void Equals_EdgeCaseKeyValues_CorrectComparison(string key1, string key2)
        {
            // Arrange
            var expr1 = new GenericSqlExpr(key1) { Arg = 100 };
            var expr2 = new GenericSqlExpr(key2) { Arg = 100 };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false for edge case string values that differ.
        /// </summary>
        [Theory]
        [InlineData("", " ")]
        [InlineData("Key", "key")]
        [InlineData("Key1", "Key2")]
        public void Equals_DifferentEdgeCaseKeyValues_ReturnsFalse(string key1, string key2)
        {
            // Arrange
            var expr1 = new GenericSqlExpr(key1) { Arg = 100 };
            var expr2 = new GenericSqlExpr(key2) { Arg = 100 };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals handles very long Key strings correctly.
        /// </summary>
        [Fact]
        public void Equals_VeryLongKeyStrings_CorrectComparison()
        {
            // Arrange
            var longKey = new string('A', 10000);
            var expr1 = new GenericSqlExpr(longKey) { Arg = 100 };
            var expr2 = new GenericSqlExpr(longKey) { Arg = 100 };
            var expr3 = new GenericSqlExpr(new string('B', 10000)) { Arg = 100 };

            // Act
            var resultEqual = expr1.Equals(expr2);
            var resultDifferent = expr1.Equals(expr3);

            // Assert
            Assert.True(resultEqual);
            Assert.False(resultDifferent);
        }

        /// <summary>
        /// Tests that Equals handles special characters in Key correctly.
        /// </summary>
        [Fact]
        public void Equals_SpecialCharactersInKey_CorrectComparison()
        {
            // Arrange
            var specialKey = "Key!@#$%^&*()_+-=[]{}|;':\",./<>?";
            var expr1 = new GenericSqlExpr(specialKey) { Arg = 100 };
            var expr2 = new GenericSqlExpr(specialKey) { Arg = 100 };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals handles different types of Arg values correctly.
        /// </summary>
        [Theory]
        [MemberData(nameof(GetDifferentArgTypesData))]
        public void Equals_DifferentArgTypes_CorrectComparison(object arg1, object arg2, bool expectedEqual)
        {
            // Arrange
            var expr1 = new GenericSqlExpr("Key") { Arg = arg1 };
            var expr2 = new GenericSqlExpr("Key") { Arg = arg2 };

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.Equal(expectedEqual, result);
        }

        /// <summary>
        /// Tests that Equals is reflexive (an object equals itself).
        /// </summary>
        [Fact]
        public void Equals_SameInstance_ReturnsTrue()
        {
            // Arrange
            var expr = new GenericSqlExpr("TestKey") { Arg = 123 };

            // Act
            var result = expr.Equals(expr);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals handles complex object types as Arg.
        /// </summary>
        [Fact]
        public void Equals_ComplexObjectArg_UsesObjectEquals()
        {
            // Arrange
            var obj1 = new List<int> { 1, 2, 3 };
            var obj2 = new List<int> { 1, 2, 3 };
            var expr1 = new GenericSqlExpr("Key") { Arg = obj1 };
            var expr2 = new GenericSqlExpr("Key") { Arg = obj2 };
            var expr3 = new GenericSqlExpr("Key") { Arg = obj1 };

            // Act
            var resultDifferentInstances = expr1.Equals(expr2);
            var resultSameReference = expr1.Equals(expr3);

            // Assert
            Assert.False(resultDifferentInstances); // Different List instances
            Assert.True(resultSameReference); // Same reference
        }

        public static IEnumerable<object[]> GetEqualInstancesData()
        {
            yield return new object[] { new GenericSqlExpr("Key1") { Arg = 123 }, new GenericSqlExpr("Key1") { Arg = 123 } };
            yield return new object[] { new GenericSqlExpr("Key2") { Arg = "test" }, new GenericSqlExpr("Key2") { Arg = "test" } };
            yield return new object[] { new GenericSqlExpr("Key3") { Arg = 0 }, new GenericSqlExpr("Key3") { Arg = 0 } };
            yield return new object[] { new GenericSqlExpr("Key4") { Arg = -999 }, new GenericSqlExpr("Key4") { Arg = -999 } };
            yield return new object[] { new GenericSqlExpr("Key5") { Arg = int.MaxValue }, new GenericSqlExpr("Key5") { Arg = int.MaxValue } };
            yield return new object[] { new GenericSqlExpr("Key6") { Arg = int.MinValue }, new GenericSqlExpr("Key6") { Arg = int.MinValue } };
            yield return new object[] { new GenericSqlExpr("") { Arg = 1 }, new GenericSqlExpr("") { Arg = 1 } };
        }

        public static IEnumerable<object[]> GetDifferentKeyData()
        {
            yield return new object[] { new GenericSqlExpr("Key1") { Arg = 123 }, new GenericSqlExpr("Key2") { Arg = 123 } };
            yield return new object[] { new GenericSqlExpr("KeyA") { Arg = "test" }, new GenericSqlExpr("KeyB") { Arg = "test" } };
            yield return new object[] { new GenericSqlExpr("") { Arg = 1 }, new GenericSqlExpr("NonEmpty") { Arg = 1 } };
            yield return new object[] { new GenericSqlExpr("Case") { Arg = 1 }, new GenericSqlExpr("case") { Arg = 1 } };
        }

        public static IEnumerable<object[]> GetDifferentArgData()
        {
            yield return new object[] { new GenericSqlExpr("Key") { Arg = 123 }, new GenericSqlExpr("Key") { Arg = 456 } };
            yield return new object[] { new GenericSqlExpr("Key") { Arg = "test1" }, new GenericSqlExpr("Key") { Arg = "test2" } };
            yield return new object[] { new GenericSqlExpr("Key") { Arg = 0 }, new GenericSqlExpr("Key") { Arg = 1 } };
            yield return new object[] { new GenericSqlExpr("Key") { Arg = -1 }, new GenericSqlExpr("Key") { Arg = 1 } };
            yield return new object[] { new GenericSqlExpr("Key") { Arg = int.MinValue }, new GenericSqlExpr("Key") { Arg = int.MaxValue } };
            yield return new object[] { new GenericSqlExpr("Key") { Arg = "" }, new GenericSqlExpr("Key") { Arg = " " } };
        }

        public static IEnumerable<object[]> GetDifferentArgTypesData()
        {
            yield return new object[] { 123, 123, true };
            yield return new object[] { "test", "test", true };
            yield return new object[] { 123, "123", false };
            yield return new object[] { 0, 0.0, false };
            yield return new object[] { true, true, true };
            yield return new object[] { false, false, true };
            yield return new object[] { true, false, false };
            yield return new object[] { 1.5, 1.5, true };
            yield return new object[] { 1.5, 1.50001, false };
            yield return new object[] { double.NaN, double.NaN, false }; // NaN != NaN
            yield return new object[] { double.PositiveInfinity, double.PositiveInfinity, true };
            yield return new object[] { double.NegativeInfinity, double.NegativeInfinity, true };
            yield return new object[] { double.PositiveInfinity, double.NegativeInfinity, false };
        }
    }
}

namespace LiteOrm.Tests.UnitTests
{
    /// <summary>
    /// Unit tests for the GenericSqlExpr.Get(string key, object arg) method.
    /// </summary>
    public sealed class GenericSqlExprGetWithArgTests
    {
        /// <summary>
        /// Tests that Get(string key, object arg) returns a GenericSqlExpr instance with the Arg property set
        /// when provided with a valid registered key and a non-null argument.
        /// </summary>
        [Fact]
        public void Get_ValidKeyWithNonNullArg_ReturnsExpressionWithArgSet()
        {
            // Arrange
            const string testKey = "Get_ValidKeyWithNonNullArg_Test";
            const int testArg = 123;
            GenericSqlExpr.Register(testKey, (ctx, builder, pms, arg) => "TEST SQL");

            // Act
            GenericSqlExpr result = GenericSqlExpr.Get(testKey, testArg);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(testKey, result.Key);
            Assert.Equal(testArg, result.Arg);
        }

        /// <summary>
        /// Tests that Get(string key, object arg) returns a GenericSqlExpr instance with Arg property set to null
        /// when provided with a valid registered key and a null argument.
        /// </summary>
        [Fact]
        public void Get_ValidKeyWithNullArg_ReturnsExpressionWithNullArg()
        {
            // Arrange
            const string testKey = "Get_ValidKeyWithNullArg_Test";
            GenericSqlExpr.Register(testKey, (ctx, builder, pms, arg) => "TEST SQL");

            // Act
            GenericSqlExpr result = GenericSqlExpr.Get(testKey, null);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(testKey, result.Key);
            Assert.Null(result.Arg);
        }

        /// <summary>
        /// Tests that Get(string key, object arg) correctly sets Arg property with various argument types.
        /// Verifies that the method handles different object types (int, string, object, double) correctly.
        /// </summary>
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        [InlineData(3.14)]
        [InlineData(true)]
        [InlineData(0)]
        [InlineData(-100)]
        public void Get_ValidKeyWithVariousArgTypes_ReturnsExpressionWithCorrectArg(object arg)
        {
            // Arrange
            const string testKey = "Get_ValidKeyWithVariousArgTypes_Test";
            GenericSqlExpr.Register(testKey, (ctx, builder, pms, a) => "TEST SQL");

            // Act
            GenericSqlExpr result = GenericSqlExpr.Get(testKey, arg);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(testKey, result.Key);
            Assert.Equal(arg, result.Arg);
        }

        /// <summary>
        /// Tests that Get(string key, object arg) throws ArgumentNullException
        /// when provided with a null key, regardless of the arg value.
        /// </summary>
        [Fact]
        public void Get_NullKey_ThrowsArgumentNullException()
        {
            // Arrange
            const string nullKey = null;
            const int testArg = 123;

            // Act & Assert
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => GenericSqlExpr.Get(nullKey, testArg));
            Assert.Equal("key", exception.ParamName);
        }

        /// <summary>
        /// Tests that Get(string key, object arg) throws KeyNotFoundException
        /// when provided with a non-existent key.
        /// </summary>
        [Fact]
        public void Get_NonExistentKey_ThrowsKeyNotFoundException()
        {
            // Arrange
            const string nonExistentKey = "NonExistent_Key_12345_XYZ";
            const int testArg = 456;

            // Act & Assert
            KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(() => GenericSqlExpr.Get(nonExistentKey, testArg));
            Assert.Contains(nonExistentKey, exception.Message);
        }

        /// <summary>
        /// Tests that Get(string key, object arg) throws KeyNotFoundException
        /// when provided with an empty string key.
        /// </summary>
        [Fact]
        public void Get_EmptyStringKey_ThrowsKeyNotFoundException()
        {
            // Arrange
            const string emptyKey = "";
            const int testArg = 789;

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => GenericSqlExpr.Get(emptyKey, testArg));
        }

        /// <summary>
        /// Tests that Get(string key, object arg) throws KeyNotFoundException
        /// when provided with a whitespace-only key.
        /// </summary>
        [Fact]
        public void Get_WhitespaceKey_ThrowsKeyNotFoundException()
        {
            // Arrange
            const string whitespaceKey = "   ";
            const int testArg = 999;

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => GenericSqlExpr.Get(whitespaceKey, testArg));
        }

        /// <summary>
        /// Tests that Get(string key, object arg) correctly handles a very long key string
        /// when the key is registered in the registry.
        /// </summary>
        [Fact]
        public void Get_VeryLongKey_ReturnsExpressionWithArgSet()
        {
            // Arrange
            string longKey = new string('A', 10000);
            const int testArg = 555;
            GenericSqlExpr.Register(longKey, (ctx, builder, pms, arg) => "TEST SQL");

            // Act
            GenericSqlExpr result = GenericSqlExpr.Get(longKey, testArg);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(longKey, result.Key);
            Assert.Equal(testArg, result.Arg);
        }

        /// <summary>
        /// Tests that Get(string key, object arg) correctly handles special characters in the key
        /// when the key is registered in the registry.
        /// </summary>
        [Fact]
        public void Get_KeyWithSpecialCharacters_ReturnsExpressionWithArgSet()
        {
            // Arrange
            const string specialKey = "Key!@#$%^&*()_+-=[]{}|;':\",./<>?";
            const string testArg = "special arg";
            GenericSqlExpr.Register(specialKey, (ctx, builder, pms, arg) => "TEST SQL");

            // Act
            GenericSqlExpr result = GenericSqlExpr.Get(specialKey, testArg);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(specialKey, result.Key);
            Assert.Equal(testArg, result.Arg);
        }

        /// <summary>
        /// Tests that Get(string key, object arg) returns different instances on multiple calls
        /// but with the same Key and Arg values set correctly.
        /// </summary>
        [Fact]
        public void Get_CalledMultipleTimes_ReturnsNewInstancesWithCorrectProperties()
        {
            // Arrange
            const string testKey = "Get_CalledMultipleTimes_Test";
            const int testArg = 777;
            GenericSqlExpr.Register(testKey, (ctx, builder, pms, arg) => "TEST SQL");

            // Act
            GenericSqlExpr result1 = GenericSqlExpr.Get(testKey, testArg);
            GenericSqlExpr result2 = GenericSqlExpr.Get(testKey, testArg);

            // Assert
            Assert.NotSame(result1, result2);
            Assert.Equal(result1.Key, result2.Key);
            Assert.Equal(result1.Arg, result2.Arg);
        }

        /// <summary>
        /// Tests that Get(string key, object arg) correctly handles complex objects as arguments.
        /// Verifies that the Arg property is set to the same reference as the provided complex object.
        /// </summary>
        [Fact]
        public void Get_ValidKeyWithComplexObjectArg_ReturnsExpressionWithComplexArgSet()
        {
            // Arrange
            const string testKey = "Get_ValidKeyWithComplexObjectArg_Test";
            var complexArg = new List<int> { 1, 2, 3, 4, 5 };
            GenericSqlExpr.Register(testKey, (ctx, builder, pms, arg) => "TEST SQL");

            // Act
            GenericSqlExpr result = GenericSqlExpr.Get(testKey, complexArg);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(testKey, result.Key);
            Assert.Same(complexArg, result.Arg);
        }
    }
}