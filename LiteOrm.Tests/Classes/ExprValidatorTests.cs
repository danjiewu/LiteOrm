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
            bool result = ExprValidator.CreateMinimum().Validate(node);

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
            bool result = ExprValidator.CreateQueryOnly().Validate(node);

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
            var allowedTypes = ExprValidator.CreateMinimum().AllowedTypes;

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
            var allowedTypes = ExprValidator.CreateMinimum().AllowedTypes;

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
            var allowedTypes = ExprValidator.CreateQueryOnly().AllowedTypes;

            // Assert
            Assert.NotNull(allowedTypes);
            Assert.Equal(21, allowedTypes.Count);
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
            var allowedTypes = ExprValidator.CreateQueryOnly().AllowedTypes;

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
        [Fact]
        public void Validate_NullNode_ReturnsTrue()
        {
            var validator = new ExprValidatorGroup();
            bool result = validator.Validate(null);
            Assert.True(result);
        }

        [Fact]
        public void Validate_NonNullNodeWithNoValidators_ReturnsTrue()
        {
            var validator = new ExprValidatorGroup();
            var mockExpr = new Mock<Expr>();
            bool result = validator.Validate(mockExpr.Object);
            Assert.True(result);
        }

        [Fact]
        public void Validate_NonNullNodeWithSinglePassingValidator_ReturnsTrue()
        {
            var mockValidator = new Mock<ExprValidator>();
            var mockExpr = new Mock<Expr>();
            mockValidator.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);
            var validator = new ExprValidatorGroup(mockValidator.Object);

            bool result = validator.Validate(mockExpr.Object);

            Assert.True(result);
            mockValidator.Verify(v => v.Validate(mockExpr.Object), Times.Once);
        }

        [Fact]
        public void Validate_NonNullNodeWithSingleFailingValidator_ReturnsFalseAndSetsFailedValidator()
        {
            var mockValidator = new Mock<ExprValidator>();
            var mockExpr = new Mock<Expr>();
            mockValidator.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(false);
            var validator = new ExprValidatorGroup(mockValidator.Object);

            bool result = validator.Validate(mockExpr.Object);

            Assert.False(result);
            Assert.Same(mockValidator.Object, validator.FailedValidator);
            mockValidator.Verify(v => v.Validate(mockExpr.Object), Times.Once);
        }

        [Fact]
        public void Validate_NonNullNodeWithMultiplePassingValidators_ReturnsTrue()
        {
            var mockValidator1 = new Mock<ExprValidator>();
            var mockValidator2 = new Mock<ExprValidator>();
            var mockValidator3 = new Mock<ExprValidator>();
            var mockExpr = new Mock<Expr>();

            mockValidator1.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);
            mockValidator2.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);
            mockValidator3.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);

            var validator = new ExprValidatorGroup(mockValidator1.Object, mockValidator2.Object, mockValidator3.Object);

            bool result = validator.Validate(mockExpr.Object);

            Assert.True(result);
            mockValidator1.Verify(v => v.Validate(mockExpr.Object), Times.Once);
            mockValidator2.Verify(v => v.Validate(mockExpr.Object), Times.Once);
            mockValidator3.Verify(v => v.Validate(mockExpr.Object), Times.Once);
        }

        [Fact]
        public void Validate_NonNullNodeWithFirstValidatorFailing_ReturnsFalseAndShortCircuits()
        {
            var mockValidator1 = new Mock<ExprValidator>();
            var mockValidator2 = new Mock<ExprValidator>();
            var mockValidator3 = new Mock<ExprValidator>();
            var mockExpr = new Mock<Expr>();

            mockValidator1.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(false);
            mockValidator2.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);
            mockValidator3.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);

            var validator = new ExprValidatorGroup(mockValidator1.Object, mockValidator2.Object, mockValidator3.Object);

            bool result = validator.Validate(mockExpr.Object);

            Assert.False(result);
            Assert.Same(mockValidator1.Object, validator.FailedValidator);
            mockValidator1.Verify(v => v.Validate(mockExpr.Object), Times.Once);
            mockValidator2.Verify(v => v.Validate(It.IsAny<Expr>()), Times.Never);
            mockValidator3.Verify(v => v.Validate(It.IsAny<Expr>()), Times.Never);
        }

        [Fact]
        public void Validate_NonNullNodeWithMiddleValidatorFailing_ReturnsFalseAndShortCircuits()
        {
            var mockValidator1 = new Mock<ExprValidator>();
            var mockValidator2 = new Mock<ExprValidator>();
            var mockValidator3 = new Mock<ExprValidator>();
            var mockExpr = new Mock<Expr>();

            mockValidator1.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);
            mockValidator2.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(false);
            mockValidator3.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);

            var validator = new ExprValidatorGroup(mockValidator1.Object, mockValidator2.Object, mockValidator3.Object);

            bool result = validator.Validate(mockExpr.Object);

            Assert.False(result);
            Assert.Same(mockValidator2.Object, validator.FailedValidator);
            mockValidator1.Verify(v => v.Validate(mockExpr.Object), Times.Once);
            mockValidator2.Verify(v => v.Validate(mockExpr.Object), Times.Once);
            mockValidator3.Verify(v => v.Validate(It.IsAny<Expr>()), Times.Never);
        }

        [Fact]
        public void Validate_NonNullNodeWithLastValidatorFailing_ReturnsFalseAndSetsFailedValidator()
        {
            var mockValidator1 = new Mock<ExprValidator>();
            var mockValidator2 = new Mock<ExprValidator>();
            var mockValidator3 = new Mock<ExprValidator>();
            var mockExpr = new Mock<Expr>();

            mockValidator1.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);
            mockValidator2.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);
            mockValidator3.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(false);

            var validator = new ExprValidatorGroup(mockValidator1.Object, mockValidator2.Object, mockValidator3.Object);

            bool result = validator.Validate(mockExpr.Object);

            Assert.False(result);
            Assert.Same(mockValidator3.Object, validator.FailedValidator);
            mockValidator1.Verify(v => v.Validate(mockExpr.Object), Times.Once);
            mockValidator2.Verify(v => v.Validate(mockExpr.Object), Times.Once);
            mockValidator3.Verify(v => v.Validate(mockExpr.Object), Times.Once);
        }

        [Fact]
        public void Validate_MultipleConsecutiveCalls_UpdatesFailedValidatorCorrectly()
        {
            var mockValidator1 = new Mock<ExprValidator>();
            var mockValidator2 = new Mock<ExprValidator>();
            var mockExpr1 = new Mock<Expr>();
            var mockExpr2 = new Mock<Expr>();

            mockValidator1.Setup(v => v.Validate(mockExpr1.Object)).Returns(false);
            mockValidator1.Setup(v => v.Validate(mockExpr2.Object)).Returns(true);
            mockValidator2.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);

            var validator = new ExprValidatorGroup(mockValidator1.Object, mockValidator2.Object);

            bool result1 = validator.Validate(mockExpr1.Object);
            bool result2 = validator.Validate(mockExpr2.Object);

            Assert.False(result1);
            Assert.Same(mockValidator1.Object, validator.FailedValidator);
            Assert.True(result2);
            Assert.Same(mockValidator1.Object, validator.FailedValidator);
        }

        [Fact]
        public void Validate_NullNodeWithValidators_DoesNotCallValidators()
        {
            var mockValidator = new Mock<ExprValidator>();
            mockValidator.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);
            var validator = new ExprValidatorGroup(mockValidator.Object);

            bool result = validator.Validate(null);

            Assert.True(result);
            mockValidator.Verify(v => v.Validate(It.IsAny<Expr>()), Times.Never);
        }

        [Fact]
        public void Constructor_WithNoValidators_ShouldSucceed()
        {
            var validatorGroup = new ExprValidatorGroup();
            var mockNode = new Mock<Expr>();

            Assert.NotNull(validatorGroup);
            Assert.True(validatorGroup.Validate(mockNode.Object));
        }

        [Fact]
        public void Constructor_WithSingleValidator_ShouldAddValidator()
        {
            var mockValidator = new Mock<ExprValidator>();
            var mockNode = new Mock<Expr>();
            mockValidator.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);

            var validatorGroup = new ExprValidatorGroup(mockValidator.Object);
            var result = validatorGroup.Validate(mockNode.Object);

            Assert.True(result);
            mockValidator.Verify(v => v.Validate(mockNode.Object), Times.Once);
        }

        [Fact]
        public void Constructor_WithMultipleValidators_ShouldAddAllValidators()
        {
            var mockValidator1 = new Mock<ExprValidator>();
            var mockValidator2 = new Mock<ExprValidator>();
            var mockValidator3 = new Mock<ExprValidator>();
            var mockNode = new Mock<Expr>();

            mockValidator1.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);
            mockValidator2.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);
            mockValidator3.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);

            var validatorGroup = new ExprValidatorGroup(mockValidator1.Object, mockValidator2.Object, mockValidator3.Object);
            var result = validatorGroup.Validate(mockNode.Object);

            Assert.True(result);
            mockValidator1.Verify(v => v.Validate(mockNode.Object), Times.Once);
            mockValidator2.Verify(v => v.Validate(mockNode.Object), Times.Once);
            mockValidator3.Verify(v => v.Validate(mockNode.Object), Times.Once);
        }

        [Fact]
        public void Constructor_WithValidatorThatFails_ShouldSetFailedValidator()
        {
            var mockValidator = new Mock<ExprValidator>();
            var mockNode = new Mock<Expr>();
            mockValidator.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(false);

            var validatorGroup = new ExprValidatorGroup(mockValidator.Object);
            var result = validatorGroup.Validate(mockNode.Object);

            Assert.False(result);
            Assert.Same(mockValidator.Object, validatorGroup.FailedValidator);
        }

        [Fact]
        public void Constructor_WithMultipleValidators_FirstFails_ShouldStopAtFirstFailure()
        {
            var mockValidator1 = new Mock<ExprValidator>();
            var mockValidator2 = new Mock<ExprValidator>();
            var mockValidator3 = new Mock<ExprValidator>();
            var mockNode = new Mock<Expr>();

            mockValidator1.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(false);
            mockValidator2.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);
            mockValidator3.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);

            var validatorGroup = new ExprValidatorGroup(mockValidator1.Object, mockValidator2.Object, mockValidator3.Object);
            var result = validatorGroup.Validate(mockNode.Object);

            Assert.False(result);
            Assert.Same(mockValidator1.Object, validatorGroup.FailedValidator);
            mockValidator1.Verify(v => v.Validate(mockNode.Object), Times.Once);
            mockValidator2.Verify(v => v.Validate(It.IsAny<Expr>()), Times.Never);
            mockValidator3.Verify(v => v.Validate(It.IsAny<Expr>()), Times.Never);
        }

        [Fact]
        public void Constructor_WithMultipleValidators_MiddleFails_ShouldStopAtFailure()
        {
            var mockValidator1 = new Mock<ExprValidator>();
            var mockValidator2 = new Mock<ExprValidator>();
            var mockValidator3 = new Mock<ExprValidator>();
            var mockNode = new Mock<Expr>();

            mockValidator1.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);
            mockValidator2.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(false);
            mockValidator3.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);

            var validatorGroup = new ExprValidatorGroup(mockValidator1.Object, mockValidator2.Object, mockValidator3.Object);
            var result = validatorGroup.Validate(mockNode.Object);

            Assert.False(result);
            Assert.Same(mockValidator2.Object, validatorGroup.FailedValidator);
            mockValidator1.Verify(v => v.Validate(mockNode.Object), Times.Once);
            mockValidator2.Verify(v => v.Validate(mockNode.Object), Times.Once);
            mockValidator3.Verify(v => v.Validate(It.IsAny<Expr>()), Times.Never);
        }

        [Fact]
        public void Constructor_WithNullArray_ShouldThrowArgumentNullException()
        {
            ExprValidator[]? validators = null;
            Assert.Throws<ArgumentNullException>(() => new ExprValidatorGroup(validators!));
        }

        [Fact]
        public void Constructor_WithArrayContainingNullElement_ShouldAcceptButFailOnValidate()
        {
            var mockValidator = new Mock<ExprValidator>();
            mockValidator.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);
            ExprValidator?[] validators = new ExprValidator?[] { mockValidator.Object, null! };
            var mockNode = new Mock<Expr>();

            var validatorGroup = new ExprValidatorGroup(validators!);

            Assert.NotNull(validatorGroup);
            Assert.Throws<NullReferenceException>(() => validatorGroup.Validate(mockNode.Object));
        }

        [Fact]
        public void Constructor_WithArrayContainingOnlyNullElements_ShouldAcceptButFailOnValidate()
        {
            ExprValidator?[] validators = new ExprValidator?[] { null!, null! };
            var mockNode = new Mock<Expr>();

            var validatorGroup = new ExprValidatorGroup(validators!);

            Assert.NotNull(validatorGroup);
            Assert.Throws<NullReferenceException>(() => validatorGroup.Validate(mockNode.Object));
        }

        [Fact]
        public void Constructor_WithNoValidators_ValidateNullNode_ShouldReturnTrue()
        {
            var validatorGroup = new ExprValidatorGroup();
            var result = validatorGroup.Validate(null!);
            Assert.True(result);
        }

        [Fact]
        public void Constructor_WithValidators_ValidateNullNode_ShouldReturnTrueWithoutCallingValidators()
        {
            var mockValidator = new Mock<ExprValidator>();
            mockValidator.Setup(v => v.Validate(It.IsAny<Expr>())).Returns(true);

            var validatorGroup = new ExprValidatorGroup(mockValidator.Object);
            var result = validatorGroup.Validate(null!);

            Assert.True(result);
            mockValidator.Verify(v => v.Validate(It.IsAny<Expr>()), Times.Never);
        }
    }

    /// <summary>
    /// Unit tests for ExprValidator and its interaction with ExprVisitor.Validate
    /// </summary>
    public class ExprValidatorVisitTests
    {
        [Fact]
        public void VisitAll_ValidateReturnsTrue_ReturnsTrue()
        {
            var mockValidator = new Mock<ExprValidator>();
            var mockNode = new Mock<Expr>();
            mockValidator.Setup(v => v.Validate(mockNode.Object)).Returns(true);
            mockValidator.CallBase = false;

            bool result = ExprVisitor.Validate(mockValidator.Object, mockNode.Object);

            Assert.True(result);
            Assert.Null(mockValidator.Object.FailedExpr);
        }

        [Fact]
        public void VisitAll_ValidateReturnsFalse_ReturnsFalseAndSetsFailedExpr()
        {
            var mockValidator = new Mock<ExprValidator>();
            var mockNode = new Mock<Expr>();
            mockValidator.Setup(v => v.Validate(mockNode.Object)).Returns(false);

            bool result = ExprVisitor.Validate(mockValidator.Object, mockNode.Object);

            Assert.False(result);
            Assert.Same(mockNode.Object, mockValidator.Object.FailedExpr);
        }

        [Fact]
        public void Validate_MultipleCallsWithDifferentResults_NoFailedExprPersists()
        {
            var validator = new ExprTypeValidator(ExprType.Value);
            Assert.True(validator.Validate(new ValueExpr(123)));
            Assert.Null(validator.FailedExpr);
            Assert.False(validator.Validate(new PropertyExpr("Name")));
            Assert.Null(validator.FailedExpr);
        }

        [Fact]
        public void VisitAll_InvokesValidateOnce()
        {
            var mockValidator = new Mock<ExprValidator>();
            var mockNode = new Mock<Expr>();
            mockValidator.Setup(v => v.Validate(mockNode.Object)).Returns(true);

            ExprVisitor.Validate(mockValidator.Object, mockNode.Object);

            mockValidator.Verify(v => v.Validate(mockNode.Object), Times.Once);
        }

        [Fact]
        public void VisitAll_WithNullRoot_ReturnsTrue()
        {
            var mockValidator = new Mock<ExprValidator>();
            mockValidator.Setup(v => v.Validate(null)).Returns(true);

            bool result = ExprVisitor.Validate(mockValidator.Object, null);

            Assert.True(result);
            mockValidator.Verify(v => v.Validate(It.IsAny<Expr>()), Times.Never);
        }
    }
}
