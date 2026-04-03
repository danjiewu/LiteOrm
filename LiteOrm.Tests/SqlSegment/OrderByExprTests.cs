using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using LiteOrm;
using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the OrderByExpr class.
    /// </summary>
    public partial class OrderByExprTests
    {
        /// <summary>
        /// Tests that GetHashCode returns the same hash code when called multiple times on the same instance.
        /// This verifies the consistency requirement of GetHashCode.
        /// </summary>
        [Fact]
        public void GetHashCode_SameInstance_ReturnsSameHashCode()
        {
            // Arrange
            var from = new FromExpr(typeof(string));
            var orderBy = new OrderByExpr(from, Expr.Prop("Age").Asc(), Expr.Prop("Name").Desc());

            // Act
            var hash1 = orderBy.GetHashCode();
            var hash2 = orderBy.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns the same hash code for two instances that are equal.
        /// This verifies that equal objects produce equal hash codes.
        /// </summary>
        [Fact]
        public void GetHashCode_EqualInstances_ReturnsSameHashCode()
        {
            // Arrange
            var from = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(from, Expr.Prop("Age").Asc(), Expr.Prop("Name").Desc());
            var orderBy2 = new OrderByExpr(from, Expr.Prop("Age").Asc(), Expr.Prop("Name").Desc());

            // Act
            var hash1 = orderBy1.GetHashCode();
            var hash2 = orderBy2.GetHashCode();

            // Assert
            Assert.True(orderBy1.Equals(orderBy2));
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles null Source property correctly.
        /// Expected behavior: Returns a hash code without throwing an exception.
        /// </summary>
        [Fact]
        public void GetHashCode_NullSource_ReturnsValidHashCode()
        {
            // Arrange
            var orderBy = new OrderByExpr(null, Expr.Prop("Age").Asc());

            // Act
            var hash = orderBy.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that GetHashCode handles an empty OrderBys list correctly.
        /// Expected behavior: Returns a hash code based on Source and empty collection.
        /// </summary>
        [Fact]
        public void GetHashCode_EmptyOrderBys_ReturnsValidHashCode()
        {
            // Arrange
            var from = new FromExpr(typeof(string));
            var orderBy = new OrderByExpr(from);

            // Act
            var hash = orderBy.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that GetHashCode produces different hash codes for instances with different Source.
        /// Expected behavior: Different Source should produce different hash codes (in most cases).
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentSource_ProducesDifferentHashCodes()
        {
            // Arrange
            var from1 = new FromExpr(typeof(string));
            var from2 = new FromExpr(typeof(int));
            var orderBy1 = new OrderByExpr(from1, Expr.Prop("Age").Asc());
            var orderBy2 = new OrderByExpr(from2, Expr.Prop("Age").Asc());

            // Act
            var hash1 = orderBy1.GetHashCode();
            var hash2 = orderBy2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode produces different hash codes for instances with different OrderBys.
        /// Expected behavior: Different OrderBys should produce different hash codes (in most cases).
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentOrderBys_ProducesDifferentHashCodes()
        {
            // Arrange
            var from = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(from, Expr.Prop("Age").Asc());
            var orderBy2 = new OrderByExpr(from, Expr.Prop("Name").Desc());

            // Act
            var hash1 = orderBy1.GetHashCode();
            var hash2 = orderBy2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode produces different hash codes for instances with different OrderBys count.
        /// Expected behavior: Different number of OrderBys should produce different hash codes.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentOrderBysCount_ProducesDifferentHashCodes()
        {
            // Arrange
            var from = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(from, Expr.Prop("Age").Asc());
            var orderBy2 = new OrderByExpr(from, Expr.Prop("Age").Asc(), Expr.Prop("Name").Desc());

            // Act
            var hash1 = orderBy1.GetHashCode();
            var hash2 = orderBy2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode is sensitive to the order of OrderBys items.
        /// Expected behavior: Same items in different order should produce different hash codes.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentOrderBysOrder_ProducesDifferentHashCodes()
        {
            // Arrange
            var from = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(from, Expr.Prop("Age").Asc(), Expr.Prop("Name").Desc());
            var orderBy2 = new OrderByExpr(from, Expr.Prop("Name").Desc(), Expr.Prop("Age").Asc());

            // Act
            var hash1 = orderBy1.GetHashCode();
            var hash2 = orderBy2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles both null Source and empty OrderBys correctly.
        /// Expected behavior: Returns a valid hash code.
        /// </summary>
        [Fact]
        public void GetHashCode_NullSourceAndEmptyOrderBys_ReturnsValidHashCode()
        {
            // Arrange
            var orderBy = new OrderByExpr();

            // Act
            var hash = orderBy.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that GetHashCode produces different hash codes when Source is null vs non-null.
        /// Expected behavior: Null Source should produce different hash code than non-null Source.
        /// </summary>
        [Fact]
        public void GetHashCode_NullVsNonNullSource_ProducesDifferentHashCodes()
        {
            // Arrange
            var from = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(null, Expr.Prop("Age").Asc());
            var orderBy2 = new OrderByExpr(from, Expr.Prop("Age").Asc());

            // Act
            var hash1 = orderBy1.GetHashCode();
            var hash2 = orderBy2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles multiple identical OrderBys items.
        /// Expected behavior: Returns a valid hash code.
        /// </summary>
        [Fact]
        public void GetHashCode_MultipleIdenticalOrderBysItems_ReturnsValidHashCode()
        {
            // Arrange
            var from = new FromExpr(typeof(string));
            var orderBy = new OrderByExpr(from, Expr.Prop("Age").Asc(), Expr.Prop("Age").Asc());

            // Act
            var hash = orderBy.GetHashCode();

            // Assert
            Assert.IsType<int>(hash);
        }

        /// <summary>
        /// Tests that GetHashCode produces the same hash code for instances with manually set OrderBys collection.
        /// Expected behavior: Setting OrderBys property directly should produce same hash as using constructor.
        /// </summary>
        [Fact]
        public void GetHashCode_ManuallySetOrderBys_ProducesSameHashCodeAsConstructor()
        {
            // Arrange
            var from = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(from, Expr.Prop("Age").Asc(), Expr.Prop("Name").Desc());
            var orderBy2 = new OrderByExpr(from)
            {
                OrderBys = new List<OrderByItemExpr> { Expr.Prop("Age").Asc(), Expr.Prop("Name").Desc() }
            };

            // Act
            var hash1 = orderBy1.GetHashCode();
            var hash2 = orderBy2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that Clone method creates a new instance with null Source and empty OrderBys list when both are null.
        /// Input: OrderByExpr with null Source and null OrderBys.
        /// Expected: New instance with null Source and empty OrderBys list.
        /// </summary>
        [Fact]
        public void Clone_WithNullSourceAndNullOrderBys_ReturnsNewInstanceWithEmptyOrderBys()
        {
            // Arrange
            var original = new OrderByExpr
            {
                Source = null,
                OrderBys = null
            };

            // Act
            var cloned = (OrderByExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.Null(cloned.Source);
            Assert.NotNull(cloned.OrderBys);
            Assert.Empty(cloned.OrderBys);
        }

        /// <summary>
        /// Tests that Clone method creates a new instance with null Source and empty OrderBys list.
        /// Input: OrderByExpr with null Source and empty OrderBys list.
        /// Expected: New instance with null Source and empty OrderBys list.
        /// </summary>
        [Fact]
        public void Clone_WithNullSourceAndEmptyOrderBys_ReturnsNewInstanceWithEmptyOrderBys()
        {
            // Arrange
            var original = new OrderByExpr
            {
                Source = null,
                OrderBys = new List<OrderByItemExpr>()
            };

            // Act
            var cloned = (OrderByExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.Null(cloned.Source);
            Assert.NotNull(cloned.OrderBys);
            Assert.Empty(cloned.OrderBys);
            Assert.NotSame(original.OrderBys, cloned.OrderBys);
        }

        /// <summary>
        /// Tests that Clone method creates a deep copy with cloned Source and empty OrderBys.
        /// Input: OrderByExpr with non-null Source and empty OrderBys list.
        /// Expected: New instance with cloned Source and empty OrderBys list.
        /// </summary>
        [Fact]
        public void Clone_WithSourceAndEmptyOrderBys_ReturnsCopiedInstance()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var clonedSource = new Mock<SqlSegment>();
            mockSource.Setup(s => s.Clone()).Returns(clonedSource.Object);

            var original = new OrderByExpr
            {
                Source = mockSource.Object,
                OrderBys = new List<OrderByItemExpr>()
            };

            // Act
            var cloned = (OrderByExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.NotNull(cloned.Source);
            Assert.Same(clonedSource.Object, cloned.Source);
            Assert.NotSame(original.Source, cloned.Source);
            Assert.NotNull(cloned.OrderBys);
            Assert.Empty(cloned.OrderBys);
            Assert.NotSame(original.OrderBys, cloned.OrderBys);
            mockSource.Verify(s => s.Clone(), Times.Once);
        }

        /// <summary>
        /// Tests that Clone method creates a deep copy with cloned Source and single OrderBy item.
        /// Input: OrderByExpr with non-null Source and single OrderByItemExpr.
        /// Expected: New instance with cloned Source and cloned OrderBy item.
        /// </summary>
        [Fact]
        public void Clone_WithSourceAndSingleOrderBy_ReturnsCopiedInstance()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var clonedSource = new Mock<SqlSegment>();
            mockSource.Setup(s => s.Clone()).Returns(clonedSource.Object);

            var mockField = new Mock<ValueTypeExpr>();
            var clonedField = new Mock<ValueTypeExpr>();
            mockField.Setup(f => f.Clone()).Returns(clonedField.Object);

            var orderByItem = new OrderByItemExpr(mockField.Object, true);

            var original = new OrderByExpr
            {
                Source = mockSource.Object,
                OrderBys = new List<OrderByItemExpr> { orderByItem }
            };

            // Act
            var cloned = (OrderByExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.NotNull(cloned.Source);
            Assert.Same(clonedSource.Object, cloned.Source);
            Assert.NotSame(original.Source, cloned.Source);
            Assert.NotNull(cloned.OrderBys);
            Assert.Single(cloned.OrderBys);
            Assert.NotSame(original.OrderBys, cloned.OrderBys);
            Assert.NotSame(original.OrderBys[0], cloned.OrderBys[0]);
            Assert.Equal(original.OrderBys[0].Ascending, cloned.OrderBys[0].Ascending);
            mockSource.Verify(s => s.Clone(), Times.Once);
            mockField.Verify(f => f.Clone(), Times.Once);
        }

        /// <summary>
        /// Tests that Clone method creates a deep copy with cloned Source and multiple OrderBy items.
        /// Input: OrderByExpr with non-null Source and multiple OrderByItemExpr instances.
        /// Expected: New instance with cloned Source and all OrderBy items cloned.
        /// </summary>
        [Fact]
        public void Clone_WithSourceAndMultipleOrderBys_ReturnsCopiedInstance()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var clonedSource = new Mock<SqlSegment>();
            mockSource.Setup(s => s.Clone()).Returns(clonedSource.Object);

            var mockField1 = new Mock<ValueTypeExpr>();
            var clonedField1 = new Mock<ValueTypeExpr>();
            mockField1.Setup(f => f.Clone()).Returns(clonedField1.Object);

            var mockField2 = new Mock<ValueTypeExpr>();
            var clonedField2 = new Mock<ValueTypeExpr>();
            mockField2.Setup(f => f.Clone()).Returns(clonedField2.Object);

            var mockField3 = new Mock<ValueTypeExpr>();
            var clonedField3 = new Mock<ValueTypeExpr>();
            mockField3.Setup(f => f.Clone()).Returns(clonedField3.Object);

            var orderByItem1 = new OrderByItemExpr(mockField1.Object, true);
            var orderByItem2 = new OrderByItemExpr(mockField2.Object, false);
            var orderByItem3 = new OrderByItemExpr(mockField3.Object, true);

            var original = new OrderByExpr
            {
                Source = mockSource.Object,
                OrderBys = new List<OrderByItemExpr> { orderByItem1, orderByItem2, orderByItem3 }
            };

            // Act
            var cloned = (OrderByExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.NotNull(cloned.Source);
            Assert.Same(clonedSource.Object, cloned.Source);
            Assert.NotSame(original.Source, cloned.Source);
            Assert.NotNull(cloned.OrderBys);
            Assert.Equal(3, cloned.OrderBys.Count);
            Assert.NotSame(original.OrderBys, cloned.OrderBys);

            for (int i = 0; i < 3; i++)
            {
                Assert.NotSame(original.OrderBys[i], cloned.OrderBys[i]);
                Assert.Equal(original.OrderBys[i].Ascending, cloned.OrderBys[i].Ascending);
            }

            mockSource.Verify(s => s.Clone(), Times.Once);
            mockField1.Verify(f => f.Clone(), Times.Once);
            mockField2.Verify(f => f.Clone(), Times.Once);
            mockField3.Verify(f => f.Clone(), Times.Once);
        }

        /// <summary>
        /// Tests that Clone method creates a deep copy and modifying the clone does not affect the original.
        /// Input: OrderByExpr with Source and OrderBy items.
        /// Expected: Modifications to clone do not affect original instance.
        /// </summary>
        [Fact]
        public void Clone_DeepCopy_ModifyingCloneDoesNotAffectOriginal()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var clonedSource = new Mock<SqlSegment>();
            mockSource.Setup(s => s.Clone()).Returns(clonedSource.Object);

            var mockField1 = new Mock<ValueTypeExpr>();
            var clonedField1 = new Mock<ValueTypeExpr>();
            mockField1.Setup(f => f.Clone()).Returns(clonedField1.Object);

            var mockField2 = new Mock<ValueTypeExpr>();
            var clonedField2 = new Mock<ValueTypeExpr>();
            mockField2.Setup(f => f.Clone()).Returns(clonedField2.Object);

            var orderByItem1 = new OrderByItemExpr(mockField1.Object, true);
            var orderByItem2 = new OrderByItemExpr(mockField2.Object, false);

            var original = new OrderByExpr
            {
                Source = mockSource.Object,
                OrderBys = new List<OrderByItemExpr> { orderByItem1, orderByItem2 }
            };

            // Act
            var cloned = (OrderByExpr)original.Clone();
            var originalOrderBysCount = original.OrderBys.Count;
            var mockNewSource = new Mock<SqlSegment>();
            cloned.Source = mockNewSource.Object;
            cloned.OrderBys.Clear();

            // Assert
            Assert.Equal(originalOrderBysCount, original.OrderBys.Count);
            Assert.Same(mockSource.Object, original.Source);
            Assert.NotSame(original.Source, cloned.Source);
            Assert.Empty(cloned.OrderBys);
            Assert.Equal(2, original.OrderBys.Count);
        }

        /// <summary>
        /// Tests that Clone method properly handles OrderBys list with items that have null Field.
        /// Input: OrderByExpr with OrderBy item containing null Field.
        /// Expected: Clone succeeds and creates a new instance with cloned OrderBy item.
        /// </summary>
        [Fact]
        public void Clone_WithOrderByItemContainingNullField_ReturnsCopiedInstance()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var clonedSource = new Mock<SqlSegment>();
            mockSource.Setup(s => s.Clone()).Returns(clonedSource.Object);

            var orderByItem = new OrderByItemExpr(null, true);

            var original = new OrderByExpr
            {
                Source = mockSource.Object,
                OrderBys = new List<OrderByItemExpr> { orderByItem }
            };

            // Act
            var cloned = (OrderByExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.NotNull(cloned.Source);
            Assert.NotNull(cloned.OrderBys);
            Assert.Single(cloned.OrderBys);
            Assert.NotSame(original.OrderBys[0], cloned.OrderBys[0]);
            Assert.Null(cloned.OrderBys[0].Field);
            Assert.Equal(original.OrderBys[0].Ascending, cloned.OrderBys[0].Ascending);
        }

        /// <summary>
        /// Tests that the ExprType property returns ExprType.OrderBy
        /// when accessed on a default constructed instance.
        /// </summary>
        [Fact]
        public void ExprType_DefaultConstructor_ReturnsOrderBy()
        {
            // Arrange
            var orderByExpr = new OrderByExpr();

            // Act
            var result = orderByExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.OrderBy, result);
        }

        /// <summary>
        /// Tests that the ExprType property returns ExprType.OrderBy
        /// when accessed on an instance constructed with parameters.
        /// </summary>
        [Fact]
        public void ExprType_ParameterizedConstructor_ReturnsOrderBy()
        {
            // Arrange
            var source = new OrderByExpr();
            var orderByItems = new[] { new OrderByItemExpr() };
            var orderByExpr = new OrderByExpr(source, orderByItems);

            // Act
            var result = orderByExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.OrderBy, result);
        }

        /// <summary>
        /// Tests that the ExprType property consistently returns ExprType.OrderBy
        /// when accessed multiple times on the same instance.
        /// </summary>
        [Fact]
        public void ExprType_MultipleAccesses_ReturnsConsistentValue()
        {
            // Arrange
            var orderByExpr = new OrderByExpr();

            // Act
            var result1 = orderByExpr.ExprType;
            var result2 = orderByExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.OrderBy, result1);
            Assert.Equal(ExprType.OrderBy, result2);
            Assert.Equal(result1, result2);
        }

        /// <summary>
        /// Tests that Equals returns false when the input is null.
        /// </summary>
        [Fact]
        public void Equals_NullInput_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var orderBy = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Name"), true));

            // Act
            var result = orderBy.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when the input is a different type.
        /// </summary>
        [Theory]
        [InlineData("string")]
        [InlineData(42)]
        [InlineData(3.14)]
        public void Equals_DifferentType_ReturnsFalse(object obj)
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var orderBy = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Name"), true));

            // Act
            var result = orderBy.Equals(obj);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing the same instance (reflexive property).
        /// </summary>
        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var orderBy = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Name"), true));

            // Act
            var result = orderBy.Equals(orderBy);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing two objects with equal Source and OrderBys.
        /// </summary>
        [Fact]
        public void Equals_EqualSourceAndOrderBys_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Name"), true), new OrderByItemExpr(new PropExpr("Age"), false));
            var orderBy2 = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Name"), true), new OrderByItemExpr(new PropExpr("Age"), false));

            // Act
            var result = orderBy1.Equals(orderBy2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when Source properties differ.
        /// </summary>
        [Fact]
        public void Equals_DifferentSource_ReturnsFalse()
        {
            // Arrange
            var source1 = new FromExpr(typeof(string));
            var source2 = new FromExpr(typeof(int));
            var orderBy1 = new OrderByExpr(source1, new OrderByItemExpr(new PropExpr("Name"), true));
            var orderBy2 = new OrderByExpr(source2, new OrderByItemExpr(new PropExpr("Name"), true));

            // Act
            var result = orderBy1.Equals(orderBy2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when OrderBys properties differ.
        /// </summary>
        [Fact]
        public void Equals_DifferentOrderBys_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Name"), true));
            var orderBy2 = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Age"), false));

            // Act
            var result = orderBy1.Equals(orderBy2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both objects have empty OrderBys and equal Source.
        /// </summary>
        [Fact]
        public void Equals_EmptyOrderBysOnBoth_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(source);
            var orderBy2 = new OrderByExpr(source);

            // Act
            var result = orderBy1.Equals(orderBy2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when OrderBys have different counts.
        /// </summary>
        [Fact]
        public void Equals_DifferentOrderBysCount_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Name"), true));
            var orderBy2 = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Name"), true), new OrderByItemExpr(new PropExpr("Age"), false));

            // Act
            var result = orderBy1.Equals(orderBy2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when OrderBys have the same items but in different order.
        /// SequenceEqual is order-sensitive.
        /// </summary>
        [Fact]
        public void Equals_OrderBysDifferentOrder_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Name"), true), new OrderByItemExpr(new PropExpr("Age"), false));
            var orderBy2 = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Age"), false), new OrderByItemExpr(new PropExpr("Name"), true));

            // Act
            var result = orderBy1.Equals(orderBy2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both objects have null Source and equal OrderBys.
        /// </summary>
        [Fact]
        public void Equals_BothSourceNull_ReturnsTrue()
        {
            // Arrange
            var orderBy1 = new OrderByExpr(null, new OrderByItemExpr(new PropExpr("Name"), true));
            var orderBy2 = new OrderByExpr(null, new OrderByItemExpr(new PropExpr("Name"), true));

            // Act
            var result = orderBy1.Equals(orderBy2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one Source is null and the other is not.
        /// </summary>
        [Fact]
        public void Equals_OneSourceNull_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Name"), true));
            var orderBy2 = new OrderByExpr(null, new OrderByItemExpr(new PropExpr("Name"), true));

            // Act
            var result = orderBy1.Equals(orderBy2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one OrderBys is empty and the other has items.
        /// </summary>
        [Fact]
        public void Equals_OneEmptyOrderBys_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(source);
            var orderBy2 = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Name"), true));

            // Act
            var result = orderBy1.Equals(orderBy2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when OrderBys have the same field but different ascending flags.
        /// </summary>
        [Fact]
        public void Equals_SameFieldDifferentAscending_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Name"), true));
            var orderBy2 = new OrderByExpr(source, new OrderByItemExpr(new PropExpr("Name"), false));

            // Act
            var result = orderBy1.Equals(orderBy2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true for complex equal scenarios with multiple OrderBy items.
        /// </summary>
        [Fact]
        public void Equals_ComplexEqualObjects_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr(typeof(string));
            var orderBy1 = new OrderByExpr(source,
                new OrderByItemExpr(new PropExpr("Name"), true),
                new OrderByItemExpr(new PropExpr("Age"), false),
                new OrderByItemExpr(new PropExpr("Id"), true));
            var orderBy2 = new OrderByExpr(source,
                new OrderByItemExpr(new PropExpr("Name"), true),
                new OrderByItemExpr(new PropExpr("Age"), false),
                new OrderByItemExpr(new PropExpr("Id"), true));

            // Act
            var result = orderBy1.Equals(orderBy2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both objects have null Source and empty OrderBys.
        /// </summary>
        [Fact]
        public void Equals_BothNullSourceAndEmptyOrderBys_ReturnsTrue()
        {
            // Arrange
            var orderBy1 = new OrderByExpr(null);
            var orderBy2 = new OrderByExpr(null);

            // Act
            var result = orderBy1.Equals(orderBy2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that the constructor properly initializes Source and OrderBys properties
        /// when provided with a valid source and multiple orderBy items.
        /// Expected: Source is set to the provided value and OrderBys contains all items.
        /// </summary>
        [Fact]
        public void Constructor_WithSourceAndMultipleOrderBys_SetsPropertiesCorrectly()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var orderBy1 = new OrderByItemExpr();
            var orderBy2 = new OrderByItemExpr();
            var orderBy3 = new OrderByItemExpr();

            // Act
            var result = new OrderByExpr(mockSource.Object, orderBy1, orderBy2, orderBy3);

            // Assert
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.OrderBys);
            Assert.Equal(3, result.OrderBys.Count);
            Assert.Contains(orderBy1, result.OrderBys);
            Assert.Contains(orderBy2, result.OrderBys);
            Assert.Contains(orderBy3, result.OrderBys);
        }

        /// <summary>
        /// Tests that the constructor properly initializes Source and OrderBys properties
        /// when provided with a valid source and a single orderBy item.
        /// Expected: Source is set and OrderBys contains the single item.
        /// </summary>
        [Fact]
        public void Constructor_WithSourceAndSingleOrderBy_SetsPropertiesCorrectly()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var orderBy = new OrderByItemExpr();

            // Act
            var result = new OrderByExpr(mockSource.Object, orderBy);

            // Assert
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.OrderBys);
            Assert.Single(result.OrderBys);
            Assert.Same(orderBy, result.OrderBys[0]);
        }

        /// <summary>
        /// Tests that the constructor properly initializes Source and OrderBys properties
        /// when provided with a valid source and an empty orderBys array.
        /// Expected: Source is set and OrderBys is an empty list.
        /// </summary>
        [Fact]
        public void Constructor_WithSourceAndEmptyOrderBys_CreatesEmptyList()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();

            // Act
            var result = new OrderByExpr(mockSource.Object);

            // Assert
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.OrderBys);
            Assert.Empty(result.OrderBys);
        }

        /// <summary>
        /// Tests that the constructor properly handles null orderBys parameter
        /// by creating an empty list.
        /// Expected: Source is set and OrderBys is an empty list (not null).
        /// </summary>
        [Fact]
        public void Constructor_WithSourceAndNullOrderBys_CreatesEmptyList()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            OrderByItemExpr[]? orderBys = null;

            // Act
            var result = new OrderByExpr(mockSource.Object, orderBys);

            // Assert
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.OrderBys);
            Assert.Empty(result.OrderBys);
        }

        /// <summary>
        /// Tests that the constructor accepts null source parameter
        /// and properly initializes OrderBys with provided items.
        /// Expected: Source is null and OrderBys contains the provided items.
        /// </summary>
        [Fact]
        public void Constructor_WithNullSourceAndOrderBys_SetsSourceToNull()
        {
            // Arrange
            SqlSegment? source = null;
            var orderBy1 = new OrderByItemExpr();
            var orderBy2 = new OrderByItemExpr();

            // Act
            var result = new OrderByExpr(source, orderBy1, orderBy2);

            // Assert
            Assert.Null(result.Source);
            Assert.NotNull(result.OrderBys);
            Assert.Equal(2, result.OrderBys.Count);
            Assert.Contains(orderBy1, result.OrderBys);
            Assert.Contains(orderBy2, result.OrderBys);
        }

        /// <summary>
        /// Tests that the constructor accepts both null source and null orderBys parameters.
        /// Expected: Source is null and OrderBys is an empty list.
        /// </summary>
        [Fact]
        public void Constructor_WithNullSourceAndNullOrderBys_InitializesWithNullsAndEmptyList()
        {
            // Arrange
            SqlSegment? source = null;
            OrderByItemExpr[]? orderBys = null;

            // Act
            var result = new OrderByExpr(source, orderBys);

            // Assert
            Assert.Null(result.Source);
            Assert.NotNull(result.OrderBys);
            Assert.Empty(result.OrderBys);
        }

        /// <summary>
        /// Tests that the constructor properly handles a large number of orderBy items.
        /// Expected: Source is set and OrderBys contains all items.
        /// </summary>
        [Fact]
        public void Constructor_WithLargeNumberOfOrderBys_SetsAllItems()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var orderBys = Enumerable.Range(0, 100).Select(_ => new OrderByItemExpr()).ToArray();

            // Act
            var result = new OrderByExpr(mockSource.Object, orderBys);

            // Assert
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.OrderBys);
            Assert.Equal(100, result.OrderBys.Count);
            for (int i = 0; i < 100; i++)
            {
                Assert.Same(orderBys[i], result.OrderBys[i]);
            }
        }

        /// <summary>
        /// Tests that the OrderBys list created by the constructor is independent
        /// from the input array (modifications to the input don't affect the list).
        /// Expected: Modifying input array after construction doesn't affect OrderBys.
        /// </summary>
        [Fact]
        public void Constructor_OrderBysListIndependentFromInputArray_ModificationsDoNotAffectList()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var orderBy1 = new OrderByItemExpr();
            var orderBy2 = new OrderByItemExpr();
            var orderBysArray = new[] { orderBy1, orderBy2 };

            // Act
            var result = new OrderByExpr(mockSource.Object, orderBysArray);
            orderBysArray[0] = new OrderByItemExpr();

            // Assert
            Assert.Same(orderBy1, result.OrderBys[0]);
            Assert.Same(orderBy2, result.OrderBys[1]);
        }

        /// <summary>
        /// Tests that the constructor properly handles an array containing null elements.
        /// Expected: OrderBys list includes the null elements from the array.
        /// </summary>
        [Fact]
        public void Constructor_WithOrderByArrayContainingNulls_IncludesNullsInList()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var orderBy1 = new OrderByItemExpr();
            OrderByItemExpr? nullOrderBy = null;
            var orderBy2 = new OrderByItemExpr();

            // Act
            var result = new OrderByExpr(mockSource.Object, orderBy1, nullOrderBy, orderBy2);

            // Assert
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.OrderBys);
            Assert.Equal(3, result.OrderBys.Count);
            Assert.Same(orderBy1, result.OrderBys[0]);
            Assert.Null(result.OrderBys[1]);
            Assert.Same(orderBy2, result.OrderBys[2]);
        }

        /// <summary>
        /// Tests that the default constructor creates an instance with properly initialized properties.
        /// Verifies that OrderBys is initialized to an empty list, Source is null, and ExprType returns OrderBy.
        /// </summary>
        [Fact]
        public void OrderByExpr_DefaultConstructor_InitializesPropertiesCorrectly()
        {
            // Arrange & Act
            var orderByExpr = new OrderByExpr();

            // Assert
            Assert.NotNull(orderByExpr);
            Assert.NotNull(orderByExpr.OrderBys);
            Assert.Empty(orderByExpr.OrderBys);
            Assert.Null(orderByExpr.Source);
            Assert.Equal(ExprType.OrderBy, orderByExpr.ExprType);
        }

        /// <summary>
        /// Tests that the OrderBys list created by the default constructor is mutable.
        /// Verifies that items can be added to the list after construction.
        /// </summary>
        [Fact]
        public void OrderByExpr_DefaultConstructor_OrderBysList_IsMutable()
        {
            // Arrange
            var orderByExpr = new OrderByExpr();
            var orderByItem = Expr.Prop("Age").Asc();

            // Act
            orderByExpr.OrderBys.Add(orderByItem);

            // Assert
            Assert.Single(orderByExpr.OrderBys);
            Assert.Equal(orderByItem, orderByExpr.OrderBys[0]);
        }

        /// <summary>
        /// Tests that the instance created by the default constructor can be used in equality comparisons.
        /// Verifies that two instances created with the default constructor are equal.
        /// </summary>
        [Fact]
        public void OrderByExpr_DefaultConstructor_SupportsEqualityComparison()
        {
            // Arrange
            var orderByExpr1 = new OrderByExpr();
            var orderByExpr2 = new OrderByExpr();

            // Act
            var areEqual = orderByExpr1.Equals(orderByExpr2);

            // Assert
            Assert.True(areEqual);
        }

        /// <summary>
        /// Tests that the instance created by the default constructor can generate hash codes.
        /// Verifies that GetHashCode does not throw an exception.
        /// </summary>
        [Fact]
        public void OrderByExpr_DefaultConstructor_GeneratesHashCode()
        {
            // Arrange
            var orderByExpr = new OrderByExpr();

            // Act
            var hashCode = orderByExpr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests that the instance created by the default constructor generates a valid string representation.
        /// Verifies that ToString does not throw an exception and returns a non-null value.
        /// </summary>
        [Fact]
        public void OrderByExpr_DefaultConstructor_GeneratesValidStringRepresentation()
        {
            // Arrange
            var orderByExpr = new OrderByExpr();

            // Act
            var stringRepresentation = orderByExpr.ToString();

            // Assert
            Assert.NotNull(stringRepresentation);
        }

        /// <summary>
        /// Tests that multiple instances created by the default constructor have equal hash codes.
        /// Verifies hash code consistency for equal objects.
        /// </summary>
        [Fact]
        public void OrderByExpr_DefaultConstructor_EqualInstancesHaveEqualHashCodes()
        {
            // Arrange
            var orderByExpr1 = new OrderByExpr();
            var orderByExpr2 = new OrderByExpr();

            // Act
            var hashCode1 = orderByExpr1.GetHashCode();
            var hashCode2 = orderByExpr2.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that ToString returns correct format with non-null source and multiple order by items.
        /// Expected: "{Source} ORDER BY {OrderBy1}, {OrderBy2}"
        /// </summary>
        [Fact]
        public void ToString_WithSourceAndMultipleOrderBys_ReturnsCorrectFormat()
        {
            // Arrange
            var source = new FromExpr();
            var orderBy1 = Expr.Prop("Age").Asc();
            var orderBy2 = Expr.Prop("Name").Desc();
            var orderByExpr = new OrderByExpr(source, orderBy1, orderBy2);

            // Act
            var result = orderByExpr.ToString();

            // Assert
            Assert.Contains("ORDER BY", result);
            Assert.Contains(orderBy1.ToString(), result);
            Assert.Contains(orderBy2.ToString(), result);
            Assert.Contains(", ", result);
        }

        /// <summary>
        /// Tests that ToString returns correct format with non-null source and single order by item.
        /// Expected: "{Source} ORDER BY {OrderBy}"
        /// </summary>
        [Fact]
        public void ToString_WithSourceAndSingleOrderBy_ReturnsCorrectFormat()
        {
            // Arrange
            var source = new FromExpr();
            var orderBy = Expr.Prop("Age").Asc();
            var orderByExpr = new OrderByExpr(source, orderBy);

            // Act
            var result = orderByExpr.ToString();

            // Assert
            Assert.Contains("ORDER BY", result);
            Assert.Contains(orderBy.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString returns correct format with non-null source and empty order by list.
        /// Expected: "{Source} ORDER BY "
        /// </summary>
        [Fact]
        public void ToString_WithSourceAndEmptyOrderBys_ReturnsSourceWithOrderByClause()
        {
            // Arrange
            var source = new FromExpr();
            var orderByExpr = new OrderByExpr(source);

            // Act
            var result = orderByExpr.ToString();

            // Assert
            Assert.Contains("ORDER BY", result);
            Assert.EndsWith("ORDER BY ", result);
        }

        /// <summary>
        /// Tests that ToString returns correct format when source is null and order by items exist.
        /// Expected: " ORDER BY {OrderBy1}, {OrderBy2}"
        /// </summary>
        [Fact]
        public void ToString_WithNullSourceAndOrderBys_ReturnsOrderByClauseOnly()
        {
            // Arrange
            var orderBy1 = Expr.Prop("Age").Asc();
            var orderBy2 = Expr.Prop("Name").Desc();
            var orderByExpr = new OrderByExpr(null, orderBy1, orderBy2);

            // Act
            var result = orderByExpr.ToString();

            // Assert
            Assert.Contains("ORDER BY", result);
            Assert.Contains(orderBy1.ToString(), result);
            Assert.Contains(orderBy2.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString returns correct format when source is null and order by list is empty.
        /// Expected: " ORDER BY "
        /// </summary>
        [Fact]
        public void ToString_WithNullSourceAndEmptyOrderBys_ReturnsOrderByClauseOnly()
        {
            // Arrange
            var orderByExpr = new OrderByExpr(null);

            // Act
            var result = orderByExpr.ToString();

            // Assert
            Assert.Contains("ORDER BY", result);
        }

        /// <summary>
        /// Tests that ToString throws NullReferenceException when OrderBys property is set to null.
        /// Expected: NullReferenceException thrown
        /// </summary>
        [Fact]
        public void ToString_WithNullOrderBysList_ThrowsNullReferenceException()
        {
            // Arrange
            var source = new FromExpr();
            var orderByExpr = new OrderByExpr(source)
            {
                OrderBys = null
            };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => orderByExpr.ToString());
        }
    }
}