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
    /// Unit tests for SelectExpr class
    /// </summary>
    public partial class SelectExprTests
    {
        /// <summary>
        /// Tests the SelectExpr constructor with null source and null selects array.
        /// Verifies that Source is set to null and Selects is initialized as an empty list.
        /// </summary>
        [Fact]
        public void SelectExpr_NullSourceAndNullSelects_CreatesInstanceWithEmptySelectsList()
        {
            // Arrange
            SqlSegment? source = null;
            SelectItemExpr[]? selects = null;

            // Act
            var result = new SelectExpr(source, selects);

            // Assert
            Assert.Null(result.Source);
            Assert.NotNull(result.Selects);
            Assert.Empty(result.Selects);
        }

        /// <summary>
        /// Tests the SelectExpr constructor with null source and empty selects array.
        /// Verifies that Source is set to null and Selects is initialized as an empty list.
        /// </summary>
        [Fact]
        public void SelectExpr_NullSourceAndEmptySelects_CreatesInstanceWithEmptySelectsList()
        {
            // Arrange
            SqlSegment? source = null;
            SelectItemExpr[] selects = Array.Empty<SelectItemExpr>();

            // Act
            var result = new SelectExpr(source, selects);

            // Assert
            Assert.Null(result.Source);
            Assert.NotNull(result.Selects);
            Assert.Empty(result.Selects);
        }

        /// <summary>
        /// Tests the SelectExpr constructor with null source and non-empty selects array.
        /// Verifies that Source is set to null and Selects contains the provided items.
        /// </summary>
        [Fact]
        public void SelectExpr_NullSourceAndNonEmptySelects_CreatesInstanceWithPopulatedSelectsList()
        {
            // Arrange
            SqlSegment? source = null;
            var item1 = new SelectItemExpr(new Mock<ValueTypeExpr>().Object);
            var item2 = new SelectItemExpr(new Mock<ValueTypeExpr>().Object);
            SelectItemExpr[] selects = new[] { item1, item2 };

            // Act
            var result = new SelectExpr(source, selects);

            // Assert
            Assert.Null(result.Source);
            Assert.NotNull(result.Selects);
            Assert.Equal(2, result.Selects.Count);
            Assert.Equal(item1, result.Selects[0]);
            Assert.Equal(item2, result.Selects[1]);
        }

        /// <summary>
        /// Tests the SelectExpr constructor with valid source and null selects array.
        /// Verifies that Source is set correctly and Selects is initialized as an empty list.
        /// </summary>
        [Fact]
        public void SelectExpr_ValidSourceAndNullSelects_CreatesInstanceWithEmptySelectsList()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            SelectItemExpr[]? selects = null;

            // Act
            var result = new SelectExpr(mockSource.Object, selects);

            // Assert
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.Selects);
            Assert.Empty(result.Selects);
        }

        /// <summary>
        /// Tests the SelectExpr constructor with valid source and empty selects array.
        /// Verifies that Source is set correctly and Selects is initialized as an empty list.
        /// </summary>
        [Fact]
        public void SelectExpr_ValidSourceAndEmptySelects_CreatesInstanceWithEmptySelectsList()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            SelectItemExpr[] selects = Array.Empty<SelectItemExpr>();

            // Act
            var result = new SelectExpr(mockSource.Object, selects);

            // Assert
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.Selects);
            Assert.Empty(result.Selects);
        }

        /// <summary>
        /// Tests the SelectExpr constructor with valid source and a single select item.
        /// Verifies that Source is set correctly and Selects contains exactly one item.
        /// </summary>
        [Fact]
        public void SelectExpr_ValidSourceAndSingleSelectItem_CreatesInstanceWithSingleItem()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var selectItem = new SelectItemExpr(new Mock<ValueTypeExpr>().Object);

            // Act
            var result = new SelectExpr(mockSource.Object, selectItem);

            // Assert
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.Selects);
            Assert.Single(result.Selects);
            Assert.Same(selectItem, result.Selects[0]);
        }

        /// <summary>
        /// Tests the SelectExpr constructor with valid source and multiple select items.
        /// Verifies that Source is set correctly and Selects contains all provided items in order.
        /// </summary>
        [Fact]
        public void SelectExpr_ValidSourceAndMultipleSelectItems_CreatesInstanceWithAllItems()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var item1 = new SelectItemExpr(new Mock<ValueTypeExpr>().Object);
            var item2 = new SelectItemExpr(new Mock<ValueTypeExpr>().Object);
            var item3 = new SelectItemExpr(new Mock<ValueTypeExpr>().Object);

            // Act
            var result = new SelectExpr(mockSource.Object, item1, item2, item3);

            // Assert
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.Selects);
            Assert.Equal(3, result.Selects.Count);
            Assert.Same(item1, result.Selects[0]);
            Assert.Same(item2, result.Selects[1]);
            Assert.Same(item3, result.Selects[2]);
        }

        /// <summary>
        /// Tests that the SelectExpr constructor creates a new List instance for Selects.
        /// Verifies that modifications to the original array do not affect the Selects property.
        /// </summary>
        [Fact]
        public void SelectExpr_WithSelectsArray_CreatesNewListInstance()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var item1 = new SelectItemExpr(new Mock<ValueTypeExpr>().Object);
            var item2 = new SelectItemExpr(new Mock<ValueTypeExpr>().Object);
            var selectsArray = new[] { item1, item2 };

            // Act
            var result = new SelectExpr(mockSource.Object, selectsArray);
            selectsArray[0] = new SelectItemExpr(new Mock<ValueTypeExpr>().Object);

            // Assert
            Assert.Same(item1, result.Selects[0]);
            Assert.NotSame(selectsArray[0], result.Selects[0]);
        }

        /// <summary>
        /// Tests the SelectExpr constructor with no parameters passed to params array.
        /// Verifies that Selects is initialized as an empty list when no items are provided.
        /// </summary>
        [Fact]
        public void SelectExpr_ValidSourceAndNoParamsProvided_CreatesInstanceWithEmptySelectsList()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();

            // Act
            var result = new SelectExpr(mockSource.Object);

            // Assert
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.Selects);
            Assert.Empty(result.Selects);
        }

        /// <summary>
        /// Tests that the constructor creates SelectExpr with valid source and selects containing ValueTypeExpr instances.
        /// Verifies that non-SelectItemExpr ValueTypeExpr instances are wrapped in SelectItemExpr.
        /// </summary>
        [Fact]
        public void SelectExpr_Constructor_WithSourceAndValueTypeExprSelects_WrapsInSelectItemExpr()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var mockValue1 = new Mock<ValueTypeExpr>();
            var mockValue2 = new Mock<ValueTypeExpr>();

            // Act
            var result = new SelectExpr(mockSource.Object, mockValue1.Object, mockValue2.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.Selects);
            Assert.Equal(2, result.Selects.Count);
            Assert.All(result.Selects, item => Assert.IsType<SelectItemExpr>(item));
            Assert.Same(mockValue1.Object, result.Selects[0].Value);
            Assert.Same(mockValue2.Object, result.Selects[1].Value);
        }

        /// <summary>
        /// Tests that the constructor preserves SelectItemExpr instances without wrapping them again.
        /// Verifies that when selects contains SelectItemExpr, they are kept as-is.
        /// </summary>
        [Fact]
        public void SelectExpr_Constructor_WithSourceAndSelectItemExprSelects_PreservesSelectItemExpr()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var mockValue = new Mock<ValueTypeExpr>();
            var selectItem = new SelectItemExpr(mockValue.Object);

            // Act
            var result = new SelectExpr(mockSource.Object, selectItem);

            // Assert
            Assert.NotNull(result);
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.Selects);
            Assert.Single(result.Selects);
            Assert.Same(selectItem, result.Selects[0]);
        }

        /// <summary>
        /// Tests that the constructor correctly handles a mix of SelectItemExpr and other ValueTypeExpr.
        /// Verifies that SelectItemExpr are preserved and other ValueTypeExpr are wrapped.
        /// </summary>
        [Fact]
        public void SelectExpr_Constructor_WithMixedSelectTypes_HandlesCorrectly()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var mockValue1 = new Mock<ValueTypeExpr>();
            var selectItem = new SelectItemExpr(new Mock<ValueTypeExpr>().Object);
            var mockValue2 = new Mock<ValueTypeExpr>();

            // Act
            var result = new SelectExpr(mockSource.Object, mockValue1.Object, selectItem, mockValue2.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Same(mockSource.Object, result.Source);
            Assert.Equal(3, result.Selects.Count);
            Assert.IsType<SelectItemExpr>(result.Selects[0]);
            Assert.Same(mockValue1.Object, result.Selects[0].Value);
            Assert.Same(selectItem, result.Selects[1]);
            Assert.IsType<SelectItemExpr>(result.Selects[2]);
            Assert.Same(mockValue2.Object, result.Selects[2].Value);
        }

        /// <summary>
        /// Tests that the constructor creates empty Selects list when selects parameter is null.
        /// Verifies null handling for the params array.
        /// </summary>
        [Fact]
        public void SelectExpr_Constructor_WithNullSelects_CreatesEmptyList()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();

            // Act
            var result = new SelectExpr(mockSource.Object, (ValueTypeExpr[])null);

            // Assert
            Assert.NotNull(result);
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.Selects);
            Assert.Empty(result.Selects);
        }

        /// <summary>
        /// Tests that the constructor creates empty Selects list when no selects are provided.
        /// Verifies that params array with no arguments results in empty list.
        /// </summary>
        [Fact]
        public void SelectExpr_Constructor_WithNoSelects_CreatesEmptyList()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();

            // Act
            var result = new SelectExpr(mockSource.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.Selects);
            Assert.Empty(result.Selects);
        }

        /// <summary>
        /// Tests that the constructor creates empty Selects list when selects is an empty array.
        /// Verifies edge case of empty collection.
        /// </summary>
        [Fact]
        public void SelectExpr_Constructor_WithEmptySelectsArray_CreatesEmptyList()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var emptySelects = new ValueTypeExpr[0];

            // Act
            var result = new SelectExpr(mockSource.Object, emptySelects);

            // Assert
            Assert.NotNull(result);
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.Selects);
            Assert.Empty(result.Selects);
        }

        /// <summary>
        /// Tests that the constructor accepts null source parameter.
        /// Verifies that null source is assigned without validation.
        /// </summary>
        [Fact]
        public void SelectExpr_Constructor_WithNullSource_AssignsNullSource()
        {
            // Arrange
            var mockValue = new Mock<ValueTypeExpr>();

            // Act
            var result = new SelectExpr(null, mockValue.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Null(result.Source);
            Assert.NotNull(result.Selects);
            Assert.Single(result.Selects);
        }

        /// <summary>
        /// Tests that the constructor throws ArgumentNullException when selects contains null element.
        /// Verifies that null elements in the selects array are not allowed.
        /// </summary>
        [Fact]
        public void SelectExpr_Constructor_WithNullElementInSelects_ThrowsArgumentNullException()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var selectsWithNull = new ValueTypeExpr[] { new Mock<ValueTypeExpr>().Object, null, new Mock<ValueTypeExpr>().Object };

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new SelectExpr(mockSource.Object, selectsWithNull));
            Assert.Equal("value", exception.ParamName);
        }

        /// <summary>
        /// Tests that the constructor throws ArgumentNullException when selects contains only a single null element.
        /// Verifies null validation for single null element case.
        /// </summary>
        [Fact]
        public void SelectExpr_Constructor_WithSingleNullElement_ThrowsArgumentNullException()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new SelectExpr(mockSource.Object, (ValueTypeExpr)null));
            Assert.Equal("value", exception.ParamName);
        }

        /// <summary>
        /// Tests that the constructor correctly handles a single ValueTypeExpr select.
        /// Verifies single element edge case.
        /// </summary>
        [Fact]
        public void SelectExpr_Constructor_WithSingleSelect_CreatesListWithOneItem()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var mockValue = new Mock<ValueTypeExpr>();

            // Act
            var result = new SelectExpr(mockSource.Object, mockValue.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.Selects);
            Assert.Single(result.Selects);
            Assert.Same(mockValue.Object, result.Selects[0].Value);
        }

        /// <summary>
        /// Tests that the constructor handles a large number of selects correctly.
        /// Verifies that the constructor scales with many elements.
        /// </summary>
        [Fact]
        public void SelectExpr_Constructor_WithManySelects_CreatesCorrectList()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            var selects = Enumerable.Range(0, 100).Select(_ => new Mock<ValueTypeExpr>().Object).ToArray();

            // Act
            var result = new SelectExpr(mockSource.Object, selects);

            // Assert
            Assert.NotNull(result);
            Assert.Same(mockSource.Object, result.Source);
            Assert.NotNull(result.Selects);
            Assert.Equal(100, result.Selects.Count);
            Assert.All(result.Selects, item => Assert.IsType<SelectItemExpr>(item));
        }

        /// <summary>
        /// Tests that Clone creates a new instance with all properties copied when Source is null.
        /// Input: SelectExpr with null Source
        /// Expected: Cloned instance is a new object with Source set to null
        /// </summary>
        [Fact]
        public void Clone_WithNullSource_CreatesNewInstanceWithNullSource()
        {
            // Arrange
            var original = new SelectExpr
            {
                Source = null,
                Alias = "TestAlias",
                Selects = new List<SelectItemExpr> { new SelectItemExpr(Expr.Const(1), "Col1") },
                SetType = SelectSetType.Union
            };

            // Act
            var cloned = (SelectExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.Null(cloned.Source);
            Assert.Equal(original.Alias, cloned.Alias);
            Assert.Equal(original.SetType, cloned.SetType);
            Assert.NotSame(original.Selects, cloned.Selects);
            Assert.Single(cloned.Selects);
        }

        /// <summary>
        /// Tests that Clone creates a deep copy when Source is an Expr.
        /// Input: SelectExpr with Source that is an Expr
        /// Expected: Cloned instance has a cloned Source (different object reference)
        /// </summary>
        [Fact]
        public void Clone_WithExprSource_ClonesSourceExpr()
        {
            // Arrange
            var sourceExpr = new FromExpr(typeof(string));
            var original = new SelectExpr
            {
                Source = sourceExpr,
                Alias = "TestAlias"
            };

            // Act
            var cloned = (SelectExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.NotNull(cloned.Source);
            Assert.NotSame(original.Source, cloned.Source);
            Assert.Equal(original.Source.ExprType, cloned.Source.ExprType);
        }

        /// <summary>
        /// Tests that Clone copies Alias property correctly.
        /// Input: SelectExpr with null Alias
        /// Expected: Cloned instance has null Alias
        /// </summary>
        [Fact]
        public void Clone_WithNullAlias_CopiesNullAlias()
        {
            // Arrange
            var original = new SelectExpr
            {
                Source = new FromExpr(typeof(int)),
                Alias = null
            };

            // Act
            var cloned = (SelectExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.Null(cloned.Alias);
        }

        /// <summary>
        /// Tests that Clone copies Alias property correctly when it has a value.
        /// Input: SelectExpr with non-null Alias
        /// Expected: Cloned instance has the same Alias value
        /// </summary>
        [Fact]
        public void Clone_WithNonNullAlias_CopiesAliasValue()
        {
            // Arrange
            var original = new SelectExpr
            {
                Source = new FromExpr(typeof(int)),
                Alias = "MyAlias"
            };

            // Act
            var cloned = (SelectExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.Equal("MyAlias", cloned.Alias);
        }

        /// <summary>
        /// Tests that Clone handles null Selects list.
        /// Input: SelectExpr with null Selects
        /// Expected: Cloned instance has empty Selects list
        /// </summary>
        [Fact]
        public void Clone_WithNullSelects_CreatesEmptySelectsList()
        {
            // Arrange
            var original = new SelectExpr
            {
                Source = new FromExpr(typeof(int)),
                Selects = null
            };

            // Act
            var cloned = (SelectExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotNull(cloned.Selects);
            Assert.Empty(cloned.Selects);
        }

        /// <summary>
        /// Tests that Clone handles empty Selects list.
        /// Input: SelectExpr with empty Selects list
        /// Expected: Cloned instance has empty Selects list
        /// </summary>
        [Fact]
        public void Clone_WithEmptySelects_CreatesEmptySelectsList()
        {
            // Arrange
            var original = new SelectExpr
            {
                Source = new FromExpr(typeof(int)),
                Selects = new List<SelectItemExpr>()
            };

            // Act
            var cloned = (SelectExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotNull(cloned.Selects);
            Assert.Empty(cloned.Selects);
            Assert.NotSame(original.Selects, cloned.Selects);
        }

        /// <summary>
        /// Tests that Clone creates deep copies of all SelectItemExpr in Selects list.
        /// Input: SelectExpr with multiple SelectItemExpr
        /// Expected: Cloned instance has cloned SelectItemExpr (different object references)
        /// </summary>
        [Fact]
        public void Clone_WithMultipleSelects_ClonesAllSelectItems()
        {
            // Arrange
            var selectItem1 = new SelectItemExpr(Expr.Const(1), "Col1");
            var selectItem2 = new SelectItemExpr(Expr.Prop("Name"), "Col2");
            var selectItem3 = new SelectItemExpr(Expr.Const("test"));
            var original = new SelectExpr
            {
                Source = new FromExpr(typeof(int)),
                Selects = new List<SelectItemExpr> { selectItem1, selectItem2, selectItem3 }
            };

            // Act
            var cloned = (SelectExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original.Selects, cloned.Selects);
            Assert.Equal(3, cloned.Selects.Count);
            for (int i = 0; i < original.Selects.Count; i++)
            {
                Assert.NotSame(original.Selects[i], cloned.Selects[i]);
                Assert.Equal(original.Selects[i].Alias, cloned.Selects[i].Alias);
            }
        }

        /// <summary>
        /// Tests that Clone copies SetType enum property correctly for all enum values.
        /// Input: SelectExpr with different SetType values
        /// Expected: Cloned instance has the same SetType value
        /// </summary>
        [Theory]
        [InlineData(SelectSetType.UnionAll)]
        [InlineData(SelectSetType.Union)]
        [InlineData(SelectSetType.Intersect)]
        [InlineData(SelectSetType.Except)]
        public void Clone_WithDifferentSetTypes_CopiesSetTypeCorrectly(SelectSetType setType)
        {
            // Arrange
            var original = new SelectExpr
            {
                Source = new FromExpr(typeof(int)),
                SetType = setType
            };

            // Act
            var cloned = (SelectExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.Equal(setType, cloned.SetType);
        }

        /// <summary>
        /// Tests that Clone does not clone _nextSelects when it is null.
        /// Input: SelectExpr with null _nextSelects (default state)
        /// Expected: Cloned instance has null _nextSelects field
        /// </summary>
        [Fact]
        public void Clone_WithNullNextSelects_DoesNotCloneNextSelects()
        {
            // Arrange
            var original = new SelectExpr
            {
                Source = new FromExpr(typeof(int)),
                Alias = "TestAlias"
            };
            // _nextSelects is null by default

            // Act
            var cloned = (SelectExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
        }

        /// <summary>
        /// Tests that Clone creates deep copies of all SelectExpr in NextSelects list.
        /// Input: SelectExpr with NextSelects containing multiple SelectExpr
        /// Expected: Cloned instance has cloned NextSelects with cloned SelectExpr items
        /// </summary>
        [Fact]
        public void Clone_WithNextSelects_ClonesAllNextSelectItems()
        {
            // Arrange
            var nextSelect1 = new SelectExpr(new FromExpr(typeof(int)), Expr.Const(1));
            nextSelect1.SetType = SelectSetType.Union;
            var nextSelect2 = new SelectExpr(new FromExpr(typeof(string)), Expr.Const(2));
            nextSelect2.SetType = SelectSetType.Intersect;

            var original = new SelectExpr
            {
                Source = new FromExpr(typeof(int)),
                Alias = "TestAlias"
            };
            original.NextSelects.Add(nextSelect1);
            original.NextSelects.Add(nextSelect2);

            // Act
            var cloned = (SelectExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);
            Assert.NotNull(cloned.NextSelects);
            Assert.Equal(2, cloned.NextSelects.Count);
            Assert.NotSame(original.NextSelects[0], cloned.NextSelects[0]);
            Assert.NotSame(original.NextSelects[1], cloned.NextSelects[1]);
            Assert.Equal(SelectSetType.Union, cloned.NextSelects[0].SetType);
            Assert.Equal(SelectSetType.Intersect, cloned.NextSelects[1].SetType);
        }

        /// <summary>
        /// Tests that Clone creates a complete deep copy with all properties set.
        /// Input: SelectExpr with all properties set including nested NextSelects
        /// Expected: Cloned instance is a complete deep copy with all properties matching but different object references
        /// </summary>
        [Fact]
        public void Clone_WithAllPropertiesSet_CreatesCompleteDeepCopy()
        {
            // Arrange
            var selectItem1 = new SelectItemExpr(Expr.Const(1), "Col1");
            var selectItem2 = new SelectItemExpr(Expr.Prop("Name"), "Col2");
            var nextSelect = new SelectExpr(new FromExpr(typeof(string)), Expr.Const("test"));
            nextSelect.SetType = SelectSetType.Except;

            var original = new SelectExpr
            {
                Source = new FromExpr(typeof(int)),
                Alias = "MainQuery",
                Selects = new List<SelectItemExpr> { selectItem1, selectItem2 },
                SetType = SelectSetType.UnionAll
            };
            original.NextSelects.Add(nextSelect);

            // Act
            var cloned = (SelectExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.NotSame(original, cloned);

            // Verify Source is cloned
            Assert.NotNull(cloned.Source);
            Assert.NotSame(original.Source, cloned.Source);

            // Verify Alias is copied
            Assert.Equal("MainQuery", cloned.Alias);

            // Verify Selects is deep cloned
            Assert.NotSame(original.Selects, cloned.Selects);
            Assert.Equal(2, cloned.Selects.Count);
            Assert.NotSame(original.Selects[0], cloned.Selects[0]);
            Assert.NotSame(original.Selects[1], cloned.Selects[1]);

            // Verify SetType is copied
            Assert.Equal(SelectSetType.UnionAll, cloned.SetType);

            // Verify NextSelects is deep cloned
            Assert.Single(cloned.NextSelects);
            Assert.NotSame(original.NextSelects[0], cloned.NextSelects[0]);
            Assert.Equal(SelectSetType.Except, cloned.NextSelects[0].SetType);
        }

        /// <summary>
        /// Tests that Clone creates independent copies that don't share references.
        /// Input: SelectExpr with Selects list
        /// Expected: Modifying the cloned instance does not affect the original
        /// </summary>
        [Fact]
        public void Clone_ModifyingClonedInstance_DoesNotAffectOriginal()
        {
            // Arrange
            var selectItem = new SelectItemExpr(Expr.Const(1), "Col1");
            var original = new SelectExpr
            {
                Source = new FromExpr(typeof(int)),
                Alias = "Original",
                Selects = new List<SelectItemExpr> { selectItem }
            };

            // Act
            var cloned = (SelectExpr)original.Clone();
            cloned.Alias = "Modified";
            cloned.Selects.Add(new SelectItemExpr(Expr.Const(2), "Col2"));
            cloned.SetType = SelectSetType.Intersect;

            // Assert
            Assert.Equal("Original", original.Alias);
            Assert.Single(original.Selects);
            Assert.NotEqual(original.SetType, cloned.SetType);
        }

        /// <summary>
        /// Tests that Clone handles nested NextSelects with multiple levels.
        /// Input: SelectExpr with NextSelects that also have NextSelects
        /// Expected: All levels are properly cloned
        /// </summary>
        [Fact]
        public void Clone_WithNestedNextSelects_ClonesAllLevels()
        {
            // Arrange
            var innerNextSelect = new SelectExpr(new FromExpr(typeof(double)), Expr.Const(3.14));
            innerNextSelect.SetType = SelectSetType.Union;

            var outerNextSelect = new SelectExpr(new FromExpr(typeof(string)), Expr.Const("test"));
            outerNextSelect.SetType = SelectSetType.Intersect;
            outerNextSelect.NextSelects.Add(innerNextSelect);

            var original = new SelectExpr
            {
                Source = new FromExpr(typeof(int)),
                Alias = "MainQuery"
            };
            original.NextSelects.Add(outerNextSelect);

            // Act
            var cloned = (SelectExpr)original.Clone();

            // Assert
            Assert.NotNull(cloned);
            Assert.Single(cloned.NextSelects);
            Assert.NotSame(original.NextSelects[0], cloned.NextSelects[0]);
            Assert.Single(cloned.NextSelects[0].NextSelects);
            Assert.NotSame(original.NextSelects[0].NextSelects[0], cloned.NextSelects[0].NextSelects[0]);
            Assert.Equal(SelectSetType.Union, cloned.NextSelects[0].NextSelects[0].SetType);
        }

        /// <summary>
        /// Tests that the ExprType property returns the Select enum value.
        /// </summary>
        [Fact]
        public void ExprType_WhenCalled_ReturnsSelect()
        {
            // Arrange
            var selectExpr = new SelectExpr();

            // Act
            var result = selectExpr.ExprType;

            // Assert
            Assert.Equal(ExprType.Select, result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing a SelectExpr instance with itself (same reference).
        /// </summary>
        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr();
            var selectExpr = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id")));

            // Act
            var result = selectExpr.Equals(selectExpr);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing two SelectExpr instances with identical values.
        /// </summary>
        [Fact]
        public void Equals_IdenticalValues_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr();
            var select1 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id")), new SelectItemExpr(Expr.Prop("Name")));
            var select2 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id")), new SelectItemExpr(Expr.Prop("Name")));

            // Act
            var result = select1.Equals(select2);

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
            var source = new FromExpr();
            var selectExpr = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id")));

            // Act
            var result = selectExpr.Equals(null);

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
            var source = new FromExpr();
            var selectExpr = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id")));
            var differentObject = new object();

            // Act
            var result = selectExpr.Equals(differentObject);

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
            var source1 = new FromExpr();
            var source2 = new FromExpr();
            var select1 = new SelectExpr(source1, new SelectItemExpr(Expr.Prop("Id")));
            var select2 = new SelectExpr(source2, new SelectItemExpr(Expr.Prop("Id")));

            // Act
            var result = select1.Equals(select2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when Selects lists have different items.
        /// </summary>
        [Fact]
        public void Equals_DifferentSelects_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var select1 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id")));
            var select2 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Name")));

            // Act
            var result = select1.Equals(select2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when Selects lists have same items but in different order.
        /// SequenceEqual is order-sensitive.
        /// </summary>
        [Fact]
        public void Equals_SelectsInDifferentOrder_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var select1 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id")), new SelectItemExpr(Expr.Prop("Name")));
            var select2 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Name")), new SelectItemExpr(Expr.Prop("Id")));

            // Act
            var result = select1.Equals(select2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when Selects lists have different counts.
        /// </summary>
        [Fact]
        public void Equals_SelectsWithDifferentCounts_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var select1 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id")));
            var select2 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id")), new SelectItemExpr(Expr.Prop("Name")));

            // Act
            var result = select1.Equals(select2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when Alias properties differ.
        /// </summary>
        [Fact]
        public void Equals_DifferentAlias_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var select1 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id"))) { Alias = "Alias1" };
            var select2 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id"))) { Alias = "Alias2" };

            // Act
            var result = select1.Equals(select2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have the same Alias.
        /// </summary>
        [Fact]
        public void Equals_SameAlias_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr();
            var select1 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id"))) { Alias = "MyAlias" };
            var select2 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id"))) { Alias = "MyAlias" };

            // Act
            var result = select1.Equals(select2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null Alias.
        /// </summary>
        [Fact]
        public void Equals_BothAliasNull_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr();
            var select1 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id"))) { Alias = null };
            var select2 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id"))) { Alias = null };

            // Act
            var result = select1.Equals(select2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one Alias is null and the other is not.
        /// </summary>
        [Fact]
        public void Equals_OneAliasNullOtherNot_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var select1 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id"))) { Alias = null };
            var select2 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id"))) { Alias = "MyAlias" };

            // Act
            var result = select1.Equals(select2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one Alias is empty and the other is null.
        /// </summary>
        [Fact]
        public void Equals_EmptyAliasVsNullAlias_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var select1 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id"))) { Alias = "" };
            var select2 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id"))) { Alias = null };

            // Act
            var result = select1.Equals(select2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have empty Selects lists.
        /// </summary>
        [Fact]
        public void Equals_BothEmptySelects_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr();
            var select1 = new SelectExpr { Source = source };
            var select2 = new SelectExpr { Source = source };

            // Act
            var result = select1.Equals(select2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one Selects list is empty and the other is not.
        /// </summary>
        [Fact]
        public void Equals_OneEmptySelectsOtherNot_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var select1 = new SelectExpr { Source = source };
            var select2 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id")));

            // Act
            var result = select1.Equals(select2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null Source.
        /// </summary>
        [Fact]
        public void Equals_BothSourceNull_ReturnsTrue()
        {
            // Arrange
            var select1 = new SelectExpr { Source = null };
            var select2 = new SelectExpr { Source = null };

            // Act
            var result = select1.Equals(select2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one Source is null and the other is not.
        /// </summary>
        [Fact]
        public void Equals_OneSourceNullOtherNot_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var select1 = new SelectExpr { Source = null };
            var select2 = new SelectExpr { Source = source };

            // Act
            var result = select1.Equals(select2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true for complex SelectExpr instances with multiple Selects and Alias.
        /// </summary>
        [Fact]
        public void Equals_ComplexInstancesWithMultipleSelects_ReturnsTrue()
        {
            // Arrange
            var source = new FromExpr();
            var select1 = new SelectExpr(source,
                new SelectItemExpr(Expr.Prop("Id"), "UserId"),
                new SelectItemExpr(Expr.Prop("Name"), "UserName"),
                new SelectItemExpr(Expr.Prop("Age")))
            { Alias = "UserData" };

            var select2 = new SelectExpr(source,
                new SelectItemExpr(Expr.Prop("Id"), "UserId"),
                new SelectItemExpr(Expr.Prop("Name"), "UserName"),
                new SelectItemExpr(Expr.Prop("Age")))
            { Alias = "UserData" };

            // Act
            var result = select1.Equals(select2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with a string object.
        /// </summary>
        [Fact]
        public void Equals_WithStringObject_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var selectExpr = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id")));
            var stringObject = "not a SelectExpr";

            // Act
            var result = selectExpr.Equals(stringObject);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with an integer object.
        /// </summary>
        [Fact]
        public void Equals_WithIntegerObject_ReturnsFalse()
        {
            // Arrange
            var source = new FromExpr();
            var selectExpr = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id")));
            object intObject = 42;

            // Act
            var result = selectExpr.Equals(intObject);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals properly compares Alias with whitespace strings.
        /// </summary>
        [Fact]
        public void Equals_AliasWithWhitespace_ReturnsCorrectResult()
        {
            // Arrange
            var source = new FromExpr();
            var select1 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id"))) { Alias = "  " };
            var select2 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id"))) { Alias = "  " };
            var select3 = new SelectExpr(source, new SelectItemExpr(Expr.Prop("Id"))) { Alias = " " };

            // Act & Assert
            Assert.True(select1.Equals(select2));
            Assert.False(select1.Equals(select3));
        }

        /// <summary>
        /// Tests that ToString returns correct SQL SELECT statement with basic source and selects without alias.
        /// </summary>
        [Fact]
        public void ToString_WithBasicSelectNoAlias_ReturnsCorrectSqlString()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            mockSource.Setup(s => s.ToString()).Returns("TestTable");

            var mockSelect1 = new Mock<SelectItemExpr>();
            mockSelect1.Setup(s => s.ToString()).Returns("Column1");

            var mockSelect2 = new Mock<SelectItemExpr>();
            mockSelect2.Setup(s => s.ToString()).Returns("Column2");

            var selectExpr = new SelectExpr
            {
                Source = mockSource.Object,
                Selects = new List<SelectItemExpr> { mockSelect1.Object, mockSelect2.Object }
            };

            // Act
            var result = selectExpr.ToString();

            // Assert
            Assert.Equal("SELECT Column1, Column2 FROM TestTable", result);
        }

        /// <summary>
        /// Tests that ToString includes AS clause when Alias is a non-empty string.
        /// </summary>
        [Fact]
        public void ToString_WithNonEmptyAlias_IncludesAsClause()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            mockSource.Setup(s => s.ToString()).Returns("TestTable");

            var mockSelect = new Mock<SelectItemExpr>();
            mockSelect.Setup(s => s.ToString()).Returns("Column1");

            var selectExpr = new SelectExpr
            {
                Source = mockSource.Object,
                Selects = new List<SelectItemExpr> { mockSelect.Object },
                Alias = "T1"
            };

            // Act
            var result = selectExpr.ToString();

            // Assert
            Assert.Equal("SELECT Column1 FROM TestTable AS T1", result);
        }

        /// <summary>
        /// Tests that ToString does not include AS clause when Alias is null.
        /// </summary>
        [Fact]
        public void ToString_WithNullAlias_DoesNotIncludeAsClause()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            mockSource.Setup(s => s.ToString()).Returns("TestTable");

            var mockSelect = new Mock<SelectItemExpr>();
            mockSelect.Setup(s => s.ToString()).Returns("Column1");

            var selectExpr = new SelectExpr
            {
                Source = mockSource.Object,
                Selects = new List<SelectItemExpr> { mockSelect.Object },
                Alias = null
            };

            // Act
            var result = selectExpr.ToString();

            // Assert
            Assert.Equal("SELECT Column1 FROM TestTable", result);
            Assert.DoesNotContain(" AS ", result);
        }

        /// <summary>
        /// Tests that ToString does not include AS clause when Alias is empty string.
        /// </summary>
        [Fact]
        public void ToString_WithEmptyAlias_DoesNotIncludeAsClause()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            mockSource.Setup(s => s.ToString()).Returns("TestTable");

            var mockSelect = new Mock<SelectItemExpr>();
            mockSelect.Setup(s => s.ToString()).Returns("Column1");

            var selectExpr = new SelectExpr
            {
                Source = mockSource.Object,
                Selects = new List<SelectItemExpr> { mockSelect.Object },
                Alias = string.Empty
            };

            // Act
            var result = selectExpr.ToString();

            // Assert
            Assert.Equal("SELECT Column1 FROM TestTable", result);
            Assert.DoesNotContain(" AS ", result);
        }

        /// <summary>
        /// Tests that ToString includes AS clause when Alias contains only whitespace.
        /// </summary>
        [Fact]
        public void ToString_WithWhitespaceAlias_IncludesAsClause()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            mockSource.Setup(s => s.ToString()).Returns("TestTable");

            var mockSelect = new Mock<SelectItemExpr>();
            mockSelect.Setup(s => s.ToString()).Returns("Column1");

            var selectExpr = new SelectExpr
            {
                Source = mockSource.Object,
                Selects = new List<SelectItemExpr> { mockSelect.Object },
                Alias = "   "
            };

            // Act
            var result = selectExpr.ToString();

            // Assert
            Assert.Equal("SELECT Column1 FROM TestTable AS    ", result);
        }

        /// <summary>
        /// Tests that ToString correctly appends UNION operator for Union SetType.
        /// </summary>
        [Fact]
        public void ToString_WithNextSelectUnion_AppendsUnionOperator()
        {
            // Arrange
            var mockSource1 = new Mock<SqlSegment>();
            mockSource1.Setup(s => s.ToString()).Returns("Table1");

            var mockSource2 = new Mock<SqlSegment>();
            mockSource2.Setup(s => s.ToString()).Returns("Table2");

            var mockSelect1 = new Mock<SelectItemExpr>();
            mockSelect1.Setup(s => s.ToString()).Returns("Col1");

            var mockSelect2 = new Mock<SelectItemExpr>();
            mockSelect2.Setup(s => s.ToString()).Returns("Col2");

            var selectExpr1 = new SelectExpr
            {
                Source = mockSource1.Object,
                Selects = new List<SelectItemExpr> { mockSelect1.Object }
            };

            var selectExpr2 = new SelectExpr
            {
                Source = mockSource2.Object,
                Selects = new List<SelectItemExpr> { mockSelect2.Object },
                SetType = SelectSetType.Union
            };

            selectExpr1.NextSelects.Add(selectExpr2);

            // Act
            var result = selectExpr1.ToString();

            // Assert
            Assert.Contains("UNION", result);
            Assert.Equal("SELECT Col1 FROM Table1 UNION SELECT Col2 FROM Table2", result);
        }

        /// <summary>
        /// Tests that ToString correctly appends UNION ALL operator for UnionAll SetType.
        /// </summary>
        [Fact]
        public void ToString_WithNextSelectUnionAll_AppendsUnionAllOperator()
        {
            // Arrange
            var mockSource1 = new Mock<SqlSegment>();
            mockSource1.Setup(s => s.ToString()).Returns("Table1");

            var mockSource2 = new Mock<SqlSegment>();
            mockSource2.Setup(s => s.ToString()).Returns("Table2");

            var mockSelect1 = new Mock<SelectItemExpr>();
            mockSelect1.Setup(s => s.ToString()).Returns("Col1");

            var mockSelect2 = new Mock<SelectItemExpr>();
            mockSelect2.Setup(s => s.ToString()).Returns("Col2");

            var selectExpr1 = new SelectExpr
            {
                Source = mockSource1.Object,
                Selects = new List<SelectItemExpr> { mockSelect1.Object }
            };

            var selectExpr2 = new SelectExpr
            {
                Source = mockSource2.Object,
                Selects = new List<SelectItemExpr> { mockSelect2.Object },
                SetType = SelectSetType.UnionAll
            };

            selectExpr1.NextSelects.Add(selectExpr2);

            // Act
            var result = selectExpr1.ToString();

            // Assert
            Assert.Contains("UNION ALL", result);
            Assert.Equal("SELECT Col1 FROM Table1 UNION ALL SELECT Col2 FROM Table2", result);
        }

        /// <summary>
        /// Tests that ToString correctly appends INTERSECT operator for Intersect SetType.
        /// </summary>
        [Fact]
        public void ToString_WithNextSelectIntersect_AppendsIntersectOperator()
        {
            // Arrange
            var mockSource1 = new Mock<SqlSegment>();
            mockSource1.Setup(s => s.ToString()).Returns("Table1");

            var mockSource2 = new Mock<SqlSegment>();
            mockSource2.Setup(s => s.ToString()).Returns("Table2");

            var mockSelect1 = new Mock<SelectItemExpr>();
            mockSelect1.Setup(s => s.ToString()).Returns("Col1");

            var mockSelect2 = new Mock<SelectItemExpr>();
            mockSelect2.Setup(s => s.ToString()).Returns("Col2");

            var selectExpr1 = new SelectExpr
            {
                Source = mockSource1.Object,
                Selects = new List<SelectItemExpr> { mockSelect1.Object }
            };

            var selectExpr2 = new SelectExpr
            {
                Source = mockSource2.Object,
                Selects = new List<SelectItemExpr> { mockSelect2.Object },
                SetType = SelectSetType.Intersect
            };

            selectExpr1.NextSelects.Add(selectExpr2);

            // Act
            var result = selectExpr1.ToString();

            // Assert
            Assert.Contains("INTERSECT", result);
            Assert.Equal("SELECT Col1 FROM Table1 INTERSECT SELECT Col2 FROM Table2", result);
        }

        /// <summary>
        /// Tests that ToString correctly appends EXCEPT operator for Except SetType.
        /// </summary>
        [Fact]
        public void ToString_WithNextSelectExcept_AppendsExceptOperator()
        {
            // Arrange
            var mockSource1 = new Mock<SqlSegment>();
            mockSource1.Setup(s => s.ToString()).Returns("Table1");

            var mockSource2 = new Mock<SqlSegment>();
            mockSource2.Setup(s => s.ToString()).Returns("Table2");

            var mockSelect1 = new Mock<SelectItemExpr>();
            mockSelect1.Setup(s => s.ToString()).Returns("Col1");

            var mockSelect2 = new Mock<SelectItemExpr>();
            mockSelect2.Setup(s => s.ToString()).Returns("Col2");

            var selectExpr1 = new SelectExpr
            {
                Source = mockSource1.Object,
                Selects = new List<SelectItemExpr> { mockSelect1.Object }
            };

            var selectExpr2 = new SelectExpr
            {
                Source = mockSource2.Object,
                Selects = new List<SelectItemExpr> { mockSelect2.Object },
                SetType = SelectSetType.Except
            };

            selectExpr1.NextSelects.Add(selectExpr2);

            // Act
            var result = selectExpr1.ToString();

            // Assert
            Assert.Contains("EXCEPT", result);
            Assert.Equal("SELECT Col1 FROM Table1 EXCEPT SELECT Col2 FROM Table2", result);
        }

        /// <summary>
        /// Tests that ToString defaults to UNION operator for undefined SetType values.
        /// </summary>
        [Fact]
        public void ToString_WithUndefinedSetType_DefaultsToUnionOperator()
        {
            // Arrange
            var mockSource1 = new Mock<SqlSegment>();
            mockSource1.Setup(s => s.ToString()).Returns("Table1");

            var mockSource2 = new Mock<SqlSegment>();
            mockSource2.Setup(s => s.ToString()).Returns("Table2");

            var mockSelect1 = new Mock<SelectItemExpr>();
            mockSelect1.Setup(s => s.ToString()).Returns("Col1");

            var mockSelect2 = new Mock<SelectItemExpr>();
            mockSelect2.Setup(s => s.ToString()).Returns("Col2");

            var selectExpr1 = new SelectExpr
            {
                Source = mockSource1.Object,
                Selects = new List<SelectItemExpr> { mockSelect1.Object }
            };

            var selectExpr2 = new SelectExpr
            {
                Source = mockSource2.Object,
                Selects = new List<SelectItemExpr> { mockSelect2.Object },
                SetType = (SelectSetType)999
            };

            selectExpr1.NextSelects.Add(selectExpr2);

            // Act
            var result = selectExpr1.ToString();

            // Assert
            Assert.Contains("UNION", result);
            Assert.DoesNotContain("UNION ALL", result);
            Assert.DoesNotContain("INTERSECT", result);
            Assert.DoesNotContain("EXCEPT", result);
        }

        /// <summary>
        /// Tests that ToString correctly chains multiple NextSelects with different set operators.
        /// </summary>
        [Fact]
        public void ToString_WithMultipleNextSelects_ChainsAllOperators()
        {
            // Arrange
            var mockSource1 = new Mock<SqlSegment>();
            mockSource1.Setup(s => s.ToString()).Returns("T1");

            var mockSource2 = new Mock<SqlSegment>();
            mockSource2.Setup(s => s.ToString()).Returns("T2");

            var mockSource3 = new Mock<SqlSegment>();
            mockSource3.Setup(s => s.ToString()).Returns("T3");

            var mockSelect1 = new Mock<SelectItemExpr>();
            mockSelect1.Setup(s => s.ToString()).Returns("A");

            var mockSelect2 = new Mock<SelectItemExpr>();
            mockSelect2.Setup(s => s.ToString()).Returns("B");

            var mockSelect3 = new Mock<SelectItemExpr>();
            mockSelect3.Setup(s => s.ToString()).Returns("C");

            var selectExpr1 = new SelectExpr
            {
                Source = mockSource1.Object,
                Selects = new List<SelectItemExpr> { mockSelect1.Object }
            };

            var selectExpr2 = new SelectExpr
            {
                Source = mockSource2.Object,
                Selects = new List<SelectItemExpr> { mockSelect2.Object },
                SetType = SelectSetType.Union
            };

            var selectExpr3 = new SelectExpr
            {
                Source = mockSource3.Object,
                Selects = new List<SelectItemExpr> { mockSelect3.Object },
                SetType = SelectSetType.Intersect
            };

            selectExpr1.NextSelects.Add(selectExpr2);
            selectExpr1.NextSelects.Add(selectExpr3);

            // Act
            var result = selectExpr1.ToString();

            // Assert
            Assert.Equal("SELECT A FROM T1 UNION SELECT B FROM T2 INTERSECT SELECT C FROM T3", result);
        }

        /// <summary>
        /// Tests that ToString works correctly with empty Selects list.
        /// </summary>
        [Fact]
        public void ToString_WithEmptySelects_ReturnsSelectWithEmptyColumns()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            mockSource.Setup(s => s.ToString()).Returns("TestTable");

            var selectExpr = new SelectExpr
            {
                Source = mockSource.Object,
                Selects = new List<SelectItemExpr>()
            };

            // Act
            var result = selectExpr.ToString();

            // Assert
            Assert.Equal("SELECT  FROM TestTable", result);
        }

        /// <summary>
        /// Tests that ToString works correctly with single select item.
        /// </summary>
        [Fact]
        public void ToString_WithSingleSelect_ReturnsCorrectFormat()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            mockSource.Setup(s => s.ToString()).Returns("Users");

            var mockSelect = new Mock<SelectItemExpr>();
            mockSelect.Setup(s => s.ToString()).Returns("Id");

            var selectExpr = new SelectExpr
            {
                Source = mockSource.Object,
                Selects = new List<SelectItemExpr> { mockSelect.Object }
            };

            // Act
            var result = selectExpr.ToString();

            // Assert
            Assert.Equal("SELECT Id FROM Users", result);
        }

        /// <summary>
        /// Tests that ToString with both alias and NextSelects combines both features correctly.
        /// </summary>
        [Fact]
        public void ToString_WithAliasAndNextSelects_CombinesBothFeatures()
        {
            // Arrange
            var mockSource1 = new Mock<SqlSegment>();
            mockSource1.Setup(s => s.ToString()).Returns("Table1");

            var mockSource2 = new Mock<SqlSegment>();
            mockSource2.Setup(s => s.ToString()).Returns("Table2");

            var mockSelect1 = new Mock<SelectItemExpr>();
            mockSelect1.Setup(s => s.ToString()).Returns("Col1");

            var mockSelect2 = new Mock<SelectItemExpr>();
            mockSelect2.Setup(s => s.ToString()).Returns("Col2");

            var selectExpr1 = new SelectExpr
            {
                Source = mockSource1.Object,
                Selects = new List<SelectItemExpr> { mockSelect1.Object },
                Alias = "T1"
            };

            var selectExpr2 = new SelectExpr
            {
                Source = mockSource2.Object,
                Selects = new List<SelectItemExpr> { mockSelect2.Object },
                SetType = SelectSetType.Union
            };

            selectExpr1.NextSelects.Add(selectExpr2);

            // Act
            var result = selectExpr1.ToString();

            // Assert
            Assert.Equal("SELECT Col1 FROM Table1 AS T1 UNION SELECT Col2 FROM Table2", result);
        }

        /// <summary>
        /// Tests that ToString with many select columns joins them correctly with commas.
        /// </summary>
        [Fact]
        public void ToString_WithManySelects_JoinsWithCommas()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            mockSource.Setup(s => s.ToString()).Returns("Table");

            var mockSelect1 = new Mock<SelectItemExpr>();
            mockSelect1.Setup(s => s.ToString()).Returns("Col1");

            var mockSelect2 = new Mock<SelectItemExpr>();
            mockSelect2.Setup(s => s.ToString()).Returns("Col2");

            var mockSelect3 = new Mock<SelectItemExpr>();
            mockSelect3.Setup(s => s.ToString()).Returns("Col3");

            var mockSelect4 = new Mock<SelectItemExpr>();
            mockSelect4.Setup(s => s.ToString()).Returns("Col4");

            var selectExpr = new SelectExpr
            {
                Source = mockSource.Object,
                Selects = new List<SelectItemExpr>
                {
                    mockSelect1.Object,
                    mockSelect2.Object,
                    mockSelect3.Object,
                    mockSelect4.Object
                }
            };

            // Act
            var result = selectExpr.ToString();

            // Assert
            Assert.Equal("SELECT Col1, Col2, Col3, Col4 FROM Table", result);
        }

        /// <summary>
        /// Tests that ToString handles null Source gracefully by calling ToString on null.
        /// </summary>
        [Fact]
        public void ToString_WithNullSource_IncludesEmptySource()
        {
            // Arrange
            var mockSelect = new Mock<SelectItemExpr>();
            mockSelect.Setup(s => s.ToString()).Returns("Col1");

            var selectExpr = new SelectExpr
            {
                Source = null,
                Selects = new List<SelectItemExpr> { mockSelect.Object }
            };

            // Act
            var result = selectExpr.ToString();

            // Assert
            Assert.Equal("SELECT Col1 FROM ", result);
        }

        /// <summary>
        /// Tests that ToString with special characters in column and table names preserves them.
        /// </summary>
        [Fact]
        public void ToString_WithSpecialCharactersInNames_PreservesCharacters()
        {
            // Arrange
            var mockSource = new Mock<SqlSegment>();
            mockSource.Setup(s => s.ToString()).Returns("[Table-Name]");

            var mockSelect = new Mock<SelectItemExpr>();
            mockSelect.Setup(s => s.ToString()).Returns("[Column@Name]");

            var selectExpr = new SelectExpr
            {
                Source = mockSource.Object,
                Selects = new List<SelectItemExpr> { mockSelect.Object },
                Alias = "T$1"
            };

            // Act
            var result = selectExpr.ToString();

            // Assert
            Assert.Equal("SELECT [Column@Name] FROM [Table-Name] AS T$1", result);
        }

        /// <summary>
        /// Tests that GetHashCode returns a consistent value when called multiple times on the same instance.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsSameValue()
        {
            // Arrange
            var from = new FromExpr();
            var selectExpr = new SelectExpr(from, new SelectItemExpr(Expr.Prop("Id")));

            // Act
            int hash1 = selectExpr.GetHashCode();
            int hash2 = selectExpr.GetHashCode();
            int hash3 = selectExpr.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
            Assert.Equal(hash2, hash3);
        }

        /// <summary>
        /// Tests that GetHashCode returns the same value for two instances with identical properties.
        /// </summary>
        [Fact]
        public void GetHashCode_IdenticalInstances_ReturnsSameHashCode()
        {
            // Arrange
            var from1 = new FromExpr();
            var from2 = new FromExpr();
            var selectExpr1 = new SelectExpr(from1, new SelectItemExpr(Expr.Prop("Id")));
            var selectExpr2 = new SelectExpr(from2, new SelectItemExpr(Expr.Prop("Id")));

            // Act
            int hash1 = selectExpr1.GetHashCode();
            int hash2 = selectExpr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different values for instances with different Source properties.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentSource_ReturnsDifferentHashCode()
        {
            // Arrange
            var from = new FromExpr();
            var selectExpr1 = new SelectExpr(from, new SelectItemExpr(Expr.Prop("Id")));
            var selectExpr2 = new SelectExpr(null, new SelectItemExpr(Expr.Prop("Id")));

            // Act
            int hash1 = selectExpr1.GetHashCode();
            int hash2 = selectExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different values for instances with different Selects collections.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentSelects_ReturnsDifferentHashCode()
        {
            // Arrange
            var from = new FromExpr();
            var selectExpr1 = new SelectExpr(from, new SelectItemExpr(Expr.Prop("Id")));
            var selectExpr2 = new SelectExpr(from, new SelectItemExpr(Expr.Prop("Name")));

            // Act
            int hash1 = selectExpr1.GetHashCode();
            int hash2 = selectExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different values for instances with different Alias properties.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentAlias_ReturnsDifferentHashCode()
        {
            // Arrange
            var from = new FromExpr();
            var selectExpr1 = new SelectExpr(from, new SelectItemExpr(Expr.Prop("Id"))) { Alias = "A" };
            var selectExpr2 = new SelectExpr(from, new SelectItemExpr(Expr.Prop("Id"))) { Alias = "B" };

            // Act
            int hash1 = selectExpr1.GetHashCode();
            int hash2 = selectExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests GetHashCode with null Source property.
        /// </summary>
        [Fact]
        public void GetHashCode_NullSource_ReturnsValidHashCode()
        {
            // Arrange
            var selectExpr = new SelectExpr(null, new SelectItemExpr(Expr.Prop("Id")));

            // Act
            int hashCode = selectExpr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests GetHashCode with null Alias property.
        /// </summary>
        [Fact]
        public void GetHashCode_NullAlias_ReturnsValidHashCode()
        {
            // Arrange
            var from = new FromExpr();
            var selectExpr = new SelectExpr(from, new SelectItemExpr(Expr.Prop("Id"))) { Alias = null };

            // Act
            int hashCode = selectExpr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests GetHashCode with empty Selects collection.
        /// </summary>
        [Fact]
        public void GetHashCode_EmptySelects_ReturnsValidHashCode()
        {
            // Arrange
            var from = new FromExpr();
            var selectExpr = new SelectExpr(from);

            // Act
            int hashCode = selectExpr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests GetHashCode with multiple items in Selects collection.
        /// </summary>
        [Fact]
        public void GetHashCode_MultipleSelects_ReturnsValidHashCode()
        {
            // Arrange
            var from = new FromExpr();
            var selectExpr = new SelectExpr(from,
                new SelectItemExpr(Expr.Prop("Id")),
                new SelectItemExpr(Expr.Prop("Name")),
                new SelectItemExpr(Expr.Prop("Age")));

            // Act
            int hashCode = selectExpr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns different values when Selects collection order differs.
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentSelectsOrder_ReturnsDifferentHashCode()
        {
            // Arrange
            var from = new FromExpr();
            var selectExpr1 = new SelectExpr(from,
                new SelectItemExpr(Expr.Prop("Id")),
                new SelectItemExpr(Expr.Prop("Name")));
            var selectExpr2 = new SelectExpr(from,
                new SelectItemExpr(Expr.Prop("Name")),
                new SelectItemExpr(Expr.Prop("Id")));

            // Act
            int hash1 = selectExpr1.GetHashCode();
            int hash2 = selectExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests GetHashCode with default constructor (all properties at default values).
        /// </summary>
        [Fact]
        public void GetHashCode_DefaultConstructor_ReturnsValidHashCode()
        {
            // Arrange
            var selectExpr = new SelectExpr();

            // Act
            int hashCode = selectExpr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns same value for two default-constructed instances.
        /// </summary>
        [Fact]
        public void GetHashCode_TwoDefaultInstances_ReturnsSameHashCode()
        {
            // Arrange
            var selectExpr1 = new SelectExpr();
            var selectExpr2 = new SelectExpr();

            // Act
            int hash1 = selectExpr1.GetHashCode();
            int hash2 = selectExpr2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests GetHashCode with all properties set to non-null values.
        /// </summary>
        [Fact]
        public void GetHashCode_AllPropertiesSet_ReturnsValidHashCode()
        {
            // Arrange
            var from = new FromExpr();
            var selectExpr = new SelectExpr(from, new SelectItemExpr(Expr.Prop("Id")))
            {
                Alias = "MyAlias"
            };

            // Act
            int hashCode = selectExpr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests GetHashCode with empty string Alias.
        /// </summary>
        [Fact]
        public void GetHashCode_EmptyStringAlias_ReturnsValidHashCode()
        {
            // Arrange
            var from = new FromExpr();
            var selectExpr = new SelectExpr(from, new SelectItemExpr(Expr.Prop("Id")))
            {
                Alias = string.Empty
            };

            // Act
            int hashCode = selectExpr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns different values for null vs empty string Alias.
        /// </summary>
        [Fact]
        public void GetHashCode_NullVsEmptyAlias_ReturnsDifferentHashCode()
        {
            // Arrange
            var from = new FromExpr();
            var selectExpr1 = new SelectExpr(from, new SelectItemExpr(Expr.Prop("Id"))) { Alias = null };
            var selectExpr2 = new SelectExpr(from, new SelectItemExpr(Expr.Prop("Id"))) { Alias = string.Empty };

            // Act
            int hash1 = selectExpr1.GetHashCode();
            int hash2 = selectExpr2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests GetHashCode with very long Alias string.
        /// </summary>
        [Fact]
        public void GetHashCode_VeryLongAlias_ReturnsValidHashCode()
        {
            // Arrange
            var from = new FromExpr();
            var longAlias = new string('A', 10000);
            var selectExpr = new SelectExpr(from, new SelectItemExpr(Expr.Prop("Id")))
            {
                Alias = longAlias
            };

            // Act
            int hashCode = selectExpr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests GetHashCode with Alias containing special characters.
        /// </summary>
        [Fact]
        public void GetHashCode_AliasWithSpecialCharacters_ReturnsValidHashCode()
        {
            // Arrange
            var from = new FromExpr();
            var selectExpr = new SelectExpr(from, new SelectItemExpr(Expr.Prop("Id")))
            {
                Alias = "Alias_123"
            };

            // Act
            int hashCode = selectExpr.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests that hash code is consistent with Equals for equal objects.
        /// </summary>
        [Fact]
        public void GetHashCode_EqualObjects_HaveSameHashCode()
        {
            // Arrange
            var from1 = new FromExpr();
            var from2 = new FromExpr();
            var selectExpr1 = new SelectExpr(from1, new SelectItemExpr(Expr.Prop("Id"))) { Alias = "Test" };
            var selectExpr2 = new SelectExpr(from2, new SelectItemExpr(Expr.Prop("Id"))) { Alias = "Test" };

            // Act & Assert
            if (selectExpr1.Equals(selectExpr2))
            {
                Assert.Equal(selectExpr1.GetHashCode(), selectExpr2.GetHashCode());
            }
        }

        /// <summary>
        /// Tests that the NextSelects getter creates a new empty list when the backing field is initially null.
        /// </summary>
        [Fact]
        public void NextSelects_Get_WhenInitiallyNull_CreatesNewEmptyList()
        {
            // Arrange
            var selectExpr = new SelectExpr();

            // Act
            var result = selectExpr.NextSelects;

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that multiple accesses to the NextSelects getter return the same instance when not set explicitly.
        /// </summary>
        [Fact]
        public void NextSelects_Get_WhenAccessedMultipleTimes_ReturnsSameInstance()
        {
            // Arrange
            var selectExpr = new SelectExpr();

            // Act
            var firstAccess = selectExpr.NextSelects;
            var secondAccess = selectExpr.NextSelects;

            // Assert
            Assert.Same(firstAccess, secondAccess);
        }

        /// <summary>
        /// Tests that setting NextSelects to a non-null list stores and returns that exact list instance.
        /// </summary>
        [Fact]
        public void NextSelects_Set_WithNonNullList_StoresAndReturnsTheList()
        {
            // Arrange
            var selectExpr = new SelectExpr();
            var customList = new List<SelectExpr>();

            // Act
            selectExpr.NextSelects = customList;
            var result = selectExpr.NextSelects;

            // Assert
            Assert.Same(customList, result);
        }

        /// <summary>
        /// Tests that setting NextSelects to null and then accessing the getter creates a new list.
        /// </summary>
        [Fact]
        public void NextSelects_SetToNull_ThenGet_CreatesNewList()
        {
            // Arrange
            var selectExpr = new SelectExpr();
            var originalList = selectExpr.NextSelects;

            // Act
            selectExpr.NextSelects = null;
            var result = selectExpr.NextSelects;

            // Assert
            Assert.NotNull(result);
            Assert.NotSame(originalList, result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that setting NextSelects to an empty list returns that empty list, not a new one.
        /// </summary>
        [Fact]
        public void NextSelects_Set_WithEmptyList_ReturnsEmptyList()
        {
            // Arrange
            var selectExpr = new SelectExpr();
            var emptyList = new List<SelectExpr>();

            // Act
            selectExpr.NextSelects = emptyList;
            var result = selectExpr.NextSelects;

            // Assert
            Assert.Same(emptyList, result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that setting NextSelects to a list containing items returns that list with those items.
        /// </summary>
        [Fact]
        public void NextSelects_Set_WithListContainingItems_ReturnsListWithItems()
        {
            // Arrange
            var selectExpr = new SelectExpr();
            var listWithItems = new List<SelectExpr>
            {
                new SelectExpr(),
                new SelectExpr()
            };

            // Act
            selectExpr.NextSelects = listWithItems;
            var result = selectExpr.NextSelects;

            // Assert
            Assert.Same(listWithItems, result);
            Assert.Equal(2, result.Count);
        }

        /// <summary>
        /// Tests that modifications to the list returned by NextSelects are persisted across subsequent accesses.
        /// </summary>
        [Fact]
        public void NextSelects_Get_ModificationsArePersisted()
        {
            // Arrange
            var selectExpr = new SelectExpr();
            var firstAccess = selectExpr.NextSelects;
            var newItem = new SelectExpr();

            // Act
            firstAccess.Add(newItem);
            var secondAccess = selectExpr.NextSelects;

            // Assert
            Assert.Single(secondAccess);
            Assert.Same(newItem, secondAccess[0]);
        }

        /// <summary>
        /// Tests that setting NextSelects to null after it was previously set to a list with items resets it properly.
        /// </summary>
        [Fact]
        public void NextSelects_SetToNull_AfterSettingListWithItems_ResetsToNull()
        {
            // Arrange
            var selectExpr = new SelectExpr();
            var listWithItems = new List<SelectExpr> { new SelectExpr() };
            selectExpr.NextSelects = listWithItems;

            // Act
            selectExpr.NextSelects = null;
            var result = selectExpr.NextSelects;

            // Assert
            Assert.NotSame(listWithItems, result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates a valid SelectExpr instance with properly initialized default values.
        /// </summary>
        [Fact]
        public void Constructor_Parameterless_CreatesInstanceWithDefaultValues()
        {
            // Arrange & Act
            var selectExpr = new SelectExpr();

            // Assert
            Assert.NotNull(selectExpr);
            Assert.NotNull(selectExpr.Selects);
            Assert.Empty(selectExpr.Selects);
            Assert.Null(selectExpr.Alias);
            Assert.Equal(SelectSetType.UnionAll, selectExpr.SetType);
            Assert.Equal(ExprType.Select, selectExpr.ExprType);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates an instance that can be used in equality comparisons.
        /// </summary>
        [Fact]
        public void Constructor_Parameterless_CreatesInstanceUsableInEqualityComparison()
        {
            // Arrange & Act
            var selectExpr1 = new SelectExpr();
            var selectExpr2 = new SelectExpr();

            // Assert
            Assert.True(selectExpr1.Equals(selectExpr2));
            Assert.Equal(selectExpr1.GetHashCode(), selectExpr2.GetHashCode());
        }

        /// <summary>
        /// Tests that the parameterless constructor creates an instance with a modifiable Selects collection.
        /// </summary>
        [Fact]
        public void Constructor_Parameterless_CreatesInstanceWithModifiableSelectsCollection()
        {
            // Arrange
            var selectExpr = new SelectExpr();

            // Act
            selectExpr.Selects.Add(new SelectItemExpr(Expr.Prop("Id")));

            // Assert
            Assert.Single(selectExpr.Selects);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates an instance where properties can be set.
        /// </summary>
        [Fact]
        public void Constructor_Parameterless_CreatesInstanceWithSettableProperties()
        {
            // Arrange
            var selectExpr = new SelectExpr();

            // Act
            selectExpr.Alias = "TestAlias";
            selectExpr.SetType = SelectSetType.Union;

            // Assert
            Assert.Equal("TestAlias", selectExpr.Alias);
            Assert.Equal(SelectSetType.Union, selectExpr.SetType);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates an instance with lazy-initialized NextSelects property.
        /// </summary>
        [Fact]
        public void Constructor_Parameterless_CreatesInstanceWithLazyInitializedNextSelects()
        {
            // Arrange
            var selectExpr = new SelectExpr();

            // Act
            var nextSelects = selectExpr.NextSelects;

            // Assert
            Assert.NotNull(nextSelects);
            Assert.Empty(nextSelects);
        }
    }
}

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Tests for SelectItemExpr class
    /// </summary>
    public partial class SelectItemExprTests
    {
        #region Constructor with value and aliasName

        /// <summary>
        /// Tests that the constructor throws ArgumentNullException when value parameter is null.
        /// Input: null value, valid aliasName
        /// Expected: ArgumentNullException with parameter name "value"
        /// </summary>
        [Fact]
        public void Constructor_NullValue_ThrowsArgumentNullException()
        {
            // Arrange
            ValueTypeExpr? value = null;
            string aliasName = "ValidAlias";

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new SelectItemExpr(value!, aliasName));
            Assert.Equal("value", exception.ParamName);
        }

        /// <summary>
        /// Tests that the constructor correctly sets properties when value is valid and aliasName is null.
        /// Input: valid ValueTypeExpr, null aliasName
        /// Expected: Value property is set, Alias property is null, no exception thrown
        /// </summary>
        [Fact]
        public void Constructor_ValidValueAndNullAlias_SetsPropertiesCorrectly()
        {
            // Arrange
            ValueTypeExpr value = "TestValue";
            string? aliasName = null;

            // Act
            var result = new SelectItemExpr(value, aliasName!);

            // Assert
            Assert.NotNull(result.Value);
            Assert.Equal(value, result.Value);
            Assert.Null(result.Alias);
        }

        /// <summary>
        /// Tests that the constructor correctly sets properties when value is valid and aliasName is empty.
        /// Input: valid ValueTypeExpr, empty string aliasName
        /// Expected: Value property is set, Alias property is empty, no exception thrown
        /// </summary>
        [Fact]
        public void Constructor_ValidValueAndEmptyAlias_SetsPropertiesCorrectly()
        {
            // Arrange
            ValueTypeExpr value = "TestValue";
            string aliasName = string.Empty;

            // Act
            var result = new SelectItemExpr(value, aliasName);

            // Assert
            Assert.NotNull(result.Value);
            Assert.Equal(value, result.Value);
            Assert.Equal(string.Empty, result.Alias);
        }

        /// <summary>
        /// Tests that the constructor correctly sets properties when both value and aliasName are valid.
        /// Input: valid ValueTypeExpr, valid aliasName
        /// Expected: Both Value and Alias properties are set correctly
        /// </summary>
        [Theory]
        [InlineData("ValidAlias")]
        [InlineData("Valid_Alias")]
        [InlineData("ValidAlias123")]
        [InlineData("_ValidAlias")]
        [InlineData("Valid_Alias_123")]
        [InlineData("A")]
        [InlineData("a1")]
        [InlineData("_")]
        public void Constructor_ValidValueAndValidAlias_SetsPropertiesCorrectly(string aliasName)
        {
            // Arrange
            ValueTypeExpr value = 42;

            // Act
            var result = new SelectItemExpr(value, aliasName);

            // Assert
            Assert.NotNull(result.Value);
            Assert.Equal(value, result.Value);
            Assert.Equal(aliasName, result.Alias);
        }

        /// <summary>
        /// Tests that the constructor throws ArgumentException when aliasName contains invalid characters.
        /// Input: valid ValueTypeExpr, aliasName with invalid characters
        /// Expected: ArgumentException with parameter name "Alias"
        /// </summary>
        [Theory]
        [InlineData("Invalid-Alias")]
        [InlineData("Invalid Alias")]
        [InlineData("Invalid@Alias")]
        [InlineData("Invalid.Alias")]
        [InlineData("Invalid#Alias")]
        [InlineData("Invalid$Alias")]
        [InlineData("Invalid%Alias")]
        [InlineData("Invalid&Alias")]
        [InlineData("Invalid*Alias")]
        [InlineData("Invalid+Alias")]
        [InlineData("Invalid=Alias")]
        [InlineData("Invalid!Alias")]
        [InlineData("Invalid?Alias")]
        [InlineData("Invalid/Alias")]
        [InlineData("Invalid\\Alias")]
        [InlineData("Invalid|Alias")]
        [InlineData("Invalid<Alias")]
        [InlineData("Invalid>Alias")]
        [InlineData("Invalid,Alias")]
        [InlineData("Invalid;Alias")]
        [InlineData("Invalid:Alias")]
        [InlineData("Invalid'Alias")]
        [InlineData("Invalid\"Alias")]
        [InlineData("Invalid(Alias")]
        [InlineData("Invalid)Alias")]
        [InlineData("Invalid[Alias")]
        [InlineData("Invalid]Alias")]
        [InlineData("Invalid{Alias")]
        [InlineData("Invalid}Alias")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\n")]
        [InlineData("\r\n")]
        public void Constructor_ValidValueAndInvalidAlias_ThrowsArgumentException(string aliasName)
        {
            // Arrange
            ValueTypeExpr value = true;

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new SelectItemExpr(value, aliasName));
            Assert.Equal("Alias", exception.ParamName);
            Assert.Contains("invalid characters", exception.Message);
        }

        /// <summary>
        /// Tests that the constructor correctly handles long valid alias names.
        /// Input: valid ValueTypeExpr, very long valid aliasName
        /// Expected: Properties are set correctly without exception
        /// </summary>
        [Fact]
        public void Constructor_ValidValueAndLongValidAlias_SetsPropertiesCorrectly()
        {
            // Arrange
            ValueTypeExpr value = 3.14;
            string aliasName = new string('A', 1000) + "_" + new string('Z', 1000);

            // Act
            var result = new SelectItemExpr(value, aliasName);

            // Assert
            Assert.NotNull(result.Value);
            Assert.Equal(value, result.Value);
            Assert.Equal(aliasName, result.Alias);
        }

        /// <summary>
        /// Tests that the constructor correctly handles different ValueTypeExpr types created via implicit conversion.
        /// Input: different primitive types implicitly converted to ValueTypeExpr, valid aliasName
        /// Expected: Properties are set correctly for all types
        /// </summary>
        [Fact]
        public void Constructor_DifferentValueTypes_SetsPropertiesCorrectly()
        {
            // Arrange & Act & Assert

            // String value
            ValueTypeExpr stringValue = "TestString";
            var stringResult = new SelectItemExpr(stringValue, "StringAlias");
            Assert.Equal(stringValue, stringResult.Value);
            Assert.Equal("StringAlias", stringResult.Alias);

            // Int value
            ValueTypeExpr intValue = 123;
            var intResult = new SelectItemExpr(intValue, "IntAlias");
            Assert.Equal(intValue, intResult.Value);
            Assert.Equal("IntAlias", intResult.Alias);

            // Long value
            ValueTypeExpr longValue = 123456789L;
            var longResult = new SelectItemExpr(longValue, "LongAlias");
            Assert.Equal(longValue, longResult.Value);
            Assert.Equal("LongAlias", longResult.Alias);

            // Bool value
            ValueTypeExpr boolValue = false;
            var boolResult = new SelectItemExpr(boolValue, "BoolAlias");
            Assert.Equal(boolValue, boolResult.Value);
            Assert.Equal("BoolAlias", boolResult.Alias);

            // DateTime value
            ValueTypeExpr dateTimeValue = DateTime.Now;
            var dateTimeResult = new SelectItemExpr(dateTimeValue, "DateTimeAlias");
            Assert.Equal(dateTimeValue, dateTimeResult.Value);
            Assert.Equal("DateTimeAlias", dateTimeResult.Alias);

            // Double value
            ValueTypeExpr doubleValue = 123.456;
            var doubleResult = new SelectItemExpr(doubleValue, "DoubleAlias");
            Assert.Equal(doubleValue, doubleResult.Value);
            Assert.Equal("DoubleAlias", doubleResult.Alias);

            // Decimal value
            ValueTypeExpr decimalValue = 123.456m;
            var decimalResult = new SelectItemExpr(decimalValue, "DecimalAlias");
            Assert.Equal(decimalValue, decimalResult.Value);
            Assert.Equal("DecimalAlias", decimalResult.Alias);
        }

        #endregion

        /// <summary>
        /// Tests that the Alias property accepts and returns null values.
        /// </summary>
        [Fact]
        public void Alias_SetNull_ReturnsNull()
        {
            // Arrange
            var selectItem = new SelectItemExpr();

            // Act
            selectItem.Alias = null;

            // Assert
            Assert.Null(selectItem.Alias);
        }

        /// <summary>
        /// Tests that the Alias property accepts and returns empty string values.
        /// </summary>
        [Fact]
        public void Alias_SetEmptyString_ReturnsEmptyString()
        {
            // Arrange
            var selectItem = new SelectItemExpr();

            // Act
            selectItem.Alias = string.Empty;

            // Assert
            Assert.Equal(string.Empty, selectItem.Alias);
        }

        /// <summary>
        /// Tests that the Alias property accepts and returns valid SQL names.
        /// </summary>
        /// <param name="validAlias">A valid SQL name to test</param>
        [Theory]
        [InlineData("MyAlias")]
        [InlineData("Alias123")]
        [InlineData("my_alias")]
        [InlineData("My_Alias_123")]
        [InlineData("A")]
        [InlineData("_alias")]
        [InlineData("_")]
        [InlineData("alias_")]
        [InlineData("UPPERCASE")]
        [InlineData("lowercase")]
        [InlineData("MixedCase")]
        [InlineData("_123")]
        [InlineData("a1b2c3")]
        [InlineData("________")]
        public void Alias_SetValidSqlName_ReturnsValue(string validAlias)
        {
            // Arrange
            var selectItem = new SelectItemExpr();

            // Act
            selectItem.Alias = validAlias;

            // Assert
            Assert.Equal(validAlias, selectItem.Alias);
        }

        /// <summary>
        /// Tests that the Alias property throws ArgumentException when set to invalid SQL names containing special characters.
        /// </summary>
        /// <param name="invalidAlias">An invalid SQL name containing special characters</param>
        [Theory]
        [InlineData("My-Alias")]
        [InlineData("My.Alias")]
        [InlineData("My Alias")]
        [InlineData("alias!")]
        [InlineData("alias?")]
        [InlineData("alias@")]
        [InlineData("alias#")]
        [InlineData("alias$")]
        [InlineData("alias%")]
        [InlineData("alias^")]
        [InlineData("alias&")]
        [InlineData("alias*")]
        [InlineData("alias(")]
        [InlineData("alias)")]
        [InlineData("alias+")]
        [InlineData("alias=")]
        [InlineData("alias[")]
        [InlineData("alias]")]
        [InlineData("alias{")]
        [InlineData("alias}")]
        [InlineData("alias|")]
        [InlineData("alias\\")]
        [InlineData("alias/")]
        [InlineData("alias<")]
        [InlineData("alias>")]
        [InlineData("alias,")]
        [InlineData("alias;")]
        [InlineData("alias:")]
        [InlineData("alias'")]
        [InlineData("alias\"")]
        [InlineData("alias`")]
        [InlineData("alias~")]
        public void Alias_SetInvalidSqlNameWithSpecialCharacters_ThrowsArgumentException(string invalidAlias)
        {
            // Arrange
            var selectItem = new SelectItemExpr();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => selectItem.Alias = invalidAlias);
            Assert.Equal("Alias", exception.ParamName);
            Assert.Contains("contains invalid characters", exception.Message);
            Assert.Contains("only letters, numbers, and underscores are allowed", exception.Message);
        }

        /// <summary>
        /// Tests that the Alias property throws ArgumentException when set to whitespace-only strings.
        /// </summary>
        /// <param name="whitespaceAlias">A whitespace-only string</param>
        [Theory]
        [InlineData(" ")]
        [InlineData("  ")]
        [InlineData("\t")]
        [InlineData("\n")]
        [InlineData("\r")]
        [InlineData("\r\n")]
        [InlineData("   \t   ")]
        public void Alias_SetWhitespaceOnlyString_ThrowsArgumentException(string whitespaceAlias)
        {
            // Arrange
            var selectItem = new SelectItemExpr();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => selectItem.Alias = whitespaceAlias);
            Assert.Equal("Alias", exception.ParamName);
            Assert.Contains("contains invalid characters", exception.Message);
        }

        /// <summary>
        /// Tests that the Alias property throws ArgumentException when set to strings with embedded whitespace.
        /// </summary>
        [Theory]
        [InlineData("My Alias")]
        [InlineData("Alias Name")]
        [InlineData("a b")]
        [InlineData("alias\tname")]
        [InlineData("alias\nname")]
        public void Alias_SetStringWithEmbeddedWhitespace_ThrowsArgumentException(string invalidAlias)
        {
            // Arrange
            var selectItem = new SelectItemExpr();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => selectItem.Alias = invalidAlias);
            Assert.Equal("Alias", exception.ParamName);
        }

        /// <summary>
        /// Tests that the Alias property can be set multiple times and returns the latest value.
        /// </summary>
        [Fact]
        public void Alias_SetMultipleTimes_ReturnsLatestValue()
        {
            // Arrange
            var selectItem = new SelectItemExpr();

            // Act
            selectItem.Alias = "FirstAlias";
            selectItem.Alias = "SecondAlias";
            selectItem.Alias = "ThirdAlias";

            // Assert
            Assert.Equal("ThirdAlias", selectItem.Alias);
        }

        /// <summary>
        /// Tests that the Alias property can be set from a valid value to null.
        /// </summary>
        [Fact]
        public void Alias_SetValidThenNull_ReturnsNull()
        {
            // Arrange
            var selectItem = new SelectItemExpr();
            selectItem.Alias = "ValidAlias";

            // Act
            selectItem.Alias = null;

            // Assert
            Assert.Null(selectItem.Alias);
        }

        /// <summary>
        /// Tests that the Alias property can be set from a valid value to empty string.
        /// </summary>
        [Fact]
        public void Alias_SetValidThenEmpty_ReturnsEmptyString()
        {
            // Arrange
            var selectItem = new SelectItemExpr();
            selectItem.Alias = "ValidAlias";

            // Act
            selectItem.Alias = string.Empty;

            // Assert
            Assert.Equal(string.Empty, selectItem.Alias);
        }

        /// <summary>
        /// Tests that the Alias property throws ArgumentException for very long strings with invalid characters.
        /// </summary>
        [Fact]
        public void Alias_SetVeryLongStringWithInvalidCharacters_ThrowsArgumentException()
        {
            // Arrange
            var selectItem = new SelectItemExpr();
            var veryLongInvalidAlias = new string('a', 1000) + "-invalid";

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => selectItem.Alias = veryLongInvalidAlias);
            Assert.Equal("Alias", exception.ParamName);
        }

        /// <summary>
        /// Tests that the Alias property accepts very long strings with only valid characters.
        /// </summary>
        [Fact]
        public void Alias_SetVeryLongStringWithValidCharacters_ReturnsValue()
        {
            // Arrange
            var selectItem = new SelectItemExpr();
            var veryLongValidAlias = new string('a', 10000);

            // Act
            selectItem.Alias = veryLongValidAlias;

            // Assert
            Assert.Equal(veryLongValidAlias, selectItem.Alias);
        }

        /// <summary>
        /// Tests that the Alias property throws ArgumentException for strings with Unicode special characters.
        /// </summary>
        [Theory]
        [InlineData("alias™")]
        [InlineData("alias©")]
        [InlineData("alias®")]
        [InlineData("alias€")]
        [InlineData("alias£")]
        [InlineData("alias¥")]
        [InlineData("alias§")]
        [InlineData("alias¶")]
        public void Alias_SetStringWithUnicodeSpecialCharacters_ThrowsArgumentException(string invalidAlias)
        {
            // Arrange
            var selectItem = new SelectItemExpr();

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => selectItem.Alias = invalidAlias);
            Assert.Equal("Alias", exception.ParamName);
        }

        /// <summary>
        /// Tests that the Alias property default value is null when not explicitly set.
        /// </summary>
        [Fact]
        public void Alias_DefaultValue_IsNull()
        {
            // Arrange & Act
            var selectItem = new SelectItemExpr();

            // Assert
            Assert.Null(selectItem.Alias);
        }
        #region Constructor Tests

        /// <summary>
        /// Tests that the parameterless constructor creates an instance with null Value property.
        /// Input: None (parameterless constructor).
        /// Expected: Value property should be null.
        /// </summary>
        [Fact]
        public void Constructor_NoParameters_CreatesInstanceWithNullValue()
        {
            // Act
            var selectItem = new SelectItemExpr();

            // Assert
            Assert.Null(selectItem.Value);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates an instance with null Alias property.
        /// Input: None (parameterless constructor).
        /// Expected: Alias property should be null.
        /// </summary>
        [Fact]
        public void Constructor_NoParameters_CreatesInstanceWithNullAlias()
        {
            // Act
            var selectItem = new SelectItemExpr();

            // Assert
            Assert.Null(selectItem.Alias);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates an instance with correct ExprType.
        /// Input: None (parameterless constructor).
        /// Expected: ExprType should be ExprType.SelectItem.
        /// </summary>
        [Fact]
        public void Constructor_NoParameters_CreatesInstanceWithCorrectExprType()
        {
            // Act
            var selectItem = new SelectItemExpr();

            // Assert
            Assert.Equal(ExprType.SelectItem, selectItem.ExprType);
        }

        /// <summary>
        /// Tests that the parameterless constructor creates a valid instance of SelectItemExpr.
        /// Input: None (parameterless constructor).
        /// Expected: Instance is not null and is of type SelectItemExpr.
        /// </summary>
        [Fact]
        public void Constructor_NoParameters_CreatesValidInstance()
        {
            // Act
            var selectItem = new SelectItemExpr();

            // Assert
            Assert.NotNull(selectItem);
            Assert.IsType<SelectItemExpr>(selectItem);
        }

        #endregion

        /// <summary>
        /// Tests that ToString returns the Value's string representation when Alias is not set (null).
        /// </summary>
        [Fact]
        public void ToString_WhenAliasIsNull_ReturnsValueToString()
        {
            // Arrange
            var valueExpr = Expr.Prop("TestProperty");
            var selectItem = new SelectItemExpr(valueExpr);

            // Act
            var result = selectItem.ToString();

            // Assert
            Assert.Equal(valueExpr.ToString(), result);
        }

        /// <summary>
        /// Tests that ToString returns the formatted string with alias when Alias is set to a valid value.
        /// </summary>
        [Fact]
        public void ToString_WhenAliasIsSet_ReturnsFormattedStringWithAlias()
        {
            // Arrange
            var valueExpr = Expr.Prop("Id");
            var aliasName = "UserId";
            var selectItem = new SelectItemExpr(valueExpr, aliasName);

            // Act
            var result = selectItem.ToString();

            // Assert
            Assert.Equal($"{valueExpr} AS {aliasName}", result);
        }

        /// <summary>
        /// Tests that ToString returns null when Value property is null and Alias is not set.
        /// </summary>
        [Fact]
        public void ToString_WhenValueIsNull_ReturnsNull()
        {
            // Arrange
            var selectItem = new SelectItemExpr
            {
                Value = null
            };

            // Act
            var result = selectItem.ToString();

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that ToString returns the formatted string "null AS {Alias}" when Value is null but Alias is set.
        /// </summary>
        [Fact]
        public void ToString_WhenValueIsNullAndAliasIsSet_ReturnsFormattedStringWithNullValue()
        {
            // Arrange
            var aliasName = "TestAlias";
            var selectItem = new SelectItemExpr
            {
                Value = null,
                Alias = aliasName
            };

            // Act
            var result = selectItem.ToString();

            // Assert
            Assert.Equal($" AS {aliasName}", result);
        }

        /// <summary>
        /// Tests that ToString with complex value expression and alias returns correctly formatted string.
        /// </summary>
        [Fact]
        public void ToString_WithFunctionExpressionAndAlias_ReturnsFormattedString()
        {
            // Arrange
            var funcExpr = new FunctionExpr("COUNT", Expr.Prop("Id"));
            var aliasName = "TotalCount";
            var selectItem = new SelectItemExpr(funcExpr, aliasName);

            // Act
            var result = selectItem.ToString();

            // Assert
            Assert.Equal($"{funcExpr} AS {aliasName}", result);
            Assert.Contains("AS", result);
            Assert.Contains(aliasName, result);
        }

        /// <summary>
        /// Tests that ToString without alias uses only the value expression's string representation.
        /// </summary>
        [Fact]
        public void ToString_WithFunctionExpressionWithoutAlias_ReturnsValueString()
        {
            // Arrange
            var funcExpr = new FunctionExpr("MAX", Expr.Prop("Score"));
            var selectItem = new SelectItemExpr(funcExpr);

            // Act
            var result = selectItem.ToString();

            // Assert
            Assert.Equal(funcExpr.ToString(), result);
            Assert.DoesNotContain("AS", result);
        }
        #region Equals Method Tests

        /// <summary>
        /// Tests that Equals returns false when comparing with null.
        /// Input: null object.
        /// Expected: false.
        /// </summary>
        [Fact]
        public void Equals_NullObject_ReturnsFalse()
        {
            // Arrange
            var value = new PropExpr("Id");
            var selectItem = new SelectItemExpr(value) { Alias = "UserId" };

            // Act
            var result = selectItem.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with a different type.
        /// Input: Object of different type (string).
        /// Expected: false.
        /// </summary>
        [Fact]
        public void Equals_DifferentType_ReturnsFalse()
        {
            // Arrange
            var value = new PropExpr("Id");
            var selectItem = new SelectItemExpr(value) { Alias = "UserId" };
            var differentType = "not a SelectItemExpr";

            // Act
            var result = selectItem.Equals(differentType);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing with the same reference.
        /// Input: Same instance.
        /// Expected: true.
        /// </summary>
        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            // Arrange
            var value = new PropExpr("Id");
            var selectItem = new SelectItemExpr(value) { Alias = "UserId" };

            // Act
            var result = selectItem.Equals(selectItem);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have the same Value and Alias.
        /// Input: Two SelectItemExpr with same Value and Alias.
        /// Expected: true.
        /// </summary>
        [Fact]
        public void Equals_SameValueAndAlias_ReturnsTrue()
        {
            // Arrange
            var value1 = new PropExpr("Id");
            var value2 = new PropExpr("Id");
            var item1 = new SelectItemExpr(value1) { Alias = "UserId" };
            var item2 = new SelectItemExpr(value2) { Alias = "UserId" };

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when Value is the same but Alias is different.
        /// Input: Two SelectItemExpr with same Value but different Alias.
        /// Expected: false.
        /// </summary>
        [Fact]
        public void Equals_SameValueDifferentAlias_ReturnsFalse()
        {
            // Arrange
            var value1 = new PropExpr("Id");
            var value2 = new PropExpr("Id");
            var item1 = new SelectItemExpr(value1) { Alias = "UserId" };
            var item2 = new SelectItemExpr(value2) { Alias = "Id" };

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when Alias is the same but Value is different.
        /// Input: Two SelectItemExpr with same Alias but different Value.
        /// Expected: false.
        /// </summary>
        [Fact]
        public void Equals_DifferentValueSameAlias_ReturnsFalse()
        {
            // Arrange
            var value1 = new PropExpr("Id");
            var value2 = new PropExpr("Name");
            var item1 = new SelectItemExpr(value1) { Alias = "UserId" };
            var item2 = new SelectItemExpr(value2) { Alias = "UserId" };

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null Alias and same Value.
        /// Input: Two SelectItemExpr with null Alias and same Value.
        /// Expected: true.
        /// </summary>
        [Fact]
        public void Equals_BothNullAliasSameValue_ReturnsTrue()
        {
            // Arrange
            var value1 = new PropExpr("Id");
            var value2 = new PropExpr("Id");
            var item1 = new SelectItemExpr(value1);
            var item2 = new SelectItemExpr(value2);

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when both instances have null Alias but different Value.
        /// Input: Two SelectItemExpr with null Alias and different Value.
        /// Expected: false.
        /// </summary>
        [Fact]
        public void Equals_BothNullAliasDifferentValue_ReturnsFalse()
        {
            // Arrange
            var value1 = new PropExpr("Id");
            var value2 = new PropExpr("Name");
            var item1 = new SelectItemExpr(value1);
            var item2 = new SelectItemExpr(value2);

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one has null Alias and the other has non-null Alias.
        /// Input: One SelectItemExpr with null Alias, another with non-null Alias, same Value.
        /// Expected: false.
        /// </summary>
        [Fact]
        public void Equals_OneNullAliasOneNonNull_ReturnsFalse()
        {
            // Arrange
            var value1 = new PropExpr("Id");
            var value2 = new PropExpr("Id");
            var item1 = new SelectItemExpr(value1);
            var item2 = new SelectItemExpr(value2) { Alias = "UserId" };

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing empty string Alias with null Alias.
        /// Input: One SelectItemExpr with empty string Alias, another with null Alias, same Value.
        /// Expected: false.
        /// </summary>
        [Fact]
        public void Equals_EmptyStringAliasVsNullAlias_ReturnsFalse()
        {
            // Arrange
            var value1 = new PropExpr("Id");
            var value2 = new PropExpr("Id");
            var item1 = new SelectItemExpr(value1) { Alias = "" };
            var item2 = new SelectItemExpr(value2);

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both have empty string Alias and same Value.
        /// Input: Two SelectItemExpr with empty string Alias and same Value.
        /// Expected: true.
        /// </summary>
        [Fact]
        public void Equals_BothEmptyStringAliasSameValue_ReturnsTrue()
        {
            // Arrange
            var value1 = new PropExpr("Id");
            var value2 = new PropExpr("Id");
            var item1 = new SelectItemExpr(value1) { Alias = "" };
            var item2 = new SelectItemExpr(value2) { Alias = "" };

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both have whitespace Alias and same Value.
        /// Input: Two SelectItemExpr with whitespace Alias and same Value.
        /// Expected: true.
        /// </summary>
        [Fact]
        public void Equals_BothWhitespaceAliasSameValue_ReturnsTrue()
        {
            // Arrange
            var value1 = new PropExpr("Id");
            var value2 = new PropExpr("Id");
            var item1 = new SelectItemExpr(value1) { Alias = "   " };
            var item2 = new SelectItemExpr(value2) { Alias = "   " };

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when Alias differs only in whitespace.
        /// Input: Two SelectItemExpr with different whitespace patterns in Alias, same Value.
        /// Expected: false.
        /// </summary>
        [Fact]
        public void Equals_DifferentWhitespaceAliases_ReturnsFalse()
        {
            // Arrange
            var value1 = new PropExpr("Id");
            var value2 = new PropExpr("Id");
            var item1 = new SelectItemExpr(value1) { Alias = "   " };
            var item2 = new SelectItemExpr(value2) { Alias = "  " };

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals works correctly with complex ValueTypeExpr (FunctionExpr).
        /// Input: Two SelectItemExpr with same FunctionExpr Value and Alias.
        /// Expected: true.
        /// </summary>
        [Fact]
        public void Equals_ComplexValueTypeExpr_ReturnsTrue()
        {
            // Arrange
            var funcExpr1 = new FunctionExpr("COUNT", new PropExpr("Id"));
            var funcExpr2 = new FunctionExpr("COUNT", new PropExpr("Id"));
            var item1 = new SelectItemExpr(funcExpr1) { Alias = "UserCount" };
            var item2 = new SelectItemExpr(funcExpr2) { Alias = "UserCount" };

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing different complex ValueTypeExpr.
        /// Input: Two SelectItemExpr with different FunctionExpr Value but same Alias.
        /// Expected: false.
        /// </summary>
        [Fact]
        public void Equals_DifferentComplexValueTypeExpr_ReturnsFalse()
        {
            // Arrange
            var funcExpr1 = new FunctionExpr("COUNT", new PropExpr("Id"));
            var funcExpr2 = new FunctionExpr("SUM", new PropExpr("Id"));
            var item1 = new SelectItemExpr(funcExpr1) { Alias = "UserCount" };
            var item2 = new SelectItemExpr(funcExpr2) { Alias = "UserCount" };

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have long Alias strings that are identical.
        /// Input: Two SelectItemExpr with very long identical Alias and same Value.
        /// Expected: true.
        /// </summary>
        [Fact]
        public void Equals_VeryLongIdenticalAliases_ReturnsTrue()
        {
            // Arrange
            var value1 = new PropExpr("Id");
            var value2 = new PropExpr("Id");
            var longAlias = new string('a', 1000);
            var item1 = new SelectItemExpr(value1) { Alias = longAlias };
            var item2 = new SelectItemExpr(value2) { Alias = longAlias };

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals is case-sensitive for Alias comparison.
        /// Input: Two SelectItemExpr with same Value but Alias differing only in case.
        /// Expected: false.
        /// </summary>
        [Fact]
        public void Equals_AliasCaseSensitive_ReturnsFalse()
        {
            // Arrange
            var value1 = new PropExpr("Id");
            var value2 = new PropExpr("Id");
            var item1 = new SelectItemExpr(value1) { Alias = "UserId" };
            var item2 = new SelectItemExpr(value2) { Alias = "userid" };

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both have special characters in Alias and same Value.
        /// Input: Two SelectItemExpr with special characters in identical Alias and same Value.
        /// Expected: true.
        /// </summary>
        [Fact]
        public void Equals_SpecialCharactersInAlias_ReturnsTrue()
        {
            // Arrange
            var value1 = new PropExpr("Id");
            var value2 = new PropExpr("Id");
            var item1 = new SelectItemExpr(value1) { Alias = "User_Id_123" };
            var item2 = new SelectItemExpr(value2) { Alias = "User_Id_123" };

            // Act
            var result = item1.Equals(item2);

            // Assert
            Assert.True(result);
        }

        #endregion
        #region Constructor Tests - SelectItemExpr(ValueTypeExpr)

        /// <summary>
        /// Tests that the constructor throws ArgumentNullException when value parameter is null.
        /// Input: null value
        /// Expected: ArgumentNullException with parameter name "value"
        /// </summary>
        [Fact]
        public void Constructor_WithNullValue_ThrowsArgumentNullException()
        {
            // Arrange
            ValueTypeExpr? value = null;

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new SelectItemExpr(value!));
            Assert.Equal("value", exception.ParamName);
        }

        /// <summary>
        /// Tests that the constructor correctly sets the Value property when provided a valid ValueTypeExpr.
        /// Input: Valid ValueTypeExpr instance
        /// Expected: SelectItemExpr created with Value property set to the provided value
        /// </summary>
        [Fact]
        public void Constructor_WithValidValue_SetsValueProperty()
        {
            // Arrange
            var mockValue = new Mock<ValueTypeExpr>();

            // Act
            var result = new SelectItemExpr(mockValue.Object);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(mockValue.Object, result.Value);
        }

        /// <summary>
        /// Tests that the constructor leaves the Alias property null when not specified.
        /// Input: Valid ValueTypeExpr instance without alias
        /// Expected: SelectItemExpr created with Alias property null
        /// </summary>
        [Fact]
        public void Constructor_WithValidValue_AliasIsNull()
        {
            // Arrange
            var mockValue = new Mock<ValueTypeExpr>();

            // Act
            var result = new SelectItemExpr(mockValue.Object);

            // Assert
            Assert.Null(result.Alias);
        }

        #endregion

        /// <summary>
        /// Provides different SelectItemExpr instances to validate the ExprType property.
        /// Cases:
        /// - Default constructed instance.
        /// - Constructed with a mocked ValueTypeExpr.
        /// - Constructed with a mocked ValueTypeExpr and an alias.
        /// - Instance with Value set post-construction.
        /// This parameterization checks that ExprType is stable regardless of construction path or property state.
        /// </summary>
        public static IEnumerable<object?[]> ExprTypeInstances()
        {
            // Default ctor
            yield return new object?[] { new SelectItemExpr() };

            // Constructor with mocked ValueTypeExpr
            var mockValue1 = new Mock<ValueTypeExpr>();
            // Provide a simple ToString override to avoid surprises if called elsewhere
            mockValue1.Setup(v => v.ToString()).Returns("MockValue1");
            yield return new object?[] { new SelectItemExpr(mockValue1.Object) };

            // Constructor with mocked ValueTypeExpr and alias
            var mockValue2 = new Mock<ValueTypeExpr>();
            mockValue2.Setup(v => v.ToString()).Returns("MockValue2");
            yield return new object?[] { new SelectItemExpr(mockValue2.Object, "alias") };

            // Default instance with Value set later
            var mockValue3 = new Mock<ValueTypeExpr>();
            mockValue3.Setup(v => v.ToString()).Returns("MockValue3");
            var laterSet = new SelectItemExpr();
            laterSet.Value = mockValue3.Object;
            laterSet.Alias = string.Empty;
            yield return new object?[] { laterSet };
        }

        /// <summary>
        /// Verifies that SelectItemExpr.ExprType returns ExprType.SelectItem for various instances.
        /// Input conditions: parameterized SelectItemExpr instances created via different constructors and property assignments.
        /// Expected result: ExprType.SelectItem is returned and no exceptions are thrown.
        /// </summary>
        /// <param name="instance">The SelectItemExpr instance under test (may be constructed differently per case).</param>
        [Theory]
        [MemberData(nameof(ExprTypeInstances))]
        public void ExprType_InstanceVarious_ReturnsSelectItem(SelectItemExpr? instance)
        {
            // Arrange
            Assert.NotNull(instance); // guard from test data mistakes

            // Act
            var result = instance!.ExprType;

            // Assert
            Assert.Equal(ExprType.SelectItem, result);
        }
        #region GetHashCode Tests

        /// <summary>
        /// Tests that GetHashCode returns consistent hash codes for the same instance when called multiple times.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsSameValue()
        {
            // Arrange
            var mockValue = new Mock<ValueTypeExpr>();
            mockValue.Setup(v => v.GetHashCode()).Returns(12345);
            var selectItem = new SelectItemExpr(mockValue.Object, "TestAlias");

            // Act
            var hashCode1 = selectItem.GetHashCode();
            var hashCode2 = selectItem.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns equal hash codes for two instances with the same Alias and Value.
        /// This verifies the GetHashCode contract: equal objects must have equal hash codes.
        /// </summary>
        [Fact]
        public void GetHashCode_EqualObjects_ReturnsSameHashCode()
        {
            // Arrange
            var mockValue1 = new Mock<ValueTypeExpr>();
            var mockValue2 = new Mock<ValueTypeExpr>();
            mockValue1.Setup(v => v.GetHashCode()).Returns(42);
            mockValue1.Setup(v => v.Equals(It.IsAny<object>())).Returns<object>(o => ReferenceEquals(o, mockValue1.Object) || ReferenceEquals(o, mockValue2.Object));
            mockValue2.Setup(v => v.GetHashCode()).Returns(42);
            mockValue2.Setup(v => v.Equals(It.IsAny<object>())).Returns<object>(o => ReferenceEquals(o, mockValue1.Object) || ReferenceEquals(o, mockValue2.Object));

            var selectItem1 = new SelectItemExpr(mockValue1.Object, "TestAlias");
            var selectItem2 = new SelectItemExpr(mockValue2.Object, "TestAlias");

            // Act
            var hashCode1 = selectItem1.GetHashCode();
            var hashCode2 = selectItem2.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode handles null Alias property correctly.
        /// Input: SelectItemExpr with null Alias.
        /// Expected: Returns a valid hash code without throwing an exception.
        /// </summary>
        [Fact]
        public void GetHashCode_NullAlias_ReturnsValidHashCode()
        {
            // Arrange
            var mockValue = new Mock<ValueTypeExpr>();
            mockValue.Setup(v => v.GetHashCode()).Returns(100);
            var selectItem = new SelectItemExpr(mockValue.Object);

            // Act
            var hashCode = selectItem.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode handles null Value property correctly.
        /// Input: SelectItemExpr with null Value and non-null Alias.
        /// Expected: Returns a valid hash code without throwing an exception.
        /// </summary>
        [Fact]
        public void GetHashCode_NullValue_ReturnsValidHashCode()
        {
            // Arrange
            var selectItem = new SelectItemExpr
            {
                Value = null,
                Alias = "TestAlias"
            };

            // Act
            var hashCode = selectItem.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode handles both null Alias and null Value properties correctly.
        /// Input: SelectItemExpr with both Alias and Value set to null.
        /// Expected: Returns a valid hash code without throwing an exception.
        /// </summary>
        [Fact]
        public void GetHashCode_BothAliasAndValueNull_ReturnsValidHashCode()
        {
            // Arrange
            var selectItem = new SelectItemExpr
            {
                Value = null,
                Alias = null
            };

            // Act
            var hashCode = selectItem.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes for instances with different Alias values.
        /// Input: Two SelectItemExpr instances with same Value but different Alias.
        /// Expected: Different hash codes (not guaranteed by contract, but expected in practice).
        /// </summary>
        [Theory]
        [InlineData("Alias1", "Alias2")]
        [InlineData("", "NonEmpty")]
        [InlineData("Short", "VeryLongAliasNameThatExceedsTypicalLength")]
        public void GetHashCode_DifferentAlias_ReturnsDifferentHashCodes(string alias1, string alias2)
        {
            // Arrange
            var mockValue = new Mock<ValueTypeExpr>();
            mockValue.Setup(v => v.GetHashCode()).Returns(999);
            var selectItem1 = new SelectItemExpr(mockValue.Object, alias1);
            var selectItem2 = new SelectItemExpr(mockValue.Object, alias2);

            // Act
            var hashCode1 = selectItem1.GetHashCode();
            var hashCode2 = selectItem2.GetHashCode();

            // Assert
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hash codes for instances with different Value objects.
        /// Input: Two SelectItemExpr instances with same Alias but different Value.
        /// Expected: Different hash codes (not guaranteed by contract, but expected in practice).
        /// </summary>
        [Fact]
        public void GetHashCode_DifferentValue_ReturnsDifferentHashCodes()
        {
            // Arrange
            var mockValue1 = new Mock<ValueTypeExpr>();
            var mockValue2 = new Mock<ValueTypeExpr>();
            mockValue1.Setup(v => v.GetHashCode()).Returns(100);
            mockValue2.Setup(v => v.GetHashCode()).Returns(200);

            var selectItem1 = new SelectItemExpr(mockValue1.Object, "SameAlias");
            var selectItem2 = new SelectItemExpr(mockValue2.Object, "SameAlias");

            // Act
            var hashCode1 = selectItem1.GetHashCode();
            var hashCode2 = selectItem2.GetHashCode();

            // Assert
            Assert.NotEqual(hashCode1, hashCode2);
        }

        /// <summary>
        /// Tests that GetHashCode handles empty string Alias correctly.
        /// Input: SelectItemExpr with empty string Alias.
        /// Expected: Returns a valid hash code without throwing an exception.
        /// </summary>
        [Fact]
        public void GetHashCode_EmptyStringAlias_ReturnsValidHashCode()
        {
            // Arrange
            var mockValue = new Mock<ValueTypeExpr>();
            mockValue.Setup(v => v.GetHashCode()).Returns(555);
            var selectItem = new SelectItemExpr(mockValue.Object, "");

            // Act
            var hashCode = selectItem.GetHashCode();

            // Assert
            Assert.NotEqual(0, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode produces different results for null vs empty string Alias.
        /// Input: Two SelectItemExpr instances, one with null Alias and one with empty string.
        /// Expected: Different hash codes.
        /// </summary>
        [Fact]
        public void GetHashCode_NullVsEmptyAlias_ReturnsDifferentHashCodes()
        {
            // Arrange
            var mockValue = new Mock<ValueTypeExpr>();
            mockValue.Setup(v => v.GetHashCode()).Returns(777);

            var selectItemNull = new SelectItemExpr
            {
                Value = mockValue.Object,
                Alias = null
            };
            var selectItemEmpty = new SelectItemExpr(mockValue.Object, "");

            // Act
            var hashCodeNull = selectItemNull.GetHashCode();
            var hashCodeEmpty = selectItemEmpty.GetHashCode();

            // Assert
            Assert.NotEqual(hashCodeNull, hashCodeEmpty);
        }

        #endregion

        /// <summary>
        /// Tests that Clone returns a new SelectItemExpr whose Value is the object returned by the original Value.Clone()
        /// and whose Alias is preserved. Covers alias edge cases including null, empty and very long valid names.
        /// </summary>
        /// <param name="alias">Alias value to test (may be null or empty).</param>
        [Theory]
        [MemberData(nameof(ValidAliases))]
        public void Clone_WithNonNullValue_PreservesAliasAndUsesValueClone(string? alias)
        {
            // Arrange
            var originalValueMock = new Mock<ValueTypeExpr>() { CallBase = false };
            var clonedValueMock = new Mock<ValueTypeExpr>() { CallBase = false };

            // Setup Clone on original to return the clone instance
            originalValueMock.Setup(v => v.Clone()).Returns(clonedValueMock.Object);

            var item = new SelectItemExpr();
            item.Value = originalValueMock.Object;
            item.Alias = alias; // valid aliases provided by MemberData

            // Act
            var result = (SelectItemExpr)item.Clone();

            // Assert
            Assert.NotSame(item, result); // different instance
            Assert.Same(clonedValueMock.Object, result.Value); // Value is the returned clone
            Assert.NotSame(originalValueMock.Object, result.Value); // not the original value instance
            Assert.Equal(alias, result.Alias); // alias preserved (including null/empty)
            originalValueMock.Verify(v => v.Clone(), Times.Once);
        }

        /// <summary>
        /// Tests that Clone throws NullReferenceException when Value is null because Clone calls Value.Clone() without null-check.
        /// </summary>
        [Fact]
        public void Clone_WhenValueIsNull_ThrowsNullReferenceException()
        {
            // Arrange
            var item = new SelectItemExpr();
            item.Value = null; // allowed through the property (constructor would prevent this)

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => item.Clone());
        }

        /// <summary>
        /// Tests that Clone throws InvalidCastException when Value.Clone() returns an Expr that is not a ValueTypeExpr,
        /// because SelectItemExpr casts the result to ValueTypeExpr.
        /// </summary>
        [Fact]
        public void Clone_WhenCloneReturnsNonValueTypeExpr_ThrowsInvalidCastException()
        {
            // Arrange
            var originalValueMock = new Mock<ValueTypeExpr>() { CallBase = false };

            // Create a mock of Expr that is not a ValueTypeExpr to simulate invalid cast scenario
            var nonValueExprMock = new Mock<Expr>() { CallBase = false };
            originalValueMock.Setup(v => v.Clone()).Returns(nonValueExprMock.Object);

            var item = new SelectItemExpr();
            item.Value = originalValueMock.Object;
            item.Alias = null;

            // Act & Assert
            Assert.Throws<InvalidCastException>(() => item.Clone());
        }

        // MemberData providing valid alias test cases (null, empty, simple, very long)
        public static IEnumerable<object?[]> ValidAliases()
        {
            yield return new object?[] { null };
            yield return new object?[] { string.Empty };
            yield return new object?[] { "UserId" };
            yield return new object?[] { new string('a', 500) }; // very long valid alias (letters only)
        }
    }
}