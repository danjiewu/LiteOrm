using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Unit tests for the ValueSet class constructor with joinType and params items.
    /// </summary>
    public partial class ValueSetTests
    {
        /// <summary>
        /// Tests that the constructor with joinType and items correctly sets JoinType and adds all items.
        /// </summary>
        /// <param name="joinType">The ValueJoinType to use</param>
        /// <param name="itemCount">Number of items to add</param>
        [Theory]
        [InlineData(ValueJoinType.List, 0)]
        [InlineData(ValueJoinType.List, 1)]
        [InlineData(ValueJoinType.List, 3)]
        [InlineData(ValueJoinType.List, 10)]
        [InlineData(ValueJoinType.Concat, 0)]
        [InlineData(ValueJoinType.Concat, 1)]
        [InlineData(ValueJoinType.Concat, 3)]
        [InlineData(ValueJoinType.Blank, 0)]
        [InlineData(ValueJoinType.Blank, 1)]
        [InlineData(ValueJoinType.Blank, 3)]
        public void Constructor_WithJoinTypeAndItems_SetsJoinTypeAndAddsItems(ValueJoinType joinType, int itemCount)
        {
            // Arrange
            var items = new ValueTypeExpr[itemCount];
            for (int i = 0; i < itemCount; i++)
            {
                items[i] = new ValueExpr(i);
            }

            // Act
            var valueSet = new ValueSet(joinType, items);

            // Assert
            Assert.Equal(joinType, valueSet.JoinType);
            Assert.Equal(itemCount, valueSet.Count);
            for (int i = 0; i < itemCount; i++)
            {
                Assert.Equal(items[i], valueSet[i]);
            }
        }

        /// <summary>
        /// Tests that the constructor with joinType and null items array sets JoinType and creates empty collection.
        /// </summary>
        [Theory]
        [InlineData(ValueJoinType.List)]
        [InlineData(ValueJoinType.Concat)]
        [InlineData(ValueJoinType.Blank)]
        public void Constructor_WithJoinTypeAndNullItems_SetsJoinTypeWithEmptyCollection(ValueJoinType joinType)
        {
            // Arrange
            ValueTypeExpr[] items = null;

            // Act
            var valueSet = new ValueSet(joinType, items);

            // Assert
            Assert.Equal(joinType, valueSet.JoinType);
            Assert.Equal(0, valueSet.Count);
        }

        /// <summary>
        /// Tests that the constructor with joinType and items containing null elements converts null to Null constant.
        /// </summary>
        [Fact]
        public void Constructor_WithJoinTypeAndItemsContainingNull_ConvertsNullToNullConstant()
        {
            // Arrange
            var items = new ValueTypeExpr[] { new ValueExpr(1), null, new ValueExpr(3) };

            // Act
            var valueSet = new ValueSet(ValueJoinType.List, items);

            // Assert
            Assert.Equal(ValueJoinType.List, valueSet.JoinType);
            Assert.Equal(3, valueSet.Count);
            Assert.NotNull(valueSet[1]);
        }

        /// <summary>
        /// Tests that the constructor with invalid enum value (outside defined range) still sets the JoinType.
        /// </summary>
        [Fact]
        public void Constructor_WithInvalidEnumValue_SetsJoinTypeToInvalidValue()
        {
            // Arrange
            var invalidJoinType = (ValueJoinType)999;
            var items = new ValueTypeExpr[] { new ValueExpr(1), new ValueExpr(2) };

            // Act
            var valueSet = new ValueSet(invalidJoinType, items);

            // Assert
            Assert.Equal(invalidJoinType, valueSet.JoinType);
            Assert.Equal(2, valueSet.Count);
        }

        /// <summary>
        /// Tests that the constructor with nested ValueSet with same JoinType flattens the items.
        /// </summary>
        [Fact]
        public void Constructor_WithNestedValueSetSameJoinType_FlattensItems()
        {
            // Arrange
            var nestedSet = new ValueSet(ValueJoinType.List, new ValueExpr(1), new ValueExpr(2));
            var items = new ValueTypeExpr[] { new ValueExpr(0), nestedSet, new ValueExpr(3) };

            // Act
            var valueSet = new ValueSet(ValueJoinType.List, items);

            // Assert
            Assert.Equal(ValueJoinType.List, valueSet.JoinType);
            Assert.Equal(4, valueSet.Count); // 0, 1, 2, 3 (flattened)
        }

        /// <summary>
        /// Tests that the constructor with nested ValueSet with different JoinType does not flatten the items.
        /// </summary>
        [Fact]
        public void Constructor_WithNestedValueSetDifferentJoinType_DoesNotFlatten()
        {
            // Arrange
            var nestedSet = new ValueSet(ValueJoinType.Concat, new ValueExpr(1), new ValueExpr(2));
            var items = new ValueTypeExpr[] { new ValueExpr(0), nestedSet, new ValueExpr(3) };

            // Act
            var valueSet = new ValueSet(ValueJoinType.List, items);

            // Assert
            Assert.Equal(ValueJoinType.List, valueSet.JoinType);
            Assert.Equal(3, valueSet.Count); // 0, nestedSet, 3 (not flattened)
            Assert.IsType<ValueExpr>(valueSet[0]);
            Assert.IsType<ValueSet>(valueSet[1]);
            Assert.IsType<ValueExpr>(valueSet[2]);
        }

        /// <summary>
        /// Tests that the constructor preserves the order of items as they are added.
        /// </summary>
        [Fact]
        public void Constructor_WithJoinTypeAndItems_PreservesItemOrder()
        {
            // Arrange
            var item1 = new ValueExpr(1);
            var item2 = new ValueExpr(2);
            var item3 = new ValueExpr(3);

            // Act
            var valueSet = new ValueSet(ValueJoinType.List, item1, item2, item3);

            // Assert
            Assert.Equal(3, valueSet.Count);
            Assert.Same(item1, valueSet[0]);
            Assert.Same(item2, valueSet[1]);
            Assert.Same(item3, valueSet[2]);
        }

        /// <summary>
        /// Tests that the constructor with empty items array creates empty collection with correct JoinType.
        /// </summary>
        [Theory]
        [InlineData(ValueJoinType.List)]
        [InlineData(ValueJoinType.Concat)]
        [InlineData(ValueJoinType.Blank)]
        public void Constructor_WithJoinTypeAndEmptyItems_CreatesEmptyCollection(ValueJoinType joinType)
        {
            // Arrange
            var items = new ValueTypeExpr[] { };

            // Act
            var valueSet = new ValueSet(joinType, items);

            // Assert
            Assert.Equal(joinType, valueSet.JoinType);
            Assert.Equal(0, valueSet.Count);
        }

        /// <summary>
        /// Tests that the constructor with large number of items correctly adds all items.
        /// </summary>
        [Fact]
        public void Constructor_WithLargeNumberOfItems_AddsAllItems()
        {
            // Arrange
            const int itemCount = 1000;
            var items = new ValueTypeExpr[itemCount];
            for (int i = 0; i < itemCount; i++)
            {
                items[i] = new ValueExpr(i);
            }

            // Act
            var valueSet = new ValueSet(ValueJoinType.List, items);

            // Assert
            Assert.Equal(ValueJoinType.List, valueSet.JoinType);
            Assert.Equal(itemCount, valueSet.Count);
        }

        /// <summary>
        /// Tests that the constructor with only null items converts all to Null constant.
        /// </summary>
        [Fact]
        public void Constructor_WithOnlyNullItems_ConvertsAllToNullConstant()
        {
            // Arrange
            var items = new ValueTypeExpr[] { null, null, null };

            // Act
            var valueSet = new ValueSet(ValueJoinType.List, items);

            // Assert
            Assert.Equal(3, valueSet.Count);
            Assert.NotNull(valueSet[0]);
            Assert.NotNull(valueSet[1]);
            Assert.NotNull(valueSet[2]);
        }

        /// <summary>
        /// Tests that the constructor with different value types correctly adds all items.
        /// </summary>
        [Fact]
        public void Constructor_WithMixedValueTypes_AddsAllItems()
        {
            // Arrange
            var items = new ValueTypeExpr[]
            {
                new ValueExpr(1),
                new ValueExpr("text"),
                new ValueExpr(true),
                new ValueExpr(3.14)
            };

            // Act
            var valueSet = new ValueSet(ValueJoinType.Concat, items);

            // Assert
            Assert.Equal(ValueJoinType.Concat, valueSet.JoinType);
            Assert.Equal(4, valueSet.Count);
        }

        /// <summary>
        /// Tests that the constructor properly initializes a ValueSet with the specified JoinType and null items collection.
        /// Expected: JoinType is set correctly, Count is 0, no exception is thrown.
        /// </summary>
        [Theory]
        [InlineData(ValueJoinType.List)]
        [InlineData(ValueJoinType.Concat)]
        [InlineData(ValueJoinType.Blank)]
        public void Constructor_WithJoinTypeAndNullItems_InitializesEmptyValueSet(ValueJoinType joinType)
        {
            // Arrange & Act
            var valueSet = new ValueSet(joinType, (IEnumerable<ValueTypeExpr>?)null);

            // Assert
            Assert.Equal(joinType, valueSet.JoinType);
            Assert.Equal(0, valueSet.Count);
            Assert.Empty(valueSet.Items);
        }

        /// <summary>
        /// Tests that the constructor properly initializes a ValueSet with the specified JoinType and empty items collection.
        /// Expected: JoinType is set correctly, Count is 0.
        /// </summary>
        [Theory]
        [InlineData(ValueJoinType.List)]
        [InlineData(ValueJoinType.Concat)]
        [InlineData(ValueJoinType.Blank)]
        public void Constructor_WithJoinTypeAndEmptyItems_InitializesEmptyValueSet(ValueJoinType joinType)
        {
            // Arrange
            var items = new List<ValueTypeExpr>();

            // Act
            var valueSet = new ValueSet(joinType, items);

            // Assert
            Assert.Equal(joinType, valueSet.JoinType);
            Assert.Equal(0, valueSet.Count);
            Assert.Empty(valueSet.Items);
        }

        /// <summary>
        /// Tests that the constructor properly initializes a ValueSet with the specified JoinType and a single item.
        /// Expected: JoinType is set correctly, Count is 1, item is accessible.
        /// </summary>
        [Theory]
        [InlineData(ValueJoinType.List)]
        [InlineData(ValueJoinType.Concat)]
        [InlineData(ValueJoinType.Blank)]
        public void Constructor_WithJoinTypeAndSingleItem_InitializesValueSetWithOneItem(ValueJoinType joinType)
        {
            // Arrange
            ValueTypeExpr item = new ValueExpr(42);
            var items = new List<ValueTypeExpr> { item };

            // Act
            var valueSet = new ValueSet(joinType, items);

            // Assert
            Assert.Equal(joinType, valueSet.JoinType);
            Assert.Equal(1, valueSet.Count);
            Assert.Single(valueSet.Items);
            Assert.Same(item, valueSet.Items[0]);
        }

        /// <summary>
        /// Tests that the constructor properly initializes a ValueSet with the specified JoinType and multiple items.
        /// Expected: JoinType is set correctly, Count matches number of items, all items are accessible in order.
        /// </summary>
        [Theory]
        [InlineData(ValueJoinType.List)]
        [InlineData(ValueJoinType.Concat)]
        [InlineData(ValueJoinType.Blank)]
        public void Constructor_WithJoinTypeAndMultipleItems_InitializesValueSetWithAllItems(ValueJoinType joinType)
        {
            // Arrange
            ValueTypeExpr item1 = new ValueExpr(1);
            ValueTypeExpr item2 = new ValueExpr(2);
            ValueTypeExpr item3 = new ValueExpr(3);
            var items = new List<ValueTypeExpr> { item1, item2, item3 };

            // Act
            var valueSet = new ValueSet(joinType, items);

            // Assert
            Assert.Equal(joinType, valueSet.JoinType);
            Assert.Equal(3, valueSet.Count);
            Assert.Equal(3, valueSet.Items.Count);
            Assert.Same(item1, valueSet.Items[0]);
            Assert.Same(item2, valueSet.Items[1]);
            Assert.Same(item3, valueSet.Items[2]);
        }

        /// <summary>
        /// Tests that the constructor handles collections with null items by replacing them with Expr.Null.
        /// Expected: Null items are replaced with Expr.Null, Count reflects all items including nulls.
        /// </summary>
        [Fact]
        public void Constructor_WithJoinTypeAndCollectionContainingNullItems_ReplacesNullsWithExprNull()
        {
            // Arrange
            ValueTypeExpr item1 = new ValueExpr(1);
            ValueTypeExpr? item2 = null;
            ValueTypeExpr item3 = new ValueExpr(3);
            var items = new List<ValueTypeExpr?> { item1, item2, item3 };

            // Act
            var valueSet = new ValueSet(ValueJoinType.List, items!);

            // Assert
            Assert.Equal(ValueJoinType.List, valueSet.JoinType);
            Assert.Equal(3, valueSet.Count);
            Assert.Same(item1, valueSet.Items[0]);
            Assert.Same(Expr.Null, valueSet.Items[1]);
            Assert.Same(item3, valueSet.Items[2]);
        }

        /// <summary>
        /// Tests that the constructor properly handles invalid enum values for JoinType.
        /// Expected: The invalid enum value is set without throwing an exception.
        /// </summary>
        [Fact]
        public void Constructor_WithInvalidJoinTypeEnumValue_SetsJoinTypeWithoutException()
        {
            // Arrange
            var invalidJoinType = (ValueJoinType)999;
            ValueTypeExpr item = new ValueExpr(42);
            var items = new List<ValueTypeExpr> { item };

            // Act
            var valueSet = new ValueSet(invalidJoinType, items);

            // Assert
            Assert.Equal(invalidJoinType, valueSet.JoinType);
            Assert.Equal(1, valueSet.Count);
        }

        /// <summary>
        /// Tests that the constructor properly handles different IEnumerable implementations (array, list, custom enumerable).
        /// Expected: All items from the enumerable are added to the ValueSet.
        /// </summary>
        [Fact]
        public void Constructor_WithDifferentEnumerableImplementations_AddsAllItems()
        {
            // Arrange
            ValueTypeExpr item1 = new ValueExpr(1);
            ValueTypeExpr item2 = new ValueExpr(2);

            // Test with array
            var arrayItems = new ValueTypeExpr[] { item1, item2 };
            var valueSetFromArray = new ValueSet(ValueJoinType.List, arrayItems);

            // Test with List
            var listItems = new List<ValueTypeExpr> { item1, item2 };
            var valueSetFromList = new ValueSet(ValueJoinType.List, listItems);

            // Test with custom enumerable (using LINQ)
            var enumerableItems = new List<ValueTypeExpr> { item1, item2 }.Where(x => x != null);
            var valueSetFromEnumerable = new ValueSet(ValueJoinType.List, enumerableItems);

            // Assert
            Assert.Equal(2, valueSetFromArray.Count);
            Assert.Equal(2, valueSetFromList.Count);
            Assert.Equal(2, valueSetFromEnumerable.Count);
        }

        /// <summary>
        /// Tests that the constructor properly flattens nested ValueSet with same JoinType.
        /// Expected: Items from nested ValueSet are added directly to the collection.
        /// </summary>
        [Fact]
        public void Constructor_WithNestedValueSetSameJoinType_FlattensNestedItems()
        {
            // Arrange
            ValueTypeExpr item1 = new ValueExpr(1);
            ValueTypeExpr item2 = new ValueExpr(2);
            var nestedValueSet = new ValueSet(ValueJoinType.List, new List<ValueTypeExpr> { item1, item2 });

            ValueTypeExpr item3 = new ValueExpr(3);
            var items = new List<ValueTypeExpr> { nestedValueSet, item3 };

            // Act
            var valueSet = new ValueSet(ValueJoinType.List, items);

            // Assert
            Assert.Equal(ValueJoinType.List, valueSet.JoinType);
            Assert.Equal(3, valueSet.Count);
            Assert.Same(item1, valueSet.Items[0]);
            Assert.Same(item2, valueSet.Items[1]);
            Assert.Same(item3, valueSet.Items[2]);
        }

        /// <summary>
        /// Tests that the constructor properly handles nested ValueSet with different JoinType.
        /// Expected: The nested ValueSet is added as a single item, not flattened.
        /// </summary>
        [Fact]
        public void Constructor_WithNestedValueSetDifferentJoinType_DoesNotFlattenNestedItems()
        {
            // Arrange
            ValueTypeExpr item1 = new ValueExpr(1);
            ValueTypeExpr item2 = new ValueExpr(2);
            var nestedValueSet = new ValueSet(ValueJoinType.Concat, new List<ValueTypeExpr> { item1, item2 });

            ValueTypeExpr item3 = new ValueExpr(3);
            var items = new List<ValueTypeExpr> { nestedValueSet, item3 };

            // Act
            var valueSet = new ValueSet(ValueJoinType.List, items);

            // Assert
            Assert.Equal(ValueJoinType.List, valueSet.JoinType);
            Assert.Equal(2, valueSet.Count);
            Assert.Same(nestedValueSet, valueSet.Items[0]);
            Assert.Same(item3, valueSet.Items[1]);
        }

        /// <summary>
        /// Tests that the constructor with List JoinType maintains item order.
        /// Expected: Items are stored in the same order as provided.
        /// </summary>
        [Fact]
        public void Constructor_WithListJoinType_MaintainsItemOrder()
        {
            // Arrange
            ValueTypeExpr item1 = new ValueExpr(10);
            ValueTypeExpr item2 = new ValueExpr(5);
            ValueTypeExpr item3 = new ValueExpr(15);
            var items = new List<ValueTypeExpr> { item1, item2, item3 };

            // Act
            var valueSet = new ValueSet(ValueJoinType.List, items);

            // Assert
            Assert.Equal(3, valueSet.Count);
            Assert.Same(item1, valueSet.Items[0]);
            Assert.Same(item2, valueSet.Items[1]);
            Assert.Same(item3, valueSet.Items[2]);
        }

        /// <summary>
        /// Tests that the constructor with Concat JoinType maintains item order.
        /// Expected: Items are stored in the same order as provided.
        /// </summary>
        [Fact]
        public void Constructor_WithConcatJoinType_MaintainsItemOrder()
        {
            // Arrange
            ValueTypeExpr item1 = new ValueExpr("Hello");
            ValueTypeExpr item2 = new ValueExpr(" ");
            ValueTypeExpr item3 = new ValueExpr("World");
            var items = new List<ValueTypeExpr> { item1, item2, item3 };

            // Act
            var valueSet = new ValueSet(ValueJoinType.Concat, items);

            // Assert
            Assert.Equal(ValueJoinType.Concat, valueSet.JoinType);
            Assert.Equal(3, valueSet.Count);
            Assert.Same(item1, valueSet.Items[0]);
            Assert.Same(item2, valueSet.Items[1]);
            Assert.Same(item3, valueSet.Items[2]);
        }

        /// <summary>
        /// Tests that the constructor with Blank JoinType sets the JoinType correctly.
        /// Expected: JoinType is set to Blank and items are added.
        /// </summary>
        [Fact]
        public void Constructor_WithBlankJoinType_SetsJoinTypeCorrectly()
        {
            // Arrange
            ValueTypeExpr item1 = new ValueExpr("First");
            ValueTypeExpr item2 = new ValueExpr("Second");
            var items = new List<ValueTypeExpr> { item1, item2 };

            // Act
            var valueSet = new ValueSet(ValueJoinType.Blank, items);

            // Assert
            Assert.Equal(ValueJoinType.Blank, valueSet.JoinType);
            Assert.Equal(2, valueSet.Count);
        }

        /// <summary>
        /// Tests that the constructor handles a large collection of items efficiently.
        /// Expected: All items are added correctly, Count matches number of items.
        /// </summary>
        [Fact]
        public void Constructor_WithLargeCollection_AddsAllItems()
        {
            // Arrange
            var items = new List<ValueTypeExpr>();
            for (int i = 0; i < 1000; i++)
            {
                items.Add(new ValueExpr(i));
            }

            // Act
            var valueSet = new ValueSet(ValueJoinType.List, items);

            // Assert
            Assert.Equal(ValueJoinType.List, valueSet.JoinType);
            Assert.Equal(1000, valueSet.Count);
            Assert.Equal(1000, valueSet.Items.Count);
        }

        /// <summary>
        /// Tests that the constructor with duplicate items adds all duplicates.
        /// Expected: All duplicate items are added to the collection.
        /// </summary>
        [Fact]
        public void Constructor_WithDuplicateItems_AddsAllDuplicates()
        {
            // Arrange
            ValueTypeExpr item = new ValueExpr(42);
            var items = new List<ValueTypeExpr> { item, item, item };

            // Act
            var valueSet = new ValueSet(ValueJoinType.List, items);

            // Assert
            Assert.Equal(3, valueSet.Count);
            Assert.Same(item, valueSet.Items[0]);
            Assert.Same(item, valueSet.Items[1]);
            Assert.Same(item, valueSet.Items[2]);
        }

        /// <summary>
        /// Tests that Items property returns an empty ReadOnlyCollection when ValueSet is newly created.
        /// </summary>
        [Fact]
        public void Items_WhenValueSetIsEmpty_ReturnsEmptyReadOnlyCollection()
        {
            // Arrange
            var valueSet = new ValueSet();

            // Act
            ReadOnlyCollection<ValueTypeExpr> result = valueSet.Items;

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            Assert.IsType<ReadOnlyCollection<ValueTypeExpr>>(result);
        }

        /// <summary>
        /// Tests that Items property returns a ReadOnlyCollection with a single item after adding one element.
        /// </summary>
        [Fact]
        public void Items_WhenSingleItemAdded_ReturnsCollectionWithOneItem()
        {
            // Arrange
            var valueSet = new ValueSet();
            ValueTypeExpr item = 42;

            // Act
            valueSet.Add(item);
            ReadOnlyCollection<ValueTypeExpr> result = valueSet.Items;

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(item, result[0]);
        }

        /// <summary>
        /// Tests that Items property returns a ReadOnlyCollection with multiple items in the correct order.
        /// </summary>
        [Fact]
        public void Items_WhenMultipleItemsAdded_ReturnsCollectionWithAllItemsInOrder()
        {
            // Arrange
            var valueSet = new ValueSet();
            ValueTypeExpr item1 = 1;
            ValueTypeExpr item2 = "test";
            ValueTypeExpr item3 = 3.14;

            // Act
            valueSet.Add(item1);
            valueSet.Add(item2);
            valueSet.Add(item3);
            ReadOnlyCollection<ValueTypeExpr> result = valueSet.Items;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal(item1, result[0]);
            Assert.Equal(item2, result[1]);
            Assert.Equal(item3, result[2]);
        }

        /// <summary>
        /// Tests that Items property count matches the Count property of ValueSet.
        /// </summary>
        [Fact]
        public void Items_Count_MatchesValueSetCount()
        {
            // Arrange
            var valueSet = new ValueSet();
            valueSet.Add(1);
            valueSet.Add(2);
            valueSet.Add(3);

            // Act
            ReadOnlyCollection<ValueTypeExpr> result = valueSet.Items;

            // Assert
            Assert.Equal(valueSet.Count, result.Count);
        }

        /// <summary>
        /// Tests that Items property returns an empty collection after clearing the ValueSet.
        /// </summary>
        [Fact]
        public void Items_AfterClear_ReturnsEmptyCollection()
        {
            // Arrange
            var valueSet = new ValueSet();
            valueSet.Add(1);
            valueSet.Add(2);
            valueSet.Clear();

            // Act
            ReadOnlyCollection<ValueTypeExpr> result = valueSet.Items;

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that Items property returns updated collection after removing an item.
        /// </summary>
        [Fact]
        public void Items_AfterRemovingItem_ReturnsUpdatedCollection()
        {
            // Arrange
            var valueSet = new ValueSet();
            ValueTypeExpr item1 = 1;
            ValueTypeExpr item2 = 2;
            valueSet.Add(item1);
            valueSet.Add(item2);

            // Act
            valueSet.Remove(item1);
            ReadOnlyCollection<ValueTypeExpr> result = valueSet.Items;

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal(item2, result[0]);
            Assert.DoesNotContain(item1, result);
        }

        /// <summary>
        /// Tests that Items property returns a new ReadOnlyCollection wrapper on each access.
        /// </summary>
        [Fact]
        public void Items_CalledMultipleTimes_ReturnsNewWrapperEachTime()
        {
            // Arrange
            var valueSet = new ValueSet();
            valueSet.Add(1);

            // Act
            ReadOnlyCollection<ValueTypeExpr> result1 = valueSet.Items;
            ReadOnlyCollection<ValueTypeExpr> result2 = valueSet.Items;

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.NotSame(result1, result2);
            Assert.Equal(result1.Count, result2.Count);
        }

        /// <summary>
        /// Tests that Items property returns a collection that reflects changes made to the ValueSet.
        /// </summary>
        [Fact]
        public void Items_AfterModification_ReflectsCurrentState()
        {
            // Arrange
            var valueSet = new ValueSet();
            valueSet.Add(1);
            ReadOnlyCollection<ValueTypeExpr> firstSnapshot = valueSet.Items;

            // Act
            valueSet.Add(2);
            ReadOnlyCollection<ValueTypeExpr> secondSnapshot = valueSet.Items;

            // Assert
            Assert.Single(firstSnapshot);
            Assert.Equal(2, secondSnapshot.Count);
        }

        /// <summary>
        /// Tests that Items property returns a ReadOnlyCollection for a ValueSet initialized with constructor items.
        /// </summary>
        [Fact]
        public void Items_WhenInitializedWithConstructorItems_ReturnsAllItems()
        {
            // Arrange
            ValueTypeExpr item1 = 1;
            ValueTypeExpr item2 = "test";
            ValueTypeExpr item3 = true;

            // Act
            var valueSet = new ValueSet(item1, item2, item3);
            ReadOnlyCollection<ValueTypeExpr> result = valueSet.Items;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Contains(item1, result);
            Assert.Contains(item2, result);
            Assert.Contains(item3, result);
        }

        /// <summary>
        /// Tests that Items property returns a ReadOnlyCollection for a ValueSet initialized with IEnumerable.
        /// </summary>
        [Fact]
        public void Items_WhenInitializedWithEnumerable_ReturnsAllItems()
        {
            // Arrange
            var items = new List<ValueTypeExpr> { 1, 2, 3 };

            // Act
            var valueSet = new ValueSet(items);
            ReadOnlyCollection<ValueTypeExpr> result = valueSet.Items;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Equal(items[0], result[0]);
            Assert.Equal(items[1], result[1]);
            Assert.Equal(items[2], result[2]);
        }

        /// <summary>
        /// Tests that Items property returns a ReadOnlyCollection that cannot be modified by casting.
        /// </summary>
        [Fact]
        public void Items_ReturnsReadOnlyCollection_CannotBeCastToList()
        {
            // Arrange
            var valueSet = new ValueSet();
            valueSet.Add(1);

            // Act
            ReadOnlyCollection<ValueTypeExpr> result = valueSet.Items;

            // Assert
            Assert.IsNotType<List<ValueTypeExpr>>(result);
            Assert.IsType<ReadOnlyCollection<ValueTypeExpr>>(result);
        }

        /// <summary>
        /// Tests that Items property returns a non-null collection even when no items have been added.
        /// </summary>
        [Fact]
        public void Items_NeverReturnsNull_EvenWhenEmpty()
        {
            // Arrange
            var valueSet = new ValueSet();

            // Act
            ReadOnlyCollection<ValueTypeExpr> result = valueSet.Items;

            // Assert
            Assert.NotNull(result);
        }

        /// <summary>
        /// Tests that Items property works correctly with AddRange method.
        /// </summary>
        [Fact]
        public void Items_AfterAddRange_ReturnsAllAddedItems()
        {
            // Arrange
            var valueSet = new ValueSet();
            var itemsToAdd = new List<ValueTypeExpr> { 1, 2, 3, 4, 5 };

            // Act
            valueSet.AddRange(itemsToAdd);
            ReadOnlyCollection<ValueTypeExpr> result = valueSet.Items;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.Count);
            for (int i = 0; i < itemsToAdd.Count; i++)
            {
                Assert.Equal(itemsToAdd[i], result[i]);
            }
        }

        /// <summary>
        /// Provides test cases for ToString: various join types and item string representations.
        /// Each case supplies:
        /// - ValueJoinType: the JoinType to use for the ValueSet.
        /// - string[]: the sequence of strings that mocked ValueTypeExpr.ToString() will return (null allowed).
        /// - string: the expected ToString() result from the ValueSet.
        /// </summary>
        public static IEnumerable<object[]> ToStringTestCases()
        {
            // Empty collection -> empty string regardless of join type (List)
            yield return new object[] { ValueJoinType.List, new string[0], string.Empty };

            // Single item -> parentheses containing single representation
            yield return new object[] { ValueJoinType.List, new[] { "Solo" }, "(Solo)" };

            // Multiple items with List join type -> comma separated
            yield return new object[] { ValueJoinType.List, new[] { "1", "2", "3" }, "(1,2,3)" };

            // Multiple items with Concat join type -> ' || ' separated
            yield return new object[] { ValueJoinType.Concat, new[] { "First", "Second" }, "(First || Second)" };

            // Null item ToString() returns null -> string.Join treats it as empty string segment
            yield return new object[] { ValueJoinType.List, new string[] { null, "B" }, "(,B)" };

            // Whitespace and special characters preserved
            yield return new object[] { ValueJoinType.Concat, new[] { " A ", "\t\n" }, "( A  || \t\n)" };

            // Very long strings (boundary case)
            string longStr = new string('x', 1024);
            yield return new object[] { ValueJoinType.List, new[] { longStr, "end" }, $"({longStr},end)" };
        }

        /// <summary>
        /// Tests ValueSet.ToString() for a variety of join types and item string representations.
        /// Input conditions:
        /// - joinType: ValueJoinType specified by the test case.
        /// - itemStrings: array of strings that mocked ValueTypeExpr.ToString() should return (may contain null).
        /// Expected result:
        /// - The ValueSet.ToString() matches the expected string produced by joining the items with the proper separator,
        ///   enclosed in parentheses, or empty string when there are no items.
        /// </summary>
        [Theory]
        [MemberData(nameof(ToStringTestCases))]
        public void ToString_VariousJoinTypesAndItems_ReturnsExpected(ValueJoinType joinType, string[] itemStrings, string expected)
        {
            // Arrange
            var mocks = itemStrings.Select(s =>
            {
                var m = new Mock<ValueTypeExpr>(MockBehavior.Strict);
                // Setup ToString to return provided string (null allowed)
                m.Setup(x => x.ToString()).Returns(s);
                return m.Object;
            }).ToArray();

            ValueSet vs;
            // Use constructor that sets JoinType for clarity when joinType provided differs from default
            if (mocks.Length == 0)
            {
                // Use parameterless ctor to ensure default behavior for empty collection is tested as well
                vs = new ValueSet();
                vs.JoinType = joinType;
            }
            else
            {
                vs = new ValueSet(joinType, mocks);
            }

            // Act
            string result = vs.ToString();

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that the default constructor creates a valid instance with correct default values.
        /// </summary>
        [Fact]
        public void ValueSet_DefaultConstructor_CreatesValidInstanceWithDefaultValues()
        {
            // Arrange & Act
            var valueSet = new ValueSet();

            // Assert
            Assert.NotNull(valueSet);
            Assert.Equal(ValueJoinType.List, valueSet.JoinType);
            Assert.Equal(0, valueSet.Count);
            Assert.False(valueSet.IsReadOnly);
            Assert.Equal(ExprType.ValueSet, valueSet.ExprType);
            Assert.NotNull(valueSet.Items);
            Assert.Equal(0, valueSet.Items.Count);
        }

        /// <summary>
        /// Tests that the default constructor creates an instance with an empty collection
        /// that can be enumerated without errors.
        /// </summary>
        [Fact]
        public void ValueSet_DefaultConstructor_CreatesEnumerableEmptyCollection()
        {
            // Arrange & Act
            var valueSet = new ValueSet();
            var count = 0;

            foreach (var item in valueSet)
            {
                count++;
            }

            // Assert
            Assert.Equal(0, count);
        }

        /// <summary>
        /// Tests that the default constructor creates an instance that can immediately accept items.
        /// </summary>
        [Fact]
        public void ValueSet_DefaultConstructor_AllowsImmediateItemAddition()
        {
            // Arrange
            var valueSet = new ValueSet();
            var testExpr = new ValueExpr(42);

            // Act
            valueSet.Add(testExpr);

            // Assert
            Assert.Equal(1, valueSet.Count);
            Assert.Contains(testExpr, valueSet);
        }

        /// <summary>
        /// Tests that multiple instances created by the default constructor are independent.
        /// </summary>
        [Fact]
        public void ValueSet_DefaultConstructor_CreatesIndependentInstances()
        {
            // Arrange & Act
            var valueSet1 = new ValueSet();
            var valueSet2 = new ValueSet();
            var testExpr = new ValueExpr(123);

            valueSet1.Add(testExpr);

            // Assert
            Assert.Equal(1, valueSet1.Count);
            Assert.Equal(0, valueSet2.Count);
            Assert.NotSame(valueSet1, valueSet2);
        }

        /// <summary>
        /// Tests that the default constructor creates an instance with JoinType that can be modified.
        /// </summary>
        [Fact]
        public void ValueSet_DefaultConstructor_AllowsJoinTypeModification()
        {
            // Arrange
            var valueSet = new ValueSet();

            // Act
            valueSet.JoinType = ValueJoinType.Concat;

            // Assert
            Assert.Equal(ValueJoinType.Concat, valueSet.JoinType);
        }

        /// <summary>
        /// Tests that the default constructor creates an instance whose ToString returns empty string.
        /// </summary>
        [Fact]
        public void ValueSet_DefaultConstructor_ToStringReturnsEmptyString()
        {
            // Arrange & Act
            var valueSet = new ValueSet();
            var result = valueSet.ToString();

            // Assert
            Assert.Equal(string.Empty, result);
        }

        /// <summary>
        /// Tests that the default constructor creates an instance that supports collection operations.
        /// </summary>
        [Fact]
        public void ValueSet_DefaultConstructor_SupportsCollectionOperations()
        {
            // Arrange
            var valueSet = new ValueSet();
            var testExpr = new ValueExpr(10);

            // Act & Assert - Clear on empty collection
            valueSet.Clear();
            Assert.Equal(0, valueSet.Count);

            // Act & Assert - Contains on empty collection
            Assert.False(valueSet.Contains(testExpr));

            // Act & Assert - Remove on empty collection
            Assert.False(valueSet.Remove(testExpr));

            // Act & Assert - CopyTo on empty collection
            var array = new ValueTypeExpr[5];
            valueSet.CopyTo(array, 0);
            Assert.Null(array[0]);
        }

        /// <summary>
        /// Tests that the constructor with a null array parameter creates an empty collection.
        /// </summary>
        [Fact]
        public void Constructor_WithNullArray_CreatesEmptyCollection()
        {
            // Arrange
            ValueTypeExpr[]? items = null;

            // Act
            var result = new ValueSet(items);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.Count);
            Assert.Equal(ValueJoinType.List, result.JoinType);
        }

        /// <summary>
        /// Tests that the constructor with an empty array creates an empty collection.
        /// </summary>
        [Fact]
        public void Constructor_WithEmptyArray_CreatesEmptyCollection()
        {
            // Arrange
            var items = Array.Empty<ValueTypeExpr>();

            // Act
            var result = new ValueSet(items);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.Count);
            Assert.Equal(ValueJoinType.List, result.JoinType);
        }

        /// <summary>
        /// Tests that the constructor with a single item adds that item to the collection.
        /// </summary>
        [Fact]
        public void Constructor_WithSingleItem_AddsSingleItem()
        {
            // Arrange
            ValueTypeExpr item = Expr.Const(42);

            // Act
            var result = new ValueSet(item);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(1, result.Count);
            Assert.Contains(item, result.Items);
        }

        /// <summary>
        /// Tests that the constructor with multiple items adds all items to the collection.
        /// </summary>
        [Fact]
        public void Constructor_WithMultipleItems_AddsAllItems()
        {
            // Arrange
            ValueTypeExpr item1 = Expr.Const(1);
            ValueTypeExpr item2 = Expr.Const(2);
            ValueTypeExpr item3 = Expr.Const(3);

            // Act
            var result = new ValueSet(item1, item2, item3);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Contains(item1, result.Items);
            Assert.Contains(item2, result.Items);
            Assert.Contains(item3, result.Items);
        }

        /// <summary>
        /// Tests that the constructor with null elements in the array converts them to Expr.Null.
        /// </summary>
        [Fact]
        public void Constructor_WithNullElements_ConvertsNullsToExprNull()
        {
            // Arrange
            ValueTypeExpr item1 = Expr.Const(1);
            ValueTypeExpr? item2 = null;
            ValueTypeExpr item3 = Expr.Const(3);

            // Act
            var result = new ValueSet(item1, item2, item3);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Contains(item1, result.Items);
            Assert.Contains(Expr.Null, result.Items);
            Assert.Contains(item3, result.Items);
        }

        /// <summary>
        /// Tests that the constructor with a nested ValueSet having the same JoinType flattens the collection.
        /// </summary>
        [Fact]
        public void Constructor_WithNestedValueSetSameJoinType_FlattensCollection()
        {
            // Arrange
            ValueTypeExpr item1 = Expr.Const(1);
            ValueTypeExpr item2 = Expr.Const(2);
            var nestedSet = new ValueSet(ValueJoinType.List, Expr.Const(3), Expr.Const(4));
            ValueTypeExpr item5 = Expr.Const(5);

            // Act
            var result = new ValueSet(item1, item2, nestedSet, item5);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.Count);
            Assert.Contains(item1, result.Items);
            Assert.Contains(item2, result.Items);
            Assert.Contains(Expr.Const(3), result.Items);
            Assert.Contains(Expr.Const(4), result.Items);
            Assert.Contains(item5, result.Items);
        }

        /// <summary>
        /// Tests that the constructor with a nested ValueSet having a different JoinType adds it as a single item.
        /// </summary>
        [Fact]
        public void Constructor_WithNestedValueSetDifferentJoinType_AddsValueSetAsItem()
        {
            // Arrange
            ValueTypeExpr item1 = Expr.Const(1);
            var nestedSet = new ValueSet(ValueJoinType.Concat, Expr.Const(2), Expr.Const(3));
            ValueTypeExpr item4 = Expr.Const(4);

            // Act
            var result = new ValueSet(item1, nestedSet, item4);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Contains(item1, result.Items);
            Assert.Contains(nestedSet, result.Items);
            Assert.Contains(item4, result.Items);
        }

        /// <summary>
        /// Tests that the constructor with various primitive types (using implicit conversions) adds all items.
        /// </summary>
        [Fact]
        public void Constructor_WithVariousPrimitiveTypes_AddsAllItems()
        {
            // Arrange & Act
            var result = new ValueSet(
                (ValueTypeExpr)123,
                (ValueTypeExpr)"test",
                (ValueTypeExpr)true,
                (ValueTypeExpr)3.14
            );

            // Assert
            Assert.NotNull(result);
            Assert.Equal(4, result.Count);
        }

        /// <summary>
        /// Tests that the constructor with only null elements creates a collection with Expr.Null items.
        /// </summary>
        [Fact]
        public void Constructor_WithOnlyNullElements_CreatesCollectionWithExprNulls()
        {
            // Arrange
            ValueTypeExpr? item1 = null;
            ValueTypeExpr? item2 = null;
            ValueTypeExpr? item3 = null;

            // Act
            var result = new ValueSet(item1, item2, item3);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.All(result.Items, item => Assert.Equal(Expr.Null, item));
        }

        /// <summary>
        /// Tests that the constructor initializes JoinType to the default value of List.
        /// </summary>
        [Fact]
        public void Constructor_InitializesJoinTypeToDefaultList()
        {
            // Arrange
            ValueTypeExpr item = Expr.Const(1);

            // Act
            var result = new ValueSet(item);

            // Assert
            Assert.Equal(ValueJoinType.List, result.JoinType);
        }

        /// <summary>
        /// Tests that Count returns 0 when ValueSet is initialized with default constructor.
        /// </summary>
        [Fact]
        public void Count_DefaultConstructor_ReturnsZero()
        {
            // Arrange
            var valueSet = new ValueSet();

            // Act
            var count = valueSet.Count;

            // Assert
            Assert.Equal(0, count);
        }

        /// <summary>
        /// Tests that Count returns correct value after adding items.
        /// Input: Adding specified number of items.
        /// Expected: Count equals the number of items added.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(100)]
        public void Count_AfterAddingItems_ReturnsCorrectCount(int itemCount)
        {
            // Arrange
            var valueSet = new ValueSet();
            for (int i = 0; i < itemCount; i++)
            {
                var mockExpr = new Mock<ValueTypeExpr>();
                valueSet.Add(mockExpr.Object);
            }

            // Act
            var count = valueSet.Count;

            // Assert
            Assert.Equal(itemCount, count);
        }

        /// <summary>
        /// Tests that Count returns correct value when initialized with params constructor.
        /// Input: Array of ValueTypeExpr items.
        /// Expected: Count equals the number of items in the array.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        public void Count_ParamsConstructor_ReturnsCorrectCount(int itemCount)
        {
            // Arrange
            var items = new ValueTypeExpr[itemCount];
            for (int i = 0; i < itemCount; i++)
            {
                items[i] = new Mock<ValueTypeExpr>().Object;
            }

            // Act
            var valueSet = new ValueSet(items);

            // Assert
            Assert.Equal(itemCount, valueSet.Count);
        }

        /// <summary>
        /// Tests that Count returns correct value when initialized with IEnumerable constructor.
        /// Input: IEnumerable collection of ValueTypeExpr items.
        /// Expected: Count equals the number of items in the collection.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        public void Count_IEnumerableConstructor_ReturnsCorrectCount(int itemCount)
        {
            // Arrange
            var items = new List<ValueTypeExpr>();
            for (int i = 0; i < itemCount; i++)
            {
                items.Add(new Mock<ValueTypeExpr>().Object);
            }

            // Act
            var valueSet = new ValueSet(items);

            // Assert
            Assert.Equal(itemCount, valueSet.Count);
        }

        /// <summary>
        /// Tests that Count returns 0 when initialized with null params array.
        /// Input: null params array.
        /// Expected: Count returns 0.
        /// </summary>
        [Fact]
        public void Count_NullParamsConstructor_ReturnsZero()
        {
            // Arrange & Act
            var valueSet = new ValueSet((ValueTypeExpr[])null);

            // Assert
            Assert.Equal(0, valueSet.Count);
        }

        /// <summary>
        /// Tests that Count returns 0 when initialized with null IEnumerable.
        /// Input: null IEnumerable collection.
        /// Expected: Count returns 0.
        /// </summary>
        [Fact]
        public void Count_NullIEnumerableConstructor_ReturnsZero()
        {
            // Arrange & Act
            var valueSet = new ValueSet((IEnumerable<ValueTypeExpr>)null);

            // Assert
            Assert.Equal(0, valueSet.Count);
        }

        /// <summary>
        /// Tests that Count returns 0 after calling Clear on non-empty collection.
        /// Input: Collection with items, then Clear is called.
        /// Expected: Count returns 0 after Clear.
        /// </summary>
        [Fact]
        public void Count_AfterClear_ReturnsZero()
        {
            // Arrange
            var valueSet = new ValueSet();
            valueSet.Add(new Mock<ValueTypeExpr>().Object);
            valueSet.Add(new Mock<ValueTypeExpr>().Object);
            valueSet.Add(new Mock<ValueTypeExpr>().Object);

            // Act
            valueSet.Clear();

            // Assert
            Assert.Equal(0, valueSet.Count);
        }

        /// <summary>
        /// Tests that Count decreases correctly after removing an item.
        /// Input: Collection with items, then one item is removed.
        /// Expected: Count decreases by 1.
        /// </summary>
        [Fact]
        public void Count_AfterRemovingItem_DecreasesCorrectly()
        {
            // Arrange
            var valueSet = new ValueSet();
            var item1 = new Mock<ValueTypeExpr>().Object;
            var item2 = new Mock<ValueTypeExpr>().Object;
            var item3 = new Mock<ValueTypeExpr>().Object;
            valueSet.Add(item1);
            valueSet.Add(item2);
            valueSet.Add(item3);

            // Act
            valueSet.Remove(item2);

            // Assert
            Assert.Equal(2, valueSet.Count);
        }

        /// <summary>
        /// Tests that Count increases correctly after using AddRange.
        /// Input: Collection with AddRange called to add multiple items.
        /// Expected: Count reflects all added items.
        /// </summary>
        [Fact]
        public void Count_AfterAddRange_ReturnsCorrectCount()
        {
            // Arrange
            var valueSet = new ValueSet();
            valueSet.Add(new Mock<ValueTypeExpr>().Object);
            var additionalItems = new List<ValueTypeExpr>
            {
                new Mock<ValueTypeExpr>().Object,
                new Mock<ValueTypeExpr>().Object,
                new Mock<ValueTypeExpr>().Object
            };

            // Act
            valueSet.AddRange(additionalItems);

            // Assert
            Assert.Equal(4, valueSet.Count);
        }

        /// <summary>
        /// Tests that Count returns correct value when initialized with JoinType constructor.
        /// Input: JoinType and array of items.
        /// Expected: Count equals the number of items in the array.
        /// </summary>
        [Fact]
        public void Count_JoinTypeParamsConstructor_ReturnsCorrectCount()
        {
            // Arrange
            var items = new ValueTypeExpr[]
            {
                new Mock<ValueTypeExpr>().Object,
                new Mock<ValueTypeExpr>().Object
            };

            // Act
            var valueSet = new ValueSet(ValueJoinType.Concat, items);

            // Assert
            Assert.Equal(2, valueSet.Count);
        }

        /// <summary>
        /// Tests that Count returns correct value when initialized with JoinType IEnumerable constructor.
        /// Input: JoinType and IEnumerable collection of items.
        /// Expected: Count equals the number of items in the collection.
        /// </summary>
        [Fact]
        public void Count_JoinTypeIEnumerableConstructor_ReturnsCorrectCount()
        {
            // Arrange
            var items = new List<ValueTypeExpr>
            {
                new Mock<ValueTypeExpr>().Object,
                new Mock<ValueTypeExpr>().Object,
                new Mock<ValueTypeExpr>().Object
            };

            // Act
            var valueSet = new ValueSet(ValueJoinType.List, items);

            // Assert
            Assert.Equal(3, valueSet.Count);
        }

        /// <summary>
        /// Tests that Count returns 0 for empty collection regardless of JoinType.
        /// Input: JoinType with null items array.
        /// Expected: Count returns 0.
        /// </summary>
        [Fact]
        public void Count_JoinTypeWithNullParams_ReturnsZero()
        {
            // Arrange & Act
            var valueSet = new ValueSet(ValueJoinType.Concat, (ValueTypeExpr[])null);

            // Assert
            Assert.Equal(0, valueSet.Count);
        }

        /// <summary>
        /// Tests that Count handles adding null item (which gets converted to Null).
        /// Input: Null item added to collection.
        /// Expected: Count increases by 1.
        /// </summary>
        [Fact]
        public void Count_AfterAddingNullItem_IncrementsCorrectly()
        {
            // Arrange
            var valueSet = new ValueSet();

            // Act
            valueSet.Add(null);

            // Assert
            Assert.Equal(1, valueSet.Count);
        }

        /// <summary>
        /// Provides test cases where two ValueSet instances have identical JoinType and identical sequences of integer values.
        /// Expected result: GetHashCode returns equal values for both instances.
        /// </summary>
        public static IEnumerable<object[]> EqualHashCases()
        {
            yield return new object[] { ValueJoinType.List, new int[] { } };
            yield return new object[] { ValueJoinType.List, new int[] { 1 } };
            yield return new object[] { ValueJoinType.List, new int[] { 1, 2, 3 } };
            yield return new object[] { ValueJoinType.Concat, new int[] { 1, 2, 3 } };
            // duplicate values kept in same order should still be identical between two instances
            yield return new object[] { ValueJoinType.List, new int[] { 5, 5, 5 } };
        }

        /// <summary>
        /// Provides test cases where two ValueSet instances differ by JoinType, element order, or element multiplicity.
        /// Expected result: GetHashCode should differ between the two instances.
        /// </summary>
        public static IEnumerable<object[]> DifferentHashCases()
        {
            // same items different JoinType
            yield return new object[] { ValueJoinType.List, ValueJoinType.Concat, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 } };
            // same JoinType different order (order should matter)
            yield return new object[] { ValueJoinType.List, ValueJoinType.List, new int[] { 1, 2, 3 }, new int[] { 3, 2, 1 } };
            // multiplicity: [1] vs [1,1]
            yield return new object[] { ValueJoinType.List, ValueJoinType.List, new int[] { 1 }, new int[] { 1, 1 } };
            // extreme/out-of-range join type vs default
            yield return new object[] { ValueJoinType.List, (ValueJoinType)int.MaxValue, new int[] { 7, 8 }, new int[] { 7, 8 } };
        }

        /// <summary>
        /// Arrange: create two ValueSet instances with the same JoinType and same sequence of integer values.
        /// Act: compute GetHashCode for both.
        /// Assert: hash codes must be equal.
        /// </summary>
        /// <param name="joinType">The ValueJoinType for both ValueSet instances.</param>
        /// <param name="values">Sequence of integer values to populate the ValueSet.</param>
        [Theory]
        [MemberData(nameof(EqualHashCases))]
        public void GetHashCode_SameContent_SameHash(ValueJoinType joinType, int[] values)
        {
            // Arrange
            var vs1 = BuildValueSet(joinType, values);
            var vs2 = BuildValueSet(joinType, values);

            // Act
            int h1 = vs1.GetHashCode();
            int h2 = vs2.GetHashCode();

            // Assert
            Assert.Equal(h1, h2);
        }

        /// <summary>
        /// Arrange: create two ValueSet instances that differ by JoinType, element order, or multiplicity.
        /// Act: compute GetHashCode for both.
        /// Assert: hash codes must be different to reflect the differing content/JoinType.
        /// </summary>
        /// <param name="joinTypeA">JoinType for first ValueSet.</param>
        /// <param name="joinTypeB">JoinType for second ValueSet.</param>
        /// <param name="valuesA">Sequence for first ValueSet.</param>
        /// <param name="valuesB">Sequence for second ValueSet.</param>
        [Theory]
        [MemberData(nameof(DifferentHashCases))]
        public void GetHashCode_DifferentContent_DifferentHash(ValueJoinType joinTypeA, ValueJoinType joinTypeB, int[] valuesA, int[] valuesB)
        {
            // Arrange
            var vsA = BuildValueSet(joinTypeA, valuesA);
            var vsB = BuildValueSet(joinTypeB, valuesB);

            // Act
            int hA = vsA.GetHashCode();
            int hB = vsB.GetHashCode();

            // Assert
            // It's possible (though extremely unlikely) for different content to produce same hash.
            // The test asserts inequality to catch common implementation mistakes (e.g., ignoring order or JoinType).
            Assert.NotEqual(hA, hB);
        }

        /// <summary>
        /// Arrange: create two empty ValueSet instances (default JoinType).
        /// Act: compute GetHashCode for both.
        /// Assert: Empty sets with same JoinType must yield same hash and non-zero deterministically.
        /// </summary>
        [Fact]
        public void GetHashCode_EmptySets_StableNonThrowingHash()
        {
            // Arrange
            var a = new ValueSet();
            var b = new ValueSet();

            // Act
            int ha = a.GetHashCode();
            int hb = b.GetHashCode();

            // Assert
            Assert.Equal(ha, hb);
            // Ensure method returns some deterministic int (not throwing and same across two empties)
            // No specific numeric value asserted to avoid coupling to implementation details of GetType().GetHashCode().
        }

        /// <summary>
        /// Helper: builds a ValueSet instance from the given join type and integer sequence.
        /// Uses ValueExpr to wrap integers into ValueTypeExpr instances.
        /// </summary>
        private static ValueSet BuildValueSet(ValueJoinType joinType, int[] values)
        {
            if (values is null) throw new ArgumentNullException(nameof(values));
            var exprs = new ValueTypeExpr[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                exprs[i] = new ValueExpr(values[i]);
            }
            return new ValueSet(joinType, exprs);
        }

        /// <summary>
        /// Tests that the IsReadOnly property returns false, indicating the ValueSet collection is mutable.
        /// Input: A ValueSet instance.
        /// Expected result: IsReadOnly returns false as per ICollection interface contract for mutable collections.
        /// </summary>
        [Fact]
        public void IsReadOnly_Always_ReturnsFalse()
        {
            // Arrange
            var valueSet = new ValueSet();

            // Act
            var result = valueSet.IsReadOnly;

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Provides various ValueSet comparison scenarios.
        /// Each item: [left ValueSet, right object to compare, expected result]
        /// </summary>
        public static IEnumerable<object?[]> EqualsCases
        {
            get
            {
                // Common mock items reused to ensure reference-equality when needed
                var mockItemA = new Mock<ValueTypeExpr>().Object;
                var mockItemB = new Mock<ValueTypeExpr>().Object;
                var mockItemC = new Mock<ValueTypeExpr>().Object;

                // Case: same instance -> true
                var sameInstance = new ValueSet(ValueJoinType.List, mockItemA, mockItemB);

                yield return new object?[] { sameInstance, sameInstance, true };

                // Case: two separate sets with same JoinType and same item references in same order -> true
                var vs1 = new ValueSet(ValueJoinType.List, mockItemA, mockItemB);
                var vs2 = new ValueSet(ValueJoinType.List, mockItemA, mockItemB);
                yield return new object?[] { vs1, vs2, true };

                // Case: same JoinType but different item order -> false (order matters)
                var vsOrder1 = new ValueSet(ValueJoinType.List, mockItemA, mockItemB, mockItemC);
                var vsOrder2 = new ValueSet(ValueJoinType.List, mockItemC, mockItemB, mockItemA);
                yield return new object?[] { vsOrder1, vsOrder2, false };

                // Case: same resultant items due to flattening nested ValueSet -> true
                var baseSet = new ValueSet(ValueJoinType.List, mockItemA, mockItemB);
                var container = new ValueSet(ValueJoinType.List);
                // Adding a ValueSet with same JoinType should flatten its items
                container.Add(baseSet);
                var direct = new ValueSet(ValueJoinType.List, mockItemA, mockItemB);
                yield return new object?[] { container, direct, true };

                // Case: different JoinType but identical item references -> false
                var listSet = new ValueSet(ValueJoinType.List, mockItemA, mockItemB);
                var concatSet = new ValueSet(ValueJoinType.Concat, mockItemA, mockItemB);
                yield return new object?[] { listSet, concatSet, false };

                // Case: different counts -> false
                var shorter = new ValueSet(ValueJoinType.List, mockItemA);
                var longer = new ValueSet(ValueJoinType.List, mockItemA, mockItemB);
                yield return new object?[] { shorter, longer, false };

                // Case: comparing with non-ValueSet object -> false
                var someObject = new object();
                var someSet = new ValueSet(ValueJoinType.List, mockItemA);
                yield return new object?[] { someSet, someObject, false };

                // Case: comparing with null -> false
                yield return new object?[] { someSet, null, false };
            }
        }

        /// <summary>
        /// Verify ValueSet.Equals behaves correctly across a variety of cases.
        /// Inputs include same-instance, equal contents, order-differences, join-type differences,
        /// count differences, comparing with non-ValueSet, and null.
        /// Expected result is asserted for each scenario.
        /// </summary>
        /// <param name="left">Left-hand ValueSet to call Equals on (not null).</param>
        /// <param name="right">Object to compare with (may be ValueSet, other object, or null).</param>
        /// <param name="expected">Expected boolean result from Equals.</param>
        [Theory]
        [MemberData(nameof(EqualsCases))]
        public void Equals_ObjectVariants_ExpectedResult(ValueSet left, object? right, bool expected)
        {
            // Arrange
            Assert.NotNull(left); // left is constructed in MemberData

            // Act
            bool result = left.Equals(right);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Additional sanity check: two distinct ValueSet instances that share the same
        /// item object references and same JoinType should be equal (reference-equality of items).
        /// This ensures Equals uses object.Equals semantics on items (reference equality for our mocks).
        /// </summary>
        [Fact]
        public void Equals_ReferenceEqualItems_ReturnsTrue()
        {
            // Arrange
            var shared = new Mock<ValueTypeExpr>().Object;
            var a = new ValueSet(ValueJoinType.Concat, shared);
            var b = new ValueSet(ValueJoinType.Concat, shared);

            // Act
            var result = a.Equals(b);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that adding a null item converts it to Expr.Null and adds it to the collection.
        /// </summary>
        [Fact]
        public void Add_NullItem_ConvertsToExprNullAndAddsToCollection()
        {
            // Arrange
            var valueSet = new ValueSet();
            var initialCount = valueSet.Count;

            // Act
            valueSet.Add(null);

            // Assert
            Assert.Equal(initialCount + 1, valueSet.Count);
            Assert.Same(Expr.Null, valueSet[0]);
        }

        /// <summary>
        /// Tests that adding a regular ValueTypeExpr adds it directly to the collection.
        /// </summary>
        [Fact]
        public void Add_RegularValueTypeExpr_AddsDirectlyToCollection()
        {
            // Arrange
            var valueSet = new ValueSet();
            var valueExpr = new ValueExpr(42);

            // Act
            valueSet.Add(valueExpr);

            // Assert
            Assert.Equal(1, valueSet.Count);
            Assert.Same(valueExpr, valueSet[0]);
        }

        /// <summary>
        /// Tests that adding multiple regular ValueTypeExpr items accumulates them in the collection.
        /// </summary>
        [Fact]
        public void Add_MultipleRegularItems_AccumulatesInCollection()
        {
            // Arrange
            var valueSet = new ValueSet();
            var expr1 = new ValueExpr(1);
            var expr2 = new ValueExpr(2);
            var expr3 = new ValueExpr(3);

            // Act
            valueSet.Add(expr1);
            valueSet.Add(expr2);
            valueSet.Add(expr3);

            // Assert
            Assert.Equal(3, valueSet.Count);
            Assert.Same(expr1, valueSet[0]);
            Assert.Same(expr2, valueSet[1]);
            Assert.Same(expr3, valueSet[2]);
        }

        /// <summary>
        /// Tests that adding a ValueSet with the same JoinType flattens the items.
        /// The nested ValueSet's items should be added individually rather than adding the ValueSet itself.
        /// </summary>
        [Fact]
        public void Add_ValueSetWithSameJoinType_FlattensItems()
        {
            // Arrange
            var outerSet = new ValueSet(ValueJoinType.List);
            var innerSet = new ValueSet(ValueJoinType.List, new ValueExpr(1), new ValueExpr(2), new ValueExpr(3));

            // Act
            outerSet.Add(innerSet);

            // Assert
            Assert.Equal(3, outerSet.Count);
            Assert.IsType<ValueExpr>(outerSet[0]);
            Assert.IsType<ValueExpr>(outerSet[1]);
            Assert.IsType<ValueExpr>(outerSet[2]);
        }

        /// <summary>
        /// Tests that adding a ValueSet with a different JoinType does not flatten.
        /// The ValueSet should be added as a single item.
        /// </summary>
        [Fact]
        public void Add_ValueSetWithDifferentJoinType_DoesNotFlatten()
        {
            // Arrange
            var outerSet = new ValueSet(ValueJoinType.List);
            var innerSet = new ValueSet(ValueJoinType.Concat, new ValueExpr(1), new ValueExpr(2));

            // Act
            outerSet.Add(innerSet);

            // Assert
            Assert.Equal(1, outerSet.Count);
            Assert.IsType<ValueSet>(outerSet[0]);
            Assert.Same(innerSet, outerSet[0]);
        }

        /// <summary>
        /// Tests that adding an empty ValueSet with the same JoinType results in no items being added.
        /// </summary>
        [Fact]
        public void Add_EmptyValueSetWithSameJoinType_AddsNoItems()
        {
            // Arrange
            var outerSet = new ValueSet(ValueJoinType.List);
            var emptyInnerSet = new ValueSet(ValueJoinType.List);

            // Act
            outerSet.Add(emptyInnerSet);

            // Assert
            Assert.Equal(0, outerSet.Count);
        }

        /// <summary>
        /// Tests that adding an empty ValueSet with a different JoinType adds the ValueSet itself.
        /// </summary>
        [Fact]
        public void Add_EmptyValueSetWithDifferentJoinType_AddsValueSetItself()
        {
            // Arrange
            var outerSet = new ValueSet(ValueJoinType.List);
            var emptyInnerSet = new ValueSet(ValueJoinType.Concat);

            // Act
            outerSet.Add(emptyInnerSet);

            // Assert
            Assert.Equal(1, outerSet.Count);
            Assert.Same(emptyInnerSet, outerSet[0]);
        }

        /// <summary>
        /// Tests the flattening behavior with Concat JoinType.
        /// When adding a ValueSet with matching Concat JoinType, items should be flattened.
        /// </summary>
        [Fact]
        public void Add_ValueSetWithMatchingConcatJoinType_FlattensItems()
        {
            // Arrange
            var outerSet = new ValueSet(ValueJoinType.Concat);
            var innerSet = new ValueSet(ValueJoinType.Concat, Expr.Prop("FirstName"), Expr.Const(" "), Expr.Prop("LastName"));

            // Act
            outerSet.Add(innerSet);

            // Assert
            Assert.Equal(3, outerSet.Count);
            Assert.IsType<PropertyExpr>(outerSet[0]);
            Assert.IsType<ValueExpr>(outerSet[1]);
            Assert.IsType<PropertyExpr>(outerSet[2]);
        }

        /// <summary>
        /// Tests that adding mixed items (null, regular ValueTypeExpr, and ValueSet) works correctly.
        /// </summary>
        [Fact]
        public void Add_MixedItems_HandlesAllTypesCorrectly()
        {
            // Arrange
            var outerSet = new ValueSet(ValueJoinType.List);
            var regularExpr = new ValueExpr(100);
            var innerSetSameType = new ValueSet(ValueJoinType.List, new ValueExpr(1), new ValueExpr(2));
            var innerSetDiffType = new ValueSet(ValueJoinType.Concat, new ValueExpr(3));

            // Act
            outerSet.Add(null);
            outerSet.Add(regularExpr);
            outerSet.Add(innerSetSameType);
            outerSet.Add(innerSetDiffType);

            // Assert
            Assert.Equal(5, outerSet.Count); // 1 (null->Expr.Null) + 1 (regularExpr) + 2 (flattened from innerSetSameType) + 1 (innerSetDiffType as single item)
            Assert.Same(Expr.Null, outerSet[0]);
            Assert.Same(regularExpr, outerSet[1]);
            Assert.IsType<ValueExpr>(outerSet[2]);
            Assert.IsType<ValueExpr>(outerSet[3]);
            Assert.IsType<ValueSet>(outerSet[4]);
        }

        /// <summary>
        /// Tests that adding a ValueSet containing null items handles the null conversion correctly during flattening.
        /// </summary>
        [Fact]
        public void Add_ValueSetContainingNullWithSameJoinType_FlattensWithNullHandling()
        {
            // Arrange
            var outerSet = new ValueSet(ValueJoinType.List);
            var innerSet = new ValueSet(ValueJoinType.List);
            innerSet.Add(null); // This will be converted to Expr.Null in innerSet
            innerSet.Add(new ValueExpr(42));

            // Act
            outerSet.Add(innerSet);

            // Assert
            Assert.Equal(2, outerSet.Count);
            Assert.Same(Expr.Null, outerSet[0]);
            Assert.IsType<ValueExpr>(outerSet[1]);
        }

        /// <summary>
        /// Tests that multiple consecutive additions of ValueSets with the same JoinType all get flattened correctly.
        /// </summary>
        [Fact]
        public void Add_MultipleValueSetsWithSameJoinType_AllGetFlattened()
        {
            // Arrange
            var outerSet = new ValueSet(ValueJoinType.List);
            var innerSet1 = new ValueSet(ValueJoinType.List, new ValueExpr(1), new ValueExpr(2));
            var innerSet2 = new ValueSet(ValueJoinType.List, new ValueExpr(3), new ValueExpr(4));
            var innerSet3 = new ValueSet(ValueJoinType.List, new ValueExpr(5));

            // Act
            outerSet.Add(innerSet1);
            outerSet.Add(innerSet2);
            outerSet.Add(innerSet3);

            // Assert
            Assert.Equal(5, outerSet.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.IsType<ValueExpr>(outerSet[i]);
            }
        }

        /// <summary>
        /// Tests that adding items to a ValueSet that already has existing items maintains correct order.
        /// </summary>
        [Fact]
        public void Add_ToNonEmptyValueSet_MaintainsCorrectOrder()
        {
            // Arrange
            var valueSet = new ValueSet(ValueJoinType.List, new ValueExpr(1), new ValueExpr(2));
            var newExpr = new ValueExpr(3);

            // Act
            valueSet.Add(newExpr);

            // Assert
            Assert.Equal(3, valueSet.Count);
            Assert.IsType<ValueExpr>(valueSet[0]);
            Assert.IsType<ValueExpr>(valueSet[1]);
            Assert.Same(newExpr, valueSet[2]);
        }

        /// <summary>
        /// Tests that AddRange throws NullReferenceException when items parameter is null.
        /// </summary>
        [Fact]
        public void AddRange_NullCollection_ThrowsNullReferenceException()
        {
            // Arrange
            var valueSet = new ValueSet();
            IEnumerable<ValueTypeExpr>? items = null;

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => valueSet.AddRange(items!));
        }

        /// <summary>
        /// Tests that AddRange handles an empty collection correctly without adding any items.
        /// </summary>
        [Fact]
        public void AddRange_EmptyCollection_AddsNoItems()
        {
            // Arrange
            var valueSet = new ValueSet();
            var emptyCollection = new List<ValueTypeExpr>();

            // Act
            valueSet.AddRange(emptyCollection);

            // Assert
            Assert.Equal(0, valueSet.Count);
        }

        /// <summary>
        /// Tests that AddRange correctly adds a single item from a collection.
        /// </summary>
        [Fact]
        public void AddRange_SingleItem_AddsOneItem()
        {
            // Arrange
            var valueSet = new ValueSet();
            var items = new List<ValueTypeExpr> { Expr.Const(42) };

            // Act
            valueSet.AddRange(items);

            // Assert
            Assert.Equal(1, valueSet.Count);
            Assert.Equal(Expr.Const(42), valueSet[0]);
        }

        /// <summary>
        /// Tests that AddRange correctly adds multiple items from a collection in order.
        /// </summary>
        [Fact]
        public void AddRange_MultipleItems_AddsAllItemsInOrder()
        {
            // Arrange
            var valueSet = new ValueSet();
            var items = new List<ValueTypeExpr>
            {
                Expr.Const(1),
                Expr.Const(2),
                Expr.Const(3)
            };

            // Act
            valueSet.AddRange(items);

            // Assert
            Assert.Equal(3, valueSet.Count);
            Assert.Equal(Expr.Const(1), valueSet[0]);
            Assert.Equal(Expr.Const(2), valueSet[1]);
            Assert.Equal(Expr.Const(3), valueSet[2]);
        }

        /// <summary>
        /// Tests that AddRange correctly appends items to an existing collection.
        /// </summary>
        [Fact]
        public void AddRange_ExistingItems_AppendsNewItems()
        {
            // Arrange
            var valueSet = new ValueSet(Expr.Const(1), Expr.Const(2));
            var newItems = new List<ValueTypeExpr>
            {
                Expr.Const(3),
                Expr.Const(4)
            };

            // Act
            valueSet.AddRange(newItems);

            // Assert
            Assert.Equal(4, valueSet.Count);
            Assert.Equal(Expr.Const(1), valueSet[0]);
            Assert.Equal(Expr.Const(2), valueSet[1]);
            Assert.Equal(Expr.Const(3), valueSet[2]);
            Assert.Equal(Expr.Const(4), valueSet[3]);
        }

        /// <summary>
        /// Tests that AddRange handles collections containing null items by converting them to Null constant.
        /// </summary>
        [Fact]
        public void AddRange_CollectionWithNullItems_ConvertsNullsToNullConstant()
        {
            // Arrange
            var valueSet = new ValueSet();
            var items = new List<ValueTypeExpr?> { Expr.Const(1), null, Expr.Const(2) };

            // Act
            valueSet.AddRange(items!);

            // Assert
            Assert.Equal(3, valueSet.Count);
            Assert.Equal(Expr.Const(1), valueSet[0]);
            Assert.Equal(Expr.Null, valueSet[1]);
            Assert.Equal(Expr.Const(2), valueSet[2]);
        }

        /// <summary>
        /// Tests that AddRange flattens nested ValueSets when they have the same JoinType.
        /// </summary>
        [Fact]
        public void AddRange_NestedValueSetWithSameJoinType_FlattensItems()
        {
            // Arrange
            var outerSet = new ValueSet(ValueJoinType.List);
            var nestedSet = new ValueSet(ValueJoinType.List, Expr.Const(1), Expr.Const(2));
            var items = new List<ValueTypeExpr> { nestedSet, Expr.Const(3) };

            // Act
            outerSet.AddRange(items);

            // Assert
            Assert.Equal(3, outerSet.Count);
            Assert.Equal(Expr.Const(1), outerSet[0]);
            Assert.Equal(Expr.Const(2), outerSet[1]);
            Assert.Equal(Expr.Const(3), outerSet[2]);
        }

        /// <summary>
        /// Tests that AddRange does not flatten nested ValueSets when they have different JoinType.
        /// </summary>
        [Fact]
        public void AddRange_NestedValueSetWithDifferentJoinType_DoesNotFlatten()
        {
            // Arrange
            var outerSet = new ValueSet(ValueJoinType.List);
            var nestedSet = new ValueSet(ValueJoinType.Concat, Expr.Const(1), Expr.Const(2));
            var items = new List<ValueTypeExpr> { nestedSet, Expr.Const(3) };

            // Act
            outerSet.AddRange(items);

            // Assert
            Assert.Equal(2, outerSet.Count);
            Assert.IsType<ValueSet>(outerSet[0]);
            Assert.Equal(Expr.Const(3), outerSet[1]);
        }

        /// <summary>
        /// Tests that AddRange handles collections with various ValueTypeExpr types.
        /// </summary>
        [Fact]
        public void AddRange_MixedValueTypeExprs_AddsAllTypes()
        {
            // Arrange
            var valueSet = new ValueSet();
            var items = new List<ValueTypeExpr>
            {
                Expr.Const(42),
                Expr.Const("test"),
                Expr.Const(true),
                Expr.Prop("Name")
            };

            // Act
            valueSet.AddRange(items);

            // Assert
            Assert.Equal(4, valueSet.Count);
        }

        /// <summary>
        /// Tests that AddRange can be called multiple times to accumulate items.
        /// </summary>
        [Fact]
        public void AddRange_CalledMultipleTimes_AccumulatesItems()
        {
            // Arrange
            var valueSet = new ValueSet();
            var firstBatch = new List<ValueTypeExpr> { Expr.Const(1), Expr.Const(2) };
            var secondBatch = new List<ValueTypeExpr> { Expr.Const(3), Expr.Const(4) };
            var thirdBatch = new List<ValueTypeExpr> { Expr.Const(5) };

            // Act
            valueSet.AddRange(firstBatch);
            valueSet.AddRange(secondBatch);
            valueSet.AddRange(thirdBatch);

            // Assert
            Assert.Equal(5, valueSet.Count);
            Assert.Equal(Expr.Const(1), valueSet[0]);
            Assert.Equal(Expr.Const(2), valueSet[1]);
            Assert.Equal(Expr.Const(3), valueSet[2]);
            Assert.Equal(Expr.Const(4), valueSet[3]);
            Assert.Equal(Expr.Const(5), valueSet[4]);
        }

        /// <summary>
        /// Tests that Clear removes all items from a non-empty collection.
        /// Verifies that Count becomes 0 and Items property returns an empty collection.
        /// </summary>
        [Fact]
        public void Clear_NonEmptyCollection_RemovesAllItems()
        {
            // Arrange
            var valueSet = new ValueSet();
            valueSet.Add(new ValueExpr(1));
            valueSet.Add(new ValueExpr(2));
            valueSet.Add(new ValueExpr(3));
            var previousItem = new ValueExpr(1);
            valueSet.Add(previousItem);

            // Act
            valueSet.Clear();

            // Assert
            Assert.Equal(0, valueSet.Count);
            Assert.Empty(valueSet.Items);
            Assert.False(valueSet.Contains(previousItem));
        }

        /// <summary>
        /// Tests that Clear on an empty collection does not throw an exception.
        /// Verifies that Count remains 0 after clearing an already empty collection.
        /// </summary>
        [Fact]
        public void Clear_EmptyCollection_DoesNotThrow()
        {
            // Arrange
            var valueSet = new ValueSet();

            // Act
            valueSet.Clear();

            // Assert
            Assert.Equal(0, valueSet.Count);
            Assert.Empty(valueSet.Items);
        }

        /// <summary>
        /// Tests that Clear can be called multiple times without throwing an exception.
        /// Verifies that the collection remains empty after multiple Clear calls.
        /// </summary>
        [Fact]
        public void Clear_CalledMultipleTimes_DoesNotThrow()
        {
            // Arrange
            var valueSet = new ValueSet(new ValueExpr(10), new ValueExpr(20));

            // Act
            valueSet.Clear();
            valueSet.Clear();
            valueSet.Clear();

            // Assert
            Assert.Equal(0, valueSet.Count);
            Assert.Empty(valueSet.Items);
        }

        /// <summary>
        /// Tests that after Clear, enumeration yields no elements.
        /// Verifies that the collection is properly cleared and can be enumerated.
        /// </summary>
        [Fact]
        public void Clear_AfterClearing_EnumerationYieldsNoElements()
        {
            // Arrange
            var valueSet = new ValueSet(ValueJoinType.List);
            valueSet.Add(new ValueExpr(1));
            valueSet.Add(new ValueExpr(2));

            // Act
            valueSet.Clear();

            // Assert
            Assert.Empty(valueSet);
        }

        /// <summary>
        /// Tests that after Clear, items can be added again to the collection.
        /// Verifies that the collection is in a valid state after being cleared.
        /// </summary>
        [Fact]
        public void Clear_ThenAddItems_CollectionIsUsable()
        {
            // Arrange
            var valueSet = new ValueSet(new ValueExpr(1), new ValueExpr(2));
            valueSet.Clear();

            // Act
            valueSet.Add(new ValueExpr(100));
            valueSet.Add(new ValueExpr(200));

            // Assert
            Assert.Equal(2, valueSet.Count);
            Assert.Contains(new ValueExpr(100), valueSet);
            Assert.Contains(new ValueExpr(200), valueSet);
        }

        /// <summary>
        /// Verifies that Contains returns false when the collection is empty.
        /// </summary>
        [Fact]
        public void Contains_EmptyCollection_ReturnsFalse()
        {
            // Arrange
            var valueSet = new ValueSet();
            ValueTypeExpr item = Expr.Const(1);

            // Act
            var result = valueSet.Contains(item);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Verifies that Contains returns true when the exact item exists in the collection.
        /// </summary>
        [Fact]
        public void Contains_ItemExists_ReturnsTrue()
        {
            // Arrange
            ValueTypeExpr item = Expr.Const(1);
            var valueSet = new ValueSet(item);

            // Act
            var result = valueSet.Contains(item);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Verifies that Contains returns false when the item does not exist in the collection.
        /// </summary>
        [Fact]
        public void Contains_ItemDoesNotExist_ReturnsFalse()
        {
            // Arrange
            ValueTypeExpr item1 = Expr.Const(1);
            ValueTypeExpr item2 = Expr.Const(2);
            var valueSet = new ValueSet(item1);

            // Act
            var result = valueSet.Contains(item2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Verifies that Contains returns false when searching for null in a non-empty collection.
        /// </summary>
        [Fact]
        public void Contains_NullItem_ReturnsFalse()
        {
            // Arrange
            var valueSet = new ValueSet(Expr.Const(1), Expr.Const(2));

            // Act
            var result = valueSet.Contains(null!);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Verifies that Contains returns false when searching for null in an empty collection.
        /// </summary>
        [Fact]
        public void Contains_NullItemInEmptyCollection_ReturnsFalse()
        {
            // Arrange
            var valueSet = new ValueSet();

            // Act
            var result = valueSet.Contains(null!);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Verifies that Contains works correctly with multiple items in the collection.
        /// Tests both existing and non-existing items.
        /// </summary>
        [Theory]
        [InlineData(1, true)]
        [InlineData(2, true)]
        [InlineData(3, true)]
        [InlineData(4, false)]
        [InlineData(0, false)]
        public void Contains_MultipleItems_ReturnsExpectedResult(int searchValue, bool expectedResult)
        {
            // Arrange
            var valueSet = new ValueSet(Expr.Const(1), Expr.Const(2), Expr.Const(3));
            ValueTypeExpr item = Expr.Const(searchValue);

            // Act
            var result = valueSet.Contains(item);

            // Assert
            Assert.Equal(expectedResult, result);
        }

        /// <summary>
        /// Verifies that Contains returns true for the first occurrence when duplicate items exist.
        /// </summary>
        [Fact]
        public void Contains_DuplicateItems_ReturnsTrue()
        {
            // Arrange
            ValueTypeExpr item = Expr.Const(1);
            var valueSet = new ValueSet();
            valueSet.Add(item);
            valueSet.Add(item);
            valueSet.Add(item);

            // Act
            var result = valueSet.Contains(item);

            // Assert
            Assert.True(result);
            Assert.Equal(3, valueSet.Count);
        }

        /// <summary>
        /// Verifies that Contains works correctly with different types of ValueTypeExpr.
        /// Tests with ValueExpr, property expressions, and mixed types.
        /// </summary>
        [Fact]
        public void Contains_DifferentExpressionTypes_WorksCorrectly()
        {
            // Arrange
            ValueTypeExpr constExpr = Expr.Const(42);
            ValueTypeExpr propExpr = Expr.Prop("Name");
            var valueSet = new ValueSet(constExpr, propExpr);

            // Act
            var containsConst = valueSet.Contains(constExpr);
            var containsProp = valueSet.Contains(propExpr);
            var containsOther = valueSet.Contains(Expr.Const(100));

            // Assert
            Assert.True(containsConst);
            Assert.True(containsProp);
            Assert.False(containsOther);
        }

        /// <summary>
        /// Verifies that Contains works correctly after items are added using AddRange.
        /// </summary>
        [Fact]
        public void Contains_AfterAddRange_WorksCorrectly()
        {
            // Arrange
            var valueSet = new ValueSet();
            ValueTypeExpr item1 = Expr.Const(1);
            ValueTypeExpr item2 = Expr.Const(2);
            ValueTypeExpr item3 = Expr.Const(3);
            valueSet.AddRange(new[] { item1, item2, item3 });

            // Act
            var contains1 = valueSet.Contains(item1);
            var contains2 = valueSet.Contains(item2);
            var contains3 = valueSet.Contains(item3);
            var contains4 = valueSet.Contains(Expr.Const(4));

            // Assert
            Assert.True(contains1);
            Assert.True(contains2);
            Assert.True(contains3);
            Assert.False(contains4);
        }

        /// <summary>
        /// Verifies that Contains returns false after the item is removed from the collection.
        /// </summary>
        [Fact]
        public void Contains_AfterRemove_ReturnsFalse()
        {
            // Arrange
            ValueTypeExpr item = Expr.Const(1);
            var valueSet = new ValueSet(item, Expr.Const(2));

            // Act - Before removal
            var containsBeforeRemove = valueSet.Contains(item);
            valueSet.Remove(item);
            var containsAfterRemove = valueSet.Contains(item);

            // Assert
            Assert.True(containsBeforeRemove);
            Assert.False(containsAfterRemove);
        }

        /// <summary>
        /// Verifies that Contains returns false after the collection is cleared.
        /// </summary>
        [Fact]
        public void Contains_AfterClear_ReturnsFalse()
        {
            // Arrange
            ValueTypeExpr item = Expr.Const(1);
            var valueSet = new ValueSet(item, Expr.Const(2), Expr.Const(3));

            // Act - Before clear
            var containsBeforeClear = valueSet.Contains(item);
            valueSet.Clear();
            var containsAfterClear = valueSet.Contains(item);

            // Assert
            Assert.True(containsBeforeClear);
            Assert.False(containsAfterClear);
            Assert.Equal(0, valueSet.Count);
        }

        /// <summary>
        /// Verifies that Contains works correctly with ValueSet initialized using different constructors.
        /// </summary>
        [Fact]
        public void Contains_WithDifferentConstructors_WorksCorrectly()
        {
            // Arrange
            ValueTypeExpr item1 = Expr.Const(1);
            ValueTypeExpr item2 = Expr.Const(2);
            ValueTypeExpr item3 = Expr.Const(3);

            var valueSet1 = new ValueSet(item1, item2);
            var valueSet2 = new ValueSet(new List<ValueTypeExpr> { item1, item2 });
            var valueSet3 = new ValueSet(ValueJoinType.List, item1, item2);
            var valueSet4 = new ValueSet(ValueJoinType.Concat, new List<ValueTypeExpr> { item1, item2 });

            // Act & Assert
            Assert.True(valueSet1.Contains(item1));
            Assert.True(valueSet2.Contains(item1));
            Assert.True(valueSet3.Contains(item1));
            Assert.True(valueSet4.Contains(item1));

            Assert.False(valueSet1.Contains(item3));
            Assert.False(valueSet2.Contains(item3));
            Assert.False(valueSet3.Contains(item3));
            Assert.False(valueSet4.Contains(item3));
        }

        /// <summary>
        /// Verifies that Contains works with string value expressions.
        /// </summary>
        [Fact]
        public void Contains_WithStringValues_WorksCorrectly()
        {
            // Arrange
            ValueTypeExpr stringExpr1 = Expr.Const("test");
            ValueTypeExpr stringExpr2 = Expr.Const("hello");
            var valueSet = new ValueSet(stringExpr1, stringExpr2);

            // Act
            var containsTest = valueSet.Contains(stringExpr1);
            var containsHello = valueSet.Contains(stringExpr2);
            var containsOther = valueSet.Contains(Expr.Const("other"));

            // Assert
            Assert.True(containsTest);
            Assert.True(containsHello);
            Assert.False(containsOther);
        }

        /// <summary>
        /// Verifies that Contains works with various numeric types.
        /// </summary>
        [Fact]
        public void Contains_WithVariousNumericTypes_WorksCorrectly()
        {
            // Arrange
            ValueTypeExpr intExpr = Expr.Const(42);
            ValueTypeExpr longExpr = Expr.Const(9999999999L);
            ValueTypeExpr doubleExpr = Expr.Const(3.14);
            ValueTypeExpr decimalExpr = Expr.Const(99.99m);
            var valueSet = new ValueSet(intExpr, longExpr, doubleExpr, decimalExpr);

            // Act & Assert
            Assert.True(valueSet.Contains(intExpr));
            Assert.True(valueSet.Contains(longExpr));
            Assert.True(valueSet.Contains(doubleExpr));
            Assert.True(valueSet.Contains(decimalExpr));
            Assert.False(valueSet.Contains(Expr.Const(999)));
        }

        /// <summary>
        /// Tests that CopyTo throws ArgumentNullException when array parameter is null.
        /// </summary>
        [Fact]
        public void CopyTo_NullArray_ThrowsArgumentNullException()
        {
            // Arrange
            var valueSet = new ValueSet(new ValueExpr(1), new ValueExpr(2), new ValueExpr(3));
            ValueTypeExpr[]? array = null;
            int arrayIndex = 0;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => valueSet.CopyTo(array!, arrayIndex));
        }

        /// <summary>
        /// Tests that CopyTo throws ArgumentOutOfRangeException when arrayIndex is negative.
        /// </summary>
        /// <param name="arrayIndex">The negative array index to test.</param>
        [Theory]
        [InlineData(-1)]
        [InlineData(-100)]
        [InlineData(int.MinValue)]
        public void CopyTo_NegativeArrayIndex_ThrowsArgumentOutOfRangeException(int arrayIndex)
        {
            // Arrange
            var valueSet = new ValueSet(new ValueExpr(1), new ValueExpr(2));
            var array = new ValueTypeExpr[10];

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => valueSet.CopyTo(array, arrayIndex));
        }

        /// <summary>
        /// Tests that CopyTo throws ArgumentException when arrayIndex is too large.
        /// </summary>
        [Fact]
        public void CopyTo_ArrayIndexTooLarge_ThrowsArgumentException()
        {
            // Arrange
            var valueSet = new ValueSet(new ValueExpr(1), new ValueExpr(2));
            var array = new ValueTypeExpr[5];
            int arrayIndex = 5;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => valueSet.CopyTo(array, arrayIndex));
        }

        /// <summary>
        /// Tests that CopyTo throws ArgumentException when there is insufficient space in destination array.
        /// </summary>
        /// <param name="arrayLength">The length of the destination array.</param>
        /// <param name="arrayIndex">The starting index in the destination array.</param>
        /// <param name="itemCount">The number of items in the ValueSet.</param>
        [Theory]
        [InlineData(5, 3, 3)] // Need 3 slots from index 3, but only 2 available
        [InlineData(5, 4, 2)] // Need 2 slots from index 4, but only 1 available
        [InlineData(3, 1, 3)] // Need 3 slots from index 1, but only 2 available
        [InlineData(10, 8, 5)] // Need 5 slots from index 8, but only 2 available
        public void CopyTo_InsufficientSpace_ThrowsArgumentException(int arrayLength, int arrayIndex, int itemCount)
        {
            // Arrange
            var items = new List<ValueTypeExpr>();
            for (int i = 0; i < itemCount; i++)
            {
                items.Add(new ValueExpr(i));
            }
            var valueSet = new ValueSet(items);
            var array = new ValueTypeExpr[arrayLength];

            // Act & Assert
            Assert.Throws<ArgumentException>(() => valueSet.CopyTo(array, arrayIndex));
        }

        /// <summary>
        /// Tests that CopyTo successfully copies elements from an empty ValueSet.
        /// </summary>
        [Fact]
        public void CopyTo_EmptyValueSet_CopiesSuccessfully()
        {
            // Arrange
            var valueSet = new ValueSet();
            var array = new ValueTypeExpr[5];
            int arrayIndex = 2;

            // Act
            valueSet.CopyTo(array, arrayIndex);

            // Assert
            Assert.Equal(0, valueSet.Count);
            Assert.All(array, item => Assert.Null(item));
        }

        /// <summary>
        /// Tests that CopyTo successfully copies a single element to the start of array.
        /// </summary>
        [Fact]
        public void CopyTo_SingleElement_ToStartOfArray_CopiesSuccessfully()
        {
            // Arrange
            var expectedValue = new ValueExpr(42);
            var valueSet = new ValueSet(expectedValue);
            var array = new ValueTypeExpr[5];
            int arrayIndex = 0;

            // Act
            valueSet.CopyTo(array, arrayIndex);

            // Assert
            Assert.Same(expectedValue, array[0]);
            Assert.Null(array[1]);
        }

        /// <summary>
        /// Tests that CopyTo successfully copies multiple elements to the start of array.
        /// </summary>
        [Fact]
        public void CopyTo_MultipleElements_ToStartOfArray_CopiesSuccessfully()
        {
            // Arrange
            var value1 = new ValueExpr(1);
            var value2 = new ValueExpr(2);
            var value3 = new ValueExpr(3);
            var valueSet = new ValueSet(value1, value2, value3);
            var array = new ValueTypeExpr[5];
            int arrayIndex = 0;

            // Act
            valueSet.CopyTo(array, arrayIndex);

            // Assert
            Assert.Same(value1, array[0]);
            Assert.Same(value2, array[1]);
            Assert.Same(value3, array[2]);
            Assert.Null(array[3]);
            Assert.Null(array[4]);
        }

        /// <summary>
        /// Tests that CopyTo successfully copies elements to the middle of array.
        /// </summary>
        [Fact]
        public void CopyTo_MultipleElements_ToMiddleOfArray_CopiesSuccessfully()
        {
            // Arrange
            var value1 = new ValueExpr(10);
            var value2 = new ValueExpr(20);
            var valueSet = new ValueSet(value1, value2);
            var array = new ValueTypeExpr[6];
            int arrayIndex = 2;

            // Act
            valueSet.CopyTo(array, arrayIndex);

            // Assert
            Assert.Null(array[0]);
            Assert.Null(array[1]);
            Assert.Same(value1, array[2]);
            Assert.Same(value2, array[3]);
            Assert.Null(array[4]);
            Assert.Null(array[5]);
        }

        /// <summary>
        /// Tests that CopyTo successfully copies elements with exact fit at the end of array.
        /// </summary>
        [Fact]
        public void CopyTo_ExactFit_CopiesSuccessfully()
        {
            // Arrange
            var value1 = new ValueExpr("A");
            var value2 = new ValueExpr("B");
            var value3 = new ValueExpr("C");
            var valueSet = new ValueSet(value1, value2, value3);
            var array = new ValueTypeExpr[5];
            int arrayIndex = 2;

            // Act
            valueSet.CopyTo(array, arrayIndex);

            // Assert
            Assert.Null(array[0]);
            Assert.Null(array[1]);
            Assert.Same(value1, array[2]);
            Assert.Same(value2, array[3]);
            Assert.Same(value3, array[4]);
        }

        /// <summary>
        /// Tests that CopyTo with arrayIndex equal to array length succeeds for empty ValueSet.
        /// </summary>
        [Fact]
        public void CopyTo_EmptyValueSet_ArrayIndexEqualsArrayLength_CopiesSuccessfully()
        {
            // Arrange
            var valueSet = new ValueSet();
            var array = new ValueTypeExpr[3];
            int arrayIndex = 3;

            // Act
            valueSet.CopyTo(array, arrayIndex);

            // Assert
            Assert.Equal(0, valueSet.Count);
            Assert.All(array, item => Assert.Null(item));
        }

        /// <summary>
        /// Tests that CopyTo correctly copies ValueSet with different ValueTypeExpr types.
        /// </summary>
        [Fact]
        public void CopyTo_MixedValueTypes_CopiesSuccessfully()
        {
            // Arrange
            ValueTypeExpr stringValue = "test";
            ValueTypeExpr intValue = 123;
            var valueSet = new ValueSet(stringValue, intValue);
            var array = new ValueTypeExpr[4];
            int arrayIndex = 1;

            // Act
            valueSet.CopyTo(array, arrayIndex);

            // Assert
            Assert.Null(array[0]);
            Assert.Same(stringValue, array[1]);
            Assert.Same(intValue, array[2]);
            Assert.Null(array[3]);
        }

        /// <summary>
        /// Tests that CopyTo preserves the order of elements.
        /// </summary>
        [Fact]
        public void CopyTo_PreservesElementOrder()
        {
            // Arrange
            var items = new List<ValueTypeExpr>();
            for (int i = 0; i < 10; i++)
            {
                items.Add(new ValueExpr(i));
            }
            var valueSet = new ValueSet(items);
            var array = new ValueTypeExpr[10];
            int arrayIndex = 0;

            // Act
            valueSet.CopyTo(array, arrayIndex);

            // Assert
            for (int i = 0; i < 10; i++)
            {
                Assert.Same(items[i], array[i]);
            }
        }

        /// <summary>
        /// Tests that CopyTo throws ArgumentException when arrayIndex is at maximum boundary with non-empty collection.
        /// </summary>
        [Fact]
        public void CopyTo_ArrayIndexAtMaxBoundary_NonEmptyCollection_ThrowsArgumentException()
        {
            // Arrange
            var valueSet = new ValueSet(new ValueExpr(1));
            var array = new ValueTypeExpr[5];
            int arrayIndex = int.MaxValue;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => valueSet.CopyTo(array, arrayIndex));
        }

        /// <summary>
        /// Tests that CopyTo handles ValueSet with ValueJoinType.List correctly.
        /// </summary>
        [Fact]
        public void CopyTo_ValueSetWithListJoinType_CopiesSuccessfully()
        {
            // Arrange
            var value1 = new ValueExpr(1);
            var value2 = new ValueExpr(2);
            var valueSet = new ValueSet(ValueJoinType.List, value1, value2);
            var array = new ValueTypeExpr[3];
            int arrayIndex = 0;

            // Act
            valueSet.CopyTo(array, arrayIndex);

            // Assert
            Assert.Same(value1, array[0]);
            Assert.Same(value2, array[1]);
            Assert.Null(array[2]);
        }

        /// <summary>
        /// Tests that CopyTo handles ValueSet with ValueJoinType.Concat correctly.
        /// </summary>
        [Fact]
        public void CopyTo_ValueSetWithConcatJoinType_CopiesSuccessfully()
        {
            // Arrange
            var value1 = new ValueExpr("Hello");
            var value2 = new ValueExpr(" ");
            var value3 = new ValueExpr("World");
            var valueSet = new ValueSet(ValueJoinType.Concat, value1, value2, value3);
            var array = new ValueTypeExpr[4];
            int arrayIndex = 1;

            // Act
            valueSet.CopyTo(array, arrayIndex);

            // Assert
            Assert.Null(array[0]);
            Assert.Same(value1, array[1]);
            Assert.Same(value2, array[2]);
            Assert.Same(value3, array[3]);
        }

        /// <summary>
        /// Tests that Remove returns true and removes the item when the item exists in the collection.
        /// </summary>
        [Fact]
        public void Remove_ExistingItem_ReturnsTrueAndRemovesItem()
        {
            // Arrange
            var item1 = new ValueExpr(1);
            var item2 = new ValueExpr(2);
            var item3 = new ValueExpr(3);
            var valueSet = new ValueSet(item1, item2, item3);
            int originalCount = valueSet.Count;

            // Act
            bool result = valueSet.Remove(item2);

            // Assert
            Assert.True(result);
            Assert.Equal(originalCount - 1, valueSet.Count);
            Assert.False(valueSet.Contains(item2));
            Assert.True(valueSet.Contains(item1));
            Assert.True(valueSet.Contains(item3));
        }

        /// <summary>
        /// Tests that Remove returns false when the item does not exist in the collection.
        /// </summary>
        [Fact]
        public void Remove_NonExistingItem_ReturnsFalse()
        {
            // Arrange
            var item1 = new ValueExpr(1);
            var item2 = new ValueExpr(2);
            var itemNotInSet = new ValueExpr(99);
            var valueSet = new ValueSet(item1, item2);
            int originalCount = valueSet.Count;

            // Act
            bool result = valueSet.Remove(itemNotInSet);

            // Assert
            Assert.False(result);
            Assert.Equal(originalCount, valueSet.Count);
        }

        /// <summary>
        /// Tests that Remove returns false when called on an empty collection.
        /// </summary>
        [Fact]
        public void Remove_EmptyCollection_ReturnsFalse()
        {
            // Arrange
            var valueSet = new ValueSet();
            var item = new ValueExpr(1);

            // Act
            bool result = valueSet.Remove(item);

            // Assert
            Assert.False(result);
            Assert.Equal(0, valueSet.Count);
        }

        /// <summary>
        /// Tests that Remove handles null parameter correctly.
        /// </summary>
        [Fact]
        public void Remove_NullItem_ReturnsFalse()
        {
            // Arrange
            var item1 = new ValueExpr(1);
            var valueSet = new ValueSet(item1);
            int originalCount = valueSet.Count;

            // Act
            bool result = valueSet.Remove(null);

            // Assert
            Assert.False(result);
            Assert.Equal(originalCount, valueSet.Count);
        }

        /// <summary>
        /// Tests that Remove only removes the first occurrence when multiple identical items exist.
        /// </summary>
        [Fact]
        public void Remove_DuplicateItems_RemovesFirstOccurrenceOnly()
        {
            // Arrange
            var item1 = new ValueExpr(1);
            var item2 = new ValueExpr(2);
            var valueSet = new ValueSet();
            valueSet.Add(item1);
            valueSet.Add(item2);
            valueSet.Add(item1); // Add duplicate
            int originalCount = valueSet.Count;

            // Act
            bool result = valueSet.Remove(item1);

            // Assert
            Assert.True(result);
            Assert.Equal(originalCount - 1, valueSet.Count);
            Assert.True(valueSet.Contains(item1)); // Should still contain the second occurrence
        }

        /// <summary>
        /// Tests that Remove works correctly with different ValueTypeExpr types.
        /// </summary>
        [Fact]
        public void Remove_DifferentValueTypes_RemovesCorrectly()
        {
            // Arrange
            var stringValue = new ValueExpr("test");
            var intValue = new ValueExpr(42);
            var boolValue = new ValueExpr(true);
            var valueSet = new ValueSet(stringValue, intValue, boolValue);

            // Act
            bool result = valueSet.Remove(intValue);

            // Assert
            Assert.True(result);
            Assert.Equal(2, valueSet.Count);
            Assert.False(valueSet.Contains(intValue));
            Assert.True(valueSet.Contains(stringValue));
            Assert.True(valueSet.Contains(boolValue));
        }

        /// <summary>
        /// Tests that Remove correctly uses equality semantics for value-equal items.
        /// </summary>
        [Fact]
        public void Remove_ValueEqualItems_RemovesCorrectly()
        {
            // Arrange
            var item1 = new ValueExpr(100);
            var item2 = new ValueExpr(100); // Same value, different instance
            var valueSet = new ValueSet();
            valueSet.Add(item1);

            // Act
            bool result = valueSet.Remove(item2);

            // Assert
            // If ValueExpr uses value equality, this should return true
            // If it uses reference equality, this should return false
            // Based on the Equals override in ValueSet, it appears to use value equality
            Assert.True(result);
            Assert.Equal(0, valueSet.Count);
        }

        /// <summary>
        /// Tests that Remove can be called multiple times on the same collection.
        /// </summary>
        [Fact]
        public void Remove_MultipleCalls_WorksCorrectly()
        {
            // Arrange
            var item1 = new ValueExpr(1);
            var item2 = new ValueExpr(2);
            var item3 = new ValueExpr(3);
            var valueSet = new ValueSet(item1, item2, item3);

            // Act & Assert
            Assert.True(valueSet.Remove(item1));
            Assert.Equal(2, valueSet.Count);

            Assert.True(valueSet.Remove(item2));
            Assert.Equal(1, valueSet.Count);

            Assert.True(valueSet.Remove(item3));
            Assert.Equal(0, valueSet.Count);

            // Trying to remove from empty collection
            Assert.False(valueSet.Remove(item1));
            Assert.Equal(0, valueSet.Count);
        }

        /// <summary>
        /// Tests that Remove maintains collection integrity after removal.
        /// </summary>
        [Fact]
        public void Remove_AfterRemoval_CollectionRemainsValid()
        {
            // Arrange
            var item1 = new ValueExpr(1);
            var item2 = new ValueExpr(2);
            var item3 = new ValueExpr(3);
            var valueSet = new ValueSet(item1, item2, item3);

            // Act
            valueSet.Remove(item2);

            // Assert
            Assert.Equal(2, valueSet.Count);
            Assert.False(valueSet.IsReadOnly);

            // Verify Items property works correctly
            var items = valueSet.Items;
            Assert.Equal(2, items.Count);
            Assert.Contains(item1, items);
            Assert.Contains(item3, items);
            Assert.DoesNotContain(item2, items);

            // Verify enumeration works
            int count = 0;
            foreach (var item in valueSet)
            {
                count++;
            }
            Assert.Equal(2, count);
        }

        /// <summary>
        /// Tests that GetEnumerator returns an empty enumerator when the collection is empty.
        /// Input: Empty ValueSet.
        /// Expected: Enumerator with no elements.
        /// </summary>
        [Fact]
        public void GetEnumerator_EmptyCollection_ReturnsEmptyEnumerator()
        {
            // Arrange
            var valueSet = new ValueSet();

            // Act
            var enumerator = valueSet.GetEnumerator();
            var items = new List<ValueTypeExpr>();
            while (enumerator.MoveNext())
            {
                items.Add(enumerator.Current);
            }

            // Assert
            Assert.Empty(items);
        }

        /// <summary>
        /// Tests that GetEnumerator returns all items when the collection has a single item.
        /// Input: ValueSet with one ValueTypeExpr.
        /// Expected: Enumerator returns the single item.
        /// </summary>
        [Fact]
        public void GetEnumerator_SingleItem_ReturnsSingleItem()
        {
            // Arrange
            var valueSet = new ValueSet();
            ValueTypeExpr item = new ValueExpr(42);
            valueSet.Add(item);

            // Act
            var enumerator = valueSet.GetEnumerator();
            var items = new List<ValueTypeExpr>();
            while (enumerator.MoveNext())
            {
                items.Add(enumerator.Current);
            }

            // Assert
            Assert.Single(items);
            Assert.Same(item, items[0]);
        }

        /// <summary>
        /// Tests that GetEnumerator returns all items when the collection has multiple items.
        /// Input: ValueSet with multiple ValueTypeExpr items.
        /// Expected: Enumerator returns all items.
        /// </summary>
        [Fact]
        public void GetEnumerator_MultipleItems_ReturnsAllItems()
        {
            // Arrange
            var valueSet = new ValueSet();
            ValueTypeExpr item1 = new ValueExpr(1);
            ValueTypeExpr item2 = new ValueExpr(2);
            ValueTypeExpr item3 = new ValueExpr(3);
            valueSet.Add(item1);
            valueSet.Add(item2);
            valueSet.Add(item3);

            // Act
            var enumerator = valueSet.GetEnumerator();
            var items = new List<ValueTypeExpr>();
            while (enumerator.MoveNext())
            {
                items.Add(enumerator.Current);
            }

            // Assert
            Assert.Equal(3, items.Count);
            Assert.Same(item1, items[0]);
            Assert.Same(item2, items[1]);
            Assert.Same(item3, items[2]);
        }

        /// <summary>
        /// Tests that GetEnumerator preserves the order of items as they were added.
        /// Input: ValueSet with items added in specific order.
        /// Expected: Enumerator returns items in the same order.
        /// </summary>
        [Fact]
        public void GetEnumerator_PreservesOrder_ReturnsItemsInCorrectOrder()
        {
            // Arrange
            var valueSet = new ValueSet();
            ValueTypeExpr first = new ValueExpr("first");
            ValueTypeExpr second = new ValueExpr("second");
            ValueTypeExpr third = new ValueExpr("third");
            valueSet.Add(first);
            valueSet.Add(second);
            valueSet.Add(third);

            // Act
            var enumeratedItems = new List<ValueTypeExpr>();
            foreach (var item in valueSet)
            {
                enumeratedItems.Add(item);
            }

            // Assert
            Assert.Equal(3, enumeratedItems.Count);
            Assert.Same(first, enumeratedItems[0]);
            Assert.Same(second, enumeratedItems[1]);
            Assert.Same(third, enumeratedItems[2]);
        }

        /// <summary>
        /// Tests that multiple calls to GetEnumerator return independent enumerators.
        /// Input: ValueSet with multiple items.
        /// Expected: Each enumerator can iterate independently without affecting the other.
        /// </summary>
        [Fact]
        public void GetEnumerator_MultipleCalls_ReturnsIndependentEnumerators()
        {
            // Arrange
            var valueSet = new ValueSet();
            ValueTypeExpr item1 = new ValueExpr(10);
            ValueTypeExpr item2 = new ValueExpr(20);
            valueSet.Add(item1);
            valueSet.Add(item2);

            // Act
            var enumerator1 = valueSet.GetEnumerator();
            var enumerator2 = valueSet.GetEnumerator();

            enumerator1.MoveNext();
            var firstFromEnum1 = enumerator1.Current;

            enumerator2.MoveNext();
            var firstFromEnum2 = enumerator2.Current;

            enumerator2.MoveNext();
            var secondFromEnum2 = enumerator2.Current;

            enumerator1.MoveNext();
            var secondFromEnum1 = enumerator1.Current;

            // Assert
            Assert.Same(item1, firstFromEnum1);
            Assert.Same(item1, firstFromEnum2);
            Assert.Same(item2, secondFromEnum1);
            Assert.Same(item2, secondFromEnum2);
        }

        /// <summary>
        /// Tests that GetEnumerator works correctly with foreach statement.
        /// Input: ValueSet with multiple items.
        /// Expected: foreach can iterate through all items.
        /// </summary>
        [Fact]
        public void GetEnumerator_WithForeach_IteratesAllItems()
        {
            // Arrange
            var valueSet = new ValueSet(
                new ValueExpr(1),
                new ValueExpr(2),
                new ValueExpr(3),
                new ValueExpr(4),
                new ValueExpr(5)
            );

            // Act
            var count = 0;
            var sum = 0;
            foreach (var item in valueSet)
            {
                count++;
                if (item is ValueExpr ve && ve.Value is int intValue)
                {
                    sum += intValue;
                }
            }

            // Assert
            Assert.Equal(5, count);
            Assert.Equal(15, sum);
        }

        /// <summary>
        /// Tests that GetEnumerator can be used with LINQ extensions.
        /// Input: ValueSet with multiple items.
        /// Expected: LINQ methods work correctly with the enumerator.
        /// </summary>
        [Fact]
        public void GetEnumerator_WithLinq_WorksCorrectly()
        {
            // Arrange
            var valueSet = new ValueSet(
                new ValueExpr(1),
                new ValueExpr(2),
                new ValueExpr(3)
            );

            // Act
            var count = valueSet.Count();
            var firstItem = valueSet.First();
            var lastItem = valueSet.Last();

            // Assert
            Assert.Equal(3, count);
            Assert.NotNull(firstItem);
            Assert.NotNull(lastItem);
        }

        /// <summary>
        /// Verifies that the ExprType property returns ExprType.ValueSet
        /// for a ValueSet instance created with the default constructor.
        /// </summary>
        [Fact]
        public void ExprType_DefaultConstructor_ReturnsValueSet()
        {
            // Arrange
            var valueSet = new ValueSet();

            // Act
            var result = valueSet.ExprType;

            // Assert
            Assert.Equal(ExprType.ValueSet, result);
        }

        /// <summary>
        /// Verifies that the ExprType property returns ExprType.ValueSet
        /// for a ValueSet instance created with various constructors.
        /// This ensures the property returns a consistent value regardless
        /// of initialization method.
        /// </summary>
        [Theory]
        [InlineData(ValueJoinType.List)]
        [InlineData(ValueJoinType.Concat)]
        public void ExprType_VariousConstructors_ReturnsValueSet(ValueJoinType joinType)
        {
            // Arrange
            var valueSet1 = new ValueSet();
            var valueSet2 = new ValueSet(joinType);
            var valueSet3 = new ValueSet(new ValueTypeExpr[] { });

            // Act
            var result1 = valueSet1.ExprType;
            var result2 = valueSet2.ExprType;
            var result3 = valueSet3.ExprType;

            // Assert
            Assert.Equal(ExprType.ValueSet, result1);
            Assert.Equal(ExprType.ValueSet, result2);
            Assert.Equal(ExprType.ValueSet, result3);
        }

        /// <summary>
        /// Provides ValueSet instances for parameterized clone tests:
        /// - A populated ValueSet with mixed ValueExpr and PropertyExpr items (JoinType.Concat).
        /// - A ValueSet containing the same PropertyExpr instance added twice (duplicate reference scenario, JoinType.List).
        /// </summary>
        public static IEnumerable<object?[]> CloneCases()
        {
            // Populated set: int, string, property expression
            yield return new object?[]
            {
                new ValueSet(ValueJoinType.Concat,
                    new ValueExpr(1),
                    new ValueExpr("s"),
                    new PropertyExpr("Name"))
            };

            // Duplicate-reference scenario: same PropertyExpr instance added twice
            var p = new PropertyExpr("Dup");
            yield return new object?[]
            {
                new ValueSet(ValueJoinType.List, p, p)
            };
        }

        /// <summary>
        /// Verifies that cloning an empty ValueSet produces a distinct ValueSet with the same JoinType and zero items.
        /// Input: default constructed empty ValueSet.
        /// Expected: returned clone is a different instance, JoinType preserved, Count == 0.
        /// </summary>
        [Fact]
        public void Clone_EmptyValueSet_ReturnsDistinctEmptyClone()
        {
            // Arrange
            var original = new ValueSet(); // default JoinType = List

            // Act
            var cloned = (ValueSet)original.Clone();

            // Assert
            Assert.NotSame(original, cloned);
            Assert.Equal(original.JoinType, cloned.JoinType);
            Assert.Equal(0, cloned.Count);
        }

        /// <summary>
        /// Verifies that Clone produces a ValueSet with:
        /// - the same JoinType,
        /// - the same number of items,
        /// - each item value-equal to the original but not the same reference,
        /// - duplicate references in the original result in distinct clones,
        /// - clones are independent from original items (modifying original items does not affect clones).
        /// Inputs: various ValueSet instances (see MemberData).
        /// Expected: behavior as described above.
        /// </summary>
        [Theory]
        [MemberData(nameof(CloneCases))]
        public void Clone_PopulatedValueSet_ItemsAreClonedAndIndependent(ValueSet original)
        {
            // Arrange
            Assert.NotNull(original); // guard for MemberData
            int originalCount = original.Count;
            var originalJoin = original.JoinType;

            // Capture whether the original has duplicate references at indexes 0 and 1 (if applicable)
            bool originalHasFirstTwoSameReference = originalCount >= 2 && object.ReferenceEquals(original[0], original[1]);

            // For property-name independence check: capture the first PropertyExpr encountered (index and name)
            int propIndex = -1;
            string? propOriginalName = null;
            for (int i = 0; i < originalCount; i++)
            {
                if (original[i] is PropertyExpr pe)
                {
                    propIndex = i;
                    propOriginalName = pe.PropertyName;
                    break;
                }
            }

            // Act
            var cloned = (ValueSet)original.Clone();

            // Assert - basic structural checks
            Assert.NotSame(original, cloned);
            Assert.Equal(originalJoin, cloned.JoinType);
            Assert.Equal(originalCount, cloned.Count);

            // Assert - items are value-equal but not the same reference
            for (int i = 0; i < originalCount; i++)
            {
                var origItem = original[i];
                var cloneItem = cloned[i];

                // Items should be equal in value
                Assert.True(origItem.Equals(cloneItem), $"Item at index {i} should be value-equal after cloning.");

                // But not the same reference (deep clone)
                Assert.False(object.ReferenceEquals(origItem, cloneItem), $"Item at index {i} should not be the same reference after cloning.");
            }

            // Assert - duplicate original references produce distinct clones
            if (originalHasFirstTwoSameReference)
            {
                Assert.False(object.ReferenceEquals(cloned[0], cloned[1]), "Duplicate references in original should become distinct cloned instances.");
                // Also the two cloned items should be value-equal
                Assert.True(cloned[0].Equals(cloned[1]));
            }

            // Assert - modifying an original PropertyExpr does not affect the cloned counterpart
            if (propIndex >= 0 && propOriginalName != null)
            {
                // Mutate original property's name
                var originalProp = (PropertyExpr)original[propIndex];
                originalProp.PropertyName = propOriginalName + "_Changed";

                // The clone's property should retain the previous name
                var clonedProp = cloned[propIndex] as PropertyExpr;
                Assert.NotNull(clonedProp);
                Assert.Equal(propOriginalName, clonedProp.PropertyName);
                Assert.NotEqual(originalProp.PropertyName, clonedProp.PropertyName);
            }
        }

        /// <summary>
        /// Tests that the constructor accepting an IEnumerable parameter creates an empty ValueSet when the collection parameter is null.
        /// </summary>
        [Fact]
        public void Constructor_IEnumerable_NullCollection_CreatesEmptyValueSet()
        {
            // Arrange
            IEnumerable<ValueTypeExpr>? items = null;

            // Act
            var valueSet = new ValueSet(items);

            // Assert
            Assert.NotNull(valueSet);
            Assert.Equal(0, valueSet.Count);
            Assert.Equal(ValueJoinType.List, valueSet.JoinType);
        }

        /// <summary>
        /// Tests that the constructor accepting an IEnumerable parameter creates an empty ValueSet when the collection is empty.
        /// </summary>
        [Fact]
        public void Constructor_IEnumerable_EmptyCollection_CreatesEmptyValueSet()
        {
            // Arrange
            var items = new List<ValueTypeExpr>();

            // Act
            var valueSet = new ValueSet(items);

            // Assert
            Assert.NotNull(valueSet);
            Assert.Equal(0, valueSet.Count);
        }

        /// <summary>
        /// Tests that the constructor accepting an IEnumerable parameter correctly adds a single item to the ValueSet.
        /// </summary>
        [Fact]
        public void Constructor_IEnumerable_SingleItem_AddsItemToValueSet()
        {
            // Arrange
            ValueTypeExpr item = new ValueExpr(42);
            var items = new List<ValueTypeExpr> { item };

            // Act
            var valueSet = new ValueSet(items);

            // Assert
            Assert.Equal(1, valueSet.Count);
            Assert.Equal(item, valueSet[0]);
        }

        /// <summary>
        /// Tests that the constructor accepting an IEnumerable parameter correctly adds multiple items to the ValueSet in the correct order.
        /// </summary>
        [Fact]
        public void Constructor_IEnumerable_MultipleItems_AddsAllItemsInOrder()
        {
            // Arrange
            ValueTypeExpr item1 = new ValueExpr(1);
            ValueTypeExpr item2 = new ValueExpr("test");
            ValueTypeExpr item3 = new ValueExpr(true);
            var items = new List<ValueTypeExpr> { item1, item2, item3 };

            // Act
            var valueSet = new ValueSet(items);

            // Assert
            Assert.Equal(3, valueSet.Count);
            Assert.Equal(item1, valueSet[0]);
            Assert.Equal(item2, valueSet[1]);
            Assert.Equal(item3, valueSet[2]);
        }

        /// <summary>
        /// Tests that the constructor accepting an IEnumerable parameter handles collections containing null elements correctly.
        /// The Add method converts null to Null, so the collection should contain Null instances.
        /// </summary>
        [Fact]
        public void Constructor_IEnumerable_CollectionWithNullElements_HandlesNullsCorrectly()
        {
            // Arrange
            ValueTypeExpr item1 = new ValueExpr(1);
            ValueTypeExpr? nullItem = null;
            ValueTypeExpr item2 = new ValueExpr(2);
            var items = new List<ValueTypeExpr?> { item1, nullItem, item2 };

            // Act
            var valueSet = new ValueSet(items!);

            // Assert
            Assert.Equal(3, valueSet.Count);
            Assert.Equal(item1, valueSet[0]);
            Assert.NotNull(valueSet[1]); // Add converts null to Null
            Assert.Equal(item2, valueSet[2]);
        }

        /// <summary>
        /// Tests that the constructor accepting an IEnumerable parameter works with different IEnumerable implementations (array).
        /// </summary>
        [Fact]
        public void Constructor_IEnumerable_ArrayInput_AddsAllItems()
        {
            // Arrange
            ValueTypeExpr[] items = new ValueTypeExpr[]
            {
                new ValueExpr(10),
                new ValueExpr(20),
                new ValueExpr(30)
            };

            // Act
            var valueSet = new ValueSet(items);

            // Assert
            Assert.Equal(3, valueSet.Count);
            Assert.Equal(items[0], valueSet[0]);
            Assert.Equal(items[1], valueSet[1]);
            Assert.Equal(items[2], valueSet[2]);
        }

        /// <summary>
        /// Tests that the constructor accepting an IEnumerable parameter works with LINQ queries (deferred execution).
        /// </summary>
        [Fact]
        public void Constructor_IEnumerable_LinqQuery_AddsAllItems()
        {
            // Arrange
            var sourceList = new List<int> { 1, 2, 3, 4, 5 };
            var items = sourceList.Where(x => x > 2).Select(x => (ValueTypeExpr)new ValueExpr(x));

            // Act
            var valueSet = new ValueSet(items);

            // Assert
            Assert.Equal(3, valueSet.Count);
        }

        /// <summary>
        /// Tests that the constructor accepting an IEnumerable parameter handles a large collection correctly.
        /// </summary>
        [Fact]
        public void Constructor_IEnumerable_LargeCollection_AddsAllItems()
        {
            // Arrange
            var items = new List<ValueTypeExpr>();
            for (int i = 0; i < 1000; i++)
            {
                items.Add(new ValueExpr(i));
            }

            // Act
            var valueSet = new ValueSet(items);

            // Assert
            Assert.Equal(1000, valueSet.Count);
            Assert.Equal(new ValueExpr(0), valueSet[0]);
            Assert.Equal(new ValueExpr(999), valueSet[999]);
        }

        /// <summary>
        /// Tests that the constructor accepting an IEnumerable parameter works with various implicit conversion types.
        /// </summary>
        [Theory]
        [InlineData(42)]
        [InlineData(-100)]
        [InlineData(0)]
        public void Constructor_IEnumerable_ImplicitConversions_AddsItemsCorrectly(int value)
        {
            // Arrange
            ValueTypeExpr item = value; // Implicit conversion from int
            var items = new List<ValueTypeExpr> { item };

            // Act
            var valueSet = new ValueSet(items);

            // Assert
            Assert.Equal(1, valueSet.Count);
        }

        /// <summary>
        /// Tests that the constructor accepting an IEnumerable parameter correctly handles collections with nested ValueSet items.
        /// When a ValueSet with the same JoinType is added, the Add method flattens it.
        /// </summary>
        [Fact]
        public void Constructor_IEnumerable_NestedValueSetWithSameJoinType_FlattensItems()
        {
            // Arrange
            var nestedValueSet = new ValueSet(ValueJoinType.List, new ValueExpr(1), new ValueExpr(2));
            var items = new List<ValueTypeExpr> { new ValueExpr(0), nestedValueSet, new ValueExpr(3) };

            // Act
            var valueSet = new ValueSet(items);

            // Assert
            // The nested ValueSet with the same JoinType should be flattened
            Assert.Equal(4, valueSet.Count); // 0, 1, 2, 3
        }

        /// <summary>
        /// Tests that the constructor accepting an IEnumerable parameter correctly handles collections with nested ValueSet items of different JoinType.
        /// When a ValueSet with a different JoinType is added, it should be added as a single item.
        /// </summary>
        [Fact]
        public void Constructor_IEnumerable_NestedValueSetWithDifferentJoinType_AddsAsOneItem()
        {
            // Arrange
            var nestedValueSet = new ValueSet(ValueJoinType.Concat, new ValueExpr("Hello"), new ValueExpr(" "), new ValueExpr("World"));
            var items = new List<ValueTypeExpr> { new ValueExpr(1), nestedValueSet, new ValueExpr(2) };

            // Act
            var valueSet = new ValueSet(items); // Default JoinType is List

            // Assert
            // The nested ValueSet with different JoinType should be added as one item
            Assert.Equal(3, valueSet.Count);
            Assert.Equal(nestedValueSet, valueSet[1]);
        }

        /// <summary>
        /// Tests that the constructor accepting an IEnumerable parameter initializes JoinType to the default value (List).
        /// </summary>
        [Fact]
        public void Constructor_IEnumerable_DefaultJoinType_IsSetToList()
        {
            // Arrange
            var items = new List<ValueTypeExpr> { new ValueExpr(1), new ValueExpr(2) };

            // Act
            var valueSet = new ValueSet(items);

            // Assert
            Assert.Equal(ValueJoinType.List, valueSet.JoinType);
        }

        /// <summary>
        /// Tests that the constructor accepting an IEnumerable parameter preserves item references.
        /// </summary>
        [Fact]
        public void Constructor_IEnumerable_PreservesItemReferences()
        {
            // Arrange
            var expr1 = new ValueExpr(100);
            var expr2 = new ValueExpr("test");
            var items = new List<ValueTypeExpr> { expr1, expr2 };

            // Act
            var valueSet = new ValueSet(items);

            // Assert
            Assert.Same(expr1, valueSet[0]);
            Assert.Same(expr2, valueSet[1]);
        }

        /// <summary>
        /// Tests that the constructor accepting an IEnumerable parameter allows duplicate items.
        /// </summary>
        [Fact]
        public void Constructor_IEnumerable_DuplicateItems_AddsAllDuplicates()
        {
            // Arrange
            var expr = new ValueExpr(42);
            var items = new List<ValueTypeExpr> { expr, expr, expr };

            // Act
            var valueSet = new ValueSet(items);

            // Assert
            Assert.Equal(3, valueSet.Count);
            Assert.Same(expr, valueSet[0]);
            Assert.Same(expr, valueSet[1]);
            Assert.Same(expr, valueSet[2]);
        }
    }
}