#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;

#nullable enable
using LiteOrm;
using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Tests for ValueStringBuilder constructors and initial state derived from the provided initial buffer.
    /// </summary>
    public class ValueStringBuilderTests
    {
        /// <summary>
        /// Verifies that constructing ValueStringBuilder with different initial buffer sizes sets Capacity correctly
        /// and initializes Length to zero. Also ensures AsSpan and ToString reflect empty content regardless of buffer contents.
        /// Input conditions: initialBuffer created from a char array of varying lengths.
        /// Expected result: Capacity equals initial buffer length; Length is zero; AsSpan is empty; ToString returns empty string.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(16)]
        [InlineData(128)]
        public void Constructor_InitialBuffer_CapacityMatchesAndLengthZero(int initialCapacity)
        {
            // Arrange
            char[] buffer = new char[initialCapacity];
            // Put some non-default data into the buffer to ensure constructor does not treat existing content as length.
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (char)('A' + (i % 26));
            }

            // Act
            var builder = new ValueStringBuilder(buffer);

            // Assert
            // Capacity should reflect the provided buffer length
            Assert.Equal(initialCapacity, builder.Capacity);

            // Length must be initialized to zero by the constructor
            Assert.Equal(0, builder.Length);

            // AsSpan should be empty (slice of length 0)
            Assert.Equal(0, builder.AsSpan().Length);

            // ToString should produce an empty string
            Assert.Equal(string.Empty, builder.ToString());
        }

        /// <summary>
        /// Ensures that when the provided initial buffer contains existing characters the constructor still initializes
        /// Length to zero and does not copy or expose the pre-existing content.
        /// Input conditions: initialBuffer with prefilled characters.
        /// Expected result: Length is zero and AsSpan is empty, so existing characters are not considered part of content.
        /// </summary>
        [Fact]
        public void Constructor_WithPrefilledBuffer_DoesNotExposeExistingContent()
        {
            // Arrange
            char[] prefilled = new char[] { 'x', 'y', 'z' };
            Span<char> initialSpan = prefilled; // span over existing data

            // Act
            var builder = new ValueStringBuilder(initialSpan);

            // Assert
            Assert.Equal(prefilled.Length, builder.Capacity);
            Assert.Equal(0, builder.Length);
            Assert.Equal(0, builder.AsSpan().Length);
            Assert.Equal(string.Empty, builder.ToString());
        }

        /// <summary>
        /// Test that appending an empty ReadOnlySpan does not change the builder state.
        /// Input conditions: builder pre-populated by appending "abc", then Append is called with ReadOnlySpan.Empty.
        /// Expected result: Length and content remain unchanged.
        /// </summary>
        [Fact]
        public void Append_EmptySpan_DoesNotChangeState()
        {
            // Arrange
            Span<char> buffer = stackalloc char[8];
            var builder = new ValueStringBuilder(buffer);
            builder.Append("abc".AsSpan());
            int beforeLength = builder.Length;
            string beforeContent = builder.ToString();

            // Act
            builder.Append(ReadOnlySpan<char>.Empty);

            // Assert
            Assert.Equal(beforeLength, builder.Length);
            Assert.Equal(beforeContent, builder.ToString());
        }

        /// <summary>
        /// Parameterized test ensuring appending spans that fit within capacity appends correctly without growing.
        /// Input conditions: various test strings that exactly fit or are smaller than the initial capacity.
        /// Expected result: Length equals input length, content matches, and Capacity remains unchanged.
        /// </summary>
        [Theory]
        [MemberData(nameof(WithinCapacityData))]
        public void Append_SpanWithinCapacity_AppendsWithoutGrowing(string input)
        {
            // Arrange
            int capacity = input.Length; // exact-fit scenario is important
            Span<char> buffer = capacity > 0 ? stackalloc char[capacity] : stackalloc char[1];
            var builder = new ValueStringBuilder(buffer);
            int initialCapacity = builder.Capacity;

            // Act
            builder.Append(input.AsSpan());

            // Assert
            Assert.Equal(input.Length, builder.Length);
            Assert.Equal(input, builder.ToString());
            Assert.Equal(initialCapacity, builder.Capacity);
        }

        public static IEnumerable<object?[]> WithinCapacityData()
        {
            yield return new object?[] { "a" };
            yield return new object?[] { "Hello, World!" };
            yield return new object?[] { "\t \n" }; // whitespace and control chars
            yield return new object?[] { new string('x', 16) }; // exact-fit larger
            yield return new object?[] { "" }; // zero-length (will use capacity 1 stackalloc)
        }

        /// <summary>
        /// Test that appending a span which causes requiredAdditionalCapacity to exceed current capacity
        /// triggers growth and preserves existing content while appending new content.
        /// Input conditions: initial small capacity; first append fills it, second append exceeds it.
        /// Expected result: combined content is correct, Length matches, and Capacity grows to at least Length.
        /// </summary>
        [Fact]
        public void Append_SpanExceedingCapacity_GrowsAndAppends()
        {
            // Arrange
            Span<char> buffer = stackalloc char[4];
            var builder = new ValueStringBuilder(buffer);
            builder.Append("abcd".AsSpan()); // fills initial capacity
            int beforeCapacity = builder.Capacity;

            // Act
            builder.Append("efghij".AsSpan()); // forces Grow

            // Assert
            string expected = "abcdefghij";
            Assert.Equal(expected.Length, builder.Length);
            Assert.Equal(expected, builder.ToString());
            Assert.True(builder.Capacity >= builder.Length);
            Assert.True(builder.Capacity > beforeCapacity);
        }

        /// <summary>
        /// Verify Dispose behavior for builders created with a rented array and with an external span buffer.
        /// Conditions:
        /// - When created via Create(...) (array rented from ArrayPool), Dispose should return the array and reset the builder to default (Length==0, Capacity==0, empty string).
        /// - When created with an external span (stackalloc), Dispose should do nothing (preserve contents and length).
        /// </summary>
        /// <param name="useCreate">If true, create builder with ValueStringBuilder.Create; otherwise use stackalloc buffer constructor.</param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Dispose_WithAndWithoutRentedArray_ResetsOnlyWhenRented(bool useCreate)
        {
            // Arrange
            ValueStringBuilder builder;
            if (useCreate)
            {
                builder = ValueStringBuilder.Create(16);
            }
            else
            {
                Span<char> buffer = stackalloc char[16];
                builder = new ValueStringBuilder(buffer);
            }

            // Put some content into the builder to observe changes after Dispose.
            builder.Append("abc");
            Assert.Equal(3, builder.Length); // Sanity check before dispose

            // Act
            builder.Dispose();

            // Assert
            if (useCreate)
            {
                // Rented arrays should be returned and the builder should be reset to default
                Assert.Equal(0, builder.Length);
                Assert.Equal(0, builder.Capacity);
                Assert.Equal(string.Empty, builder.ToString());
                Assert.True(builder.AsSpan().IsEmpty);
            }
            else
            {
                // External buffer path should remain intact; Dispose should not reset state
                Assert.Equal(3, builder.Length);
                Assert.Equal("abc", builder.ToString());
                Assert.Equal(3, builder.AsSpan().Length);
            }
        }

        /// <summary>
        /// Ensure calling Dispose multiple times on a builder created via Create(...) does not throw and is safe (idempotent).
        /// Conditions:
        /// - Builder created via Create with rented array.
        /// - Dispose invoked twice.
        /// Expected result: No exception is thrown and the builder remains in default/reset state after calls.
        /// </summary>
        [Fact]
        public void Dispose_CalledTwice_OnRented_DoesNotThrowAndRemainsDefault()
        {
            // Arrange
            var builder = ValueStringBuilder.Create(8);
            builder.Append('x');
            Assert.Equal(1, builder.Length);

            // Act & Assert - first dispose should succeed
            builder.Dispose();

            // Act - second dispose should not throw
            Exception? ex = Record.Exception(() => builder.Dispose());
            Assert.Null(ex);

            // The builder should remain reset/default
            Assert.Equal(0, builder.Length);
            Assert.Equal(0, builder.Capacity);
            Assert.Equal(string.Empty, builder.ToString());
            Assert.True(builder.AsSpan().IsEmpty);
        }

        /// <summary>
        /// Verifies that Clear resets Length to zero and results in an empty span/string
        /// after the builder contains content. This is exercised for both pooled (Create)
        /// and stackallocated initial buffers, and includes cases that trigger Grow.
        /// Expected: Length becomes 0, AsSpan().Length == 0, ToString() == string.Empty,
        /// and Capacity remains at least the initial requested capacity (or larger if Grow occurred).
        /// </summary>
        [Theory]
        [MemberData(nameof(NonEmptyBuilderCases))]
        public void Clear_WithContent_ResetsLengthAndLeavesCapacity(bool usePooled, int initialCapacity, string content)
        {
            // Arrange
            ValueStringBuilder builder = default;
            if (usePooled)
            {
                builder = ValueStringBuilder.Create(initialCapacity);
            }
            else
            {
                Span<char> buffer = stackalloc char[initialCapacity];
                builder = new ValueStringBuilder(buffer);
            }

            // Act - append content to ensure _length > 0 and possibly trigger Grow
            builder.Append(content);
            // Sanity before clear: length should be > 0 for these cases
            Assert.True(builder.Length > 0);

            builder.Clear();

            // Assert
            Assert.Equal(0, builder.Length);                                // length reset
            Assert.Equal(0, builder.AsSpan().Length);                       // span empty
            Assert.Equal(string.Empty, builder.ToString());                 // string empty
            Assert.True(builder.Capacity >= initialCapacity);               // capacity not smaller than initial request

            // Cleanup pooled resources if applicable
            if (usePooled)
            {
                builder.Dispose();
            }
        }

        /// <summary>
        /// Verifies Clear is a no-op when the builder is already empty.
        /// This is tested for both pooled and stackallocated initial buffers.
        /// Expected: Length remains 0, AsSpan and ToString remain empty, capacity unchanged.
        /// </summary>
        [Theory]
        [MemberData(nameof(EmptyBuilderCases))]
        public void Clear_WhenAlreadyEmpty_NoChange(bool usePooled, int initialCapacity)
        {
            // Arrange
            ValueStringBuilder builder = default;
            if (usePooled)
            {
                builder = ValueStringBuilder.Create(initialCapacity);
            }
            else
            {
                Span<char> buffer = stackalloc char[initialCapacity];
                builder = new ValueStringBuilder(buffer);
            }

            // Pre-assert
            Assert.Equal(0, builder.Length);

            // Act
            builder.Clear();

            // Assert
            Assert.Equal(0, builder.Length);
            Assert.Equal(0, builder.AsSpan().Length);
            Assert.Equal(string.Empty, builder.ToString());
            Assert.True(builder.Capacity >= initialCapacity);

            if (usePooled)
            {
                builder.Dispose();
            }
        }

        public static IEnumerable<object[]> NonEmptyBuilderCases()
        {
            // (usePooled, initialCapacity, content)
            yield return new object[] { false, 8, "abc" };               // stackalloc, fits in initial buffer
            yield return new object[] { false, 2, "abcdefghijkl" };     // stackalloc, triggers Grow
            yield return new object[] { true, 128, "hello world" };     // pooled, typical
            yield return new object[] { true, 1, new string('x', 50) }; // pooled, content larger than initialCapacity
        }

        public static IEnumerable<object[]> EmptyBuilderCases()
        {
            // (usePooled, initialCapacity)
            yield return new object[] { false, 8 };
            yield return new object[] { true, 128 };
            yield return new object[] { false, 1 };
            yield return new object[] { true, 1 };
        }

        /// <summary>
        /// Verifies that appending a single character writes the character, increments Length to 1,
        /// and that the returned ValueStringBuilder reflects the same content.
        /// Inputs tested: normal letter, null char, newline, and char.MaxValue.
        /// Expected: Length == 1 and ToString() equals the appended character.
        /// </summary>
        [Theory]
        [InlineData('a')]
        [InlineData('\0')]
        [InlineData('\n')]
        [InlineData((char)65535)] // char.MaxValue
        public void Append_SingleChar_AppendsAndIncrementsLength(char input)
        {
            // Arrange
            Span<char> buffer = stackalloc char[1];
            var builder = new ValueStringBuilder(buffer);

            // Act
            var returned = builder.Append(input);

            // Assert
            Assert.Equal(1, builder.Length); // length updated on original
            Assert.Equal(1, returned.Length); // length observed on returned struct
            Assert.Equal(input.ToString(), builder.ToString()); // content preserved
            Assert.Equal(builder.ToString(), returned.ToString()); // returned copy has same content
        }

        /// <summary>
        /// Verifies that appending two characters preserves existing characters and grows when needed.
        /// Tested with initial capacities 0 and 1 to exercise grow from zero capacity and grow-on-second-append.
        /// Expected: Final content matches concatenation of appended chars.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void Append_TwoChars_TriggersGrowWhenNeededAndPreservesContent(int initialCapacity)
        {
            // Arrange
            Span<char> buffer = initialCapacity == 0 ? stackalloc char[0] : stackalloc char[1];
            var builder = new ValueStringBuilder(buffer);

            // Act
            builder.Append('X');
            builder.Append('Y');

            // Assert
            Assert.Equal(2, builder.Length);
            Assert.Equal("XY", builder.ToString());

            // Cleanup: if a pooled array was rented during Grow, return it.
            builder.Dispose();
        }

        /// <summary>
        /// Verifies chaining (fluent usage) of Append(char) appends characters in order.
        /// Uses a small initial buffer to ensure behavior works both with and without intermediate assignment.
        /// Expected: Final concatenated string matches sequence of appended characters.
        /// </summary>
        [Fact]
        public void Append_Chaining_AppendsInOrder()
        {
            // Arrange
            Span<char> buffer = stackalloc char[2];
            var builder = new ValueStringBuilder(buffer);

            // Act
            // chaining without assignment should mutate the original instance
            builder.Append('A').Append('B');

            // Assert
            Assert.Equal(2, builder.Length);
            Assert.Equal("AB", builder.ToString());
        }

        /// <summary>
        /// Verifies Create() with explicit valid capacities returns a builder whose Capacity
        /// is at least the requested capacity and whose Length is zero.
        /// Inputs: initialCapacity values 1, 128, 256.
        /// Expected: No exception; Length == 0; Capacity >= initialCapacity; ToString() == empty.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(128)]
        [InlineData(256)]
        public void Create_WithValidInitialCapacity_ReturnsBuilderWithCapacityAtLeastAndZeroLength(int initialCapacity)
        {
            // Arrange & Act
            var builder = ValueStringBuilder.Create(initialCapacity);

            // Assert
            Assert.Equal(0, builder.Length); // length should start at zero
            Assert.True(builder.Capacity >= initialCapacity, $"Capacity {builder.Capacity} should be >= requested {initialCapacity}");
            Assert.Equal(0, builder.AsSpan().Length);
            Assert.Equal(string.Empty, builder.ToString());
        }

        /// <summary>
        /// Verifies Create() without parameters uses the default initial capacity (128).
        /// Input: no parameter.
        /// Expected: No exception; Length == 0; Capacity >= 128.
        /// </summary>
        [Fact]
        public void Create_NoParameter_UsesDefaultInitialCapacity()
        {
            // Arrange & Act
            var builder = ValueStringBuilder.Create();

            // Assert
            Assert.Equal(0, builder.Length);
            Assert.True(builder.Capacity >= 128, $"Capacity {builder.Capacity} should be >= default 128");
        }

        /// <summary>
        /// Verifies Create() throws ArgumentOutOfRangeException for negative initialCapacity values.
        /// Inputs: -1, int.MinValue.
        /// Expected: ArgumentOutOfRangeException thrown.
        /// </summary>
        [Theory]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void Create_NegativeInitialCapacity_ThrowsArgumentOutOfRangeException(int invalidCapacity)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => ValueStringBuilder.Create(invalidCapacity));
        }

        /// <summary>
        /// Verifies Create() with an extremely large initialCapacity results in an exception.
        /// Input: int.MaxValue (resource extreme).
        /// Expected: Some exception is thrown (e.g., OutOfMemoryException or similar).
        /// This test is defensive: it asserts that the call does not succeed silently.
        /// </summary>
        [Fact]
        public void Create_VeryLargeInitialCapacity_ThrowsException()
        {
            // Arrange
            int huge = int.MaxValue;

            // Act & Assert - Accept any exception type to avoid brittle assertions about runtime-specific behavior
            Assert.ThrowsAny<Exception>(() => ValueStringBuilder.Create(huge));
        }

        /// <summary>
        /// Verifies that ToString returns an empty string when the builder is newly constructed and length is zero.
        /// Condition: newly constructed with a non-zero backing span that contains arbitrary data.
        /// Expected result: ToString returns an empty string because Length starts at 0.
        /// </summary>
        [Fact]
        public void ToString_EmptyBuilder_ReturnsEmptyString()
        {
            // Arrange
            Span<char> initial = stackalloc char[8];
            initial.Fill('x'); // pre-fill to ensure ToString respects _length, not buffer contents
            var vsb = new ValueStringBuilder(initial);

            // Act
            string result = vsb.ToString();

            // Assert
            Assert.Equal(string.Empty, result);
        }

        /// <summary>
        /// Verifies ToString returns the expected string after appending various string inputs.
        /// Condition: append different string inputs (including null, empty, single-char, whitespace, control chars, very long strings).
        /// Expected result: ToString returns the concatenation of appended content; null or empty inputs do not change content.
        /// </summary>
        [Theory]
        [MemberData(nameof(AppendStringCases))]
        public void ToString_AfterAppendString_ReturnsExpected(string? input, string expected)
        {
            // Arrange
            Span<char> initial = stackalloc char[4]; // small buffer to exercise both in-place and growth paths
            var vsb = new ValueStringBuilder(initial);

            // Act
            vsb.Append(input);
            string result = vsb.ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Test data for ToString_AfterAppendString_ReturnsExpected.
        /// Provides a variety of string inputs to exercise code paths:
        /// - null and empty should produce empty output
        /// - single-character string goes through Append(char)
        /// - multi-character strings go through Append(ReadOnlySpan<char>)
        /// - very long string forces Grow and tests correctness after reallocation
        /// - contains control character to ensure no truncation
        /// </summary>
        public static IEnumerable<object?[]> AppendStringCases()
        {
            yield return new object?[] { null, string.Empty }; // null should be ignored
            yield return new object?[] { string.Empty, string.Empty }; // empty ignored
            yield return new object?[] { "A", "A" }; // single char path
            yield return new object?[] { "BC", "BC" }; // small multi-char
            yield return new object?[] { " \t\n", " \t\n" }; // whitespace and control
            yield return new object?[] { "with\u0000null", "with\u0000null" }; // embedded null character
            string longStr = new string('x', 500); // force growth
            yield return new object?[] { longStr, longStr };
        }

        /// <summary>
        /// Verifies that ToString returns the full concatenation after multiple appends that trigger growth.
        /// Condition: append a char, then a short string, then a very long string while starting from a tiny initial buffer.
        /// Expected result: ToString returns the exact concatenated string.
        /// </summary>
        [Fact]
        public void ToString_AfterMultipleAppendsAndGrow_ReturnsConcatenatedString()
        {
            // Arrange
            string longPart = new string('D', 200);
            string expected = "A" + "BC" + longPart;

            // Use an intentionally tiny buffer to trigger Grow during appends
            Span<char> tiny = stackalloc char[2];
            var vsb = new ValueStringBuilder(tiny);

            // Act
            vsb.Append('A');                // Append char
            vsb.Append("BC");               // Append short string (multi-char path)
            vsb.Append(longPart.AsSpan());  // Append span that forces Grow
            string result = vsb.ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Verifies that AsSpan returns the expected content after appending various string inputs.
        /// Conditions:
        /// - Builder is created via Create(initialCapacity) (pooled backing array).
        /// - The input string may be null, empty, single-char, or longer than initial capacity (forcing growth).
        /// Expected:
        /// - AsSpan() reflects the appended content (or empty when input is null/empty).
        /// </summary>
        [Theory]
        [InlineData(1, "a", "a")]
        [InlineData(1, "abc", "abc")]
        [InlineData(0, null, "")]
        [InlineData(5, "", "")]
        [InlineData(1, "longstringlong", "longstringlong")]
        public void AsSpan_AfterAppend_ReturnsExpectedString(int initialCapacity, string? value, string expected)
        {
            // Arrange
            var builder = ValueStringBuilder.Create(initialCapacity);

            try
            {
                // Act
                builder.Append(value);
                var span = builder.AsSpan();

                // Assert
                Assert.Equal(expected, span.ToString());
            }
            finally
            {
                // Ensure pooled array is returned even if assertion fails
                builder.Dispose();
            }
        }

        /// <summary>
        /// Verifies that AsSpan returns the slice defined by the Length property.
        /// Conditions:
        /// - Builder is constructed over a provided buffer with known content.
        /// - Length is explicitly set to a value <= capacity.
        /// Expected:
        /// - AsSpan() returns the first 'Length' characters of the underlying buffer.
        /// </summary>
        [Fact]
        public void AsSpan_WithSetLength_ReturnsSlice()
        {
            // Arrange
            var buffer = new char[] { 'x', 'y', 'z' };
            var builder = new ValueStringBuilder(buffer);

            // Act
            builder.Length = 2;
            var result = builder.AsSpan().ToString();

            // Assert
            Assert.Equal("xy", result);
        }

        /// <summary>
        /// Verifies that after disposing a pooled ValueStringBuilder the AsSpan becomes empty.
        /// Conditions:
        /// - Builder created via Create(...) (uses pooled array).
        /// - Content appended before disposing.
        /// Expected:
        /// - After Dispose() the builder is reset and AsSpan() returns an empty span.
        /// </summary>
        [Fact]
        public void AsSpan_AfterDispose_ReturnsEmptySpan_ForPooledBuilder()
        {
            // Arrange
            var builder = ValueStringBuilder.Create(4);
            builder.Append("abc");

            // Act
            builder.Dispose();
            var span = builder.AsSpan();

            // Assert
            Assert.Equal(string.Empty, span.ToString());
        }

        /// <summary>
        /// Test purpose:
        /// Verify that Append(string?) does nothing when input is null or empty.
        /// Input conditions: value is null and empty string.
        /// Expected result: Length remains zero, ToString returns empty string, and Capacity is unchanged.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Append_String_NullOrEmpty_NoChange(string? input)
        {
            // Arrange
            var builder = ValueStringBuilder.Create(16);
            int initialCapacity = builder.Capacity;

            try
            {
                // Act
                builder.Append(input);

                // Assert
                Assert.Equal(0, builder.Length);
                Assert.Equal(string.Empty, builder.ToString());
                Assert.Equal(initialCapacity, builder.Capacity);
            }
            finally
            {
                // Ensure pooled buffers are returned
                builder.Dispose();
            }
        }

        /// <summary>
        /// Test purpose:
        /// Verify that Append(string) appends content correctly for various non-empty strings,
        /// including single-character, whitespace-only, multi-character, control/special characters,
        /// and strings long enough to trigger buffer growth.
        /// Input conditions: several representative strings (provided by MemberData).
        /// Expected result: Length equals expected length, ToString matches expected content,
        /// and Capacity is at least as large as the resulting length.
        /// </summary>
        [Theory]
        [MemberData(nameof(Append_TestCases))]
        public void Append_String_AppendsContentAndUpdatesLength(string input, int expectedLength, string expectedContent)
        {
            // Arrange
            var builder = ValueStringBuilder.Create(128);

            try
            {
                // Act
                builder.Append(input);

                // Assert
                Assert.Equal(expectedLength, builder.Length);
                Assert.Equal(expectedContent, builder.ToString());
                Assert.True(builder.Capacity >= expectedLength);
                // AsSpan should reflect the same content
                Assert.Equal(expectedContent, new string(builder.AsSpan()));
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static IEnumerable<object?[]> Append_TestCases()
        {
            // single character -> triggers Append(char) path inside Append(string)
            yield return new object?[] { "A", 1, "A" };

            // whitespace-only should be appended (IsNullOrEmpty returns false)
            yield return new object?[] { "   ", 3, "   " };

            // multi-character simple
            yield return new object?[] { "Hello", 5, "Hello" };

            // special/control characters
            yield return new object?[] { "Line1\nLine2\t\u263A", 12, "Line1\nLine2\t\u263A" };

            // long string to force Grow (initial capacity is 128; use length > 128)
            string longStr = new string('x', 200);
            yield return new object?[] { longStr, 200, longStr };
        }
    }
}