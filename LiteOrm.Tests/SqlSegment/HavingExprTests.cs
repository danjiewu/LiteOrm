using System;

using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Tests for the HavingExpr class ToString method.
    /// </summary>
    public partial class HavingExprTests
    {
        /// <summary>
        /// Tests that ToString returns the correct format with various combinations of Source and Having values.
        /// </summary>
        /// <param name="source">The source SQL segment.</param>
        /// <param name="having">The having logic expression.</param>
        /// <param name="expectedFormat">The expected string format.</param>
        [Theory]
        [MemberData(nameof(ToStringTestCases))]
        public void ToString_VariousSourceAndHavingCombinations_ReturnsExpectedFormat(SqlSegment? source, LogicExpr? having, string expectedFormat)
        {
            // Arrange
            var havingExpr = new HavingExpr
            {
                Source = source,
                Having = having
            };

            // Act
            var result = havingExpr.ToString();

            // Assert
            Assert.Equal(expectedFormat, result);
        }

        /// <summary>
        /// Provides test cases for ToString method with various Source and Having combinations.
        /// </summary>
        public static TheoryData<SqlSegment?, LogicExpr?, string> ToStringTestCases()
        {
            var fromExpr = new FromExpr(typeof(object));
            var groupByExpr = new GroupByExpr(fromExpr, Expr.Prop("Id"));
            var logicExpr = Expr.Prop("Count") > 5;

            return new TheoryData<SqlSegment?, LogicExpr?, string>
            {
                // Valid Source and Having
                { groupByExpr, logicExpr, $"{groupByExpr} HAVING {logicExpr}" },
                
                // Null Source with valid Having
                { null, logicExpr, $" HAVING {logicExpr}" },
                
                // Valid Source with null Having
                { groupByExpr, null, $"{groupByExpr} HAVING " },
                
                // Both null
                { null, null, " HAVING " }
            };
        }

        /// <summary>
        /// Tests that GetHashCode returns consistent values for the same object when called multiple times.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsSameValue()
        {
            // Arrange
            var from = new FromExpr(typeof(object));
            var groupBy = new GroupByExpr(from, Expr.Prop("TestProp"));
            var havingExpr = new HavingExpr(groupBy, Expr.Prop("Id").Count() > 5);

            // Act
            var hashCode1 = havingExpr.GetHashCode();
            var hashCode2 = havingExpr.GetHashCode();
            var hashCode3 = havingExpr.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
            Assert.Equal(hashCode2, hashCode3);
        }

        /// <summary>
        /// Tests that GetHashCode returns the same hash code for equal HavingExpr objects.
        /// This validates the correlation between Equals and GetHashCode.
        /// </summary>
        [Fact]
        public void GetHashCode_EqualObjects_ReturnsSameHashCode()
        {
            // Arrange
            var from = new FromExpr(typeof(object));
            var groupBy = new GroupByExpr(from, Expr.Prop("DeptId"));
            var havingExpr1 = new HavingExpr(groupBy, Expr.Prop("Id").Count() > 5);
            var havingExpr2 = new HavingExpr(groupBy, Expr.Prop("Id").Count() > 5);

            // Act
            var hashCode1 = havingExpr1.GetHashCode();
            var hashCode2 = havingExpr2.GetHashCode();

            // Assert
            Assert.True(havingExpr1.Equals(havingExpr2));
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes for HavingExpr objects with different Having conditions.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentHavingConditions_ReturnsDifferentHashCodes()
        {
            // Arrange
            var from = new FromExpr(typeof(object));
            var groupBy = new GroupByExpr(from, Expr.Prop("DeptId"));
            var havingExpr1 = new HavingExpr(groupBy, Expr.Prop("Id").Count() > 5);
            var havingExpr2 = new HavingExpr(groupBy, Expr.Prop("Id").Count() > 10);

            // Act
            var hashCode1 = havingExpr1.GetHashCode();
            var hashCode2 = havingExpr2.GetHashCode();

            // Assert
            Assert.False(havingExpr1.Equals(havingExpr2));
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes for HavingExpr objects with different Source segments.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentSourceSegments_ReturnsDifferentHashCodes()
        {
            // Arrange
            var from1 = new FromExpr(typeof(object));
            var groupBy1 = new GroupByExpr(from1, Expr.Prop("DeptId"));
            var from2 = new FromExpr(typeof(string));
            var groupBy2 = new GroupByExpr(from2, Expr.Prop("DeptId"));
            var havingCondition = Expr.Prop("Id").Count() > 5;
            var havingExpr1 = new HavingExpr(groupBy1, havingCondition);
            var havingExpr2 = new HavingExpr(groupBy2, havingCondition);

            // Act
            var hashCode1 = havingExpr1.GetHashCode();
            var hashCode2 = havingExpr2.GetHashCode();

            // Assert
            Assert.False(havingExpr1.Equals(havingExpr2));
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests GetHashCode with various combinations of null and non-null Source and Having properties.
        /// Validates that the method handles null properties correctly.
        /// </summary>
        /// <param name="sourceIsNull">Whether the Source property should be null</param>
        /// <param name="havingIsNull">Whether the Having property should be null</param>
        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void GetHashCode_VariousNullCombinations_ReturnsValidHashCode(bool sourceIsNull, bool havingIsNull)
        {
            // Arrange
            var from = new FromExpr(typeof(object));
            var groupBy = new GroupByExpr(from, Expr.Prop("DeptId"));
            var havingExpr = new HavingExpr
            {
                Source = sourceIsNull ? null : groupBy,
                Having = havingIsNull ? null : Expr.Prop("Id").Count() > 5
            };

            // Act
            var hashCode = havingExpr.GetHashCode();

            // Assert - Just verify it doesn't throw and returns a value
            Assert.IsType<int>(hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns the same hash code for two HavingExpr instances with both Source and Having set to null.
        /// </summary>
        [Fact]
        public void GetHashCode_BothPropertiesNull_ReturnsSameHashCodeForEqualInstances()
        {
            // Arrange
            var havingExpr1 = new HavingExpr();
            var havingExpr2 = new HavingExpr();

            // Act
            var hashCode1 = havingExpr1.GetHashCode();
            var hashCode2 = havingExpr2.GetHashCode();

            // Assert
            Assert.True(havingExpr1.Equals(havingExpr2));
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode handles the case where only the Source property is null.
        /// </summary>
        [Fact]
        public void GetHashCode_SourceNullHavingNotNull_ReturnsValidHashCode()
        {
            // Arrange
            var havingExpr1 = new HavingExpr(null, Expr.Prop("Id").Count() > 5);
            var havingExpr2 = new HavingExpr(null, Expr.Prop("Id").Count() > 5);

            // Act
            var hashCode1 = havingExpr1.GetHashCode();
            var hashCode2 = havingExpr2.GetHashCode();

            // Assert
            Assert.True(havingExpr1.Equals(havingExpr2));
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode handles the case where only the Having property is null.
        /// </summary>
        [Fact]
        public void GetHashCode_HavingNullSourceNotNull_ReturnsValidHashCode()
        {
            // Arrange
            var from = new FromExpr(typeof(object));
            var groupBy = new GroupByExpr(from, Expr.Prop("DeptId"));
            var havingExpr1 = new HavingExpr(groupBy, null);
            var havingExpr2 = new HavingExpr(groupBy, null);

            // Act
            var hashCode1 = havingExpr1.GetHashCode();
            var hashCode2 = havingExpr2.GetHashCode();

            // Assert
            Assert.True(havingExpr1.Equals(havingExpr2));
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode produces different hash codes when Having conditions differ while Source is the same.
        /// </summary>
        [Fact]
        public void GetHashCode_SameSourceDifferentHaving_ProducesDifferentHashCodes()
        {
            // Arrange
            var from = new FromExpr(typeof(object));
            var groupBy = new GroupByExpr(from, Expr.Prop("DeptId"));
            var havingExpr1 = new HavingExpr { Source = groupBy, Having = Expr.Prop("Id").Count() > 5 };
            var havingExpr2 = new HavingExpr { Source = groupBy, Having = Expr.Prop("Name").Count() > 3 };

            // Act
            var hashCode1 = havingExpr1.GetHashCode();
            var hashCode2 = havingExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing an instance to itself.
        /// </summary>
        [Fact]
        public void Equals_SameInstance_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr(typeof(int));
            var having = Expr.Prop("Id").Count() > 5;
            var havingExpr = new HavingExpr(source, having);

            // Act
            var result = havingExpr.Equals(havingExpr);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing two instances with equal Source and Having properties.
        /// </summary>
        [Fact]
        public void Equals_EqualInstances_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr(typeof(int));
            var having = Expr.Prop("Id").Count() > 5;
            var havingExpr1 = new HavingExpr(source, having);
            var havingExpr2 = new HavingExpr(source, having);

            // Act
            var result = havingExpr1.Equals(havingExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with null.
        /// </summary>
        [Fact]
        public void Equals_NullObject_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr(typeof(int));
            var having = Expr.Prop("Id").Count() > 5;
            var havingExpr = new HavingExpr(source, having);

            // Act
            var result = havingExpr.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with an object of a different type.
        /// </summary>
        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr(typeof(int));
            var having = Expr.Prop("Id").Count() > 5;
            var havingExpr = new HavingExpr(source, having);
            var differentType = new FromExpr(typeof(int));

            // Act
            var result = havingExpr.Equals(differentType);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when Source properties differ.
        /// </summary>
        [Fact]
        public void Equals_DifferentSource_ReturnsFalse()
        {
            // Arrange
            var source1 = new FromExpr(typeof(int));
            var source2 = new FromExpr(typeof(string));
            var having = Expr.Prop("Id").Count() > 5;
            var havingExpr1 = new HavingExpr(source1, having);
            var havingExpr2 = new HavingExpr(source2, having);

            // Act
            var result = havingExpr1.Equals(havingExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when Having properties differ.
        /// </summary>
        [Fact]
        public void Equals_DifferentHaving_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr(typeof(int));
            var having1 = Expr.Prop("Id").Count() > 5;
            var having2 = Expr.Prop("Id").Count() > 10;
            var havingExpr1 = new HavingExpr(source, having1);
            var havingExpr2 = new HavingExpr(source, having2);

            // Act
            var result = havingExpr1.Equals(havingExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when both Source and Having properties differ.
        /// </summary>
        [Fact]
        public void Equals_DifferentSourceAndHaving_ReturnsFalse()
        {
            // Arrange
            var source1 = new FromExpr(typeof(int));
            var source2 = new FromExpr(typeof(string));
            var having1 = Expr.Prop("Id").Count() > 5;
            var having2 = Expr.Prop("Id").Count() > 10;
            var havingExpr1 = new HavingExpr(source1, having1);
            var havingExpr2 = new HavingExpr(source2, having2);

            // Act
            var result = havingExpr1.Equals(havingExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null Source and equal Having.
        /// </summary>
        [Fact]
        public void Equals_BothSourceNull_SameHaving_ReturnsTrue()
        {
            // Arrange
            var having = Expr.Prop("Id").Count() > 5;
            var havingExpr1 = new HavingExpr(null, having);
            var havingExpr2 = new HavingExpr(null, having);

            // Act
            var result = havingExpr1.Equals(havingExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have equal Source and null Having.
        /// </summary>
        [Fact]
        public void Equals_BothHavingNull_SameSource_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr(typeof(int));
            var havingExpr1 = new HavingExpr(source, null);
            var havingExpr2 = new HavingExpr(source, null);

            // Act
            var result = havingExpr1.Equals(havingExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null Source and null Having.
        /// </summary>
        [Fact]
        public void Equals_BothSourceAndHavingNull_ReturnsTrue()
        {
            // Arrange
            var havingExpr1 = new HavingExpr(null, null);
            var havingExpr2 = new HavingExpr(null, null);

            // Act
            var result = havingExpr1.Equals(havingExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one instance has null Source and the other doesn't.
        /// </summary>
        [Fact]
        public void Equals_OneSourceNull_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr(typeof(int));
            var having = Expr.Prop("Id").Count() > 5;
            var havingExpr1 = new HavingExpr(null, having);
            var havingExpr2 = new HavingExpr(source, having);

            // Act
            var result = havingExpr1.Equals(havingExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one instance has null Having and the other doesn't.
        /// </summary>
        [Fact]
        public void Equals_OneHavingNull_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr(typeof(int));
            var having = Expr.Prop("Id").Count() > 5;
            var havingExpr1 = new HavingExpr(source, null);
            var havingExpr2 = new HavingExpr(source, having);

            // Act
            var result = havingExpr1.Equals(havingExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing instances with complex nested Source chains.
        /// </summary>
        [Fact]
        public void Equals_ComplexNestedSource_ReturnsTrue()
        {
            // Arrange
            var from = new FromExpr(typeof(int));
            var groupBy = new GroupByExpr(from, Expr.Prop("DeptId"));
            var having = Expr.Prop("Id").Count() > 5;
            var havingExpr1 = new HavingExpr(groupBy, having);
            var havingExpr2 = new HavingExpr(groupBy, having);

            // Act
            var result = havingExpr1.Equals(havingExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing instances with different complex nested Source chains.
        /// </summary>
        [Fact]
        public void Equals_DifferentComplexNestedSource_ReturnsFalse()
        {
            // Arrange
            var from1 = new FromExpr(typeof(int));
            var from2 = new FromExpr(typeof(string));
            var groupBy1 = new GroupByExpr(from1, Expr.Prop("DeptId"));
            var groupBy2 = new GroupByExpr(from2, Expr.Prop("DeptId"));
            var having = Expr.Prop("Id").Count() > 5;
            var havingExpr1 = new HavingExpr(groupBy1, having);
            var havingExpr2 = new HavingExpr(groupBy2, having);

            // Act
            var result = havingExpr1.Equals(havingExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that the ExprType property returns the correct value (ExprType.Having).
        /// </summary>
        [Fact]
        public void ExprType_WhenAccessed_ReturnsHaving()
        {
            // Arrange
            var havingExpr = new HavingExpr();

            // Act
            var result = havingExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Having, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns the correct value when the instance is constructed with parameters.
        /// </summary>
        [Fact]
        public void ExprType_WhenConstructedWithParameters_ReturnsHaving()
        {
            // Arrange
            var source = new HavingExpr();
            var having = new LogicExpr();
            var havingExpr = new HavingExpr(source, having);

            // Act
            var result = havingExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Having, result);
        }

        /// <summary>
        /// Tests that the ExprType property consistently returns the same value across multiple calls.
        /// </summary>
        [Fact]
        public void ExprType_WhenAccessedMultipleTimes_ReturnsConsistentValue()
        {
            // Arrange
            var havingExpr = new HavingExpr();

            // Act
            var result1 = havingExpr.ExprType;
            var result2 = havingExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Having, result1);
            Assert.Equal(result1, result2);
        }
        #region Constructor Tests

        /// <summary>
        /// Tests the HavingExpr constructor with valid non-null source and having parameters.
        /// Verifies that both Source and Having properties are correctly assigned.
        /// </summary>
        [Fact]
        public void Constructor_WithValidSourceAndHaving_AssignsPropertiesCorrectly()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var mockHaving = new Mock<LogicExpr>();

            // Act
            var havingExpr = new HavingExpr(mockSource.Object, mockHaving.Object);

            // Assert
            Assert.Same(mockSource.Object, havingExpr.Source);
            Assert.Same(mockHaving.Object, havingExpr.Having);
        }

        /// <summary>
        /// Tests the HavingExpr constructor with null source parameter.
        /// Verifies that null source is accepted and the Source property is set to null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullSource_AcceptsNullAndAssignsSourceProperty()
        {
            // Arrange
            var mockHaving = new Mock<LogicExpr>();

            // Act
            var havingExpr = new HavingExpr(null, mockHaving.Object);

            // Assert
            Assert.Null(havingExpr.Source);
            Assert.Same(mockHaving.Object, havingExpr.Having);
        }

        /// <summary>
        /// Tests the HavingExpr constructor with null having parameter.
        /// Verifies that null having is accepted and the Having property is set to null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullHaving_AcceptsNullAndAssignsHavingProperty()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();

            // Act
            var havingExpr = new HavingExpr(mockSource.Object, null);

            // Assert
            Assert.Same(mockSource.Object, havingExpr.Source);
            Assert.Null(havingExpr.Having);
        }

        /// <summary>
        /// Tests the HavingExpr constructor with both source and having parameters as null.
        /// Verifies that both null values are accepted and both properties are set to null.
        /// </summary>
        [Fact]
        public void Constructor_WithBothParametersNull_AcceptsNullsAndAssignsBothProperties()
        {
            // Arrange & Act
            var havingExpr = new HavingExpr(null, null);

            // Assert
            Assert.Null(havingExpr.Source);
            Assert.Null(havingExpr.Having);
        }

        #endregion

        /// <summary>
        /// Tests that the parameterless constructor creates an instance with default state.
        /// Verifies that the instance is created successfully, all properties are initialized to null,
        /// and the ExprType property returns the correct value.
        /// </summary>
        [Fact]
        public void Constructor_NoParameters_CreatesInstanceWithDefaultState()
        {
            // Arrange & Act
            var havingExpr = new HavingExpr();

            // Assert
            Assert.NotNull(havingExpr);
            Assert.Null(havingExpr.Having);
            Assert.Null(havingExpr.Source);
            Assert.Equal(ExprType.Having, havingExpr.ExprType);
        }

        /// <summary>
        /// Tests that Clone creates a deep copy when both Source and Having are set.
        /// Verifies that the cloned instance is different from the original but has equal property values.
        /// </summary>
        [Fact]
        public void Clone_WithSourceAndHaving_ReturnsDeepCopy()
        {
            // Arrange
            var from = new FromExpr(typeof(string));
            var groupBy = new GroupByExpr(from, Expr.Prop("Id"));
            var having = Expr.Prop("Count") > Expr.Const(5);
            var original = new HavingExpr(groupBy, having);

            // Act
            var cloned = (HavingExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.Equal(original, cloned);
            Assert.NotNull(cloned.Source);
            Assert.NotSame(original.Source, cloned.Source);
            Assert.NotNull(cloned.Having);
            Assert.NotSame(original.Having, cloned.Having);
        }

        /// <summary>
        /// Tests that Clone handles null Source property correctly.
        /// Verifies that the cloned instance has null Source when the original has null Source.
        /// </summary>
        [Fact]
        public void Clone_WithNullSource_ReturnsCloneWithNullSource()
        {
            // Arrange
            var having = Expr.Prop("Total") > Expr.Const(10);
            var original = new HavingExpr
            {
                Source = null,
                Having = having
            };

            // Act
            var cloned = (HavingExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.Null(cloned.Source);
            Assert.NotNull(cloned.Having);
            Assert.NotSame(original.Having, cloned.Having);
        }

        /// <summary>
        /// Tests that Clone handles null Having property correctly.
        /// Verifies that the cloned instance has null Having when the original has null Having.
        /// </summary>
        [Fact]
        public void Clone_WithNullHaving_ReturnsCloneWithNullHaving()
        {
            // Arrange
            var from = new FromExpr(typeof(string));
            var groupBy = new GroupByExpr(from, Expr.Prop("DeptId"));
            var original = new HavingExpr
            {
                Source = groupBy,
                Having = null
            };

            // Act
            var cloned = (HavingExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.NotNull(cloned.Source);
            Assert.NotSame(original.Source, cloned.Source);
            Assert.Null(cloned.Having);
        }

        /// <summary>
        /// Tests that Clone creates a new instance when both Source and Having are null.
        /// Verifies that the cloned instance is different from the original and has null properties.
        /// </summary>
        [Fact]
        public void Clone_WithBothPropertiesNull_ReturnsNewInstanceWithNullProperties()
        {
            // Arrange
            var original = new HavingExpr
            {
                Source = null,
                Having = null
            };

            // Act
            var cloned = (HavingExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.Null(cloned.Source);
            Assert.Null(cloned.Having);
        }

        /// <summary>
        /// Tests that Clone performs a deep copy by verifying that modifications to the clone
        /// do not affect the original instance.
        /// </summary>
        [Fact]
        public void Clone_ModifyingClone_DoesNotAffectOriginal()
        {
            // Arrange
            var from = new FromExpr(typeof(int));
            var groupBy = new GroupByExpr(from, Expr.Prop("CategoryId"));
            var having = Expr.Prop("Id").Count() > Expr.Const(3);
            var original = new HavingExpr(groupBy, having);

            // Act
            var cloned = (HavingExpr)original.Clone();
            var newSource = new FromExpr(typeof(double));
            var newHaving = Expr.Prop("Amount") > Expr.Const(100);
            cloned.Source = newSource;
            cloned.Having = newHaving;

            // Assert
            Assert.NotSame(original.Source, cloned.Source);
            Assert.NotSame(original.Having, cloned.Having);
            Assert.IsType<GroupByExpr>(original.Source);
            Assert.IsType<FromExpr>(cloned.Source);
        }

        /// <summary>
        /// Tests that Clone returns an instance of HavingExpr type.
        /// Verifies the cloned object has the correct ExprType.
        /// </summary>
        [Fact]
        public void Clone_ReturnsCorrectType()
        {
            // Arrange
            var from = new FromExpr(typeof(long));
            var groupBy = new GroupByExpr(from, Expr.Prop("Status"));
            var having = Expr.Prop("Price").Sum() > Expr.Const(1000);
            var original = new HavingExpr(groupBy, having);

            // Act
            var cloned = original.Clone();

            // Assert
            Assert.IsType<HavingExpr>(cloned);
            Assert.Equal(ExprType.Having, cloned.ExprType);
        }

        /// <summary>
        /// Tests that Clone creates equal instances that produce the same hash code.
        /// Verifies hash code consistency between original and cloned instances.
        /// </summary>
        [Fact]
        public void Clone_OriginalAndClone_HaveSameHashCode()
        {
            // Arrange
            var from = new FromExpr(typeof(byte));
            var groupBy = new GroupByExpr(from, Expr.Prop("Region"));
            var having = Expr.Prop("Sales") > Expr.Const(50000);
            var original = new HavingExpr(groupBy, having);

            // Act
            var cloned = (HavingExpr)original.Clone();

            // Assert
            Assert.Equal(original.GetHashCode(), cloned.GetHashCode());
        }
    }
}