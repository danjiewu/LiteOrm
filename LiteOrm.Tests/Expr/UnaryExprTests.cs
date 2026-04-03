using System;

using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the UnaryExpr class Equals method.
    /// </summary>
    public sealed partial class UnaryExprTests
    {
        /// <summary>
        /// Tests that Equals returns true when comparing the same reference.
        /// </summary>
        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            // Arrange
            var expr = new UnaryExpr(UnaryOperator.Nagive, Expr.Prop("Score"));

            // Act
            var result = expr.Equals(expr);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing instances with equal Operator and Operand values.
        /// </summary>
        [Theory]
        [InlineData(UnaryOperator.Nagive)]
        [InlineData(UnaryOperator.BitwiseNot)]
        public void Equals_EqualOperatorAndOperand_ReturnsTrue(UnaryOperator op)
        {
            // Arrange
            var expr1 = new UnaryExpr(op, Expr.Prop("Value"));
            var expr2 = new UnaryExpr(op, Expr.Prop("Value"));

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing instances with different Operator values.
        /// </summary>
        [Fact]
        public void Equals_DifferentOperator_ReturnsFalse()
        {
            // Arrange
            var expr1 = new UnaryExpr(UnaryOperator.Nagive, Expr.Prop("Value"));
            var expr2 = new UnaryExpr(UnaryOperator.BitwiseNot, Expr.Prop("Value"));

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing instances with different Operand values.
        /// </summary>
        [Fact]
        public void Equals_DifferentOperand_ReturnsFalse()
        {
            // Arrange
            var expr1 = new UnaryExpr(UnaryOperator.Nagive, Expr.Prop("Score"));
            var expr2 = new UnaryExpr(UnaryOperator.Nagive, Expr.Prop("Value"));

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing instances with both different Operator and Operand.
        /// </summary>
        [Fact]
        public void Equals_DifferentOperatorAndOperand_ReturnsFalse()
        {
            // Arrange
            var expr1 = new UnaryExpr(UnaryOperator.Nagive, Expr.Prop("Score"));
            var expr2 = new UnaryExpr(UnaryOperator.BitwiseNot, Expr.Prop("Value"));

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with null.
        /// </summary>
        [Fact]
        public void Equals_NullObject_ReturnsFalse()
        {
            // Arrange
            var expr = new UnaryExpr(UnaryOperator.Nagive, Expr.Prop("Score"));

            // Act
            var result = expr.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with a different type.
        /// </summary>
        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            // Arrange
            var expr = new UnaryExpr(UnaryOperator.Nagive, Expr.Prop("Score"));
            var differentType = new object();

            // Act
            var result = expr.Equals(differentType);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with a string object.
        /// </summary>
        [Fact]
        public void Equals_StringType_ReturnsFalse()
        {
            // Arrange
            var expr = new UnaryExpr(UnaryOperator.Nagive, Expr.Prop("Score"));
            var stringObj = "not a UnaryExpr";

            // Act
            var result = expr.Equals(stringObj);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null Operand and same Operator.
        /// </summary>
        [Theory]
        [InlineData(UnaryOperator.Nagive)]
        [InlineData(UnaryOperator.BitwiseNot)]
        public void Equals_BothOperandsNullSameOperator_ReturnsTrue(UnaryOperator op)
        {
            // Arrange
            var expr1 = new UnaryExpr(op, null);
            var expr2 = new UnaryExpr(op, null);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one Operand is null and the other is not.
        /// </summary>
        [Fact]
        public void Equals_OneOperandNullOtherNot_ReturnsFalse()
        {
            // Arrange
            var expr1 = new UnaryExpr(UnaryOperator.Nagive, Expr.Prop("Score"));
            var expr2 = new UnaryExpr(UnaryOperator.Nagive, null);

            // Act
            var result1 = expr1.Equals(expr2);
            var result2 = expr2.Equals(expr1);

            // Assert
            Assert.False(result1);
            Assert.False(result2);
        }

        /// <summary>
        /// Tests that Equals returns false when both Operands are null but Operators differ.
        /// </summary>
        [Fact]
        public void Equals_BothOperandsNullDifferentOperator_ReturnsFalse()
        {
            // Arrange
            var expr1 = new UnaryExpr(UnaryOperator.Nagive, null);
            var expr2 = new UnaryExpr(UnaryOperator.BitwiseNot, null);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals works correctly with default constructed instances.
        /// </summary>
        [Fact]
        public void Equals_DefaultConstructedInstances_ReturnsTrue()
        {
            // Arrange
            var expr1 = new UnaryExpr();
            var expr2 = new UnaryExpr();

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing instances with nested UnaryExpr operands.
        /// </summary>
        [Fact]
        public void Equals_NestedUnaryExprOperands_ReturnsTrue()
        {
            // Arrange
            var innerExpr1 = new UnaryExpr(UnaryOperator.Nagive, Expr.Prop("Value"));
            var innerExpr2 = new UnaryExpr(UnaryOperator.Nagive, Expr.Prop("Value"));
            var expr1 = new UnaryExpr(UnaryOperator.BitwiseNot, innerExpr1);
            var expr2 = new UnaryExpr(UnaryOperator.BitwiseNot, innerExpr2);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing instances with different nested UnaryExpr operands.
        /// </summary>
        [Fact]
        public void Equals_DifferentNestedUnaryExprOperands_ReturnsFalse()
        {
            // Arrange
            var innerExpr1 = new UnaryExpr(UnaryOperator.Nagive, Expr.Prop("Value1"));
            var innerExpr2 = new UnaryExpr(UnaryOperator.Nagive, Expr.Prop("Value2"));
            var expr1 = new UnaryExpr(UnaryOperator.BitwiseNot, innerExpr1);
            var expr2 = new UnaryExpr(UnaryOperator.BitwiseNot, innerExpr2);

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Verifies that GetHashCode returns equal hash codes for equal UnaryExpr objects.
        /// Tests the contract that equal objects must have equal hash codes.
        /// </summary>
        [Fact]
        public void GetHashCode_EqualObjects_ReturnsEqualHashCodes()
        {
            // Arrange
            var operand1 = new UnaryExpr(UnaryOperator.Nagive, null);
            var expr1 = new UnaryExpr(UnaryOperator.BitwiseNot, operand1);
            var operand2 = new UnaryExpr(UnaryOperator.Nagive, null);
            var expr2 = new UnaryExpr(UnaryOperator.BitwiseNot, operand2);

            // Act
            var hash1 = expr1.GetHashCode();
            var hash2 = expr2.GetHashCode();

            // Assert
            Assert.True(expr1.Equals(expr2));
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Verifies that GetHashCode returns consistent hash codes when called multiple times on the same object.
        /// Tests the consistency requirement of GetHashCode.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsConsistentValue()
        {
            // Arrange
            var operand = new UnaryExpr(UnaryOperator.Nagive, null);
            var expr = new UnaryExpr(UnaryOperator.BitwiseNot, operand);

            // Act
            var hash1 = expr.GetHashCode();
            var hash2 = expr.GetHashCode();
            var hash3 = expr.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
            Assert.Equal(hash2, hash3);
        }

        /// <summary>
        /// Verifies that GetHashCode handles null Operand correctly and returns a valid hash code.
        /// Tests edge case where Operand is null.
        /// </summary>
        [Theory]
        [InlineData(UnaryOperator.Nagive)]
        [InlineData(UnaryOperator.BitwiseNot)]
        public void GetHashCode_NullOperand_ReturnsValidHashCode(UnaryOperator op)
        {
            // Arrange
            var expr = new UnaryExpr(op, null);

            // Act
            var hash = expr.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Verifies that GetHashCode produces different hash codes for UnaryExpr objects with different operators.
        /// Tests that the Operator property affects the hash code calculation.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentOperators_ProducesDifferentHashCodes()
        {
            // Arrange
            var operand = new UnaryExpr(UnaryOperator.Nagive, null);
            var expr1 = new UnaryExpr(UnaryOperator.Nagive, operand);
            var expr2 = new UnaryExpr(UnaryOperator.BitwiseNot, operand);

            // Act
            var hash1 = expr1.GetHashCode();
            var hash2 = expr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Verifies that GetHashCode produces different hash codes for UnaryExpr objects with different operands.
        /// Tests that the Operand property affects the hash code calculation.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentOperands_ProducesDifferentHashCodes()
        {
            // Arrange
            var operand1 = new UnaryExpr(UnaryOperator.Nagive, null);
            var operand2 = new UnaryExpr(UnaryOperator.BitwiseNot, null);
            var expr1 = new UnaryExpr(UnaryOperator.Nagive, operand1);
            var expr2 = new UnaryExpr(UnaryOperator.Nagive, operand2);

            // Act
            var hash1 = expr1.GetHashCode();
            var hash2 = expr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Verifies that GetHashCode produces different hash codes when only the Operand differs (null vs non-null).
        /// Tests edge case comparing null and non-null operands.
        /// </summary>
        [Fact]
        public void GetHashCode_NullVsNonNullOperand_ProducesDifferentHashCodes()
        {
            // Arrange
            var expr1 = new UnaryExpr(UnaryOperator.Nagive, null);
            var expr2 = new UnaryExpr(UnaryOperator.Nagive, new UnaryExpr(UnaryOperator.BitwiseNot, null));

            // Act
            var hash1 = expr1.GetHashCode();
            var hash2 = expr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Verifies that GetHashCode works correctly with nested UnaryExpr as operands.
        /// Tests complex scenario with deeply nested expressions.
        /// </summary>
        [Fact]
        public void GetHashCode_NestedUnaryExpressions_ReturnsValidHashCode()
        {
            // Arrange
            var innerMost = new UnaryExpr(UnaryOperator.Nagive, null);
            var middle = new UnaryExpr(UnaryOperator.BitwiseNot, innerMost);
            var outer = new UnaryExpr(UnaryOperator.Nagive, middle);

            // Act
            var hash = outer.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Verifies that GetHashCode maintains equality contract with nested equal expressions.
        /// Tests that equal nested expressions produce equal hash codes.
        /// </summary>
        [Fact]
        public void GetHashCode_EqualNestedExpressions_ReturnsEqualHashCodes()
        {
            // Arrange
            var inner1 = new UnaryExpr(UnaryOperator.Nagive, null);
            var outer1 = new UnaryExpr(UnaryOperator.BitwiseNot, inner1);

            var inner2 = new UnaryExpr(UnaryOperator.Nagive, null);
            var outer2 = new UnaryExpr(UnaryOperator.BitwiseNot, inner2);

            // Act
            var hash1 = outer1.GetHashCode();
            var hash2 = outer2.GetHashCode();

            // Assert
            Assert.True(outer1.Equals(outer2));
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Verifies that GetHashCode works correctly when properties are set via setters instead of constructor.
        /// Tests that hash code calculation works regardless of initialization method.
        /// </summary>
        [Fact]
        public void GetHashCode_PropertiesSetViaSetters_ReturnsValidHashCode()
        {
            // Arrange
            var expr = new UnaryExpr
            {
                Operator = UnaryOperator.BitwiseNot,
                Operand = new UnaryExpr(UnaryOperator.Nagive, null)
            };

            // Act
            var hash = expr.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Verifies that GetHashCode for default constructed UnaryExpr (with default properties) returns a valid hash code.
        /// Tests edge case with default/uninitialized state.
        /// </summary>
        [Fact]
        public void GetHashCode_DefaultConstructedObject_ReturnsValidHashCode()
        {
            // Arrange
            var expr = new UnaryExpr();

            // Act
            var hash = expr.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates a valid UnaryExpr instance.
        /// Input: None (default constructor).
        /// Expected: UnaryExpr instance is created successfully.
        /// </summary>
        [Fact]
        public void UnaryExpr_DefaultConstructor_CreatesInstance()
        {
            // Arrange & Act
            var expr = new UnaryExpr();

            // Assert
            Assert.NotNull(expr);
            Assert.IsType<UnaryExpr>(expr);
        }

        /// <summary>
        /// Tests that the parameterless constructor initializes properties to default values.
        /// Input: None (default constructor).
        /// Expected: Operator is default enum value (0), Operand is null.
        /// </summary>
        [Fact]
        public void UnaryExpr_DefaultConstructor_InitializesPropertiesToDefaults()
        {
            // Arrange & Act
            var expr = new UnaryExpr();

            // Assert
            Assert.Equal((UnaryOperator)0, expr.Operator);
            Assert.Null(expr.Operand);
        }

        /// <summary>
        /// Tests that ExprType property returns Unary for default constructed instance.
        /// Input: Default UnaryExpr instance.
        /// Expected: ExprType is ExprType.Unary.
        /// </summary>
        [Fact]
        public void UnaryExpr_DefaultConstructor_ExprTypeIsUnary()
        {
            // Arrange
            var expr = new UnaryExpr();

            // Act
            var exprType = expr.ExprType;

            // Assert
            Assert.Equal(ExprType.Unary, exprType);
        }

        /// <summary>
        /// Tests ToString method with default constructed instance having null Operand.
        /// Input: Default UnaryExpr instance with null Operand.
        /// Expected: ToString returns string representation based on default Operator.
        /// </summary>
        [Fact]
        public void UnaryExpr_DefaultConstructor_ToStringHandlesNullOperand()
        {
            // Arrange
            var expr = new UnaryExpr();

            // Act
            var result = expr.ToString();

            // Assert
            Assert.NotNull(result);
        }

        /// <summary>
        /// Tests Equals method with two default constructed instances.
        /// Input: Two UnaryExpr instances created with default constructor.
        /// Expected: Both instances are equal.
        /// </summary>
        [Fact]
        public void UnaryExpr_DefaultConstructor_EqualsReturnsTrueForTwoDefaultInstances()
        {
            // Arrange
            var expr1 = new UnaryExpr();
            var expr2 = new UnaryExpr();

            // Act
            var result = expr1.Equals(expr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests GetHashCode method with two default constructed instances.
        /// Input: Two UnaryExpr instances created with default constructor.
        /// Expected: Both instances return the same hash code.
        /// </summary>
        [Fact]
        public void UnaryExpr_DefaultConstructor_GetHashCodeIsConsistentForDefaultInstances()
        {
            // Arrange
            var expr1 = new UnaryExpr();
            var expr2 = new UnaryExpr();

            // Act
            var hash1 = expr1.GetHashCode();
            var hash2 = expr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests Clone method with default constructed instance having null Operand.
        /// Input: Default UnaryExpr instance with null Operand.
        /// Expected: Clone returns a new UnaryExpr instance with same property values.
        /// </summary>
        [Fact]
        public void UnaryExpr_DefaultConstructor_CloneHandlesNullOperand()
        {
            // Arrange
            var expr = new UnaryExpr();

            // Act
            var cloned = expr.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.IsType<UnaryExpr>(cloned);
            Assert.NotSame(expr, cloned);

            var clonedUnary = (UnaryExpr)cloned;
            Assert.Equal(expr.Operator, clonedUnary.Operator);
            Assert.Null(clonedUnary.Operand);
        }

        /// <summary>
        /// Tests that default constructed instance does not equal null.
        /// Input: Default UnaryExpr instance compared to null.
        /// Expected: Equals returns false.
        /// </summary>
        [Fact]
        public void UnaryExpr_DefaultConstructor_EqualsReturnsFalseForNull()
        {
            // Arrange
            var expr = new UnaryExpr();

            // Act
            var result = expr.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that default constructed instance does not equal an object of different type.
        /// Input: Default UnaryExpr instance compared to a different object type.
        /// Expected: Equals returns false.
        /// </summary>
        [Fact]
        public void UnaryExpr_DefaultConstructor_EqualsReturnsFalseForDifferentType()
        {
            // Arrange
            var expr = new UnaryExpr();
            var other = new object();

            // Act
            var result = expr.Equals(other);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that the ExprType property returns ExprType.Unary for a UnaryExpr instance created with the default constructor.
        /// </summary>
        [Fact]
        public void ExprType_DefaultConstructor_ReturnsUnary()
        {
            // Arrange
            var unaryExpr = new UnaryExpr();

            // Act
            var result = unaryExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Unary, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns ExprType.Unary for a UnaryExpr instance created with the parameterized constructor.
        /// </summary>
        /// <param name="operator">The unary operator to test.</param>
        [Theory]
        [InlineData(UnaryOperator.Nagive)]
        [InlineData(UnaryOperator.BitwiseNot)]
        public void ExprType_ParameterizedConstructor_ReturnsUnary(UnaryOperator @operator)
        {
            // Arrange
            var mockOperand = new Mock<ValueTypeExpr>();
            var unaryExpr = new UnaryExpr(@operator, mockOperand.Object);

            // Act
            var result = unaryExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Unary, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns ExprType.Unary when operand is null.
        /// </summary>
        [Fact]
        public void ExprType_NullOperand_ReturnsUnary()
        {
            // Arrange
            var unaryExpr = new UnaryExpr(UnaryOperator.Nagive, null);

            // Act
            var result = unaryExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Unary, result);
        }

        /// <summary>
        /// Tests that Clone creates a new instance with the same Operator when Operand is null.
        /// Verifies that the cloned instance is a separate object with null Operand.
        /// </summary>
        [Theory]
        [InlineData(UnaryOperator.Nagive)]
        [InlineData(UnaryOperator.BitwiseNot)]
        public void Clone_WithNullOperand_CreatesNewInstanceWithNullOperand(UnaryOperator operatorType)
        {
            // Arrange
            var original = new UnaryExpr(operatorType, null);

            // Act
            var clone = original.Clone();

            // Assert
            Assert.NotNull(clone);
            Assert.IsType<UnaryExpr>(clone);
            Assert.NotSame(original, clone);

            var clonedUnary = (UnaryExpr)clone;
            Assert.Equal(original.Operator, clonedUnary.Operator);
            Assert.Null(clonedUnary.Operand);
        }

        /// <summary>
        /// Tests that Clone creates a deep copy with non-null Operand.
        /// Verifies that the cloned Operand is a separate instance with equal value.
        /// </summary>
        [Theory]
        [InlineData(UnaryOperator.Nagive)]
        [InlineData(UnaryOperator.BitwiseNot)]
        public void Clone_WithNonNullOperand_CreatesDeepCopy(UnaryOperator operatorType)
        {
            // Arrange
            var operand = Expr.Prop("TestProperty");
            var original = new UnaryExpr(operatorType, operand);

            // Act
            var clone = original.Clone();

            // Assert
            Assert.NotNull(clone);
            Assert.IsType<UnaryExpr>(clone);
            Assert.NotSame(original, clone);

            var clonedUnary = (UnaryExpr)clone;
            Assert.Equal(original.Operator, clonedUnary.Operator);
            Assert.NotNull(clonedUnary.Operand);
            Assert.NotSame(original.Operand, clonedUnary.Operand);
            Assert.Equal(original.Operand, clonedUnary.Operand);
        }

        /// <summary>
        /// Tests that Clone creates a truly independent copy.
        /// Verifies that modifications to the original do not affect the clone.
        /// </summary>
        [Fact]
        public void Clone_ModifyingOriginal_DoesNotAffectClone()
        {
            // Arrange
            var operand = Expr.Prop("OriginalProperty");
            var original = new UnaryExpr(UnaryOperator.Nagive, operand);
            var clone = (UnaryExpr)original.Clone();
            var originalOperand = original.Operand;

            // Act
            original.Operand = Expr.Prop("ModifiedProperty");

            // Assert
            Assert.NotEqual(original.Operand, clone.Operand);
            Assert.Equal(originalOperand, clone.Operand);
        }

        /// <summary>
        /// Tests that Clone creates a truly independent copy.
        /// Verifies that modifications to the clone do not affect the original.
        /// </summary>
        [Fact]
        public void Clone_ModifyingClone_DoesNotAffectOriginal()
        {
            // Arrange
            var operand = Expr.Prop("OriginalProperty");
            var original = new UnaryExpr(UnaryOperator.Nagive, operand);
            var clone = (UnaryExpr)original.Clone();
            var originalOperand = original.Operand;

            // Act
            clone.Operand = Expr.Prop("ModifiedProperty");

            // Assert
            Assert.NotEqual(original.Operand, clone.Operand);
            Assert.Equal(originalOperand, original.Operand);
        }

        /// <summary>
        /// Tests that Clone performs deep cloning with nested UnaryExpr.
        /// Verifies that nested expressions are also cloned, not just referenced.
        /// </summary>
        [Fact]
        public void Clone_WithNestedUnaryExpr_CreatesDeepCopyOfNestedStructure()
        {
            // Arrange
            var innerOperand = Expr.Prop("InnerProperty");
            var innerUnary = new UnaryExpr(UnaryOperator.BitwiseNot, innerOperand);
            var outerUnary = new UnaryExpr(UnaryOperator.Nagive, innerUnary);

            // Act
            var clone = (UnaryExpr)outerUnary.Clone();

            // Assert
            Assert.NotSame(outerUnary, clone);
            Assert.Equal(outerUnary.Operator, clone.Operator);
            Assert.NotNull(clone.Operand);
            Assert.NotSame(outerUnary.Operand, clone.Operand);

            var clonedInner = (UnaryExpr)clone.Operand;
            var originalInner = (UnaryExpr)outerUnary.Operand;
            Assert.Equal(originalInner.Operator, clonedInner.Operator);
            Assert.NotSame(originalInner.Operand, clonedInner.Operand);
            Assert.Equal(originalInner.Operand, clonedInner.Operand);
        }

        /// <summary>
        /// Tests that Clone preserves Operator property correctly.
        /// Verifies that modifying the clone's Operator does not affect the original.
        /// </summary>
        [Fact]
        public void Clone_ModifyingCloneOperator_DoesNotAffectOriginal()
        {
            // Arrange
            var operand = Expr.Prop("TestProperty");
            var original = new UnaryExpr(UnaryOperator.Nagive, operand);
            var clone = (UnaryExpr)original.Clone();

            // Act
            clone.Operator = UnaryOperator.BitwiseNot;

            // Assert
            Assert.Equal(UnaryOperator.Nagive, original.Operator);
            Assert.Equal(UnaryOperator.BitwiseNot, clone.Operator);
        }

        /// <summary>
        /// Tests that Clone returns base type Expr.
        /// Verifies the return type is correctly assignable to Expr.
        /// </summary>
        [Fact]
        public void Clone_ReturnsExprType_CanBeAssignedToExpr()
        {
            // Arrange
            var operand = Expr.Prop("TestProperty");
            var original = new UnaryExpr(UnaryOperator.Nagive, operand);

            // Act
            Expr clone = original.Clone();

            // Assert
            Assert.NotNull(clone);
            Assert.IsType<UnaryExpr>(clone);
            Assert.IsAssignableFrom<Expr>(clone);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes a UnaryExpr with Nagive operator and valid operand.
        /// Input: UnaryOperator.Nagive and a valid ValueTypeExpr operand.
        /// Expected: Properties are correctly assigned.
        /// </summary>
        [Fact]
        public void Constructor_WithNagiveOperatorAndValidOperand_SetsPropertiesCorrectly()
        {
            // Arrange
            UnaryOperator expectedOperator = UnaryOperator.Nagive;
            ValueTypeExpr expectedOperand = 42;

            // Act
            var unaryExpr = new UnaryExpr(expectedOperator, expectedOperand);

            // Assert
            Assert.Equal(expectedOperator, unaryExpr.Operator);
            Assert.Same(expectedOperand, unaryExpr.Operand);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes a UnaryExpr with BitwiseNot operator and valid operand.
        /// Input: UnaryOperator.BitwiseNot and a valid ValueTypeExpr operand.
        /// Expected: Properties are correctly assigned.
        /// </summary>
        [Fact]
        public void Constructor_WithBitwiseNotOperatorAndValidOperand_SetsPropertiesCorrectly()
        {
            // Arrange
            UnaryOperator expectedOperator = UnaryOperator.BitwiseNot;
            ValueTypeExpr expectedOperand = "test";

            // Act
            var unaryExpr = new UnaryExpr(expectedOperator, expectedOperand);

            // Assert
            Assert.Equal(expectedOperator, unaryExpr.Operator);
            Assert.Same(expectedOperand, unaryExpr.Operand);
        }

        /// <summary>
        /// Tests that the constructor correctly initializes a UnaryExpr with Distinct operator and valid operand.
        /// Input: UnaryOperator.Distinct and a valid ValueTypeExpr operand.
        /// Expected: Properties are correctly assigned.
        /// </summary>
        [Fact]
        public void Constructor_WithDistinctOperatorAndValidOperand_SetsPropertiesCorrectly()
        {
            // Arrange
            UnaryOperator expectedOperator = UnaryOperator.Distinct;
            ValueTypeExpr expectedOperand = true;

            // Act
            var unaryExpr = new UnaryExpr(expectedOperator, expectedOperand);

            // Assert
            Assert.Equal(expectedOperator, unaryExpr.Operator);
            Assert.Same(expectedOperand, unaryExpr.Operand);
        }

        /// <summary>
        /// Tests that the constructor accepts an undefined enum value for UnaryOperator.
        /// Input: An undefined UnaryOperator value (99) and a valid operand.
        /// Expected: Properties are set without throwing an exception.
        /// </summary>
        [Fact]
        public void Constructor_WithUndefinedEnumValue_SetsPropertiesWithoutException()
        {
            // Arrange
            UnaryOperator undefinedOperator = (UnaryOperator)99;
            ValueTypeExpr expectedOperand = 123;

            // Act
            var unaryExpr = new UnaryExpr(undefinedOperator, expectedOperand);

            // Assert
            Assert.Equal(undefinedOperator, unaryExpr.Operator);
            Assert.Same(expectedOperand, unaryExpr.Operand);
        }

        /// <summary>
        /// Tests that the constructor accepts null as the operand parameter.
        /// Input: UnaryOperator.Nagive and null operand.
        /// Expected: Operator is set correctly and Operand is null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullOperand_SetsOperandToNull()
        {
            // Arrange
            UnaryOperator expectedOperator = UnaryOperator.Nagive;
            ValueTypeExpr? nullOperand = null;

            // Act
            var unaryExpr = new UnaryExpr(expectedOperator, nullOperand);

            // Assert
            Assert.Equal(expectedOperator, unaryExpr.Operator);
            Assert.Null(unaryExpr.Operand);
        }

        /// <summary>
        /// Tests that the constructor works with different ValueTypeExpr implicit conversions.
        /// Input: Various UnaryOperator values with different ValueTypeExpr types (int, long, bool, string, DateTime, double).
        /// Expected: All properties are correctly assigned.
        /// </summary>
        [Theory]
        [InlineData(UnaryOperator.Nagive)]
        [InlineData(UnaryOperator.BitwiseNot)]
        [InlineData(UnaryOperator.Distinct)]
        public void Constructor_WithDifferentOperandTypes_SetsPropertiesCorrectly(UnaryOperator oper)
        {
            // Arrange & Act & Assert - int operand
            ValueTypeExpr intOperand = int.MaxValue;
            var expr1 = new UnaryExpr(oper, intOperand);
            Assert.Equal(oper, expr1.Operator);
            Assert.Same(intOperand, expr1.Operand);

            // Arrange & Act & Assert - long operand
            ValueTypeExpr longOperand = long.MinValue;
            var expr2 = new UnaryExpr(oper, longOperand);
            Assert.Equal(oper, expr2.Operator);
            Assert.Same(longOperand, expr2.Operand);

            // Arrange & Act & Assert - bool operand
            ValueTypeExpr boolOperand = false;
            var expr3 = new UnaryExpr(oper, boolOperand);
            Assert.Equal(oper, expr3.Operator);
            Assert.Same(boolOperand, expr3.Operand);

            // Arrange & Act & Assert - string operand
            ValueTypeExpr stringOperand = "test value";
            var expr4 = new UnaryExpr(oper, stringOperand);
            Assert.Equal(oper, expr4.Operator);
            Assert.Same(stringOperand, expr4.Operand);

            // Arrange & Act & Assert - DateTime operand
            ValueTypeExpr dateOperand = DateTime.MinValue;
            var expr5 = new UnaryExpr(oper, dateOperand);
            Assert.Equal(oper, expr5.Operator);
            Assert.Same(dateOperand, expr5.Operand);
        }

        /// <summary>
        /// Tests that the constructor handles boundary enum values.
        /// Input: Minimum and maximum defined enum values (Nagive=0, Distinct=2).
        /// Expected: Properties are correctly assigned without exceptions.
        /// </summary>
        [Theory]
        [InlineData((UnaryOperator)0)] // Nagive
        [InlineData((UnaryOperator)1)] // BitwiseNot
        [InlineData((UnaryOperator)2)] // Distinct
        public void Constructor_WithBoundaryEnumValues_SetsPropertiesCorrectly(UnaryOperator oper)
        {
            // Arrange
            ValueTypeExpr operand = 100;

            // Act
            var unaryExpr = new UnaryExpr(oper, operand);

            // Assert
            Assert.Equal(oper, unaryExpr.Operator);
            Assert.Same(operand, unaryExpr.Operand);
        }

        /// <summary>
        /// Tests that the constructor handles extreme negative and positive enum values.
        /// Input: int.MinValue and int.MaxValue cast to UnaryOperator.
        /// Expected: Properties are set without throwing an exception.
        /// </summary>
        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(int.MaxValue)]
        [InlineData(-1)]
        [InlineData(3)]
        [InlineData(100)]
        public void Constructor_WithExtremeEnumValues_SetsPropertiesWithoutException(int operatorValue)
        {
            // Arrange
            UnaryOperator extremeOperator = (UnaryOperator)operatorValue;
            ValueTypeExpr operand = 50;

            // Act
            var unaryExpr = new UnaryExpr(extremeOperator, operand);

            // Assert
            Assert.Equal(extremeOperator, unaryExpr.Operator);
            Assert.Same(operand, unaryExpr.Operand);
        }
    }
}

namespace LiteOrm.Tests.Common
{
    /// <summary>
    /// Unit tests for UnaryExpr.ToString method
    /// </summary>
    public sealed class UnaryExprToStringTests
    {
        /// <summary>
        /// Tests that ToString returns the correct format with Nagive operator.
        /// Input: Operator = Nagive, Operand = PropertyExpr
        /// Expected: String starting with "-" followed by operand's string representation
        /// </summary>
        [Fact]
        public void ToString_WithNagiveOperator_ReturnsMinusPrefixWithOperand()
        {
            // Arrange
            var operand = new PropertyExpr("TestProperty");
            var unaryExpr = new UnaryExpr(UnaryOperator.Nagive, operand);

            // Act
            var result = unaryExpr.ToString();

            // Assert
            Assert.Equal("-TestProperty", result);
        }

        /// <summary>
        /// Tests that ToString returns the correct format with BitwiseNot operator.
        /// Input: Operator = BitwiseNot, Operand = PropertyExpr
        /// Expected: String starting with "~" followed by operand's string representation
        /// </summary>
        [Fact]
        public void ToString_WithBitwiseNotOperator_ReturnsTildePrefixWithOperand()
        {
            // Arrange
            var operand = new PropertyExpr("TestProperty");
            var unaryExpr = new UnaryExpr(UnaryOperator.BitwiseNot, operand);

            // Act
            var result = unaryExpr.ToString();

            // Assert
            Assert.Equal("~TestProperty", result);
        }

        /// <summary>
        /// Tests that ToString returns the correct format with Distinct operator.
        /// Input: Operator = Distinct, Operand = PropertyExpr
        /// Expected: String starting with "~" (as per current implementation, Distinct is treated as non-Nagive)
        /// </summary>
        [Fact]
        public void ToString_WithDistinctOperator_ReturnsTildePrefixWithOperand()
        {
            // Arrange
            var operand = new PropertyExpr("TestProperty");
            var unaryExpr = new UnaryExpr(UnaryOperator.Distinct, operand);

            // Act
            var result = unaryExpr.ToString();

            // Assert
            Assert.Equal("~TestProperty", result);
        }

        /// <summary>
        /// Tests that ToString handles null operand correctly with Nagive operator.
        /// Input: Operator = Nagive, Operand = null
        /// Expected: String "-" (operator symbol with empty operand representation)
        /// </summary>
        [Fact]
        public void ToString_WithNullOperandAndNagiveOperator_ReturnsMinusSymbolOnly()
        {
            // Arrange
            var unaryExpr = new UnaryExpr { Operator = UnaryOperator.Nagive, Operand = null };

            // Act
            var result = unaryExpr.ToString();

            // Assert
            Assert.Equal("-", result);
        }

        /// <summary>
        /// Tests that ToString handles null operand correctly with BitwiseNot operator.
        /// Input: Operator = BitwiseNot, Operand = null
        /// Expected: String "~" (operator symbol with empty operand representation)
        /// </summary>
        [Fact]
        public void ToString_WithNullOperandAndBitwiseNotOperator_ReturnsTildeSymbolOnly()
        {
            // Arrange
            var unaryExpr = new UnaryExpr { Operator = UnaryOperator.BitwiseNot, Operand = null };

            // Act
            var result = unaryExpr.ToString();

            // Assert
            Assert.Equal("~", result);
        }

        /// <summary>
        /// Tests that ToString correctly includes nested unary expression in output.
        /// Input: Operator = Nagive, Operand = nested UnaryExpr
        /// Expected: String with correct nesting representation
        /// </summary>
        [Fact]
        public void ToString_WithNestedUnaryExpr_ReturnsNestedFormat()
        {
            // Arrange
            var innerOperand = new PropertyExpr("Value");
            var innerUnary = new UnaryExpr(UnaryOperator.BitwiseNot, innerOperand);
            var outerUnary = new UnaryExpr(UnaryOperator.Nagive, innerUnary);

            // Act
            var result = outerUnary.ToString();

            // Assert
            Assert.Equal("-~Value", result);
        }

        /// <summary>
        /// Tests that ToString with default constructor produces expected output.
        /// Input: Default UnaryExpr (Operator = default, Operand = null)
        /// Expected: String "~" (default operator is 0 which is Nagive, but comparing with Nagive uses ==)
        /// </summary>
        [Fact]
        public void ToString_WithDefaultConstructor_ReturnsMinusSymbolOnly()
        {
            // Arrange
            var unaryExpr = new UnaryExpr();

            // Act
            var result = unaryExpr.ToString();

            // Assert
            Assert.Equal("-", result);
        }

        /// <summary>
        /// Tests that ToString correctly handles complex operand expressions.
        /// Input: Operator = Nagive, Operand = ConstantExpr
        /// Expected: String with constant value prefixed by operator
        /// </summary>
        [Fact]
        public void ToString_WithConstantOperand_ReturnsFormattedString()
        {
            // Arrange
            var operand = new ConstantExpr(42);
            var unaryExpr = new UnaryExpr(UnaryOperator.Nagive, operand);

            // Act
            var result = unaryExpr.ToString();

            // Assert
            Assert.Equal("-42", result);
        }

        /// <summary>
        /// Tests that ToString correctly handles BitwiseNot with ConstantExpr operand.
        /// Input: Operator = BitwiseNot, Operand = ConstantExpr
        /// Expected: String with constant value prefixed by tilde
        /// </summary>
        [Fact]
        public void ToString_WithBitwiseNotAndConstantOperand_ReturnsFormattedString()
        {
            // Arrange
            var operand = new ConstantExpr(42);
            var unaryExpr = new UnaryExpr(UnaryOperator.BitwiseNot, operand);

            // Act
            var result = unaryExpr.ToString();

            // Assert
            Assert.Equal("~42", result);
        }
    }
}