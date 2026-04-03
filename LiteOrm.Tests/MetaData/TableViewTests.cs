using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the TableView class.
    /// </summary>
    public partial class TableViewTests
    {
        /// <summary>
        /// Tests that the constructor initializes the TableView correctly with valid parameters.
        /// </summary>
        [Fact]
        public void TableView_WithValidParameters_InitializesSuccessfully()
        {
            // Arrange
            var mockTable = new Mock<TableDefinition>();
            var mockJoinedTable = new Mock<JoinedTable>();
            var mockColumn = new Mock<SqlColumn>();
            var joinedTables = new List<JoinedTable> { mockJoinedTable.Object };
            var columns = new List<SqlColumn> { mockColumn.Object };

            // Act
            var tableView = new TableView(mockTable.Object, joinedTables, columns);

            // Assert
            Assert.NotNull(tableView);
            Assert.NotNull(tableView.Definition);
        }

        /// <summary>
        /// Tests that the constructor initializes correctly with empty collections.
        /// </summary>
        [Fact]
        public void TableView_WithEmptyCollections_InitializesSuccessfully()
        {
            // Arrange
            var mockTable = new Mock<TableDefinition>();
            var joinedTables = new List<JoinedTable>();
            var columns = new List<SqlColumn>();

            // Act
            var tableView = new TableView(mockTable.Object, joinedTables, columns);

            // Assert
            Assert.NotNull(tableView);
            Assert.NotNull(tableView.Definition);
        }

        /// <summary>
        /// Tests that the constructor initializes correctly with single-item collections.
        /// </summary>
        [Fact]
        public void TableView_WithSingleItemCollections_InitializesSuccessfully()
        {
            // Arrange
            var mockTable = new Mock<TableDefinition>();
            var mockJoinedTable = new Mock<JoinedTable>();
            var mockColumn = new Mock<SqlColumn>();
            var joinedTables = new List<JoinedTable> { mockJoinedTable.Object };
            var columns = new List<SqlColumn> { mockColumn.Object };

            // Act
            var tableView = new TableView(mockTable.Object, joinedTables, columns);

            // Assert
            Assert.NotNull(tableView);
            Assert.NotNull(tableView.Definition);
        }

        /// <summary>
        /// Tests that the constructor initializes correctly with multiple items in collections.
        /// </summary>
        [Fact]
        public void TableView_WithMultipleItemsInCollections_InitializesSuccessfully()
        {
            // Arrange
            var mockTable = new Mock<TableDefinition>();
            var mockJoinedTable1 = new Mock<JoinedTable>();
            var mockJoinedTable2 = new Mock<JoinedTable>();
            var mockColumn1 = new Mock<SqlColumn>();
            var mockColumn2 = new Mock<SqlColumn>();
            var mockColumn3 = new Mock<SqlColumn>();
            var joinedTables = new List<JoinedTable> { mockJoinedTable1.Object, mockJoinedTable2.Object };
            var columns = new List<SqlColumn> { mockColumn1.Object, mockColumn2.Object, mockColumn3.Object };

            // Act
            var tableView = new TableView(mockTable.Object, joinedTables, columns);

            // Assert
            Assert.NotNull(tableView);
            Assert.NotNull(tableView.Definition);
        }

        /// <summary>
        /// Tests that the constructor throws ArgumentNullException when joinedTables parameter is null.
        /// </summary>
        [Fact]
        public void TableView_WithNullJoinedTables_ThrowsArgumentNullException()
        {
            // Arrange
            var mockTable = new Mock<TableDefinition>();
            var columns = new List<SqlColumn> { new Mock<SqlColumn>().Object };
            ICollection<JoinedTable>? joinedTables = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TableView(mockTable.Object, joinedTables!, columns));
        }

        /// <summary>
        /// Tests that the constructor throws NullReferenceException when columns parameter is null.
        /// </summary>
        [Fact]
        public void TableView_WithNullColumns_ThrowsNullReferenceException()
        {
            // Arrange
            var mockTable = new Mock<TableDefinition>();
            var joinedTables = new List<JoinedTable> { new Mock<JoinedTable>().Object };
            ICollection<SqlColumn>? columns = null;

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => new TableView(mockTable.Object, joinedTables, columns!));
        }

        /// <summary>
        /// Tests that the constructor initializes successfully when table parameter is null.
        /// The constructor does not validate the table parameter.
        /// </summary>
        [Fact]
        public void TableView_WithNullTable_InitializesSuccessfully()
        {
            // Arrange
            TableDefinition? table = null;
            var joinedTables = new List<JoinedTable> { new Mock<JoinedTable>().Object };
            var columns = new List<SqlColumn> { new Mock<SqlColumn>().Object };

            // Act
            var tableView = new TableView(table!, joinedTables, columns);

            // Assert
            Assert.NotNull(tableView);
        }

        /// <summary>
        /// Tests that the constructor throws ArgumentNullException when both joinedTables and columns are null.
        /// The joinedTables null check occurs first in the List constructor.
        /// </summary>
        [Fact]
        public void TableView_WithNullJoinedTablesAndColumns_ThrowsArgumentNullException()
        {
            // Arrange
            var mockTable = new Mock<TableDefinition>();
            ICollection<JoinedTable>? joinedTables = null;
            ICollection<SqlColumn>? columns = null;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new TableView(mockTable.Object, joinedTables!, columns!));
        }

        /// <summary>
        /// Tests that the constructor initializes correctly with collections containing duplicate items.
        /// </summary>
        [Fact]
        public void TableView_WithDuplicateItemsInCollections_InitializesSuccessfully()
        {
            // Arrange
            var mockTable = new Mock<TableDefinition>();
            var mockJoinedTable = new Mock<JoinedTable>();
            var mockColumn = new Mock<SqlColumn>();
            var joinedTables = new List<JoinedTable> { mockJoinedTable.Object, mockJoinedTable.Object };
            var columns = new List<SqlColumn> { mockColumn.Object, mockColumn.Object };

            // Act
            var tableView = new TableView(mockTable.Object, joinedTables, columns);

            // Assert
            Assert.NotNull(tableView);
            Assert.NotNull(tableView.Definition);
        }

        /// <summary>
        /// Tests that the JoinedTables property returns an empty readonly collection when no joined tables are provided.
        /// </summary>
        [Fact]
        public void JoinedTables_EmptyCollection_ReturnsEmptyReadOnlyCollection()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            var emptyJoinedTables = new List<JoinedTable>();
            var mockColumns = new List<SqlColumn>();
            var tableView = new TableView(mockTableDefinition.Object, emptyJoinedTables, mockColumns);

            // Act
            var result = tableView.JoinedTables;

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
            Assert.IsType<ReadOnlyCollection<JoinedTable>>(result);
        }

        /// <summary>
        /// Tests that the JoinedTables property returns a readonly collection with a single joined table.
        /// </summary>
        [Fact]
        public void JoinedTables_SingleTable_ReturnsSingleItemReadOnlyCollection()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            var mockForeignTableDefinition = new Mock<TableDefinition>();
            mockForeignTableDefinition.Setup(t => t.Keys).Returns(new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>()));

            var joinedTable = new JoinedTable(mockForeignTableDefinition.Object);
            var joinedTables = new List<JoinedTable> { joinedTable };
            var mockColumns = new List<SqlColumn>();
            var tableView = new TableView(mockTableDefinition.Object, joinedTables, mockColumns);

            // Act
            var result = tableView.JoinedTables;

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Same(joinedTable, result[0]);
            Assert.IsType<ReadOnlyCollection<JoinedTable>>(result);
        }

        /// <summary>
        /// Tests that the JoinedTables property returns a readonly collection with multiple joined tables.
        /// </summary>
        [Fact]
        public void JoinedTables_MultipleTables_ReturnsAllTablesInReadOnlyCollection()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            var mockForeignTableDefinition1 = new Mock<TableDefinition>();
            var mockForeignTableDefinition2 = new Mock<TableDefinition>();
            var mockForeignTableDefinition3 = new Mock<TableDefinition>();

            mockForeignTableDefinition1.Setup(t => t.Keys).Returns(new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>()));
            mockForeignTableDefinition2.Setup(t => t.Keys).Returns(new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>()));
            mockForeignTableDefinition3.Setup(t => t.Keys).Returns(new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>()));

            var joinedTable1 = new JoinedTable(mockForeignTableDefinition1.Object);
            var joinedTable2 = new JoinedTable(mockForeignTableDefinition2.Object);
            var joinedTable3 = new JoinedTable(mockForeignTableDefinition3.Object);
            var joinedTables = new List<JoinedTable> { joinedTable1, joinedTable2, joinedTable3 };
            var mockColumns = new List<SqlColumn>();
            var tableView = new TableView(mockTableDefinition.Object, joinedTables, mockColumns);

            // Act
            var result = tableView.JoinedTables;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.IsType<ReadOnlyCollection<JoinedTable>>(result);
        }

        /// <summary>
        /// Tests that the JoinedTables property caches the result and returns the same instance on multiple accesses.
        /// This verifies the lazy-loading behavior.
        /// </summary>
        [Fact]
        public void JoinedTables_MultipleAccesses_ReturnsSameCachedInstance()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            var mockForeignTableDefinition = new Mock<TableDefinition>();
            mockForeignTableDefinition.Setup(t => t.Keys).Returns(new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>()));

            var joinedTable = new JoinedTable(mockForeignTableDefinition.Object);
            var joinedTables = new List<JoinedTable> { joinedTable };
            var mockColumns = new List<SqlColumn>();
            var tableView = new TableView(mockTableDefinition.Object, joinedTables, mockColumns);

            // Act
            var firstAccess = tableView.JoinedTables;
            var secondAccess = tableView.JoinedTables;
            var thirdAccess = tableView.JoinedTables;

            // Assert
            Assert.Same(firstAccess, secondAccess);
            Assert.Same(secondAccess, thirdAccess);
        }

        /// <summary>
        /// Tests that the JoinedTables property returns a truly readonly collection that cannot be cast back to a mutable list.
        /// </summary>
        [Fact]
        public void JoinedTables_ReturnedCollection_IsReadOnly()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            var mockForeignTableDefinition = new Mock<TableDefinition>();
            mockForeignTableDefinition.Setup(t => t.Keys).Returns(new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>()));

            var joinedTable = new JoinedTable(mockForeignTableDefinition.Object);
            var joinedTables = new List<JoinedTable> { joinedTable };
            var mockColumns = new List<SqlColumn>();
            var tableView = new TableView(mockTableDefinition.Object, joinedTables, mockColumns);

            // Act
            var result = tableView.JoinedTables;

            // Assert
            Assert.IsNotType<List<JoinedTable>>(result);
            Assert.IsAssignableFrom<ReadOnlyCollection<JoinedTable>>(result);
        }

        /// <summary>
        /// Tests that the JoinedTables property handles tables with no foreign key dependencies correctly.
        /// When tables have no dependencies (empty ForeignKeys), the sorting should complete without errors.
        /// </summary>
        [Fact]
        public void JoinedTables_TablesWithNoDependencies_SortsWithoutErrors()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            var mockForeignTableDefinition1 = new Mock<TableDefinition>();
            var mockForeignTableDefinition2 = new Mock<TableDefinition>();

            mockForeignTableDefinition1.Setup(t => t.Keys).Returns(new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>()));
            mockForeignTableDefinition1.Setup(t => t.Name).Returns("Table1");
            mockForeignTableDefinition2.Setup(t => t.Keys).Returns(new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>()));
            mockForeignTableDefinition2.Setup(t => t.Name).Returns("Table2");

            var joinedTable1 = new JoinedTable(mockForeignTableDefinition1.Object);
            var joinedTable2 = new JoinedTable(mockForeignTableDefinition2.Object);
            var joinedTables = new List<JoinedTable> { joinedTable1, joinedTable2 };
            var mockColumns = new List<SqlColumn>();
            var tableView = new TableView(mockTableDefinition.Object, joinedTables, mockColumns);

            // Act
            var result = tableView.JoinedTables;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }

        /// <summary>
        /// Tests that the Definition property returns the same TableDefinition instance
        /// that was passed to the constructor.
        /// Input: A valid TableDefinition instance, empty collections for joined tables and columns.
        /// Expected: The Definition property returns the exact same instance.
        /// </summary>
        [Fact]
        public void Definition_ValidTableDefinition_ReturnsSameInstance()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>(typeof(string), new List<ColumnDefinition>());
            var joinedTables = new List<JoinedTable>();
            var columns = new List<SqlColumn>();
            var tableView = new TableView(mockTableDefinition.Object, joinedTables, columns);

            // Act
            var result = tableView.Definition;

            // Assert
            Assert.Same(mockTableDefinition.Object, result);
        }

        /// <summary>
        /// Tests that multiple accesses to the Definition property return the same instance,
        /// demonstrating consistency and that the underlying field is readonly.
        /// Input: A valid TableDefinition instance.
        /// Expected: All accesses return the exact same instance.
        /// </summary>
        [Fact]
        public void Definition_MultipleAccesses_ReturnsConsistentInstance()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>(typeof(int), new List<ColumnDefinition>());
            var joinedTables = new List<JoinedTable>();
            var columns = new List<SqlColumn>();
            var tableView = new TableView(mockTableDefinition.Object, joinedTables, columns);

            // Act
            var firstAccess = tableView.Definition;
            var secondAccess = tableView.Definition;
            var thirdAccess = tableView.Definition;

            // Assert
            Assert.Same(firstAccess, secondAccess);
            Assert.Same(secondAccess, thirdAccess);
            Assert.Same(mockTableDefinition.Object, firstAccess);
        }

        /// <summary>
        /// Tests the edge case where null is passed as the table parameter to the constructor.
        /// Although not recommended in production, this tests the actual behavior when no validation is present.
        /// Input: null for TableDefinition parameter.
        /// Expected: The Definition property returns null without throwing an exception.
        /// </summary>
        [Fact]
        public void Definition_NullTableDefinition_ReturnsNull()
        {
            // Arrange
            var joinedTables = new List<JoinedTable>();
            var columns = new List<SqlColumn>();
            var tableView = new TableView(null!, joinedTables, columns);

            // Act
            var result = tableView.Definition;

            // Assert
            Assert.Null(result);
        }
    }
}