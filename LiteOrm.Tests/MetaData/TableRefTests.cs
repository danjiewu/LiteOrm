using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

using LiteOrm;
using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the TableRef class.
    /// </summary>
    public class TableRefTests
    {
        /// <summary>
        /// Tests that GetColumn returns null when propertyName is null.
        /// Validates that the method handles null input gracefully.
        /// Expected result: null.
        /// </summary>
        [Fact]
        public void GetColumn_NullPropertyName_ReturnsNull()
        {
            // Arrange
            var tableRef = CreateTableRef(new Dictionary<string, Mock<SqlColumn>>());

            // Act
            var result = tableRef.GetColumn(null);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GetColumn returns null when propertyName is an empty string.
        /// Validates that the method handles empty string input gracefully.
        /// Expected result: null.
        /// </summary>
        [Fact]
        public void GetColumn_EmptyPropertyName_ReturnsNull()
        {
            // Arrange
            var tableRef = CreateTableRef(new Dictionary<string, Mock<SqlColumn>>());

            // Act
            var result = tableRef.GetColumn(string.Empty);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GetColumn returns null when the property name does not exist in the cache.
        /// Validates that the method correctly returns null for non-existent properties.
        /// Expected result: null.
        /// </summary>
        [Theory]
        [InlineData("NonExistentProperty")]
        [InlineData("   ")]
        [InlineData("\t")]
        [InlineData("\n")]
        [InlineData("DifferentCase")]
        public void GetColumn_NonExistingPropertyName_ReturnsNull(string propertyName)
        {
            // Arrange
            var columns = new Dictionary<string, Mock<SqlColumn>>
            {
                { "ExistingColumn", CreateMockSqlColumn("ExistingColumn") }
            };
            var tableRef = CreateTableRef(columns);

            // Act
            var result = tableRef.GetColumn(propertyName);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GetColumn returns the correct ColumnRef when a valid property name exists.
        /// Validates that the method successfully retrieves columns from the cache.
        /// Expected result: The corresponding ColumnRef object.
        /// </summary>
        [Theory]
        [InlineData("Column1")]
        [InlineData("Column2")]
        [InlineData("Column3")]
        public void GetColumn_ExistingPropertyName_ReturnsCorrectColumnRef(string propertyName)
        {
            // Arrange
            var columns = new Dictionary<string, Mock<SqlColumn>>
            {
                { "Column1", CreateMockSqlColumn("Column1") },
                { "Column2", CreateMockSqlColumn("Column2") },
                { "Column3", CreateMockSqlColumn("Column3") }
            };
            var tableRef = CreateTableRef(columns);

            // Act
            var result = tableRef.GetColumn(propertyName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(propertyName, result.Column.PropertyName);
        }

        /// <summary>
        /// Tests that GetColumn is case-sensitive when looking up property names.
        /// Validates that the cache lookup respects case sensitivity.
        /// Expected result: null for mismatched case, non-null for exact match.
        /// </summary>
        [Fact]
        public void GetColumn_CaseSensitivePropertyName_RespectsCase()
        {
            // Arrange
            var columns = new Dictionary<string, Mock<SqlColumn>>
            {
                { "PropertyName", CreateMockSqlColumn("PropertyName") }
            };
            var tableRef = CreateTableRef(columns);

            // Act
            var resultLowerCase = tableRef.GetColumn("propertyname");
            var resultUpperCase = tableRef.GetColumn("PROPERTYNAME");
            var resultCorrectCase = tableRef.GetColumn("PropertyName");

            // Assert
            Assert.Null(resultLowerCase);
            Assert.Null(resultUpperCase);
            Assert.NotNull(resultCorrectCase);
        }

        /// <summary>
        /// Tests that GetColumn returns null when called on an empty table with no columns.
        /// Validates that the method handles tables without columns gracefully.
        /// Expected result: null.
        /// </summary>
        [Fact]
        public void GetColumn_EmptyTable_ReturnsNull()
        {
            // Arrange
            var tableRef = CreateTableRef(new Dictionary<string, Mock<SqlColumn>>());

            // Act
            var result = tableRef.GetColumn("AnyProperty");

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GetColumn correctly populates the cache on first access.
        /// Validates that the lazy initialization of NamedColumnCache works correctly.
        /// Expected result: Subsequent calls return the same ColumnRef instance.
        /// </summary>
        [Fact]
        public void GetColumn_MultipleCalls_ReturnsSameInstance()
        {
            // Arrange
            var columns = new Dictionary<string, Mock<SqlColumn>>
            {
                { "TestColumn", CreateMockSqlColumn("TestColumn") }
            };
            var tableRef = CreateTableRef(columns);

            // Act
            var result1 = tableRef.GetColumn("TestColumn");
            var result2 = tableRef.GetColumn("TestColumn");

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.Same(result1, result2);
        }

        /// <summary>
        /// Tests that GetColumn handles special characters in property names.
        /// Validates that the method can work with property names containing special characters.
        /// Expected result: Returns the ColumnRef if the exact name matches.
        /// </summary>
        [Theory]
        [InlineData("Property_With_Underscores")]
        [InlineData("Property123")]
        [InlineData("_LeadingUnderscore")]
        public void GetColumn_SpecialCharactersInPropertyName_ReturnsCorrectColumnRef(string propertyName)
        {
            // Arrange
            var columns = new Dictionary<string, Mock<SqlColumn>>
            {
                { propertyName, CreateMockSqlColumn(propertyName) }
            };
            var tableRef = CreateTableRef(columns);

            // Act
            var result = tableRef.GetColumn(propertyName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(propertyName, result.Column.PropertyName);
        }

        private Mock<SqlColumn> CreateMockSqlColumn(string propertyName)
        {
            var propertyInfoMock = new Mock<PropertyInfo>();
            propertyInfoMock.Setup(p => p.Name).Returns(propertyName);

            var mockColumn = new Mock<SqlColumn>(propertyInfoMock.Object) { CallBase = true };

            return mockColumn;
        }

        private TestTableRef CreateTableRef(Dictionary<string, Mock<SqlColumn>> columnMocks)
        {
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(t => t.Name).Returns("TestTable");

            var columnsList = columnMocks.Values.Select(m => m.Object).ToList();
            var readOnlyColumns = new ReadOnlyCollection<SqlColumn>(columnsList);
            mockTableDefinition.Setup(t => t.Columns).Returns(readOnlyColumns);

            return new TestTableRef(mockTableDefinition.Object);
        }

        private class TestTableRef : TableRef
        {
            public TestTableRef(TableDefinition table) : base(table)
            {
            }
        }

        /// <summary>
        /// Tests that Equals returns true when comparing an instance to itself (reference equality).
        /// </summary>
        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(td => td.Name).Returns("TestTable");
            mockTableDefinition.Setup(td => td.Columns).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<ColumnDefinition>(new System.Collections.Generic.List<ColumnDefinition>()));

            var mockTableRef = new Mock<TableRef>(mockTableDefinition.Object) { CallBase = true };
            var tableRef = mockTableRef.Object;

            // Act
            var result = tableRef.Equals(tableRef);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing to null.
        /// </summary>
        [Fact]
        public void Equals_NullObject_ReturnsFalse()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(td => td.Name).Returns("TestTable");
            mockTableDefinition.Setup(td => td.Columns).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<ColumnDefinition>(new System.Collections.Generic.List<ColumnDefinition>()));

            var mockTableRef = new Mock<TableRef>(mockTableDefinition.Object) { CallBase = true };
            var tableRef = mockTableRef.Object;

            // Act
            var result = tableRef.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing to an object of a different type.
        /// </summary>
        [Theory]
        [InlineData("string")]
        [InlineData(42)]
        [InlineData(3.14)]
        public void Equals_DifferentType_ReturnsFalse(object obj)
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(td => td.Name).Returns("TestTable");
            mockTableDefinition.Setup(td => td.Columns).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<ColumnDefinition>(new System.Collections.Generic.List<ColumnDefinition>()));

            var mockTableRef = new Mock<TableRef>(mockTableDefinition.Object) { CallBase = true };
            var tableRef = mockTableRef.Object;

            // Act
            var result = tableRef.Equals(obj);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing two instances with the same TableDefinition and Name.
        /// </summary>
        [Fact]
        public void Equals_SameTableDefinitionAndName_ReturnsTrue()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(td => td.Name).Returns("TestTable");
            mockTableDefinition.Setup(td => td.Columns).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<ColumnDefinition>(new System.Collections.Generic.List<ColumnDefinition>()));

            var mockTableRef1 = new Mock<TableRef>(mockTableDefinition.Object) { CallBase = true };
            var mockTableRef2 = new Mock<TableRef>(mockTableDefinition.Object) { CallBase = true };
            var tableRef1 = mockTableRef1.Object;
            var tableRef2 = mockTableRef2.Object;

            // Act
            var result = tableRef1.Equals(tableRef2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing two instances with different TableDefinitions.
        /// </summary>
        [Fact]
        public void Equals_DifferentTableDefinition_ReturnsFalse()
        {
            // Arrange
            var mockTableDefinition1 = new Mock<TableDefinition>();
            mockTableDefinition1.Setup(td => td.Name).Returns("TestTable1");
            mockTableDefinition1.Setup(td => td.Columns).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<ColumnDefinition>(new System.Collections.Generic.List<ColumnDefinition>()));

            var mockTableDefinition2 = new Mock<TableDefinition>();
            mockTableDefinition2.Setup(td => td.Name).Returns("TestTable2");
            mockTableDefinition2.Setup(td => td.Columns).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<ColumnDefinition>(new System.Collections.Generic.List<ColumnDefinition>()));

            var mockTableRef1 = new Mock<TableRef>(mockTableDefinition1.Object) { CallBase = true };
            var mockTableRef2 = new Mock<TableRef>(mockTableDefinition2.Object) { CallBase = true };
            var tableRef1 = mockTableRef1.Object;
            var tableRef2 = mockTableRef2.Object;

            // Act
            var result = tableRef1.Equals(tableRef2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing two instances with same TableDefinition but different Names.
        /// </summary>
        [Fact]
        public void Equals_SameTableDefinitionDifferentName_ReturnsFalse()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(td => td.Name).Returns("TestTable");
            mockTableDefinition.Setup(td => td.Columns).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<ColumnDefinition>(new System.Collections.Generic.List<ColumnDefinition>()));

            var mockTableRef1 = new Mock<TableRef>(mockTableDefinition.Object) { CallBase = true };
            var mockTableRef2 = new Mock<TableRef>(mockTableDefinition.Object) { CallBase = true };
            var tableRef1 = mockTableRef1.Object;
            var tableRef2 = mockTableRef2.Object;

            // Manually set different names
            typeof(TableRef).GetProperty("Name").SetValue(tableRef2, "DifferentName");

            // Act
            var result = tableRef1.Equals(tableRef2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing instances of different derived types,
        /// even if they have the same TableDefinition and Name.
        /// </summary>
        [Fact]
        public void Equals_DifferentDerivedTypes_ReturnsFalse()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(td => td.Name).Returns("TestTable");
            mockTableDefinition.Setup(td => td.Columns).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<ColumnDefinition>(new System.Collections.Generic.List<ColumnDefinition>()));

            var mockTableRef1 = new Mock<TableRef>(mockTableDefinition.Object) { CallBase = true };
            mockTableRef1.Setup(tr => tr.GetType()).Returns(typeof(DerivedTableRef1));

            var mockTableRef2 = new Mock<TableRef>(mockTableDefinition.Object) { CallBase = true };
            mockTableRef2.Setup(tr => tr.GetType()).Returns(typeof(DerivedTableRef2));

            var tableRef1 = mockTableRef1.Object;
            var tableRef2 = mockTableRef2.Object;

            // Act
            var result = tableRef1.Equals(tableRef2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals handles null TableDefinition correctly.
        /// </summary>
        [Fact]
        public void Equals_NullTableDefinition_HandlesCorrectly()
        {
            // Arrange
            var mockTableDefinition1 = new Mock<TableDefinition>();
            mockTableDefinition1.Setup(td => td.Name).Returns("TestTable");
            mockTableDefinition1.Setup(td => td.Columns).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<ColumnDefinition>(new System.Collections.Generic.List<ColumnDefinition>()));

            var mockTableRef1 = new Mock<TableRef>(mockTableDefinition1.Object) { CallBase = true };
            var mockTableRef2 = new Mock<TableRef>(mockTableDefinition1.Object) { CallBase = true };
            var tableRef1 = mockTableRef1.Object;
            var tableRef2 = mockTableRef2.Object;

            // Set one TableDefinition to null via reflection
            var field = typeof(TableRef).GetField("_tableDefinition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(tableRef2, null);

            // Act
            var result = tableRef1.Equals(tableRef2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when both instances have null TableDefinitions and same Name.
        /// </summary>
        [Fact]
        public void Equals_BothNullTableDefinitions_ReturnsTrue()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(td => td.Name).Returns("TestTable");
            mockTableDefinition.Setup(td => td.Columns).Returns(new System.Collections.ObjectModel.ReadOnlyCollection<ColumnDefinition>(new System.Collections.Generic.List<ColumnDefinition>()));

            var mockTableRef1 = new Mock<TableRef>(mockTableDefinition.Object) { CallBase = true };
            var mockTableRef2 = new Mock<TableRef>(mockTableDefinition.Object) { CallBase = true };
            var tableRef1 = mockTableRef1.Object;
            var tableRef2 = mockTableRef2.Object;

            // Set both TableDefinitions to null via reflection
            var field = typeof(TableRef).GetField("_tableDefinition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(tableRef1, null);
            field.SetValue(tableRef2, null);

            // Act
            var result = tableRef1.Equals(tableRef2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Helper class for testing different derived types scenario.
        /// </summary>
        private class DerivedTableRef1 : TableRef
        {
            public DerivedTableRef1(TableDefinition table) : base(table) { }
        }

        /// <summary>
        /// Helper class for testing different derived types scenario.
        /// </summary>
        private class DerivedTableRef2 : TableRef
        {
            public DerivedTableRef2(TableDefinition table) : base(table) { }
        }

        /// <summary>
        /// Tests that GetHashCode returns consistent hash codes for the same object.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsSameValue()
        {
            // Arrange
            var tableDef = CreateTableDefinition("TestTable");
            var tableRef = new TestableTableRef(tableDef);

            // Act
            var hash1 = tableRef.GetHashCode();
            var hash2 = tableRef.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns the same hash for objects with identical TableDefinition and Name.
        /// </summary>
        [Fact]
        public void GetHashCode_WithIdenticalTableDefinitionAndName_ReturnsSameHash()
        {
            // Arrange
            var tableDef = CreateTableDefinition("TestTable");
            var tableRef1 = new TestableTableRef(tableDef);
            var tableRef2 = new TestableTableRef(tableDef);

            // Act
            var hash1 = tableRef1.GetHashCode();
            var hash2 = tableRef2.GetHashCode();

            // Assert
            Assert.Equal(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode returns different hashes for objects with different TableDefinitions.
        /// </summary>
        [Fact]
        public void GetHashCode_WithDifferentTableDefinitions_ReturnsDifferentHashes()
        {
            // Arrange
            var tableDef1 = CreateTableDefinition("Table1");
            var tableDef2 = CreateTableDefinition("Table2");
            var tableRef1 = new TestableTableRef(tableDef1);
            var tableRef2 = new TestableTableRef(tableDef2);

            // Act
            var hash1 = tableRef1.GetHashCode();
            var hash2 = tableRef2.GetHashCode();

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        /// <summary>
        /// Tests that GetHashCode handles null TableDefinition correctly by using 0 in the calculation.
        /// </summary>
        [Fact]
        public void GetHashCode_WithNullTableDefinition_ReturnsHashBasedOnNameOnly()
        {
            // Arrange
            var tableDef = CreateTableDefinition("TestTable");
            var tableRef = new TestableTableRef(tableDef);
            tableRef.SetTableDefinitionNull();
            var expectedHash = 0 ^ (tableRef.Name?.GetHashCode() ?? 0);

            // Act
            var actualHash = tableRef.GetHashCode();

            // Assert
            Assert.Equal(expectedHash, actualHash);
        }

        /// <summary>
        /// Tests that GetHashCode handles null Name correctly by using 0 in the calculation.
        /// </summary>
        [Fact]
        public void GetHashCode_WithNullName_ReturnsHashBasedOnTableDefinitionOnly()
        {
            // Arrange
            var tableDef = CreateTableDefinition("TestTable");
            var tableRef = new TestableTableRef(tableDef);
            tableRef.SetNameNull();
            var expectedHash = unchecked((tableDef?.GetHashCode() ?? 0) * 31) ^ 0;

            // Act
            var actualHash = tableRef.GetHashCode();

            // Assert
            Assert.Equal(expectedHash, actualHash);
        }

        /// <summary>
        /// Tests that GetHashCode returns 0 when both TableDefinition and Name are null.
        /// </summary>
        [Fact]
        public void GetHashCode_WithBothTableDefinitionAndNameNull_ReturnsZero()
        {
            // Arrange
            var tableDef = CreateTableDefinition("TestTable");
            var tableRef = new TestableTableRef(tableDef);
            tableRef.SetTableDefinitionNull();
            tableRef.SetNameNull();
            var expectedHash = unchecked((0 * 31) ^ 0);

            // Act
            var actualHash = tableRef.GetHashCode();

            // Assert
            Assert.Equal(expectedHash, actualHash);
        }

        /// <summary>
        /// Tests that GetHashCode uses the unchecked context and handles potential integer overflow.
        /// </summary>
        [Fact]
        public void GetHashCode_WithLargeHashValues_HandlesOverflowCorrectly()
        {
            // Arrange
            var tableDef = CreateTableDefinitionWithCustomHashCode(int.MaxValue / 31 + 1);
            var tableRef = new TestableTableRef(tableDef);

            // Act
            var hash = tableRef.GetHashCode();

            // Assert
            // The test should not throw an OverflowException
            Assert.NotEqual(0, hash);
        }

        /// <summary>
        /// Tests that GetHashCode formula correctly combines TableDefinition and Name hashes.
        /// </summary>
        [Theory]
        [InlineData("Table1")]
        [InlineData("Table2")]
        [InlineData("")]
        [InlineData("VeryLongTableNameWithSpecialCharacters!@#$%^&*()")]
        public void GetHashCode_WithVariousTableNames_CalculatesCorrectly(string tableName)
        {
            // Arrange
            var tableDef = CreateTableDefinition(tableName);
            var tableRef = new TestableTableRef(tableDef);
            var expectedHash = unchecked(((tableDef?.GetHashCode() ?? 0) * 31) ^ (tableName?.GetHashCode() ?? 0));

            // Act
            var actualHash = tableRef.GetHashCode();

            // Assert
            Assert.Equal(expectedHash, actualHash);
        }

        /// <summary>
        /// Creates a TableDefinition for testing purposes.
        /// </summary>
        private TableDefinition CreateTableDefinition(string tableName)
        {
            var mockTableDef = new Mock<TableDefinition>(typeof(object), new List<ColumnDefinition>());
            mockTableDef.SetupGet(t => t.Name).Returns(tableName);
            return mockTableDef.Object;
        }

        /// <summary>
        /// Creates a TableDefinition with a custom hash code for testing overflow scenarios.
        /// </summary>
        private TableDefinition CreateTableDefinitionWithCustomHashCode(int hashCode)
        {
            var mockTableDef = new Mock<TableDefinition>(typeof(object), new List<ColumnDefinition>());
            mockTableDef.Setup(t => t.GetHashCode()).Returns(hashCode);
            mockTableDef.SetupGet(t => t.Name).Returns("TestTable");
            return mockTableDef.Object;
        }

        /// <summary>
        /// Testable implementation of the abstract TableRef class.
        /// </summary>
        private class TestableTableRef : TableRef
        {
            public TestableTableRef(TableDefinition table) : base(table)
            {
            }

            /// <summary>
            /// Sets the TableDefinition to null for testing purposes.
            /// </summary>
            public void SetTableDefinitionNull()
            {
                var field = typeof(TableRef).GetField("_tableDefinition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(this, null);
            }

            /// <summary>
            /// Sets the Name to null for testing purposes.
            /// </summary>
            public void SetNameNull()
            {
                var property = typeof(SqlObject).GetProperty("Name");
                var setter = property?.GetSetMethod(true);
                setter?.Invoke(this, new object[] { null });
            }
        }

        /// <summary>
        /// Tests that the Columns property returns the same reference on multiple accesses.
        /// </summary>
        [Fact]
        public void Columns_MultipleAccesses_ReturnsSameReference()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.SetupGet(t => t.Name).Returns("TestTable");
            mockTableDefinition.SetupGet(t => t.Columns).Returns(new ReadOnlyCollection<SqlColumn>(new SqlColumn[0]));
            var tableRef = new TestTableRef(mockTableDefinition.Object);

            // Act
            var columns1 = tableRef.Columns;
            var columns2 = tableRef.Columns;

            // Assert
            Assert.Same(columns1, columns2);
        }

        /// <summary>
        /// Tests that the Columns property returns an empty collection when TableDefinition has no columns.
        /// </summary>
        [Fact]
        public void Columns_EmptyColumnsInTableDefinition_ReturnsEmptyCollection()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.SetupGet(t => t.Name).Returns("TestTable");
            mockTableDefinition.SetupGet(t => t.Columns).Returns(new ReadOnlyCollection<SqlColumn>(new SqlColumn[0]));
            var tableRef = new TestTableRef(mockTableDefinition.Object);

            // Act
            var columns = tableRef.Columns;

            // Assert
            Assert.NotNull(columns);
            Assert.Empty(columns);
        }

        /// <summary>
        /// Tests that the Columns property returns a collection with one column when TableDefinition has one column.
        /// </summary>
        [Fact]
        public void Columns_SingleColumnInTableDefinition_ReturnsSingleColumn()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.SetupGet(c => c.Name).Returns("Column1");
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.SetupGet(t => t.Name).Returns("TestTable");
            mockTableDefinition.SetupGet(t => t.Columns).Returns(new ReadOnlyCollection<SqlColumn>(new[] { mockColumn.Object }));
            var tableRef = new TestTableRef(mockTableDefinition.Object);

            // Act
            var columns = tableRef.Columns;

            // Assert
            Assert.NotNull(columns);
            Assert.Single(columns);
            Assert.Equal("Column1", columns[0].Name);
        }

        /// <summary>
        /// Tests that the Columns property returns a collection with multiple columns when TableDefinition has multiple columns.
        /// </summary>
        [Fact]
        public void Columns_MultipleColumnsInTableDefinition_ReturnsMultipleColumns()
        {
            // Arrange
            var mockColumn1 = new Mock<SqlColumn>();
            mockColumn1.SetupGet(c => c.Name).Returns("Column1");
            var mockColumn2 = new Mock<SqlColumn>();
            mockColumn2.SetupGet(c => c.Name).Returns("Column2");
            var mockColumn3 = new Mock<SqlColumn>();
            mockColumn3.SetupGet(c => c.Name).Returns("Column3");
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.SetupGet(t => t.Name).Returns("TestTable");
            mockTableDefinition.SetupGet(t => t.Columns).Returns(new ReadOnlyCollection<SqlColumn>(new[] { mockColumn1.Object, mockColumn2.Object, mockColumn3.Object }));
            var tableRef = new TestTableRef(mockTableDefinition.Object);

            // Act
            var columns = tableRef.Columns;

            // Assert
            Assert.NotNull(columns);
            Assert.Equal(3, columns.Count);
            Assert.Equal("Column1", columns[0].Name);
            Assert.Equal("Column2", columns[1].Name);
            Assert.Equal("Column3", columns[2].Name);
        }

        /// <summary>
        /// Tests that the Columns property returns a ReadOnlyCollection.
        /// </summary>
        [Fact]
        public void Columns_ReturnsReadOnlyCollection_IsReadOnly()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>();
            mockColumn.SetupGet(c => c.Name).Returns("Column1");
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.SetupGet(t => t.Name).Returns("TestTable");
            mockTableDefinition.SetupGet(t => t.Columns).Returns(new ReadOnlyCollection<SqlColumn>(new[] { mockColumn.Object }));
            var tableRef = new TestTableRef(mockTableDefinition.Object);

            // Act
            var columns = tableRef.Columns;

            // Assert
            Assert.IsType<ReadOnlyCollection<ColumnRef>>(columns);
        }

        /// <summary>
        /// Tests that each ColumnRef in the Columns collection has a reference to the parent TableRef.
        /// </summary>
        [Fact]
        public void Columns_ColumnRefsHaveTableReference_ReferencesParentTable()
        {
            // Arrange
            var mockColumn1 = new Mock<SqlColumn>();
            mockColumn1.SetupGet(c => c.Name).Returns("Column1");
            var mockColumn2 = new Mock<SqlColumn>();
            mockColumn2.SetupGet(c => c.Name).Returns("Column2");
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.SetupGet(t => t.Name).Returns("TestTable");
            mockTableDefinition.SetupGet(t => t.Columns).Returns(new ReadOnlyCollection<SqlColumn>(new[] { mockColumn1.Object, mockColumn2.Object }));
            var tableRef = new TestTableRef(mockTableDefinition.Object);

            // Act
            var columns = tableRef.Columns;

            // Assert
            Assert.All(columns, column => Assert.Same(tableRef, column.Table));
        }

        /// <summary>
        /// Tests that each ColumnRef in the Columns collection wraps the correct SqlColumn.
        /// </summary>
        [Fact]
        public void Columns_ColumnRefsWrapCorrectSqlColumns_MatchesOriginalColumns()
        {
            // Arrange
            var mockColumn1 = new Mock<SqlColumn>();
            mockColumn1.SetupGet(c => c.Name).Returns("Column1");
            var mockColumn2 = new Mock<SqlColumn>();
            mockColumn2.SetupGet(c => c.Name).Returns("Column2");
            var sqlColumns = new[] { mockColumn1.Object, mockColumn2.Object };
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.SetupGet(t => t.Name).Returns("TestTable");
            mockTableDefinition.SetupGet(t => t.Columns).Returns(new ReadOnlyCollection<SqlColumn>(sqlColumns));
            var tableRef = new TestTableRef(mockTableDefinition.Object);

            // Act
            var columns = tableRef.Columns;

            // Assert
            Assert.Equal(sqlColumns.Length, columns.Count);
            for (int i = 0; i < sqlColumns.Length; i++)
            {
                Assert.Same(sqlColumns[i], columns[i].Column);
            }
        }

        /// <summary>
        /// Concrete test implementation of the abstract TableRef class.
        /// </summary>
        private class TestTableRef : TableRef
        {
            public TestTableRef(TableDefinition table) : base(table)
            {
            }
        }

        /// <summary>
        /// Tests that NamedColumnCache initializes correctly when accessed with an empty Columns collection.
        /// The cache should be empty and no exception should be thrown.
        /// Expected: Returns an empty ConcurrentDictionary without error.
        /// </summary>
        [Fact]
        public void NamedColumnCache_WithEmptyColumns_ReturnsEmptyConcurrentDictionary()
        {
            // Arrange
            var mockTableDefinition = CreateMockTableDefinition(new List<ColumnDefinition>());
            var tableRef = new TestableTableRef(mockTableDefinition);

            // Act
            var result = tableRef.GetNamedColumnCache();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that NamedColumnCache correctly populates with a single column.
        /// The cache should contain one entry with the column's PropertyName as the key.
        /// Expected: Returns a ConcurrentDictionary with one entry keyed by the column's PropertyName.
        /// </summary>
        [Fact]
        public void NamedColumnCache_WithSingleColumn_PopulatesCacheCorrectly()
        {
            // Arrange
            var columnDefinition = CreateColumnDefinition("TestProperty");
            var columns = new List<ColumnDefinition> { columnDefinition };
            var mockTableDefinition = CreateMockTableDefinition(columns);
            var tableRef = new TestableTableRef(mockTableDefinition);

            // Act
            var result = tableRef.GetNamedColumnCache();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.True(result.ContainsKey("TestProperty"));
            Assert.Equal("TestProperty", result["TestProperty"].Column.PropertyName);
        }

        /// <summary>
        /// Tests that NamedColumnCache correctly populates with multiple columns.
        /// The cache should contain all columns keyed by their PropertyName values.
        /// Expected: Returns a ConcurrentDictionary with all columns properly indexed.
        /// </summary>
        [Fact]
        public void NamedColumnCache_WithMultipleColumns_PopulatesAllColumnsCorrectly()
        {
            // Arrange
            var column1 = CreateColumnDefinition("Column1");
            var column2 = CreateColumnDefinition("Column2");
            var column3 = CreateColumnDefinition("Column3");
            var columns = new List<ColumnDefinition> { column1, column2, column3 };
            var mockTableDefinition = CreateMockTableDefinition(columns);
            var tableRef = new TestableTableRef(mockTableDefinition);

            // Act
            var result = tableRef.GetNamedColumnCache();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.True(result.ContainsKey("Column1"));
            Assert.True(result.ContainsKey("Column2"));
            Assert.True(result.ContainsKey("Column3"));
            Assert.Equal("Column1", result["Column1"].Column.PropertyName);
            Assert.Equal("Column2", result["Column2"].Column.PropertyName);
            Assert.Equal("Column3", result["Column3"].Column.PropertyName);
        }

        /// <summary>
        /// Tests that NamedColumnCache returns the same instance on subsequent accesses.
        /// The cache should be populated only once and reused.
        /// Expected: Returns the same ConcurrentDictionary instance on multiple accesses.
        /// </summary>
        [Fact]
        public void NamedColumnCache_MultipleAccesses_ReturnsSameInstance()
        {
            // Arrange
            var columnDefinition = CreateColumnDefinition("TestProperty");
            var columns = new List<ColumnDefinition> { columnDefinition };
            var mockTableDefinition = CreateMockTableDefinition(columns);
            var tableRef = new TestableTableRef(mockTableDefinition);

            // Act
            var firstAccess = tableRef.GetNamedColumnCache();
            var secondAccess = tableRef.GetNamedColumnCache();

            // Assert
            Assert.Same(firstAccess, secondAccess);
        }

        /// <summary>
        /// Tests that NamedColumnCache does not re-populate on subsequent accesses.
        /// After the initial population, the Columns property should not be enumerated again.
        /// Expected: The cache is populated only once even when accessed multiple times.
        /// </summary>
        [Fact]
        public void NamedColumnCache_SubsequentAccesses_DoesNotRepopulateCache()
        {
            // Arrange
            var columnDefinition = CreateColumnDefinition("TestProperty");
            var columns = new List<ColumnDefinition> { columnDefinition };
            var mockTableDefinition = CreateMockTableDefinition(columns);
            var tableRef = new TestableTableRef(mockTableDefinition);

            // Act
            var firstAccess = tableRef.GetNamedColumnCache();
            var countAfterFirst = firstAccess.Count;

            // Manually add an entry to verify the cache is not cleared
            firstAccess["ManualEntry"] = tableRef.Columns[0];

            var secondAccess = tableRef.GetNamedColumnCache();
            var countAfterSecond = secondAccess.Count;

            // Assert
            Assert.Equal(2, countAfterSecond);
            Assert.True(secondAccess.ContainsKey("ManualEntry"));
        }

        /// <summary>
        /// Tests that NamedColumnCache handles columns with duplicate PropertyNames.
        /// When multiple columns share the same PropertyName, the last one should win.
        /// Expected: The cache contains the last column with the duplicate PropertyName.
        /// </summary>
        [Fact]
        public void NamedColumnCache_WithDuplicatePropertyNames_LastColumnWins()
        {
            // Arrange
            var column1 = CreateColumnDefinition("DuplicateName", "FirstColumn");
            var column2 = CreateColumnDefinition("DuplicateName", "SecondColumn");
            var columns = new List<ColumnDefinition> { column1, column2 };
            var mockTableDefinition = CreateMockTableDefinition(columns);
            var tableRef = new TestableTableRef(mockTableDefinition);

            // Act
            var result = tableRef.GetNamedColumnCache();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.True(result.ContainsKey("DuplicateName"));
            Assert.Equal("SecondColumn", result["DuplicateName"].Name);
        }

        /// <summary>
        /// Tests that NamedColumnCache handles columns with special characters in PropertyNames.
        /// PropertyNames with special characters should be stored as-is in the cache.
        /// Expected: The cache correctly stores and retrieves columns with special characters in PropertyNames.
        /// </summary>
        [Theory]
        [InlineData("Property_With_Underscores")]
        [InlineData("Property123")]
        [InlineData("PropertyWithÜnicode")]
        [InlineData("Property$pecial")]
        public void NamedColumnCache_WithSpecialCharactersInPropertyName_StoresCorrectly(string propertyName)
        {
            // Arrange
            var columnDefinition = CreateColumnDefinition(propertyName);
            var columns = new List<ColumnDefinition> { columnDefinition };
            var mockTableDefinition = CreateMockTableDefinition(columns);
            var tableRef = new TestableTableRef(mockTableDefinition);

            // Act
            var result = tableRef.GetNamedColumnCache();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.True(result.ContainsKey(propertyName));
            Assert.Equal(propertyName, result[propertyName].Column.PropertyName);
        }

        /// <summary>
        /// Tests that NamedColumnCache works correctly with a large number of columns.
        /// The cache should handle many columns efficiently.
        /// Expected: Returns a ConcurrentDictionary with all columns properly indexed.
        /// </summary>
        [Fact]
        public void NamedColumnCache_WithManyColumns_PopulatesAllCorrectly()
        {
            // Arrange
            var columns = new List<ColumnDefinition>();
            for (int i = 0; i < 100; i++)
            {
                columns.Add(CreateColumnDefinition($"Column{i}"));
            }
            var mockTableDefinition = CreateMockTableDefinition(columns);
            var tableRef = new TestableTableRef(mockTableDefinition);

            // Act
            var result = tableRef.GetNamedColumnCache();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(100, result.Count);
            for (int i = 0; i < 100; i++)
            {
                Assert.True(result.ContainsKey($"Column{i}"));
                Assert.Equal($"Column{i}", result[$"Column{i}"].Column.PropertyName);
            }
        }

        /// <summary>
        /// Helper method to create a mock TableDefinition with specified columns.
        /// </summary>
        private TableDefinition CreateMockTableDefinition(List<ColumnDefinition> columns)
        {
            var mockObjectType = typeof(TestEntity);
            var tableDefinition = (TableDefinition)Activator.CreateInstance(
                typeof(TableDefinition),
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new object[] { mockObjectType, columns },
                null);

            return tableDefinition;
        }

        /// <summary>
        /// Helper method to create a ColumnDefinition with specified property name.
        /// </summary>
        private ColumnDefinition CreateColumnDefinition(string propertyName, string columnName = null)
        {
            var property = typeof(TestEntity).GetProperty(nameof(TestEntity.DynamicProperty));

            var columnDefinition = (ColumnDefinition)Activator.CreateInstance(
                typeof(ColumnDefinition),
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new object[] { property },
                null);

            // Override the PropertyName using reflection
            var propertyNameField = typeof(SqlColumn).GetProperty("PropertyName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            propertyNameField?.SetValue(columnDefinition, propertyName);

            if (columnName != null)
            {
                var nameField = typeof(SqlObject).GetProperty("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                nameField?.SetValue(columnDefinition, columnName);
            }

            return columnDefinition;
        }

        /// <summary>
        /// Test entity class used for creating column definitions.
        /// </summary>
        private class TestEntity
        {
            public string DynamicProperty { get; set; }
        }

        /// <summary>
        /// Testable implementation of the abstract TableRef class for testing protected members.
        /// </summary>
        private class TestableTableRef : TableRef
        {
            public TestableTableRef(TableDefinition table) : base(table)
            {
            }

            public ConcurrentDictionary<string, ColumnRef> GetNamedColumnCache()
            {
                return NamedColumnCache;
            }
        }

        /// <summary>
        /// Tests that the constructor properly initializes the TableRef with a valid TableDefinition
        /// containing multiple columns.
        /// Expected: TableRef is created successfully with Name and Columns properly set.
        /// </summary>
        [Fact]
        public void TableRef_WithValidTableDefinitionAndMultipleColumns_InitializesSuccessfully()
        {
            // Arrange
            var mockTableDef = new Mock<TableDefinition>(MockBehavior.Strict, typeof(string), new List<ColumnDefinition>());
            var mockColumn1 = new Mock<ColumnDefinition>();
            var mockColumn2 = new Mock<ColumnDefinition>();
            mockColumn1.SetupGet(c => c.Name).Returns("Column1");
            mockColumn2.SetupGet(c => c.Name).Returns("Column2");

            var columns = new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition> { mockColumn1.Object, mockColumn2.Object });
            mockTableDef.SetupGet(t => t.Name).Returns("TestTable");
            mockTableDef.SetupGet(t => t.Columns).Returns(columns);

            // Act
            var tableRef = new TestableTableRef(mockTableDef.Object);

            // Assert
            Assert.NotNull(tableRef);
            Assert.Equal("TestTable", tableRef.Name);
            Assert.NotNull(tableRef.Columns);
            Assert.Equal(2, tableRef.Columns.Count);
        }

        /// <summary>
        /// Tests that the constructor properly initializes the TableRef with a valid TableDefinition
        /// containing a single column.
        /// Expected: TableRef is created successfully with one column in the Columns collection.
        /// </summary>
        [Fact]
        public void TableRef_WithValidTableDefinitionAndSingleColumn_InitializesSuccessfully()
        {
            // Arrange
            var mockTableDef = new Mock<TableDefinition>(MockBehavior.Strict, typeof(string), new List<ColumnDefinition>());
            var mockColumn = new Mock<ColumnDefinition>();
            mockColumn.SetupGet(c => c.Name).Returns("SingleColumn");

            var columns = new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition> { mockColumn.Object });
            mockTableDef.SetupGet(t => t.Name).Returns("SingleColumnTable");
            mockTableDef.SetupGet(t => t.Columns).Returns(columns);

            // Act
            var tableRef = new TestableTableRef(mockTableDef.Object);

            // Assert
            Assert.NotNull(tableRef);
            Assert.Equal("SingleColumnTable", tableRef.Name);
            Assert.NotNull(tableRef.Columns);
            Assert.Single(tableRef.Columns);
        }

        /// <summary>
        /// Tests that the constructor properly initializes the TableRef with a valid TableDefinition
        /// containing an empty columns collection.
        /// Expected: TableRef is created successfully with an empty Columns collection.
        /// </summary>
        [Fact]
        public void TableRef_WithValidTableDefinitionAndEmptyColumns_InitializesSuccessfully()
        {
            // Arrange
            var mockTableDef = new Mock<TableDefinition>(MockBehavior.Strict, typeof(string), new List<ColumnDefinition>());
            var columns = new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>());
            mockTableDef.SetupGet(t => t.Name).Returns("EmptyTable");
            mockTableDef.SetupGet(t => t.Columns).Returns(columns);

            // Act
            var tableRef = new TestableTableRef(mockTableDef.Object);

            // Assert
            Assert.NotNull(tableRef);
            Assert.Equal("EmptyTable", tableRef.Name);
            Assert.NotNull(tableRef.Columns);
            Assert.Empty(tableRef.Columns);
        }

        /// <summary>
        /// Tests that the constructor throws NullReferenceException when passed a null TableDefinition.
        /// Expected: NullReferenceException is thrown.
        /// </summary>
        [Fact]
        public void TableRef_WithNullTableDefinition_ThrowsNullReferenceException()
        {
            // Arrange
            TableDefinition? nullTableDef = null;

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => new TestableTableRef(nullTableDef!));
        }

        /// <summary>
        /// Tests that the constructor throws NullReferenceException when TableDefinition.Columns is null.
        /// Expected: NullReferenceException is thrown during column enumeration.
        /// </summary>
        [Fact]
        public void TableRef_WithNullColumnsInTableDefinition_ThrowsNullReferenceException()
        {
            // Arrange
            var mockTableDef = new Mock<TableDefinition>(MockBehavior.Strict, typeof(string), new List<ColumnDefinition>());
            mockTableDef.SetupGet(t => t.Name).Returns("TableWithNullColumns");
            mockTableDef.SetupGet(t => t.Columns).Returns((ReadOnlyCollection<ColumnDefinition>)null!);

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => new TestableTableRef(mockTableDef.Object));
        }

        /// <summary>
        /// Tests that the constructor properly initializes TableRef when TableDefinition.Name is null.
        /// Expected: TableRef is created with null Name property.
        /// </summary>
        [Fact]
        public void TableRef_WithNullNameInTableDefinition_InitializesWithNullName()
        {
            // Arrange
            var mockTableDef = new Mock<TableDefinition>(MockBehavior.Strict, typeof(string), new List<ColumnDefinition>());
            var columns = new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>());
            mockTableDef.SetupGet(t => t.Name).Returns((string)null!);
            mockTableDef.SetupGet(t => t.Columns).Returns(columns);

            // Act
            var tableRef = new TestableTableRef(mockTableDef.Object);

            // Assert
            Assert.NotNull(tableRef);
            Assert.Null(tableRef.Name);
            Assert.NotNull(tableRef.Columns);
        }

        /// <summary>
        /// Tests that the constructor properly initializes TableRef when TableDefinition.Name is an empty string.
        /// Expected: TableRef is created with empty string Name property.
        /// </summary>
        [Fact]
        public void TableRef_WithEmptyNameInTableDefinition_InitializesWithEmptyName()
        {
            // Arrange
            var mockTableDef = new Mock<TableDefinition>(MockBehavior.Strict, typeof(string), new List<ColumnDefinition>());
            var columns = new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>());
            mockTableDef.SetupGet(t => t.Name).Returns(string.Empty);
            mockTableDef.SetupGet(t => t.Columns).Returns(columns);

            // Act
            var tableRef = new TestableTableRef(mockTableDef.Object);

            // Assert
            Assert.NotNull(tableRef);
            Assert.Equal(string.Empty, tableRef.Name);
            Assert.NotNull(tableRef.Columns);
        }

        /// <summary>
        /// Tests that the constructor properly initializes TableRef when TableDefinition.Name contains whitespace.
        /// Expected: TableRef is created with whitespace Name property.
        /// </summary>
        [Fact]
        public void TableRef_WithWhitespaceNameInTableDefinition_InitializesWithWhitespaceName()
        {
            // Arrange
            var mockTableDef = new Mock<TableDefinition>(MockBehavior.Strict, typeof(string), new List<ColumnDefinition>());
            var columns = new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>());
            mockTableDef.SetupGet(t => t.Name).Returns("   ");
            mockTableDef.SetupGet(t => t.Columns).Returns(columns);

            // Act
            var tableRef = new TestableTableRef(mockTableDef.Object);

            // Assert
            Assert.NotNull(tableRef);
            Assert.Equal("   ", tableRef.Name);
            Assert.NotNull(tableRef.Columns);
        }

        /// <summary>
        /// Tests that the constructor properly initializes TableRef when TableDefinition.Name contains special characters.
        /// Expected: TableRef is created with special characters in Name property.
        /// </summary>
        [Fact]
        public void TableRef_WithSpecialCharactersInName_InitializesSuccessfully()
        {
            // Arrange
            var mockTableDef = new Mock<TableDefinition>(MockBehavior.Strict, typeof(string), new List<ColumnDefinition>());
            var columns = new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>());
            mockTableDef.SetupGet(t => t.Name).Returns("Table@#$%^&*()");
            mockTableDef.SetupGet(t => t.Columns).Returns(columns);

            // Act
            var tableRef = new TestableTableRef(mockTableDef.Object);

            // Assert
            Assert.NotNull(tableRef);
            Assert.Equal("Table@#$%^&*()", tableRef.Name);
            Assert.NotNull(tableRef.Columns);
        }

        /// <summary>
        /// Tests that the constructor properly initializes TableRef when TableDefinition.Name is very long.
        /// Expected: TableRef is created with the long Name property.
        /// </summary>
        [Fact]
        public void TableRef_WithVeryLongName_InitializesSuccessfully()
        {
            // Arrange
            var longName = new string('A', 10000);
            var mockTableDef = new Mock<TableDefinition>(MockBehavior.Strict, typeof(string), new List<ColumnDefinition>());
            var columns = new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>());
            mockTableDef.SetupGet(t => t.Name).Returns(longName);
            mockTableDef.SetupGet(t => t.Columns).Returns(columns);

            // Act
            var tableRef = new TestableTableRef(mockTableDef.Object);

            // Assert
            Assert.NotNull(tableRef);
            Assert.Equal(longName, tableRef.Name);
            Assert.NotNull(tableRef.Columns);
        }

        /// <summary>
        /// Tests that the constructor properly stores the TableDefinition reference.
        /// Expected: The TableDefinition property returns the same instance passed to the constructor.
        /// </summary>
        [Fact]
        public void TableRef_WithValidTableDefinition_StoresTableDefinitionReference()
        {
            // Arrange
            var mockTableDef = new Mock<TableDefinition>(MockBehavior.Strict, typeof(string), new List<ColumnDefinition>());
            var columns = new ReadOnlyCollection<ColumnDefinition>(new List<ColumnDefinition>());
            mockTableDef.SetupGet(t => t.Name).Returns("TestTable");
            mockTableDef.SetupGet(t => t.Columns).Returns(columns);

            // Act
            var tableRef = new TestableTableRef(mockTableDef.Object);

            // Assert
            Assert.Same(mockTableDef.Object, tableRef.TableDefinition);
        }

        /// <summary>
        /// Helper class to expose the abstract TableRef for testing purposes.
        /// </summary>
        private class TestableTableRef : TableRef
        {
            public TestableTableRef(TableDefinition table) : base(table)
            {
            }
        }

        /// <summary>
        /// Tests that the TableDefinition property returns the same instance that was passed to the constructor.
        /// </summary>
        [Fact]
        public void TableDefinition_WhenAccessed_ReturnsSameInstancePassedToConstructor()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>(MockBehavior.Strict, typeof(string), new List<ColumnDefinition>());
            var tableRef = new TestableTableRef(mockTableDefinition.Object);

            // Act
            var result = tableRef.TableDefinition;

            // Assert
            Assert.Same(mockTableDefinition.Object, result);
        }

        /// <summary>
        /// Tests that the TableDefinition property returns the same instance on multiple accesses (immutability check).
        /// </summary>
        [Fact]
        public void TableDefinition_WhenAccessedMultipleTimes_ReturnsConsistentValue()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>(MockBehavior.Strict, typeof(int), new List<ColumnDefinition>());
            var tableRef = new TestableTableRef(mockTableDefinition.Object);

            // Act
            var firstAccess = tableRef.TableDefinition;
            var secondAccess = tableRef.TableDefinition;
            var thirdAccess = tableRef.TableDefinition;

            // Assert
            Assert.Same(firstAccess, secondAccess);
            Assert.Same(secondAccess, thirdAccess);
            Assert.Same(mockTableDefinition.Object, firstAccess);
        }

        /// <summary>
        /// Minimal concrete implementation of TableRef for testing purposes.
        /// </summary>
        private class TestableTableRef : TableRef
        {
            public TestableTableRef(TableDefinition table) : base(table)
            {
            }
        }
    }
}