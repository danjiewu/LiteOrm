﻿﻿﻿

using System;
using System.Collections.Generic;
using System.Linq;

using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for ExprTypeValidator.Validate method.
    /// </summary>
    public partial class ExprTypeValidatorTests
    {
        /// <summary>
        /// Tests that Validate returns true when the node parameter is null.
        /// </summary>
        [Fact]
        public void Validate_NullNode_ReturnsTrue()
        {
            // Arrange
            var validator = new ExprTypeValidator(ExprType.Value);

            // Act
            bool result = validator.Validate(null);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Validate returns true when the node's ExprType is in the allowed types set.
        /// Parameterized test covering multiple allowed types with matching nodes.
        /// </summary>
        /// <param name="allowedType">The ExprType to allow in the validator.</param>
        /// <param name="nodeExprType">The ExprType of the node being validated.</param>
        [Theory]
        [InlineData(ExprType.Value, ExprType.Value)]
        [InlineData(ExprType.Property, ExprType.Property)]
        [InlineData(ExprType.Function, ExprType.Function)]
        [InlineData(ExprType.LogicBinary, ExprType.LogicBinary)]
        public void Validate_NodeWithAllowedExprType_ReturnsTrue(ExprType allowedType, ExprType nodeExprType)
        {
            // Arrange
            var validator = new ExprTypeValidator(allowedType);
            var node = CreateNodeWithExprType(nodeExprType);

            // Act
            bool result = validator.Validate(node);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Validate returns false when the node's ExprType is NOT in the allowed types set.
        /// Parameterized test covering scenarios where the node type doesn't match any allowed types.
        /// </summary>
        /// <param name="allowedType">The ExprType to allow in the validator.</param>
        /// <param name="nodeExprType">The ExprType of the node being validated.</param>
        [Theory]
        [InlineData(ExprType.Value, ExprType.Property)]
        [InlineData(ExprType.Property, ExprType.Value)]
        [InlineData(ExprType.Function, ExprType.LogicBinary)]
        [InlineData(ExprType.LogicBinary, ExprType.Function)]
        public void Validate_NodeWithDisallowedExprType_ReturnsFalse(ExprType allowedType, ExprType nodeExprType)
        {
            // Arrange
            var validator = new ExprTypeValidator(allowedType);
            var node = CreateNodeWithExprType(nodeExprType);

            // Act
            bool result = validator.Validate(node);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Validate returns false when the validator has no allowed types and a non-null node is provided.
        /// This validates the edge case where the allowed types set is empty.
        /// </summary>
        [Fact]
        public void Validate_EmptyAllowedTypes_ReturnsFalse()
        {
            // Arrange
            var validator = new ExprTypeValidator();
            var node = new ValueExpr(123);

            // Act
            bool result = validator.Validate(node);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Validate correctly handles multiple allowed types.
        /// Verifies that validation succeeds when node type matches any of the allowed types.
        /// </summary>
        [Theory]
        [InlineData(ExprType.Value)]
        [InlineData(ExprType.Property)]
        public void Validate_MultipleAllowedTypes_MatchingNodeType_ReturnsTrue(ExprType nodeExprType)
        {
            // Arrange
            var validator = new ExprTypeValidator(ExprType.Value, ExprType.Property, ExprType.Function);
            var node = CreateNodeWithExprType(nodeExprType);

            // Act
            bool result = validator.Validate(node);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Validate correctly handles multiple allowed types.
        /// Verifies that validation fails when node type doesn't match any of the allowed types.
        /// </summary>
        [Fact]
        public void Validate_MultipleAllowedTypes_NonMatchingNodeType_ReturnsFalse()
        {
            // Arrange
            var validator = new ExprTypeValidator(ExprType.Value, ExprType.Property);
            var node = CreateNodeWithExprType(ExprType.Function);

            // Act
            bool result = validator.Validate(node);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Validate works correctly with the predefined Minimum validator.
        /// The Minimum validator allows a specific set of basic expression types.
        /// </summary>
        [Theory]
        [InlineData(ExprType.Value, true)]
        [InlineData(ExprType.Property, true)]
        [InlineData(ExprType.Unary, true)]
        [InlineData(ExprType.ValueSet, true)]
        [InlineData(ExprType.LogicBinary, true)]
        [InlineData(ExprType.And, true)]
        [InlineData(ExprType.Or, true)]
        [InlineData(ExprType.Not, true)]
        [InlineData(ExprType.Where, true)]
        [InlineData(ExprType.OrderBy, true)]
        [InlineData(ExprType.OrderByItem, true)]
        [InlineData(ExprType.Section, true)]
        [InlineData(ExprType.Function, false)]
        [InlineData(ExprType.Select, false)]
        public void Validate_MinimumValidator_ValidatesCorrectly(ExprType nodeExprType, bool expectedResult)
        {
            // Arrange
            var node = CreateNodeWithExprType(nodeExprType);

            // Act
            bool result = ExprTypeValidator.Minimum.Validate(node);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        /// <summary>
        /// Tests that Validate works correctly with the predefined Query validator.
        /// The Query validator allows a comprehensive set of query-related expression types.
        /// </summary>
        [Theory]
        [InlineData(ExprType.Value, true)]
        [InlineData(ExprType.Property, true)]
        [InlineData(ExprType.Function, true)]
        [InlineData(ExprType.Select, true)]
        [InlineData(ExprType.From, true)]
        [InlineData(ExprType.Where, true)]
        [InlineData(ExprType.GroupBy, true)]
        [InlineData(ExprType.OrderBy, true)]
        [InlineData(ExprType.Table, true)]
        [InlineData(ExprType.TableJoin, true)]
        [InlineData(ExprType.Update, false)]
        [InlineData(ExprType.Delete, false)]
        public void Validate_QueryValidator_ValidatesCorrectly(ExprType nodeExprType, bool expectedResult)
        {
            // Arrange
            var node = CreateNodeWithExprType(nodeExprType);

            // Act
            bool result = ExprTypeValidator.Query.Validate(node);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        /// <summary>
        /// Helper method to create a mock Expr node with a specific ExprType.
        /// Uses Moq to create abstract Expr instances with the desired ExprType.
        /// </summary>
        /// <param name="exprType">The ExprType to assign to the mocked node.</param>
        /// <returns>A mock Expr instance with the specified ExprType.</returns>
        private Expr CreateNodeWithExprType(ExprType exprType)
        {
            var mockNode = new Mock<Expr>();
            mockNode.Setup(n => n.ExprType).Returns(exprType);
            return mockNode.Object;
        }

        /// <summary>
        /// Tests that AllowedTypes returns an empty collection when no types are provided to the constructor.
        /// </summary>
        [Fact]
        public void AllowedTypes_WhenNoTypesProvided_ReturnsEmptyCollection()
        {
            // Arrange
            var validator = new ExprTypeValidator();

            // Act
            var allowedTypes = validator.AllowedTypes;

            // Assert
            Assert.NotNull(allowedTypes);
            Assert.Empty(allowedTypes);
        }

        /// <summary>
        /// Tests that AllowedTypes returns a collection containing a single type when one type is provided.
        /// </summary>
        [Fact]
        public void AllowedTypes_WhenSingleTypeProvided_ReturnsCollectionWithSingleItem()
        {
            // Arrange
            var validator = new ExprTypeValidator(ExprType.Value);

            // Act
            var allowedTypes = validator.AllowedTypes;

            // Assert
            Assert.NotNull(allowedTypes);
            Assert.Single(allowedTypes);
            Assert.Contains(ExprType.Value, allowedTypes);
        }

        /// <summary>
        /// Tests that AllowedTypes returns a collection containing all provided unique types.
        /// </summary>
        [Fact]
        public void AllowedTypes_WhenMultipleTypesProvided_ReturnsCollectionWithAllItems()
        {
            // Arrange
            var expectedTypes = new[] { ExprType.Value, ExprType.Property, ExprType.Unary };
            var validator = new ExprTypeValidator(expectedTypes);

            // Act
            var allowedTypes = validator.AllowedTypes;

            // Assert
            Assert.NotNull(allowedTypes);
            Assert.Equal(3, allowedTypes.Count);
            Assert.Contains(ExprType.Value, allowedTypes);
            Assert.Contains(ExprType.Property, allowedTypes);
            Assert.Contains(ExprType.Unary, allowedTypes);
        }

        /// <summary>
        /// Tests that AllowedTypes handles duplicate types by storing only unique values.
        /// </summary>
        [Fact]
        public void AllowedTypes_WhenDuplicateTypesProvided_ReturnsCollectionWithUniqueItems()
        {
            // Arrange
            var validator = new ExprTypeValidator(ExprType.Value, ExprType.Property, ExprType.Value, ExprType.Property);

            // Act
            var allowedTypes = validator.AllowedTypes;

            // Assert
            Assert.NotNull(allowedTypes);
            Assert.Equal(2, allowedTypes.Count);
            Assert.Contains(ExprType.Value, allowedTypes);
            Assert.Contains(ExprType.Property, allowedTypes);
        }

        /// <summary>
        /// Tests that AllowedTypes does not contain types that were not provided to the constructor.
        /// </summary>
        [Fact]
        public void AllowedTypes_WhenTypeNotProvided_DoesNotContainType()
        {
            // Arrange
            var validator = new ExprTypeValidator(ExprType.Value, ExprType.Property);

            // Act
            var allowedTypes = validator.AllowedTypes;

            // Assert
            Assert.DoesNotContain(ExprType.Select, allowedTypes);
            Assert.DoesNotContain(ExprType.Table, allowedTypes);
            Assert.DoesNotContain(ExprType.Function, allowedTypes);
        }

        /// <summary>
        /// Tests that the static Minimum validator has the correct allowed types.
        /// </summary>
        [Fact]
        public void AllowedTypes_MinimumStaticInstance_ContainsExpectedTypes()
        {
            // Arrange & Act
            var allowedTypes = ExprTypeValidator.Minimum.AllowedTypes;

            // Assert
            Assert.NotNull(allowedTypes);
            Assert.Equal(12, allowedTypes.Count);
            Assert.Contains(ExprType.Value, allowedTypes);
            Assert.Contains(ExprType.Property, allowedTypes);
            Assert.Contains(ExprType.Unary, allowedTypes);
            Assert.Contains(ExprType.ValueSet, allowedTypes);
            Assert.Contains(ExprType.LogicBinary, allowedTypes);
            Assert.Contains(ExprType.And, allowedTypes);
            Assert.Contains(ExprType.Or, allowedTypes);
            Assert.Contains(ExprType.Not, allowedTypes);
            Assert.Contains(ExprType.Where, allowedTypes);
            Assert.Contains(ExprType.OrderBy, allowedTypes);
            Assert.Contains(ExprType.OrderByItem, allowedTypes);
            Assert.Contains(ExprType.Section, allowedTypes);
        }

        /// <summary>
        /// Tests that the static Minimum validator does not contain query-specific types.
        /// </summary>
        [Fact]
        public void AllowedTypes_MinimumStaticInstance_DoesNotContainQuerySpecificTypes()
        {
            // Arrange & Act
            var allowedTypes = ExprTypeValidator.Minimum.AllowedTypes;

            // Assert
            Assert.DoesNotContain(ExprType.Select, allowedTypes);
            Assert.DoesNotContain(ExprType.From, allowedTypes);
            Assert.DoesNotContain(ExprType.Table, allowedTypes);
            Assert.DoesNotContain(ExprType.Function, allowedTypes);
        }

        /// <summary>
        /// Tests that the static Query validator has the correct allowed types.
        /// </summary>
        [Fact]
        public void AllowedTypes_QueryStaticInstance_ContainsExpectedTypes()
        {
            // Arrange & Act
            var allowedTypes = ExprTypeValidator.Query.AllowedTypes;

            // Assert
            Assert.NotNull(allowedTypes);
            Assert.Equal(20, allowedTypes.Count);
            Assert.Contains(ExprType.Value, allowedTypes);
            Assert.Contains(ExprType.Property, allowedTypes);
            Assert.Contains(ExprType.Unary, allowedTypes);
            Assert.Contains(ExprType.ValueSet, allowedTypes);
            Assert.Contains(ExprType.LogicBinary, allowedTypes);
            Assert.Contains(ExprType.And, allowedTypes);
            Assert.Contains(ExprType.Or, allowedTypes);
            Assert.Contains(ExprType.Not, allowedTypes);
            Assert.Contains(ExprType.From, allowedTypes);
            Assert.Contains(ExprType.Where, allowedTypes);
            Assert.Contains(ExprType.GroupBy, allowedTypes);
            Assert.Contains(ExprType.OrderBy, allowedTypes);
            Assert.Contains(ExprType.OrderByItem, allowedTypes);
            Assert.Contains(ExprType.Section, allowedTypes);
            Assert.Contains(ExprType.Select, allowedTypes);
            Assert.Contains(ExprType.SelectItem, allowedTypes);
            Assert.Contains(ExprType.GenericSql, allowedTypes);
            Assert.Contains(ExprType.Function, allowedTypes);
            Assert.Contains(ExprType.Table, allowedTypes);
            Assert.Contains(ExprType.TableJoin, allowedTypes);
        }

        /// <summary>
        /// Tests that the static Query validator does not contain types that are not meant for queries.
        /// </summary>
        [Fact]
        public void AllowedTypes_QueryStaticInstance_DoesNotContainNonQueryTypes()
        {
            // Arrange & Act
            var allowedTypes = ExprTypeValidator.Query.AllowedTypes;

            // Assert
            Assert.DoesNotContain(ExprType.Update, allowedTypes);
            Assert.DoesNotContain(ExprType.Delete, allowedTypes);
            Assert.DoesNotContain(ExprType.Lambda, allowedTypes);
            Assert.DoesNotContain(ExprType.Foreign, allowedTypes);
        }

        /// <summary>
        /// Tests that AllowedTypes returns a read-only collection type.
        /// </summary>
        [Fact]
        public void AllowedTypes_ReturnsReadOnlyCollection()
        {
            // Arrange
            var validator = new ExprTypeValidator(ExprType.Value, ExprType.Property);

            // Act
            var allowedTypes = validator.AllowedTypes;

            // Assert
            Assert.IsAssignableFrom<IReadOnlyCollection<ExprType>>(allowedTypes);
        }

        /// <summary>
        /// Tests that AllowedTypes can contain all possible ExprType enum values.
        /// </summary>
        [Fact]
        public void AllowedTypes_WhenAllEnumValuesProvided_ContainsAllValues()
        {
            // Arrange
            var allTypes = Enum.GetValues(typeof(ExprType)).Cast<ExprType>().ToArray();
            var validator = new ExprTypeValidator(allTypes);

            // Act
            var allowedTypes = validator.AllowedTypes;

            // Assert
            Assert.NotNull(allowedTypes);
            Assert.Equal(allTypes.Length, allowedTypes.Count);
            foreach (var type in allTypes)
            {
                Assert.Contains(type, allowedTypes);
            }
        }

        /// <summary>
        /// Verifies that the constructor initializes with an empty collection when no arguments are provided.
        /// </summary>
        [Fact]
        public void Constructor_NoArguments_InitializesEmptyCollection()
        {
            // Arrange & Act
            var validator = new ExprTypeValidator();

            // Assert
            Assert.Empty(validator.AllowedTypes);
        }

        /// <summary>
        /// Verifies that the constructor properly stores a single ExprType value.
        /// </summary>
        [Fact]
        public void Constructor_SingleType_StoresSingleType()
        {
            // Arrange & Act
            var validator = new ExprTypeValidator(ExprType.Value);

            // Assert
            Assert.Single(validator.AllowedTypes);
            Assert.Contains(ExprType.Value, validator.AllowedTypes);
        }

        /// <summary>
        /// Verifies that the constructor properly stores multiple distinct ExprType values.
        /// </summary>
        /// <param name="types">Array of ExprType values to test.</param>
        /// <param name="expectedCount">Expected number of distinct types.</param>
        [Theory]
        [MemberData(nameof(GetMultipleTypesTestData))]
        public void Constructor_MultipleTypes_StoresAllDistinctTypes(ExprType[] types, int expectedCount)
        {
            // Arrange & Act
            var validator = new ExprTypeValidator(types);

            // Assert
            Assert.Equal(expectedCount, validator.AllowedTypes.Count);
            foreach (var type in types.Distinct())
            {
                Assert.Contains(type, validator.AllowedTypes);
            }
        }

        /// <summary>
        /// Verifies that the constructor handles duplicate ExprType values correctly by storing only distinct values.
        /// </summary>
        [Fact]
        public void Constructor_DuplicateTypes_StoresOnlyDistinctTypes()
        {
            // Arrange & Act
            var validator = new ExprTypeValidator(
                ExprType.Value,
                ExprType.Property,
                ExprType.Value,
                ExprType.Property,
                ExprType.Value);

            // Assert
            Assert.Equal(2, validator.AllowedTypes.Count);
            Assert.Contains(ExprType.Value, validator.AllowedTypes);
            Assert.Contains(ExprType.Property, validator.AllowedTypes);
        }

        /// <summary>
        /// Verifies that the constructor throws ArgumentNullException when null is explicitly passed.
        /// </summary>
        [Fact]
        public void Constructor_NullArray_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ExprTypeValidator(null));
        }

        /// <summary>
        /// Verifies that the constructor accepts invalid enum values cast from integers.
        /// </summary>
        [Fact]
        public void Constructor_InvalidEnumValue_AcceptsValue()
        {
            // Arrange
            var invalidEnumValue = (ExprType)9999;

            // Act
            var validator = new ExprTypeValidator(invalidEnumValue);

            // Assert
            Assert.Single(validator.AllowedTypes);
            Assert.Contains(invalidEnumValue, validator.AllowedTypes);
        }

        /// <summary>
        /// Verifies that the constructor handles all valid ExprType enum values.
        /// </summary>
        [Fact]
        public void Constructor_AllEnumValues_StoresAllTypes()
        {
            // Arrange
            var allTypes = new[]
            {
                ExprType.Table, ExprType.TableJoin, ExprType.From, ExprType.Select,
                ExprType.SelectItem, ExprType.OrderByItem, ExprType.Function, ExprType.Foreign,
                ExprType.Lambda, ExprType.LogicBinary, ExprType.And, ExprType.Or,
                ExprType.Not, ExprType.ValueBinary, ExprType.ValueSet, ExprType.Unary,
                ExprType.Property, ExprType.Value, ExprType.GenericSql, ExprType.Update,
                ExprType.Delete, ExprType.Where, ExprType.GroupBy, ExprType.OrderBy,
                ExprType.Having, ExprType.Section
            };

            // Act
            var validator = new ExprTypeValidator(allTypes);

            // Assert
            Assert.Equal(allTypes.Length, validator.AllowedTypes.Count);
            foreach (var type in allTypes)
            {
                Assert.Contains(type, validator.AllowedTypes);
            }
        }

        /// <summary>
        /// Verifies that the constructor handles mixed valid types with duplicates.
        /// </summary>
        [Fact]
        public void Constructor_MixedTypesWithDuplicates_StoresDistinctTypes()
        {
            // Arrange & Act
            var validator = new ExprTypeValidator(
                ExprType.Select,
                ExprType.From,
                ExprType.Where,
                ExprType.Select,
                ExprType.OrderBy,
                ExprType.From);

            // Assert
            Assert.Equal(4, validator.AllowedTypes.Count);
            Assert.Contains(ExprType.Select, validator.AllowedTypes);
            Assert.Contains(ExprType.From, validator.AllowedTypes);
            Assert.Contains(ExprType.Where, validator.AllowedTypes);
            Assert.Contains(ExprType.OrderBy, validator.AllowedTypes);
        }

        /// <summary>
        /// Provides test data for multiple types scenarios.
        /// </summary>
        public static IEnumerable<object[]> GetMultipleTypesTestData()
        {
            yield return new object[]
            {
                new[] { ExprType.Value, ExprType.Property },
                2
            };

            yield return new object[]
            {
                new[] { ExprType.Select, ExprType.From, ExprType.Where, ExprType.OrderBy },
                4
            };

            yield return new object[]
            {
                new[] { ExprType.And, ExprType.Or, ExprType.Not, ExprType.LogicBinary, ExprType.ValueBinary },
                5
            };

            yield return new object[]
            {
                new[] { ExprType.Table, ExprType.TableJoin, ExprType.Function },
                3
            };

            yield return new object[]
            {
                new[] { ExprType.Value, ExprType.Value, ExprType.Property },
                2
            };
        }
    }
}

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for ExprValidatorGroup class
    /// </summary>
    public class ExprValidatorGroupTests
    {
        /// <summary>
        /// Tests that Validate returns true when the input node is null.
        /// Validates the special case where null nodes are considered valid.
        /// </summary>
        [Fact]
        public void Validate_NullNode_ReturnsTrue()
        {
            // Arrange
            var validator = new ExprValidatorGroup();

            // Act
            bool result = validator.Validate(null);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Validate returns true when no visitors are provided and node is non-null.
        /// Validates that an empty validator group accepts all nodes.
        /// </summary>
        [Fact]
        public void Validate_NonNullNodeWithNoVisitors_ReturnsTrue()
        {
            // Arrange
            var validator = new ExprValidatorGroup();
            var mockExpr = new Mock<Expr>();

            // Act
            bool result = validator.Validate(mockExpr.Object);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Validate returns true when a single visitor returns true.
        /// Validates successful validation with one passing validator.
        /// </summary>
        [Fact]
        public void Validate_NonNullNodeWithSinglePassingVisitor_ReturnsTrue()
        {
            // Arrange
            var mockVisitor = new Mock<IExprNodeVisitor>();
            var mockExpr = new Mock<Expr>();
            mockVisitor.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);
            var validator = new ExprValidatorGroup(mockVisitor.Object);

            // Act
            bool result = validator.Validate(mockExpr.Object);

            // Assert
            Assert.True(result);
            mockVisitor.Verify(v => v.Visit(mockExpr.Object), Times.Once);
        }

        /// <summary>
        /// Tests that Validate returns false when a single visitor returns false.
        /// Validates that a failing validator causes validation to fail and sets FaildedVisitor.
        /// </summary>
        [Fact]
        public void Validate_NonNullNodeWithSingleFailingVisitor_ReturnsFalseAndSetsFaildedVisitor()
        {
            // Arrange
            var mockVisitor = new Mock<IExprNodeVisitor>();
            var mockExpr = new Mock<Expr>();
            mockVisitor.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(false);
            var validator = new ExprValidatorGroup(mockVisitor.Object);

            // Act
            bool result = validator.Validate(mockExpr.Object);

            // Assert
            Assert.False(result);
            Assert.Same(mockVisitor.Object, validator.FaildedVisitor);
            mockVisitor.Verify(v => v.Visit(mockExpr.Object), Times.Once);
        }

        /// <summary>
        /// Tests that Validate returns true when all multiple visitors return true.
        /// Validates successful validation with multiple passing validators.
        /// </summary>
        [Fact]
        public void Validate_NonNullNodeWithMultiplePassingVisitors_ReturnsTrue()
        {
            // Arrange
            var mockVisitor1 = new Mock<IExprNodeVisitor>();
            var mockVisitor2 = new Mock<IExprNodeVisitor>();
            var mockVisitor3 = new Mock<IExprNodeVisitor>();
            var mockExpr = new Mock<Expr>();

            mockVisitor1.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);
            mockVisitor2.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);
            mockVisitor3.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);

            var validator = new ExprValidatorGroup(mockVisitor1.Object, mockVisitor2.Object, mockVisitor3.Object);

            // Act
            bool result = validator.Validate(mockExpr.Object);

            // Assert
            Assert.True(result);
            mockVisitor1.Verify(v => v.Visit(mockExpr.Object), Times.Once);
            mockVisitor2.Verify(v => v.Visit(mockExpr.Object), Times.Once);
            mockVisitor3.Verify(v => v.Visit(mockExpr.Object), Times.Once);
        }

        /// <summary>
        /// Tests that Validate returns false when the first visitor fails.
        /// Validates short-circuit behavior where remaining validators are not called.
        /// </summary>
        [Fact]
        public void Validate_NonNullNodeWithFirstVisitorFailing_ReturnsFalseAndShortCircuits()
        {
            // Arrange
            var mockVisitor1 = new Mock<IExprNodeVisitor>();
            var mockVisitor2 = new Mock<IExprNodeVisitor>();
            var mockVisitor3 = new Mock<IExprNodeVisitor>();
            var mockExpr = new Mock<Expr>();

            mockVisitor1.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(false);
            mockVisitor2.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);
            mockVisitor3.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);

            var validator = new ExprValidatorGroup(mockVisitor1.Object, mockVisitor2.Object, mockVisitor3.Object);

            // Act
            bool result = validator.Validate(mockExpr.Object);

            // Assert
            Assert.False(result);
            Assert.Same(mockVisitor1.Object, validator.FaildedVisitor);
            mockVisitor1.Verify(v => v.Visit(mockExpr.Object), Times.Once);
            mockVisitor2.Verify(v => v.Visit(It.IsAny<Expr>()), Times.Never);
            mockVisitor3.Verify(v => v.Visit(It.IsAny<Expr>()), Times.Never);
        }

        /// <summary>
        /// Tests that Validate returns false when a middle visitor fails.
        /// Validates short-circuit behavior and proper FaildedVisitor assignment.
        /// </summary>
        [Fact]
        public void Validate_NonNullNodeWithMiddleVisitorFailing_ReturnsFalseAndShortCircuits()
        {
            // Arrange
            var mockVisitor1 = new Mock<IExprNodeVisitor>();
            var mockVisitor2 = new Mock<IExprNodeVisitor>();
            var mockVisitor3 = new Mock<IExprNodeVisitor>();
            var mockExpr = new Mock<Expr>();

            mockVisitor1.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);
            mockVisitor2.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(false);
            mockVisitor3.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);

            var validator = new ExprValidatorGroup(mockVisitor1.Object, mockVisitor2.Object, mockVisitor3.Object);

            // Act
            bool result = validator.Validate(mockExpr.Object);

            // Assert
            Assert.False(result);
            Assert.Same(mockVisitor2.Object, validator.FaildedVisitor);
            mockVisitor1.Verify(v => v.Visit(mockExpr.Object), Times.Once);
            mockVisitor2.Verify(v => v.Visit(mockExpr.Object), Times.Once);
            mockVisitor3.Verify(v => v.Visit(It.IsAny<Expr>()), Times.Never);
        }

        /// <summary>
        /// Tests that Validate returns false when the last visitor fails.
        /// Validates that all previous validators are called and FaildedVisitor is set correctly.
        /// </summary>
        [Fact]
        public void Validate_NonNullNodeWithLastVisitorFailing_ReturnsFalseAndSetsFaildedVisitor()
        {
            // Arrange
            var mockVisitor1 = new Mock<IExprNodeVisitor>();
            var mockVisitor2 = new Mock<IExprNodeVisitor>();
            var mockVisitor3 = new Mock<IExprNodeVisitor>();
            var mockExpr = new Mock<Expr>();

            mockVisitor1.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);
            mockVisitor2.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);
            mockVisitor3.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(false);

            var validator = new ExprValidatorGroup(mockVisitor1.Object, mockVisitor2.Object, mockVisitor3.Object);

            // Act
            bool result = validator.Validate(mockExpr.Object);

            // Assert
            Assert.False(result);
            Assert.Same(mockVisitor3.Object, validator.FaildedVisitor);
            mockVisitor1.Verify(v => v.Visit(mockExpr.Object), Times.Once);
            mockVisitor2.Verify(v => v.Visit(mockExpr.Object), Times.Once);
            mockVisitor3.Verify(v => v.Visit(mockExpr.Object), Times.Once);
        }

        /// <summary>
        /// Tests that Validate handles multiple consecutive validation calls correctly.
        /// Validates that FaildedVisitor is updated properly on subsequent calls.
        /// </summary>
        [Fact]
        public void Validate_MultipleConsecutiveCalls_UpdatesFaildedVisitorCorrectly()
        {
            // Arrange
            var mockVisitor1 = new Mock<IExprNodeVisitor>();
            var mockVisitor2 = new Mock<IExprNodeVisitor>();
            var mockExpr1 = new Mock<Expr>();
            var mockExpr2 = new Mock<Expr>();

            mockVisitor1.Setup(v => v.Visit(mockExpr1.Object)).Returns(false);
            mockVisitor1.Setup(v => v.Visit(mockExpr2.Object)).Returns(true);
            mockVisitor2.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);

            var validator = new ExprValidatorGroup(mockVisitor1.Object, mockVisitor2.Object);

            // Act
            bool result1 = validator.Validate(mockExpr1.Object);
            bool result2 = validator.Validate(mockExpr2.Object);

            // Assert
            Assert.False(result1);
            Assert.Same(mockVisitor1.Object, validator.FaildedVisitor);
            Assert.True(result2);
            // FaildedVisitor should still reference the previously failed visitor
            Assert.Same(mockVisitor1.Object, validator.FaildedVisitor);
        }

        /// <summary>
        /// Tests that Validate with null node does not call any visitors.
        /// Validates early return behavior for null input.
        /// </summary>
        [Fact]
        public void Validate_NullNodeWithVisitors_DoesNotCallVisitors()
        {
            // Arrange
            var mockVisitor = new Mock<IExprNodeVisitor>();
            mockVisitor.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);
            var validator = new ExprValidatorGroup(mockVisitor.Object);

            // Act
            bool result = validator.Validate(null);

            // Assert
            Assert.True(result);
            mockVisitor.Verify(v => v.Visit(It.IsAny<Expr>()), Times.Never);
        }

        /// <summary>
        /// Tests that the constructor successfully creates an instance with no visitors.
        /// Expected: The instance is created and Validate returns true for any node.
        /// </summary>
        [Fact]
        public void Constructor_WithNoVisitors_ShouldSucceed()
        {
            // Arrange & Act
            var validatorGroup = new ExprValidatorGroup();
            var mockNode = new Mock<Expr>();

            // Assert
            Assert.NotNull(validatorGroup);
            Assert.True(validatorGroup.Validate(mockNode.Object));
        }

        /// <summary>
        /// Tests that the constructor successfully adds a single visitor.
        /// Expected: The visitor is added and called during validation.
        /// </summary>
        [Fact]
        public void Constructor_WithSingleVisitor_ShouldAddVisitor()
        {
            // Arrange
            var mockVisitor = new Mock<IExprNodeVisitor>();
            var mockNode = new Mock<Expr>();
            mockVisitor.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);

            // Act
            var validatorGroup = new ExprValidatorGroup(mockVisitor.Object);
            var result = validatorGroup.Validate(mockNode.Object);

            // Assert
            Assert.True(result);
            mockVisitor.Verify(v => v.Visit(mockNode.Object), Times.Once);
        }

        /// <summary>
        /// Tests that the constructor successfully adds multiple visitors.
        /// Expected: All visitors are added and called in order during validation.
        /// </summary>
        [Fact]
        public void Constructor_WithMultipleVisitors_ShouldAddAllVisitors()
        {
            // Arrange
            var mockVisitor1 = new Mock<IExprNodeVisitor>();
            var mockVisitor2 = new Mock<IExprNodeVisitor>();
            var mockVisitor3 = new Mock<IExprNodeVisitor>();
            var mockNode = new Mock<Expr>();

            mockVisitor1.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);
            mockVisitor2.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);
            mockVisitor3.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);

            // Act
            var validatorGroup = new ExprValidatorGroup(mockVisitor1.Object, mockVisitor2.Object, mockVisitor3.Object);
            var result = validatorGroup.Validate(mockNode.Object);

            // Assert
            Assert.True(result);
            mockVisitor1.Verify(v => v.Visit(mockNode.Object), Times.Once);
            mockVisitor2.Verify(v => v.Visit(mockNode.Object), Times.Once);
            mockVisitor3.Verify(v => v.Visit(mockNode.Object), Times.Once);
        }

        /// <summary>
        /// Tests that when a visitor fails validation, FaildedVisitor is set correctly.
        /// Expected: Validation returns false and FaildedVisitor points to the failed visitor.
        /// </summary>
        [Fact]
        public void Constructor_WithVisitorThatFails_ShouldSetFailedVisitor()
        {
            // Arrange
            var mockVisitor = new Mock<IExprNodeVisitor>();
            var mockNode = new Mock<Expr>();
            mockVisitor.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(false);

            // Act
            var validatorGroup = new ExprValidatorGroup(mockVisitor.Object);
            var result = validatorGroup.Validate(mockNode.Object);

            // Assert
            Assert.False(result);
            Assert.Same(mockVisitor.Object, validatorGroup.FaildedVisitor);
        }

        /// <summary>
        /// Tests that when the first visitor fails, subsequent visitors are not called.
        /// Expected: Validation returns false, only the first visitor is called, and FaildedVisitor is set.
        /// </summary>
        [Fact]
        public void Constructor_WithMultipleVisitors_FirstFails_ShouldStopAtFirstFailure()
        {
            // Arrange
            var mockVisitor1 = new Mock<IExprNodeVisitor>();
            var mockVisitor2 = new Mock<IExprNodeVisitor>();
            var mockVisitor3 = new Mock<IExprNodeVisitor>();
            var mockNode = new Mock<Expr>();

            mockVisitor1.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(false);
            mockVisitor2.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);
            mockVisitor3.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);

            // Act
            var validatorGroup = new ExprValidatorGroup(mockVisitor1.Object, mockVisitor2.Object, mockVisitor3.Object);
            var result = validatorGroup.Validate(mockNode.Object);

            // Assert
            Assert.False(result);
            Assert.Same(mockVisitor1.Object, validatorGroup.FaildedVisitor);
            mockVisitor1.Verify(v => v.Visit(mockNode.Object), Times.Once);
            mockVisitor2.Verify(v => v.Visit(It.IsAny<Expr>()), Times.Never);
            mockVisitor3.Verify(v => v.Visit(It.IsAny<Expr>()), Times.Never);
        }

        /// <summary>
        /// Tests that when a middle visitor fails, subsequent visitors are not called.
        /// Expected: Validation returns false, only visitors up to the failed one are called.
        /// </summary>
        [Fact]
        public void Constructor_WithMultipleVisitors_MiddleFails_ShouldStopAtFailure()
        {
            // Arrange
            var mockVisitor1 = new Mock<IExprNodeVisitor>();
            var mockVisitor2 = new Mock<IExprNodeVisitor>();
            var mockVisitor3 = new Mock<IExprNodeVisitor>();
            var mockNode = new Mock<Expr>();

            mockVisitor1.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);
            mockVisitor2.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(false);
            mockVisitor3.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);

            // Act
            var validatorGroup = new ExprValidatorGroup(mockVisitor1.Object, mockVisitor2.Object, mockVisitor3.Object);
            var result = validatorGroup.Validate(mockNode.Object);

            // Assert
            Assert.False(result);
            Assert.Same(mockVisitor2.Object, validatorGroup.FaildedVisitor);
            mockVisitor1.Verify(v => v.Visit(mockNode.Object), Times.Once);
            mockVisitor2.Verify(v => v.Visit(mockNode.Object), Times.Once);
            mockVisitor3.Verify(v => v.Visit(It.IsAny<Expr>()), Times.Never);
        }

        /// <summary>
        /// Tests that passing null as the visitors parameter throws ArgumentNullException.
        /// Expected: ArgumentNullException is thrown during construction.
        /// </summary>
        [Fact]
        public void Constructor_WithNullArray_ShouldThrowArgumentNullException()
        {
            // Arrange
            IExprNodeVisitor[]? visitors = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ExprValidatorGroup(visitors!));
        }

        /// <summary>
        /// Tests that an array containing a null element is accepted by the constructor.
        /// Expected: Constructor succeeds, but validation throws NullReferenceException when visiting the null element.
        /// </summary>
        [Fact]
        public void Constructor_WithArrayContainingNullElement_ShouldAcceptButFailOnValidate()
        {
            // Arrange
            var mockVisitor = new Mock<IExprNodeVisitor>();
            mockVisitor.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);
            IExprNodeVisitor?[] visitors = new IExprNodeVisitor?[] { mockVisitor.Object, null! };
            var mockNode = new Mock<Expr>();

            // Act
            var validatorGroup = new ExprValidatorGroup(visitors!);

            // Assert
            Assert.NotNull(validatorGroup);
            Assert.Throws<NullReferenceException>(() => validatorGroup.Validate(mockNode.Object));
        }

        /// <summary>
        /// Tests that an array containing only null elements is accepted by the constructor.
        /// Expected: Constructor succeeds, but validation throws NullReferenceException.
        /// </summary>
        [Fact]
        public void Constructor_WithArrayContainingOnlyNullElements_ShouldAcceptButFailOnValidate()
        {
            // Arrange
            IExprNodeVisitor?[] visitors = new IExprNodeVisitor?[] { null!, null! };
            var mockNode = new Mock<Expr>();

            // Act
            var validatorGroup = new ExprValidatorGroup(visitors!);

            // Assert
            Assert.NotNull(validatorGroup);
            Assert.Throws<NullReferenceException>(() => validatorGroup.Validate(mockNode.Object));
        }

        /// <summary>
        /// Tests that validation with null node returns true when no visitors are present.
        /// Expected: Returns true (base case with null node).
        /// </summary>
        [Fact]
        public void Constructor_WithNoVisitors_ValidateNullNode_ShouldReturnTrue()
        {
            // Arrange
            var validatorGroup = new ExprValidatorGroup();

            // Act
            var result = validatorGroup.Validate(null!);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that validation with null node returns true even with visitors present.
        /// Expected: Returns true without calling any visitors (null check happens before iteration).
        /// </summary>
        [Fact]
        public void Constructor_WithVisitors_ValidateNullNode_ShouldReturnTrueWithoutCallingVisitors()
        {
            // Arrange
            var mockVisitor = new Mock<IExprNodeVisitor>();
            mockVisitor.Setup(v => v.Visit(It.IsAny<Expr>())).Returns(true);

            // Act
            var validatorGroup = new ExprValidatorGroup(mockVisitor.Object);
            var result = validatorGroup.Validate(null!);

            // Assert
            Assert.True(result);
            mockVisitor.Verify(v => v.Visit(It.IsAny<Expr>()), Times.Never);
        }
    }

    /// <summary>
    /// Unit tests for ExprValidator.Visit method
    /// </summary>
    public class ExprValidatorTests
    {
        /// <summary>
        /// Tests that Visit returns true when Validate returns true,
        /// and FailedExpr is not set.
        /// </summary>
        [Fact]
        public void Visit_ValidateReturnsTrue_ReturnsTrue()
        {
            // Arrange
            var mockValidator = new Mock<ExprValidator>();
            var mockNode = new Mock<Expr>();
            mockValidator.Setup(v => v.Validate(mockNode.Object)).Returns(true);

            // Act
            bool result = mockValidator.Object.Visit(mockNode.Object);

            // Assert
            Assert.True(result);
            Assert.Null(mockValidator.Object.FailedExpr);
        }

        /// <summary>
        /// Tests that Visit returns false when Validate returns false,
        /// and sets FailedExpr to the node that failed validation.
        /// </summary>
        [Fact]
        public void Visit_ValidateReturnsFalse_ReturnsFalseAndSetsFailedExpr()
        {
            // Arrange
            var mockValidator = new Mock<ExprValidator>();
            var mockNode = new Mock<Expr>();
            mockValidator.Setup(v => v.Validate(mockNode.Object)).Returns(false);
            mockValidator.CallBase = true;

            // Act
            bool result = mockValidator.Object.Visit(mockNode.Object);

            // Assert
            Assert.False(result);
            Assert.Same(mockNode.Object, mockValidator.Object.FailedExpr);
        }

        /// <summary>
        /// Tests that Visit correctly handles multiple calls with different validation results,
        /// ensuring FailedExpr is updated only when validation fails.
        /// </summary>
        [Fact]
        public void Visit_MultipleCallsWithDifferentResults_UpdatesFailedExprCorrectly()
        {
            // Arrange
            var mockValidator = new Mock<ExprValidator>();
            var firstNode = new Mock<Expr>();
            var secondNode = new Mock<Expr>();
            mockValidator.Setup(v => v.Validate(firstNode.Object)).Returns(false);
            mockValidator.Setup(v => v.Validate(secondNode.Object)).Returns(true);
            mockValidator.CallBase = true;

            // Act
            bool firstResult = mockValidator.Object.Visit(firstNode.Object);
            bool secondResult = mockValidator.Object.Visit(secondNode.Object);

            // Assert
            Assert.False(firstResult);
            Assert.True(secondResult);
            Assert.Same(firstNode.Object, mockValidator.Object.FailedExpr);
        }

        /// <summary>
        /// Tests that Visit correctly overwrites FailedExpr when multiple validations fail,
        /// storing the most recent failed node.
        /// </summary>
        [Fact]
        public void Visit_ConsecutiveFailures_OverwritesFailedExpr()
        {
            // Arrange
            var mockValidator = new Mock<ExprValidator>();
            var firstNode = new Mock<Expr>();
            var secondNode = new Mock<Expr>();
            mockValidator.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(false);
            mockValidator.CallBase = true;

            // Act
            mockValidator.Object.Visit(firstNode.Object);
            mockValidator.Object.Visit(secondNode.Object);

            // Assert
            Assert.Same(secondNode.Object, mockValidator.Object.FailedExpr);
        }

        /// <summary>
        /// Tests that Visit invokes the Validate method exactly once with the provided node.
        /// </summary>
        [Fact]
        public void Visit_InvokesValidateOnce()
        {
            // Arrange
            var mockValidator = new Mock<ExprValidator>();
            var mockNode = new Mock<Expr>();
            mockValidator.Setup(v => v.Validate(mockNode.Object)).Returns(true);

            // Act
            mockValidator.Object.Visit(mockNode.Object);

            // Assert
            mockValidator.Verify(v => v.Validate(mockNode.Object), Times.Once);
        }

        /// <summary>
        /// Tests that Visit passes null node to Validate when null is provided,
        /// and returns based on Validate's return value.
        /// </summary>
        [Fact]
        public void Visit_WithNullNode_PassesNullToValidate()
        {
            // Arrange
            var mockValidator = new Mock<ExprValidator>();
            mockValidator.Setup(v => v.Validate(null)).Returns(true);

            // Act
            bool result = mockValidator.Object.Visit(null);

            // Assert
            Assert.True(result);
            mockValidator.Verify(v => v.Validate(null), Times.Once);
        }

        /// <summary>
        /// Tests that Visit sets FailedExpr to null when null node fails validation.
        /// </summary>
        [Fact]
        public void Visit_WithNullNodeValidationFails_SetsFailedExprToNull()
        {
            // Arrange
            var mockValidator = new Mock<ExprValidator>();
            mockValidator.Setup(v => v.Validate(null)).Returns(false);
            mockValidator.CallBase = true;

            // Act
            bool result = mockValidator.Object.Visit(null);

            // Assert
            Assert.False(result);
            Assert.Null(mockValidator.Object.FailedExpr);
        }
    }
}