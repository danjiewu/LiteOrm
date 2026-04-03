using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable
using LiteOrm;
using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Tests for LiteOrm.Common.PropertyExpr constructor behavior.
    /// </summary>
    public class PropertyExprTests
    {
        /// <summary>
        /// Verifies that providing a null propertyName to the constructor throws ArgumentNullException.
        /// Input: propertyName = null.
        /// Expected: ArgumentNullException is thrown and the ParamName references 'propertyName'.
        /// </summary>
        [Fact]
        public void PropertyExpr_Ctor_NullPropertyName_ThrowsArgumentNullException()
        {
            // Arrange
            string? nullName = null;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new PropertyExpr(nullName!));
            Assert.Equal("propertyName", ex.ParamName);
        }

        /// <summary>
        /// Verifies that valid property names are accepted and assigned to PropertyName.
        /// Inputs include empty string, whitespace, very long string, and strings with special characters.
        /// Expected: No exception and PropertyName equals the provided input.
        /// </summary>
        [Theory]
        [MemberData(nameof(ValidPropertyNames))]
        public void PropertyExpr_Ctor_ValidPropertyNames_SetsPropertyName(string? input)
        {
            // Arrange
            // (input provided by MemberData)

            // Act
            var expr = new PropertyExpr(input ?? string.Empty);

            // Assert
            Assert.Equal(input ?? string.Empty, expr.PropertyName);
        }

        public static IEnumerable<object?[]> ValidPropertyNames()
        {
            // empty string
            yield return new object?[] { string.Empty };

            // whitespace-only
            yield return new object?[] { "   " };

            // short normal name
            yield return new object?[] { "Name" };

            // name with special characters
            yield return new object?[] { "Col$#_01\n\t" };

            // very long name (boundary / stress)
            yield return new object?[] { new string('x', 5000) };

            // name containing unicode
            yield return new object?[] { "属性名😊" };
        }

        /// <summary>
        /// Tests that assigning valid SQL identifier names to PropertyName succeeds and the getter returns the same value.
        /// Inputs include simple names, underscore/numeric names, empty string (allowed by ThrowIfInvalidSqlName),
        /// and a very long valid name to exercise boundary/length handling.
        /// Expected: No exception is thrown and the assigned value is returned by the getter.
        /// </summary>
        [Theory]
        [MemberData(nameof(ValidPropertyNames))]
        public void PropertyName_SetValidValue_GetReturnsSame(string validName)
        {
            // Arrange
            var expr = new PropertyExpr();

            // Act
            expr.PropertyName = validName;

            // Assert
            Assert.Equal(validName, expr.PropertyName);
        }

        /// <summary>
        /// Tests that assigning invalid SQL identifier names to PropertyName throws ArgumentException.
        /// Inputs include whitespace-only, characters not allowed by the SQL name validation (spaces, dashes, dots, control chars).
        /// Expected: ArgumentException is thrown and the ParamName is "PropertyName".
        /// </summary>
        [Theory]
        [MemberData(nameof(InvalidPropertyNames))]
        public void PropertyName_SetInvalidValue_ThrowsArgumentException(string invalidName)
        {
            // Arrange
            var expr = new PropertyExpr();

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => expr.PropertyName = invalidName);
            Assert.Equal("PropertyName", ex.ParamName);
        }

        public static IEnumerable<object?[]> InvalidPropertyNames()
        {
            // Whitespace-only should be invalid because it's non-empty and does not match valid-name pattern.
            yield return new object[] { "   " };
            // Dashes are typically not allowed by the stated rule (only letters, numbers, underscores).
            yield return new object[] { "Name-With-Dash" };
            // Dot character should be invalid in a single identifier.
            yield return new object[] { "Name.With.Dot" };
            // Control or punctuation characters should be rejected.
            yield return new object[] { "Name!\u0001" };
        }

        /// <summary>
        /// Verifies that the parameterless constructor does not throw and leaves
        /// TableAlias and PropertyName as null. Also checks the ToString preview
        /// behavior for a freshly constructed instance.
        /// Input conditions: call new PropertyExpr() with no prior state.
        /// Expected result: no exception, TableAlias == null, PropertyName == null, ToString() == "[]".
        /// </summary>
        [Fact]
        public void PropertyExpr_ParameterlessCtor_DefaultValues_NullPropertiesAndEmptyBracketsToString()
        {
            // Arrange
            // (no setup required)

            // Act
            var expr = new PropertyExpr();

            // Assert
            Assert.Null(expr.TableAlias);
            Assert.Null(expr.PropertyName);
            Assert.Equal("[]", expr.ToString());
        }

        /// <summary>
        /// Ensures multiple parameterless constructions produce instances that are
        /// considered equal according to Equals and yield the same hash code.
        /// Input conditions: create two separate instances via new PropertyExpr().
        /// Expected result: Equals returns true and GetHashCode values are equal.
        /// </summary>
        [Fact]
        public void PropertyExpr_ParameterlessCtor_MultipleInstances_AreEqualAndHashCodesMatch()
        {
            // Arrange
            // (no setup required)

            // Act
            var first = new PropertyExpr();
            var second = new PropertyExpr();

            // Assert
            Assert.True(first.Equals(second));
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        /// <summary>
        /// Verifies Equals returns false when the other object is null.
        /// Input: PropertyExpr with a valid property name.
        /// Expected: Equals(null) returns false.
        /// </summary>
        [Fact]
        public void Equals_OtherIsNull_ReturnsFalse()
        {
            // Arrange
            var expr = new PropertyExpr("Name");

            // Act
            var result = expr.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Verifies Equals returns false when comparing to a different type.
        /// Input: PropertyExpr and an unrelated object instance.
        /// Expected: Equals returns false.
        /// </summary>
        [Fact]
        public void Equals_OtherIsDifferentType_ReturnsFalse()
        {
            // Arrange
            var expr = new PropertyExpr("Name");
            var other = new object();

            // Act
            var result = expr.Equals(other);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Parameterized tests for various combinations of TableAlias and PropertyName on two PropertyExpr instances.
        /// Inputs: alias1, name1, alias2, name2 (alias may be null).
        /// Expected: Equals returns the provided expected boolean and is symmetric.
        /// </summary>
        [Theory]
        [MemberData(nameof(EqualsCases))]
        public void Equals_VariousCombinations_ExpectedResult(string? alias1, string name1, string? alias2, string name2, bool expected)
        {
            // Arrange
            var p1 = new PropertyExpr(name1);
            if (alias1 != null) p1.TableAlias = alias1;

            var p2 = new PropertyExpr(name2);
            if (alias2 != null) p2.TableAlias = alias2;

            // Act
            var result12 = p1.Equals((object)p2);
            var result21 = p2.Equals((object)p1);

            // Assert - symmetry and expected outcome
            Assert.Equal(expected, result12);
            Assert.Equal(expected, result21);
        }

        /// <summary>
        /// Provides test data for Equals_VariousCombinations_ExpectedResult.
        /// Covers: same/no alias, different alias, different property names, and case-sensitivity.
        /// </summary>
        public static IEnumerable<object?[]> EqualsCases()
        {
            // Both no alias, same property -> equal
            yield return new object?[] { null, "Name", null, "Name", true };

            // Both same alias and same property -> equal
            yield return new object?[] { "u", "Name", "u", "Name", true };

            // Same property but different aliases -> not equal
            yield return new object?[] { "u", "Name", "d", "Name", false };

            // One alias null, the other non-null with same property -> not equal
            yield return new object?[] { null, "Name", "u", "Name", false };

            // Different property names, no alias -> not equal
            yield return new object?[] { null, "Name", null, "Age", false };

            // Case difference in property name -> not equal (string equality is case-sensitive)
            yield return new object?[] { null, "Name", null, "name", false };
        }

        /// <summary>
        /// Provides combinations of TableAlias and PropertyName to validate Clone copies state correctly.
        /// Includes: typical values, nulls, empty strings and very long strings.
        /// </summary>
        public static IEnumerable<object?[]> CloneCases()
        {
            yield return new object?[] { "u", "Name" };                        // typical alias + name
            yield return new object?[] { null, "Name" };                       // no alias
            yield return new object?[] { "", "Name" };                         // empty alias allowed
            yield return new object?[] { "u", "" };                            // empty property name allowed
            yield return new object?[] { null, null };                         // both null (default ctor state)
            yield return new object?[] { new string('a', 1000), new string('b', 1000) }; // long values
        }

        /// <summary>
        /// Verifies that Clone creates a distinct instance with identical TableAlias and PropertyName values.
        /// Conditions:
        /// - Input combinations include non-null, null, empty and very long strings for TableAlias and PropertyName.
        /// Expected:
        /// - Clone returns a different instance (reference) from the source.
        /// - The cloned instance has equal PropertyName and TableAlias values.
        /// - Equals reports the two instances as equal and GetHashCode matches.
        /// - ToString output is the same for both instances.
        /// </summary>
        [Theory]
        [MemberData(nameof(CloneCases))]
        public void Clone_StateIsCopiedAndInstanceIsDistinct(string? tableAlias, string? propertyName)
        {
            // Arrange
            PropertyExpr original;
            if (propertyName is null)
            {
                // Use parameterless ctor and set TableAlias if provided.
                original = new PropertyExpr();
                if (tableAlias != null)
                    original.TableAlias = tableAlias;
                // leave PropertyName as null
            }
            else
            {
                // Use Expr helper to construct consistent instances.
                if (tableAlias == null)
                    original = Expr.Prop(propertyName);
                else
                    original = Expr.Prop(tableAlias, propertyName);
            }

            // Act
            var clonedExpr = original.Clone();

            // Assert - instance and type checks
            Assert.NotSame(original, clonedExpr);
            Assert.IsType<PropertyExpr>(clonedExpr);

            var clone = clonedExpr as PropertyExpr;
            Assert.NotNull(clone);

            // Assert - state equality
            Assert.Equal(original.PropertyName, clone.PropertyName);
            Assert.Equal(original.TableAlias, clone.TableAlias);

            // Assert - equality and hash code consistency
            Assert.True(clone!.Equals(original));
            Assert.Equal(original.GetHashCode(), clone.GetHashCode());

            // Assert - textual representation preserved
            Assert.Equal(original.ToString(), clone.ToString());
        }

        /// <summary>
        /// Verifies that ExprType returns ExprType.Property for various valid instances created via different constructors and states.
        /// Input cases include:
        /// - Default-constructed instance.
        /// - Instance constructed with a non-null property name.
        /// - Instance constructed and assigned a table alias.
        /// Expected result: ExprType is always ExprType.Property and no exception is thrown.
        /// </summary>
        [Theory]
        [MemberData(nameof(GetValidPropertyExprInstances))]
        public void ExprType_InstanceAny_ReturnsProperty(PropertyExpr expr)
        {
            // Arrange is handled by MemberData providing different valid instances.

            // Act
            var actual = expr.ExprType;

            // Assert
            Assert.Equal(ExprType.Property, actual);
        }

        /// <summary>
        /// Verifies that ExprType remains ExprType.Property after mutating settable members (TableAlias and PropertyName) with valid values.
        /// Input conditions:
        /// - Start with an instance constructed with a valid property name.
        /// - Mutate TableAlias and PropertyName to other valid SQL identifier-like strings.
        /// Expected result: ExprType continues to return ExprType.Property.
        /// </summary>
        [Fact]
        public void ExprType_AfterMutations_ReturnsProperty()
        {
            // Arrange
            var expr = new PropertyExpr("InitialName");

            // Act
            expr.TableAlias = "tAlias";
            expr.PropertyName = "NewName";
            var actual = expr.ExprType;

            // Assert
            Assert.Equal(ExprType.Property, actual);
        }

        /// <summary>
        /// Provides a set of valid PropertyExpr instances for testing ExprType.
        /// Note: Values chosen are simple, valid-looking SQL identifier strings to avoid triggering validation helper behavior.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<object?[]> GetValidPropertyExprInstances()
        {
            yield return new object?[] { new PropertyExpr() };
            yield return new object?[] { new PropertyExpr("ColumnName") };
            yield return new object?[] { new PropertyExpr("Col") { TableAlias = "Tbl" } };
        }

        /// <summary>
        /// Verifies ToString output for combinations of table alias and property name.
        /// Inputs include null and empty values for both TableAlias and PropertyName.
        /// Expected: when TableAlias is null -> "[PropertyName]" (PropertyName null or empty yields "[]");
        /// when TableAlias is non-null (including empty string) -> "[TableAlias].[PropertyName]".
        /// </summary>
        /// <param name="tableAlias">Nullable table alias to assign to PropertyExpr.TableAlias.</param>
        /// <param name="propertyName">Nullable property name to assign to PropertyExpr.PropertyName.</param>
        /// <param name="expected">Expected string result from ToString()</param>
        [Theory]
        [InlineData(null, null, "[]")]
        [InlineData(null, "", "[]")]
        [InlineData(null, "Name", "[Name]")]
        [InlineData("u", "Name", "[u].[Name]")]
        [InlineData("", "Name", "[].[Name]")]
        [InlineData("u", "", "[u].[]")]
        [InlineData("alias_with_underscore", "prop123", "[alias_with_underscore].[prop123]")]
        public void ToString_TableAliasAndPropertyName_FormatsCorrectly(string? tableAlias, string? propertyName, string expected)
        {
            // Arrange
            var expr = new PropertyExpr(); // default ctor allowed; properties set via setters

            // Act
            // Note: the setters validate SQL names; we use only accepted values here (null or alnum/underscore/empty)
            expr.TableAlias = tableAlias;
            expr.PropertyName = propertyName;

            var actual = expr.ToString();

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Ensures ToString handles very long property names without truncation or exceptions.
        /// Input: very long property name (1000 'a' chars) and a valid table alias.
        /// Expected: full long name appears in the formatted result.
        /// </summary>
        [Theory]
        [MemberData(nameof(LongNameMemberData))]
        public void ToString_WithVeryLongPropertyName_DoesNotTruncate(string? tableAlias, string propertyName, string expected)
        {
            // Arrange
            var expr = new PropertyExpr();
            expr.TableAlias = tableAlias;
            expr.PropertyName = propertyName;

            // Act
            var actual = expr.ToString();

            // Assert
            Assert.Equal(expected, actual);
        }

        public static System.Collections.Generic.IEnumerable<object[]> LongNameMemberData()
        {
            var longName = new string('a', 1000);
            yield return new object[] { "u", longName, $"[u].[{longName}]" };
            yield return new object[] { null, longName, $"[{longName}]" };
        }

        /// <summary>
        /// Verifies that a newly constructed PropertyExpr has a null TableAlias by default.
        /// Input: default-constructed PropertyExpr.
        /// Expected: TableAlias getter returns null.
        /// </summary>
        [Fact]
        public void TableAlias_DefaultValue_NullReturned()
        {
            // Arrange
            var expr = new PropertyExpr();

            // Act
            string? result = expr.TableAlias;

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Verifies that setting TableAlias to valid inputs succeeds and the getter returns the same value.
        /// Tested inputs include: null, empty string, simple names, underscore-only, and a very long valid identifier.
        /// Expected: No exception and TableAlias equals the assigned value.
        /// </summary>
        [Theory]
        [MemberData(nameof(ValidAliases))]
        public void TableAlias_SetValidValues_ReturnsSameValue(string? candidate)
        {
            // Arrange
            var expr = new PropertyExpr();

            // Act
            expr.TableAlias = candidate;
            string? actual = expr.TableAlias;

            // Assert
            Assert.Equal(candidate, actual);
        }

        public static IEnumerable<object?[]> ValidAliases()
        {
            // null is allowed per ThrowIfInvalidSqlName description
            yield return new object?[] { null };
            // empty string is allowed
            yield return new object?[] { string.Empty };
            // typical simple alias
            yield return new object?[] { "tbl" };
            // letters, digits and underscore
            yield return new object?[] { "TBL_123" };
            // single underscore
            yield return new object?[] { "_" };
            // long valid name (1000 'a' characters)
            yield return new object?[] { new string('a', 1000) };
        }

        /// <summary>
        /// Verifies that setting TableAlias to invalid SQL identifier values throws an ArgumentException.
        /// Inputs tested: whitespace-only, contains dash, contains dollar sign, contains dot, non-ASCII characters, control characters.
        /// Expected: ArgumentException is thrown for each invalid input.
        /// </summary>
        [Theory]
        [MemberData(nameof(InvalidAliases))]
        public void TableAlias_SetInvalidValues_ThrowsArgumentException(string candidate)
        {
            // Arrange
            var expr = new PropertyExpr();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => expr.TableAlias = candidate);
        }

        public static IEnumerable<object?[]> InvalidAliases()
        {
            yield return new object?[] { " " };            // single space
            yield return new object?[] { "name with space" };
            yield return new object?[] { "name-with-dash" };
            yield return new object?[] { "name$" };
            yield return new object?[] { "name.with.dot" };
            yield return new object?[] { "中文" };         // non-latin characters
            yield return new object?[] { "\n" };          // control character
            yield return new object?[] { "\t" };          // control character
        }

        /// <summary>
        /// Helper to construct a PropertyExpr with possibly null TableAlias and PropertyName using the parameterless constructor
        /// then setting properties. This avoids calling constructors that disallow null propertyName.
        /// </summary>
        private static PropertyExpr Create(string? tableAlias, string? propertyName)
        {
            var p = new PropertyExpr();
            // Set even nulls explicitly; ThrowIfInvalidSqlName permits null or empty.
            p.TableAlias = tableAlias!;
            p.PropertyName = propertyName!;
            return p;
        }

        /// <summary>
        /// Provides test cases where two PropertyExpr instances should produce equal hash codes.
        /// Cases include both nulls, identical non-null values, empty alias, and long values.
        /// </summary>
        public static IEnumerable<object?[]> EqualHashCases()
        {
            // Both nulls
            yield return new object?[] { null, null };

            // Both have same property name, no alias
            yield return new object?[] { null, "Name" };

            // Same alias and property
            yield return new object?[] { "u", "Name" };

            // Empty alias vs empty alias (empty string is allowed)
            yield return new object?[] { "", "Name" };

            // Long values (ensure stability for long strings)
            var longAlias = new string('a', 300);
            var longProp = new string('p', 500);
            yield return new object?[] { longAlias, longProp };
        }

        /// <summary>
        /// Provides test cases where two PropertyExpr instances should produce different hash codes.
        /// Each item: alias1, prop1, alias2, prop2
        /// </summary>
        public static IEnumerable<object?[]> DifferentHashCases()
        {
            // null alias vs empty alias, same property name -> different
            yield return new object?[] { null, "Name", "", "Name" };

            // different aliases
            yield return new object?[] { "u", "Name", "v", "Name" };

            // different property names
            yield return new object?[] { "u", "Name", "u", "Age" };

            // null propertyName vs empty propertyName
            yield return new object?[] { null, null, null, "" };

            // same property but one has alias and the other doesn't
            yield return new object?[] { "u", "Name", null, "Name" };
        }

        /// <summary>
        /// Verifies that two PropertyExpr instances with identical TableAlias and PropertyName produce equal hash codes.
        /// Input conditions: various combinations including null, empty, normal and long strings for alias and name.
        /// Expected: GetHashCode returns equal values for equal logical state.
        /// </summary>
        [Theory]
        [MemberData(nameof(EqualHashCases))]
        public void GetHashCode_EqualInstances_ReturnsEqual(string? tableAlias, string? propertyName)
        {
            // Arrange
            var p1 = Create(tableAlias, propertyName);
            var p2 = Create(tableAlias, propertyName);

            // Act
            int h1 = p1.GetHashCode();
            int h2 = p2.GetHashCode();

            // Assert
            Assert.Equal(h1, h2);
        }

        /// <summary>
        /// Verifies that two PropertyExpr instances that differ by alias or property name produce different hash codes.
        /// Input conditions: pairs of differing alias/property combinations.
        /// Expected: GetHashCode returns different values for different logical state in most practical cases.
        /// Note: Hash collisions are theoretically possible but extremely unlikely for chosen distinct inputs.
        /// </summary>
        [Theory]
        [MemberData(nameof(DifferentHashCases))]
        public void GetHashCode_DifferentInstances_OftenDifferent(string? alias1, string? prop1, string? alias2, string? prop2)
        {
            // Arrange
            var p1 = Create(alias1, prop1);
            var p2 = Create(alias2, prop2);

            // Act
            int h1 = p1.GetHashCode();
            int h2 = p2.GetHashCode();

            // Assert - primary expectation is inequality for these distinct inputs
            Assert.NotEqual(h1, h2);
        }

        /// <summary>
        /// Verifies that GetHashCode is stable across multiple invocations on the same instance.
        /// Input conditions: a sample PropertyExpr with alias and property set.
        /// Expected: successive GetHashCode calls return identical values.
        /// </summary>
        [Fact]
        public void GetHashCode_SameInstance_IdempotentAcrossCalls()
        {
            // Arrange
            var p = Create("stableAlias", "stableProp");

            // Act
            int first = p.GetHashCode();
            int second = p.GetHashCode();
            int third = p.GetHashCode();

            // Assert
            Assert.Equal(first, second);
            Assert.Equal(second, third);
        }
    }
}