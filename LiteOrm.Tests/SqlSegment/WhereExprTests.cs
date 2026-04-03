using System;

using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="WhereExpr"/> class.
    /// </summary>
    public partial class WhereExprTests
    {
        /// <summary>
        /// Tests that the parameterless constructor successfully creates a WhereExpr instance
        /// with default property values (Source and Where are null) and ExprType is set to Where.
        /// </summary>
        [Fact]
        public void Constructor_Parameterless_CreatesInstanceWithDefaultValues()
        {
            // Arrange & Act
            var whereExpr = new WhereExpr();

            // Assert
            Assert.NotNull(whereExpr);
            Assert.Null(whereExpr.Source);
            Assert.Null(whereExpr.Where);
            Assert.Equal(ExprType.Where, whereExpr.ExprType);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates an instance that can have
        /// its properties set after construction.
        /// </summary>
        [Fact]
        public void Constructor_Parameterless_AllowsPropertyAssignmentAfterConstruction()
        {
            // Arrange
            var whereExpr = new WhereExpr();
            var source = new FromExpr();
            var whereCondition = Expr.Prop("Age") > 18;

            // Act
            whereExpr.Source = source;
            whereExpr.Where = whereCondition;

            // Assert
            Assert.Same(source, whereExpr.Source);
            Assert.Equal(whereCondition, whereExpr.Where);
        }

        /// <summary>
        /// Tests that Equals returns false when the object parameter is null.
        /// </summary>
        [Fact]
        public void Equals_NullObject_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var where = Expr.Prop("Age") > 18;
            var whereExpr = new WhereExpr(source, where);

            // Act
            var result = whereExpr.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when the object is of a different type.
        /// </summary>
        [Theory]
        [InlineData("string")]
        [InlineData(42)]
        [InlineData(true)]
        public void Equals_DifferentType_ReturnsFalse(object obj)
        {
            // Arrange
            var source = new FromExpr();
            var where = Expr.Prop("Age") > 18;
            var whereExpr = new WhereExpr(source, where);

            // Act
            var result = whereExpr.Equals(obj);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing an instance to itself (reference equality).
        /// </summary>
        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr();
            var where = Expr.Prop("Age") > 18;
            var whereExpr = new WhereExpr(source, where);

            // Act
            var result = whereExpr.Equals(whereExpr);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when two WhereExpr instances have equal Source and Where properties.
        /// </summary>
        [Fact]
        public void Equals_EqualSourceAndWhere_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr();
            var where = Expr.Prop("Age") > 18;
            var whereExpr1 = new WhereExpr(source, where);
            var whereExpr2 = new WhereExpr(source, where);

            // Act
            var result = whereExpr1.Equals(whereExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when two WhereExpr instances have different Where properties.
        /// </summary>
        [Fact]
        public void Equals_DifferentWhere_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var where1 = Expr.Prop("Age") > 18;
            var where2 = Expr.Prop("Age") > 20;
            var whereExpr1 = new WhereExpr(source, where1);
            var whereExpr2 = new WhereExpr(source, where2);

            // Act
            var result = whereExpr1.Equals(whereExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when two WhereExpr instances have different Source properties.
        /// </summary>
        [Fact]
        public void Equals_DifferentSource_ReturnsFalse()
        {
            // Arrange
            var source1 = new FromExpr();
            var source2 = new FromExpr();
            var where = Expr.Prop("Age") > 18;
            var whereExpr1 = new WhereExpr(source1, where);
            var whereExpr2 = new WhereExpr(source2, where);

            // Act
            var result = whereExpr1.Equals(whereExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when both Source and Where properties are different.
        /// </summary>
        [Fact]
        public void Equals_DifferentSourceAndWhere_ReturnsFalse()
        {
            // Arrange
            var source1 = new FromExpr();
            var source2 = new FromExpr();
            var where1 = Expr.Prop("Age") > 18;
            var where2 = Expr.Prop("Name") == "Test";
            var whereExpr1 = new WhereExpr(source1, where1);
            var whereExpr2 = new WhereExpr(source2, where2);

            // Act
            var result = whereExpr1.Equals(whereExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null Source and null Where properties.
        /// </summary>
        [Fact]
        public void Equals_BothNullSourceAndWhere_ReturnsTrue()
        {
            // Arrange
            var whereExpr1 = new WhereExpr();
            var whereExpr2 = new WhereExpr();

            // Act
            var result = whereExpr1.Equals(whereExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one instance has null Source and the other has non-null Source.
        /// </summary>
        [Fact]
        public void Equals_OneNullSourceOtherNonNull_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var where = Expr.Prop("Age") > 18;
            var whereExpr1 = new WhereExpr(null, where);
            var whereExpr2 = new WhereExpr(source, where);

            // Act
            var result = whereExpr1.Equals(whereExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one instance has null Where and the other has non-null Where.
        /// </summary>
        [Fact]
        public void Equals_OneNullWhereOtherNonNull_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var where = Expr.Prop("Age") > 18;
            var whereExpr1 = new WhereExpr(source, null);
            var whereExpr2 = new WhereExpr(source, where);

            // Act
            var result = whereExpr1.Equals(whereExpr2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null Source but equal non-null Where.
        /// </summary>
        [Fact]
        public void Equals_BothNullSourceWithEqualWhere_ReturnsTrue()
        {
            // Arrange
            var where = Expr.Prop("Age") > 18;
            var whereExpr1 = new WhereExpr(null, where);
            var whereExpr2 = new WhereExpr(null, where);

            // Act
            var result = whereExpr1.Equals(whereExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null Where but equal non-null Source.
        /// </summary>
        [Fact]
        public void Equals_BothNullWhereWithEqualSource_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr();
            var whereExpr1 = new WhereExpr(source, null);
            var whereExpr2 = new WhereExpr(source, null);

            // Act
            var result = whereExpr1.Equals(whereExpr2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with a different type derived from SqlSegment.
        /// </summary>
        [Fact]
        public void Equals_DifferentSqlSegmentType_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var where = Expr.Prop("Age") > 18;
            var whereExpr = new WhereExpr(source, where);
            var fromExpr = new FromExpr();

            // Act
            var result = whereExpr.Equals(fromExpr);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Clone creates a new WhereExpr instance when both Source and Where are null.
        /// Verifies that the cloned object is a different instance with null properties.
        /// </summary>
        [Fact]
        public void Clone_WhenSourceAndWhereAreNull_ReturnsNewInstanceWithNullProperties()
        {
            // Arrange
            var whereExpr = new WhereExpr();

            // Act
            var cloned = whereExpr.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.IsType<WhereExpr>(cloned);
            Assert.NotSame(whereExpr, cloned);
            var clonedWhereExpr = (WhereExpr)cloned;
            Assert.Null(clonedWhereExpr.Source);
            Assert.Null(clonedWhereExpr.Where);
        }

        /// <summary>
        /// Tests that Clone creates a new WhereExpr instance when Source is null but Where is not.
        /// Verifies that Where is cloned while Source remains null.
        /// </summary>
        [Fact]
        public void Clone_WhenSourceIsNullAndWhereIsNotNull_ClonesWhereButSourceRemainsNull()
        {
            // Arrange
            var mockWhere = new Mock<LogicExpr>();
            var clonedWhere = new Mock<LogicExpr>();
            mockWhere.Setup(x => x.Clone()).Returns(clonedWhere.Object);

            var whereExpr = new WhereExpr
            {
                Source = null,
                Where = mockWhere.Object
            };

            // Act
            var cloned = whereExpr.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.IsType<WhereExpr>(cloned);
            Assert.NotSame(whereExpr, cloned);
            var clonedWhereExpr = (WhereExpr)cloned;
            Assert.Null(clonedWhereExpr.Source);
            Assert.Same(clonedWhere.Object, clonedWhereExpr.Where);
            mockWhere.Verify(x => x.Clone(), Times.Once);
        }

        /// <summary>
        /// Tests that Clone creates a new WhereExpr instance when Where is null but Source is not.
        /// Verifies that Source is cloned while Where remains null.
        /// </summary>
        [Fact]
        public void Clone_WhenWhereIsNullAndSourceIsNotNull_ClonesSourceButWhereRemainsNull()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var clonedSource = new Mock<SqlSegment>();
            mockSource.Setup(x => x.Clone()).Returns(clonedSource.Object);

            var whereExpr = new WhereExpr
            {
                Source = mockSource.Object,
                Where = null
            };

            // Act
            var cloned = whereExpr.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.IsType<WhereExpr>(cloned);
            Assert.NotSame(whereExpr, cloned);
            var clonedWhereExpr = (WhereExpr)cloned;
            Assert.Same(clonedSource.Object, clonedWhereExpr.Source);
            Assert.Null(clonedWhereExpr.Where);
            mockSource.Verify(x => x.Clone(), Times.Once);
        }

        /// <summary>
        /// Tests that Clone creates a new WhereExpr instance when both Source and Where are not null.
        /// Verifies that both properties are cloned correctly and the result is a deep clone.
        /// </summary>
        [Fact]
        public void Clone_WhenBothSourceAndWhereAreNotNull_ClonesBothProperties()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var clonedSource = new Mock<SqlSegment>();
            mockSource.Setup(x => x.Clone()).Returns(clonedSource.Object);

            var mockWhere = new Mock<LogicExpr>();
            var clonedWhere = new Mock<LogicExpr>();
            mockWhere.Setup(x => x.Clone()).Returns(clonedWhere.Object);

            var whereExpr = new WhereExpr
            {
                Source = mockSource.Object,
                Where = mockWhere.Object
            };

            // Act
            var cloned = whereExpr.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.IsType<WhereExpr>(cloned);
            Assert.NotSame(whereExpr, cloned);
            var clonedWhereExpr = (WhereExpr)cloned;
            Assert.Same(clonedSource.Object, clonedWhereExpr.Source);
            Assert.Same(clonedWhere.Object, clonedWhereExpr.Where);
            mockSource.Verify(x => x.Clone(), Times.Once);
            mockWhere.Verify(x => x.Clone(), Times.Once);
        }

        /// <summary>
        /// Tests that Clone creates a deep copy where modifications to the original do not affect the clone.
        /// Verifies that changing the original's properties after cloning does not impact the cloned instance.
        /// </summary>
        [Fact]
        public void Clone_WhenModifyingOriginal_DoesNotAffectClone()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var clonedSource = new Mock<SqlSegment>();
            mockSource.Setup(x => x.Clone()).Returns(clonedSource.Object);

            var mockWhere = new Mock<LogicExpr>();
            var clonedWhere = new Mock<LogicExpr>();
            mockWhere.Setup(x => x.Clone()).Returns(clonedWhere.Object);

            var whereExpr = new WhereExpr
            {
                Source = mockSource.Object,
                Where = mockWhere.Object
            };

            // Act
            var cloned = (WhereExpr)whereExpr.Clone();
            var newMockSource = new Mock<SqlSegment>();
            var newMockWhere = new Mock<LogicExpr>();
            whereExpr.Source = newMockSource.Object;
            whereExpr.Where = newMockWhere.Object;

            // Assert
            Assert.Same(clonedSource.Object, cloned.Source);
            Assert.Same(clonedWhere.Object, cloned.Where);
            Assert.NotSame(whereExpr.Source, cloned.Source);
            Assert.NotSame(whereExpr.Where, cloned.Where);
        }

        /// <summary>
        /// Tests that Clone returns an object that equals the original using the Equals method.
        /// Verifies that the cloned WhereExpr is semantically equal to the original.
        /// </summary>
        [Fact]
        public void Clone_WhenCalled_ReturnsObjectEqualToOriginal()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var clonedSource = new Mock<SqlSegment>();
            mockSource.Setup(x => x.Clone()).Returns(clonedSource.Object);
            mockSource.Setup(x => x.Equals(It.IsAny<object>())).Returns((object obj) => obj == mockSource.Object || obj == clonedSource.Object);
            clonedSource.Setup(x => x.Equals(It.IsAny<object>())).Returns((object obj) => obj == mockSource.Object || obj == clonedSource.Object);

            var mockWhere = new Mock<LogicExpr>();
            var clonedWhere = new Mock<LogicExpr>();
            mockWhere.Setup(x => x.Clone()).Returns(clonedWhere.Object);
            mockWhere.Setup(x => x.Equals(It.IsAny<object>())).Returns((object obj) => obj == mockWhere.Object || obj == clonedWhere.Object);
            clonedWhere.Setup(x => x.Equals(It.IsAny<object>())).Returns((object obj) => obj == mockWhere.Object || obj == clonedWhere.Object);

            var whereExpr = new WhereExpr
            {
                Source = mockSource.Object,
                Where = mockWhere.Object
            };

            // Act
            var cloned = (WhereExpr)whereExpr.Clone();

            // Assert
            Assert.True(whereExpr.Equals(cloned));
        }

        /// <summary>
        /// Tests that the ExprType property returns ExprType.Where.
        /// </summary>
        [Fact]
        public void ExprType_WhenAccessed_ReturnsWhereType()
        {
            // Arrange
            var whereExpr = new WhereExpr();

            // Act
            var result = whereExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Where, result);
        }
        #region Constructor Tests

        /// <summary>
        /// Tests that the constructor correctly assigns valid source and where parameters to their respective properties.
        /// Input: Valid FromExpr source and LogicExpr where condition.
        /// Expected: Source and Where properties are set to the provided values.
        /// </summary>
        [Fact]
        public void Constructor_WithValidSourceAndWhere_AssignsPropertiesCorrectly()
        {
            // Arrange
            var source = new FromExpr(typeof(object));
            var where = Expr.Prop("Age") > 18;

            // Act
            var result = new WhereExpr(source, where);

            // Assert
            Assert.Same(source, result.Source);
            Assert.Same(where, result.Where);
            Assert.Equal(ExprType.Where, result.ExprType);
        }

        /// <summary>
        /// Tests that the constructor accepts a null source parameter without throwing an exception.
        /// Input: Null source and valid LogicExpr where condition.
        /// Expected: Source property is null, Where property is set to the provided value, no exception thrown.
        /// </summary>
        [Fact]
        public void Constructor_WithNullSource_AcceptsNullValue()
        {
            // Arrange
            SqlSegment? source = null;
            var where = Expr.Prop("Name") == "Test";

            // Act
            var result = new WhereExpr(source, where);

            // Assert
            Assert.Null(result.Source);
            Assert.Same(where, result.Where);
        }

        /// <summary>
        /// Tests that the constructor accepts a null where parameter without throwing an exception.
        /// Input: Valid FromExpr source and null where condition.
        /// Expected: Source property is set to the provided value, Where property is null, no exception thrown.
        /// </summary>
        [Fact]
        public void Constructor_WithNullWhere_AcceptsNullValue()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            LogicExpr? where = null;

            // Act
            var result = new WhereExpr(source, where);

            // Assert
            Assert.Same(source, result.Source);
            Assert.Null(result.Where);
        }

        /// <summary>
        /// Tests that the constructor accepts both null parameters without throwing an exception.
        /// Input: Both source and where parameters are null.
        /// Expected: Both Source and Where properties are null, no exception thrown.
        /// </summary>
        [Fact]
        public void Constructor_WithBothParametersNull_AcceptsNullValues()
        {
            // Arrange
            SqlSegment? source = null;
            LogicExpr? where = null;

            // Act
            var result = new WhereExpr(source, where);

            // Assert
            Assert.Null(result.Source);
            Assert.Null(result.Where);
        }

        /// <summary>
        /// Tests that the constructor works with different types of SqlSegment sources.
        /// Input: Different SqlSegment derived types (FromExpr, WhereExpr) and LogicExpr conditions.
        /// Expected: Properties are correctly assigned regardless of the concrete SqlSegment type.
        /// </summary>
        [Theory]
        [MemberData(nameof(GetVariousSourceTypes))]
        public void Constructor_WithVariousSourceTypes_AssignsCorrectly(SqlSegment source, LogicExpr where)
        {
            // Act
            var result = new WhereExpr(source, where);

            // Assert
            Assert.Same(source, result.Source);
            Assert.Same(where, result.Where);
        }

        /// <summary>
        /// Tests that the constructor works with various types of LogicExpr conditions.
        /// Input: Valid source and different LogicExpr conditions (comparison, logical operations).
        /// Expected: Properties are correctly assigned regardless of the LogicExpr type.
        /// </summary>
        [Theory]
        [MemberData(nameof(GetVariousWhereExpressions))]
        public void Constructor_WithVariousWhereExpressions_AssignsCorrectly(LogicExpr where)
        {
            // Arrange
            var source = new FromExpr(typeof(int));

            // Act
            var result = new WhereExpr(source, where);

            // Assert
            Assert.Same(source, result.Source);
            Assert.Same(where, result.Where);
        }

        #endregion

        #region Test Data

        public static TheoryData<SqlSegment, LogicExpr> GetVariousSourceTypes()
        {
            return new TheoryData<SqlSegment, LogicExpr>
            {
                { new FromExpr(typeof(object)), Expr.Prop("Id") > 0 },
                { new FromExpr(typeof(string)), Expr.Prop("Active") == true },
                { new WhereExpr(new FromExpr(typeof(int)), Expr.Prop("Count") >= 10), Expr.Prop("Status") != null }
            };
        }

        public static TheoryData<LogicExpr> GetVariousWhereExpressions()
        {
            return new TheoryData<LogicExpr>
            {
                Expr.Prop("Age") > 18,
                Expr.Prop("Name") == "Test",
                Expr.Prop("IsActive") == true,
                Expr.Prop("Count") >= 100,
                (Expr.Prop("Age") > 18) & (Expr.Prop("IsActive") == true),
                (Expr.Prop("Status") == "Active") | (Expr.Prop("Status") == "Pending")
            };
        }

        #endregion

        /// <summary>
        /// Tests that ToString returns the correct format when both Source and Where are non-null.
        /// </summary>
        [Fact]
        public void ToString_WithValidSourceAndWhere_ReturnsFormattedString()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var where = Expr.Prop("Age") > 18;
            var whereExpr = new WhereExpr(source, where);

            // Act
            var result = whereExpr.ToString();

            // Assert
            Assert.Contains("WHERE", result);
            Assert.Contains(source.ToString(), result);
            Assert.Contains(where.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString handles null Source property gracefully.
        /// </summary>
        [Fact]
        public void ToString_WithNullSource_ReturnsStringWithWhereOnly()
        {
            // Arrange
            var where = Expr.Prop("Name") == "Test";
            var whereExpr = new WhereExpr
            {
                Source = null,
                Where = where
            };

            // Act
            var result = whereExpr.ToString();

            // Assert
            Assert.Contains("WHERE", result);
            Assert.Contains(where.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString handles null Where property gracefully.
        /// </summary>
        [Fact]
        public void ToString_WithNullWhere_ReturnsStringWithSourceOnly()
        {
            // Arrange
            var source = new FromExpr(typeof(int));
            var whereExpr = new WhereExpr
            {
                Source = source,
                Where = null
            };

            // Act
            var result = whereExpr.ToString();

            // Assert
            Assert.Contains("WHERE", result);
            Assert.Contains(source.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString handles both null Source and Where properties.
        /// </summary>
        [Fact]
        public void ToString_WithBothNull_ReturnsWhereKeywordOnly()
        {
            // Arrange
            var whereExpr = new WhereExpr
            {
                Source = null,
                Where = null
            };

            // Act
            var result = whereExpr.ToString();

            // Assert
            Assert.Contains("WHERE", result);
        }

        /// <summary>
        /// Tests that ToString correctly formats the output with complex logical expressions.
        /// </summary>
        [Fact]
        public void ToString_WithComplexLogicExpr_ReturnsFormattedString()
        {
            // Arrange
            var source = new FromExpr(typeof(object));
            var where = (Expr.Prop("Age") > 18) & (Expr.Prop("Name") != "Admin");
            var whereExpr = new WhereExpr(source, where);

            // Act
            var result = whereExpr.ToString();

            // Assert
            Assert.Contains("WHERE", result);
            Assert.Contains(source.ToString(), result);
            Assert.Contains(where.ToString(), result);
            Assert.Equal($"{source} WHERE {where}", result);
        }

        /// <summary>
        /// Tests that ToString produces exact expected format with known inputs.
        /// </summary>
        [Fact]
        public void ToString_VerifiesExactFormat_MatchesExpectedPattern()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var where = Expr.Prop("Id") == 1;
            var whereExpr = new WhereExpr(source, where);
            var expectedFormat = $"{source} WHERE {where}";

            // Act
            var result = whereExpr.ToString();

            // Assert
            Assert.Equal(expectedFormat, result);
        }

        /// <summary>
        /// Tests that GetHashCode returns a consistent value when called multiple times on the same object.
        /// Input: WhereExpr with non-null Source and Where.
        /// Expected: Same hash code returned each time.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsConsistentValue()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var where = Expr.Prop("Age") > 18;
            var whereExpr = new WhereExpr(source, where);

            // Act
            var hashCode1 = whereExpr.GetHashCode();
            var hashCode2 = whereExpr.GetHashCode();
            var hashCode3 = whereExpr.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
            Assert.Equal(hashCode2, hashCode3);
        }

        /// <summary>
        /// Tests that GetHashCode returns equal hash codes for equal WhereExpr objects.
        /// Input: Two WhereExpr instances with equal Source and Where properties.
        /// Expected: Both instances return the same hash code.
        /// </summary>
        [Fact]
        public void GetHashCode_EqualObjects_ReturnsSameHashCode()
        {
            // Arrange
            var source1 = new FromExpr(typeof(string));
            var where1 = Expr.Prop("Age") > 18;
            var whereExpr1 = new WhereExpr(source1, where1);

            var source2 = new FromExpr(typeof(string));
            var where2 = Expr.Prop("Age") > 18;
            var whereExpr2 = new WhereExpr(source2, where2);

            // Act
            var hashCode1 = whereExpr1.GetHashCode();
            var hashCode2 = whereExpr2.GetHashCode();

            // Assert
            Assert.True(whereExpr1.Equals(whereExpr2));
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns consistent hash code when both Source and Where are null.
        /// Input: WhereExpr with null Source and null Where.
        /// Expected: Consistent hash code based on type only.
        /// </summary>
        [Fact]
        public void GetHashCode_WithNullSourceAndNullWhere_ReturnsConsistentHashCode()
        {
            // Arrange
            var whereExpr = new WhereExpr();

            // Act
            var hashCode = whereExpr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns consistent hash code when Source is null.
        /// Input: WhereExpr with null Source and non-null Where.
        /// Expected: Consistent hash code.
        /// </summary>
        [Fact]
        public void GetHashCode_WithNullSource_ReturnsConsistentHashCode()
        {
            // Arrange
            var where = Expr.Prop("Age") > 18;
            var whereExpr = new WhereExpr { Where = where };

            // Act
            var hashCode1 = whereExpr.GetHashCode();
            var hashCode2 = whereExpr.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns consistent hash code when Where is null.
        /// Input: WhereExpr with non-null Source and null Where.
        /// Expected: Consistent hash code.
        /// </summary>
        [Fact]
        public void GetHashCode_WithNullWhere_ReturnsConsistentHashCode()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var whereExpr = new WhereExpr { Source = source };

            // Act
            var hashCode1 = whereExpr.GetHashCode();
            var hashCode2 = whereExpr.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes for WhereExpr objects with different Source.
        /// Input: Two WhereExpr instances with different Source but same Where.
        /// Expected: Different hash codes (likely).
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentSource_ReturnsDifferentHashCodes()
        {
            // Arrange
            var source1 = new FromExpr(typeof(string));
            var source2 = new FromExpr(typeof(int));
            var where = Expr.Prop("Age") > 18;
            var whereExpr1 = new WhereExpr(source1, where);
            var whereExpr2 = new WhereExpr(source2, where);

            // Act
            var hashCode1 = whereExpr1.GetHashCode();
            var hashCode2 = whereExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes for WhereExpr objects with different Where.
        /// Input: Two WhereExpr instances with same Source but different Where.
        /// Expected: Different hash codes (likely).
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentWhere_ReturnsDifferentHashCodes()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var where1 = Expr.Prop("Age") > 18;
            var where2 = Expr.Prop("Age") > 20;
            var whereExpr1 = new WhereExpr(source, where1);
            var whereExpr2 = new WhereExpr(source, where2);

            // Act
            var hashCode1 = whereExpr1.GetHashCode();
            var hashCode2 = whereExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns equal hash codes for two instances with both null properties.
        /// Input: Two WhereExpr instances with both Source and Where null.
        /// Expected: Same hash code.
        /// </summary>
        [Fact]
        public void GetHashCode_TwoInstancesWithNullProperties_ReturnsSameHashCode()
        {
            // Arrange
            var whereExpr1 = new WhereExpr();
            var whereExpr2 = new WhereExpr();

            // Act
            var hashCode1 = whereExpr1.GetHashCode();
            var hashCode2 = whereExpr2.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode handles different combinations of null and non-null properties correctly.
        /// Input: WhereExpr instances with various null/non-null combinations.
        /// Expected: Proper hash code computation for each combination.
        /// </summary>
        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void GetHashCode_VariousNullCombinations_ReturnsConsistentHashCode(bool sourceIsNull, bool whereIsNull)
        {
            // Arrange
            var source = sourceIsNull ? null : new FromExpr(typeof(string));
            var where = whereIsNull ? null : Expr.Prop("Age") > 18;
            var whereExpr = new WhereExpr { Source = source, Where = where };

            // Act
            var hashCode1 = whereExpr.GetHashCode();
            var hashCode2 = whereExpr.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }
    }
}