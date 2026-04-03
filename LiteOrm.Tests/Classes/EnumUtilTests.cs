using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

using LiteOrm;
using Moq;
using Xunit;

namespace LiteOrm.UnitTests
{
    public class EnumUtilTests
    {
        /// <summary>
        /// Tests that passing a null enum reference returns null.
        /// Input: null enum value.
        /// Expected: GetDisplayName returns null and does not throw.
        /// </summary>
        [Fact]
        public void GetDisplayName_NullValue_ReturnsNull()
        {
            // Arrange
            Enum? value = null;

            // Act
            string? result = EnumUtil.GetDisplayName(value);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Parameterized tests for GetDisplayName covering:
        /// - Field with DisplayNameAttribute should return the DisplayName.
        /// - Field with DescriptionAttribute should return the Description.
        /// - Field without attributes should return the field name.
        /// - Unknown numeric enum value (not defined) should fall back to Enum.ToString() (numeric string).
        /// Inputs: integer values representing enum members (or out-of-range numeric).
        /// Expected: returned display string matches expected.
        /// </summary>
        [Theory]
        [InlineData(1, "Display One")]
        [InlineData(2, "Desc Two")]
        [InlineData(3, "Plain")]
        [InlineData(999, "999")]
        public void GetDisplayName_ValueVarious_ReturnsExpectedDisplayName(int enumValue, string expected)
        {
            // Arrange
            TestAttrEnum value = (TestAttrEnum)enumValue;

            // Act
            string? result = EnumUtil.GetDisplayName(value);

            // Assert
            Assert.Equal(expected, result);
        }

        // Nested helper enum used only by tests (allowed as inner type).
        private enum TestAttrEnum
        {
            [DisplayName("Display One")]
            WithDisplay = 1,

            [Description("Desc Two")]
            WithDescription = 2,

            Plain = 3
        }
        // Helper enum used only within these tests to exercise mapping and parsing behaviors.
        private enum TestEnum
        {
            None = 0,
            Alpha = 1,
            Beta = 2,
            [DisplayName("Custom Display")]
            Custom = 10,
            [Description("Desc Display")]
            Described = 20
        }

        /// <summary>
        /// Verifies that Parse&lt;T&gt; returns the expected enum when given either an exact display-name
        /// (from DisplayNameAttribute or DescriptionAttribute) or an exact enum field name.
        /// Inputs tested:
        ///  - "Custom Display" => TestEnum.Custom (DisplayNameAttribute)
        ///  - "Desc Display" => TestEnum.Described (DescriptionAttribute)
        ///  - "Alpha" => TestEnum.Alpha (field name)
        /// Expected result: parsed enum matches expected underlying value.
        /// </summary>
        [Theory]
        [InlineData("Custom Display", 10)]
        [InlineData("Desc Display", 20)]
        [InlineData("Alpha", 1)]
        public void Parse_DisplayOrName_ReturnsExpectedEnum(string displayName, int expectedUnderlying)
        {
            // Arrange
            var expected = (TestEnum)expectedUnderlying;

            // Act
            TestEnum actual = EnumUtil.Parse<TestEnum>(displayName);

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Verifies that Parse&lt;T&gt; is case-insensitive for enum field names when casing differs,
        /// i.e. an input of "alpha" (different case) should still parse to TestEnum.Alpha via Enum.TryParse(ignoreCase:true).
        /// Input: "alpha" (lowercase)
        /// Expected: TestEnum.Alpha
        /// </summary>
        [Fact]
        public void Parse_NameWithDifferentCase_ReturnsEnumIgnoringCase()
        {
            // Arrange
            string input = "alpha"; // different case than "Alpha"
            var expected = TestEnum.Alpha;

            // Act
            TestEnum actual = EnumUtil.Parse<TestEnum>(input);

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Verifies that a display name that matches a DisplayNameAttribute but with different casing
        /// (not exact match) does NOT resolve to the enum (attribute-mapping is matched case-sensitively)
        /// and ultimately returns the default enum value when no enum name matches.
        /// Input: "custom display" (lowercase) where the attribute value is "Custom Display".
        /// Expected: default(TestEnum) == TestEnum.None
        /// </summary>
        [Fact]
        public void Parse_DisplayNameDifferentCase_ReturnsDefault()
        {
            // Arrange
            string input = "custom display"; // different case than DisplayNameAttribute value

            // Act
            TestEnum actual = EnumUtil.Parse<TestEnum>(input);

            // Assert
            Assert.Equal(default(TestEnum), actual);
        }

        /// <summary>
        /// Verifies that passing null as displayName throws ArgumentNullException.
        /// Input: null
        /// Expected: ArgumentNullException thrown (ConcurrentDictionary.TryGetValue enforces non-null key).
        /// </summary>
        [Fact]
        public void Parse_NullDisplayName_ThrowsArgumentNullException()
        {
            // Arrange
            string? input = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => EnumUtil.Parse<TestEnum>(input!));
        }

        /// <summary>
        /// Provides a set of invalid, empty or unusual string inputs to ensure Parse&lt;T&gt; returns the default enum value.
        /// These inputs do not match any display-name or enum name.
        /// </summary>
        public static IEnumerable<object[]> InvalidStrings()
        {
            yield return new object[] { string.Empty };
            yield return new object[] { "Unknown" };
            yield return new object[] { "   " };
            yield return new object[] { new string('x', 1024) }; // very long string
            yield return new object[] { "@@@###$$$" }; // special characters
        }

        /// <summary>
        /// Verifies that various invalid or empty inputs return the default enum value.
        /// Inputs include empty string, unknown names, whitespace-only strings, very long strings and special characters.
        /// Expected: default(TestEnum) == TestEnum.None
        /// </summary>
        [Theory]
        [MemberData(nameof(InvalidStrings))]
        public void Parse_InvalidOrEmptyStrings_ReturnDefault(string input)
        {
            // Arrange
            var expected = default(TestEnum);

            // Act
            TestEnum actual = EnumUtil.Parse<TestEnum>(input);

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Test enum used for various display/description/name cases.
        /// Nested inside the test class per guidelines.
        /// </summary>
        private enum TestEnum
        {
            [DisplayName("X Display")]
            A = 0,

            [Description("Desc B")]
            B = 1,

            C = 2
        }

        /// <summary>
        /// Verifies that when a display name, description or field name is provided,
        /// Parse returns the corresponding enum value. Also verifies numeric string parsing.
        /// Input conditions:
        ///  - displayName: value from DisplayNameAttribute, DescriptionAttribute, field name, or numeric string.
        /// Expected result:
        ///  - returns the corresponding TestEnum value boxed as object.
        /// </summary>
        [Theory]
        [InlineData("X Display", 0)]
        [InlineData("Desc B", 1)]
        [InlineData("C", 2)]
        [InlineData("1", 1)]
        public void Parse_DisplayNameOrDescriptionOrFieldNameOrNumericString_ReturnsExpectedEnum(string displayName, int expectedUnderlying)
        {
            // Arrange
            Type enumType = typeof(TestEnum);

            // Act
            object result = EnumUtil.Parse(enumType, displayName);

            // Assert
            Assert.IsType<TestEnum>(result);
            Assert.Equal((TestEnum)expectedUnderlying, (TestEnum)result);
        }

        /// <summary>
        /// Verifies that Parse is case-sensitive: a lower-cased field name that does not match
        /// any display/description entry will cause Enum.Parse to be invoked and throw ArgumentException.
        /// Input conditions:
        ///  - displayName: lower-case 'c' (where the actual field name is 'C')
        /// Expected result:
        ///  - an ArgumentException is thrown.
        /// </summary>
        [Fact]
        public void Parse_InvalidCasing_ThrowsArgumentException()
        {
            // Arrange
            Type enumType = typeof(TestEnum);
            string displayName = "c";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => EnumUtil.Parse(enumType, displayName));
        }

        /// <summary>
        /// Verifies that empty or whitespace-only display names throw ArgumentException.
        /// Input conditions:
        ///  - displayName: empty string or whitespace
        /// Expected result:
        ///  - ArgumentException is thrown.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Parse_EmptyOrWhitespaceDisplayName_ThrowsArgumentException(string displayName)
        {
            // Arrange
            Type enumType = typeof(TestEnum);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => EnumUtil.Parse(enumType, displayName));
        }

        /// <summary>
        /// Verifies that providing a non-enum Type results in an ArgumentException from Enum.Parse.
        /// Input conditions:
        ///  - enumType: typeof(string)
        /// Expected result:
        ///  - ArgumentException is thrown.
        /// </summary>
        [Fact]
        public void Parse_NonEnumType_ThrowsArgumentException()
        {
            // Arrange
            Type nonEnumType = typeof(string);
            string displayName = "anything";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => EnumUtil.Parse(nonEnumType, displayName));
        }
    }
}