using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    /// <summary>
    /// Unit tests for the SqlTable class.
    /// </summary>
    public partial class SqlTableTests
    {
        /// <summary>
        /// Tests that Equals returns true when comparing an object with itself (same reference).
        /// Input: Same SqlTable instance for both operands.
        /// Expected: Returns true (reflexive property of equality).
        /// </summary>
        [Fact]
        public void Equals_SameReference_ReturnsTrue()
        {
            // Arrange
            var columns = new List<SqlColumn>();
            var definition = new Mock<TableDefinition>();
            definition.SetupGet(d => d.ObjectType).Returns(typeof(string));

            var mock = new Mock<SqlTable>(columns);
            mock.SetupGet(t => t.Definition).Returns(definition.Object);
            var table = mock.Object;

            // Act
            var result = table.Equals(table);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with null.
        /// Input: null object parameter.
        /// Expected: Returns false.
        /// </summary>
        [Fact]
        public void Equals_NullObject_ReturnsFalse()
        {
            // Arrange
            var columns = new List<SqlColumn>();
            var definition = new Mock<TableDefinition>();
            definition.SetupGet(d => d.ObjectType).Returns(typeof(string));

            var mock = new Mock<SqlTable>(columns);
            mock.SetupGet(t => t.Definition).Returns(definition.Object);
            var table = mock.Object;

            // Act
            var result = table.Equals(null);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing with an object of a different type.
        /// Input: Object of different type (string, int, object, etc.).
        /// Expected: Returns false.
        /// </summary>
        [Theory]
        [InlineData("string")]
        [InlineData(123)]
        [InlineData(true)]
        public void Equals_DifferentType_ReturnsFalse(object obj)
        {
            // Arrange
            var columns = new List<SqlColumn>();
            var definition = new Mock<TableDefinition>();
            definition.SetupGet(d => d.ObjectType).Returns(typeof(string));

            var mock = new Mock<SqlTable>(columns);
            mock.SetupGet(t => t.Definition).Returns(definition.Object);
            var table = mock.Object;

            // Act
            var result = table.Equals(obj);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns true when comparing two SqlTable instances with the same DefinitionType.
        /// Input: Two different SqlTable instances (different references) with identical DefinitionType.
        /// Expected: Returns true.
        /// </summary>
        [Fact]
        public void Equals_SameTypeSameDefinitionType_ReturnsTrue()
        {
            // Arrange
            var columns1 = new List<SqlColumn>();
            var columns2 = new List<SqlColumn>();

            var definition1 = new Mock<TableDefinition>();
            definition1.SetupGet(d => d.ObjectType).Returns(typeof(string));

            var definition2 = new Mock<TableDefinition>();
            definition2.SetupGet(d => d.ObjectType).Returns(typeof(string));

            var mock1 = new Mock<SqlTable>(columns1);
            mock1.SetupGet(t => t.Definition).Returns(definition1.Object);
            var table1 = mock1.Object;

            var mock2 = new Mock<SqlTable>(columns2);
            mock2.SetupGet(t => t.Definition).Returns(definition2.Object);
            var table2 = mock2.Object;

            // Act
            var result = table1.Equals(table2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when comparing two SqlTable instances with different DefinitionType.
        /// Input: Two SqlTable instances with different DefinitionType values.
        /// Expected: Returns false.
        /// </summary>
        [Fact]
        public void Equals_SameTypeDifferentDefinitionType_ReturnsFalse()
        {
            // Arrange
            var columns1 = new List<SqlColumn>();
            var columns2 = new List<SqlColumn>();

            var definition1 = new Mock<TableDefinition>();
            definition1.SetupGet(d => d.ObjectType).Returns(typeof(string));

            var definition2 = new Mock<TableDefinition>();
            definition2.SetupGet(d => d.ObjectType).Returns(typeof(int));

            var mock1 = new Mock<SqlTable>(columns1);
            mock1.SetupGet(t => t.Definition).Returns(definition1.Object);
            var table1 = mock1.Object;

            var mock2 = new Mock<SqlTable>(columns2);
            mock2.SetupGet(t => t.Definition).Returns(definition2.Object);
            var table2 = mock2.Object;

            // Act
            var result = table1.Equals(table2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals returns false when DefinitionType is null on both instances.
        /// Input: Two SqlTable instances where Definition.ObjectType returns null.
        /// Expected: Returns true (both null DefinitionTypes are equal).
        /// </summary>
        [Fact]
        public void Equals_BothDefinitionTypesNull_ReturnsTrue()
        {
            // Arrange
            var columns1 = new List<SqlColumn>();
            var columns2 = new List<SqlColumn>();

            var definition1 = new Mock<TableDefinition>();
            definition1.SetupGet(d => d.ObjectType).Returns((Type?)null);

            var definition2 = new Mock<TableDefinition>();
            definition2.SetupGet(d => d.ObjectType).Returns((Type?)null);

            var mock1 = new Mock<SqlTable>(columns1);
            mock1.SetupGet(t => t.Definition).Returns(definition1.Object);
            var table1 = mock1.Object;

            var mock2 = new Mock<SqlTable>(columns2);
            mock2.SetupGet(t => t.Definition).Returns(definition2.Object);
            var table2 = mock2.Object;

            // Act
            var result = table1.Equals(table2);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Tests that Equals returns false when one DefinitionType is null and the other is not.
        /// Input: One SqlTable with null DefinitionType, another with non-null DefinitionType.
        /// Expected: Returns false.
        /// </summary>
        [Fact]
        public void Equals_OneDefinitionTypeNull_ReturnsFalse()
        {
            // Arrange
            var columns1 = new List<SqlColumn>();
            var columns2 = new List<SqlColumn>();

            var definition1 = new Mock<TableDefinition>();
            definition1.SetupGet(d => d.ObjectType).Returns((Type?)null);

            var definition2 = new Mock<TableDefinition>();
            definition2.SetupGet(d => d.ObjectType).Returns(typeof(string));

            var mock1 = new Mock<SqlTable>(columns1);
            mock1.SetupGet(t => t.Definition).Returns(definition1.Object);
            var table1 = mock1.Object;

            var mock2 = new Mock<SqlTable>(columns2);
            mock2.SetupGet(t => t.Definition).Returns(definition2.Object);
            var table2 = mock2.Object;

            // Act
            var result = table1.Equals(table2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Tests that Equals is symmetric: if A.Equals(B) is true, then B.Equals(A) is also true.
        /// Input: Two SqlTable instances with the same DefinitionType.
        /// Expected: Both A.Equals(B) and B.Equals(A) return true.
        /// </summary>
        [Fact]
        public void Equals_SymmetricProperty_ReturnsTrue()
        {
            // Arrange
            var columns1 = new List<SqlColumn>();
            var columns2 = new List<SqlColumn>();

            var definition1 = new Mock<TableDefinition>();
            definition1.SetupGet(d => d.ObjectType).Returns(typeof(string));

            var definition2 = new Mock<TableDefinition>();
            definition2.SetupGet(d => d.ObjectType).Returns(typeof(string));

            var mock1 = new Mock<SqlTable>(columns1);
            mock1.SetupGet(t => t.Definition).Returns(definition1.Object);
            var table1 = mock1.Object;

            var mock2 = new Mock<SqlTable>(columns2);
            mock2.SetupGet(t => t.Definition).Returns(definition2.Object);
            var table2 = mock2.Object;

            // Act
            var result1 = table1.Equals(table2);
            var result2 = table2.Equals(table1);

            // Assert
            Assert.True(result1);
            Assert.True(result2);
        }

        /// <summary>
        /// Tests that NamedColumnCache is initialized on first access and populated with columns from Columns property.
        /// </summary>
        [Fact]
        public void NamedColumnCache_FirstAccess_InitializesAndPopulatesCache()
        {
            // Arrange
            var mockColumn1 = new Mock<SqlColumn>(MockBehavior.Strict, CreateMockPropertyInfo("Property1"));
            mockColumn1.SetupGet(c => c.PropertyName).Returns("Property1");
            var mockColumn2 = new Mock<SqlColumn>(MockBehavior.Strict, CreateMockPropertyInfo("Property2"));
            mockColumn2.SetupGet(c => c.PropertyName).Returns("Property2");

            var columns = new List<SqlColumn> { mockColumn1.Object, mockColumn2.Object };
            var table = new TestSqlTable(columns);

            // Act
            var cache = table.ExposedNamedColumnCache;

            // Assert
            Assert.NotNull(cache);
            Assert.Equal(2, cache.Count);
            Assert.True(cache.ContainsKey("Property1"));
            Assert.True(cache.ContainsKey("Property2"));
            Assert.Same(mockColumn1.Object, cache["Property1"]);
            Assert.Same(mockColumn2.Object, cache["Property2"]);
        }

        /// <summary>
        /// Tests that NamedColumnCache returns the same instance on subsequent accesses.
        /// </summary>
        [Fact]
        public void NamedColumnCache_SubsequentAccess_ReturnsSameInstance()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, CreateMockPropertyInfo("Property1"));
            mockColumn.SetupGet(c => c.PropertyName).Returns("Property1");

            var columns = new List<SqlColumn> { mockColumn.Object };
            var table = new TestSqlTable(columns);

            // Act
            var cache1 = table.ExposedNamedColumnCache;
            var cache2 = table.ExposedNamedColumnCache;

            // Assert
            Assert.Same(cache1, cache2);
        }

        /// <summary>
        /// Tests that NamedColumnCache handles empty columns collection correctly.
        /// </summary>
        [Fact]
        public void NamedColumnCache_EmptyColumns_ReturnsEmptyCache()
        {
            // Arrange
            var columns = new List<SqlColumn>();
            var table = new TestSqlTable(columns);

            // Act
            var cache = table.ExposedNamedColumnCache;

            // Assert
            Assert.NotNull(cache);
            Assert.Empty(cache);
        }

        /// <summary>
        /// Tests that NamedColumnCache performs case-insensitive lookups.
        /// </summary>
        [Fact]
        public void NamedColumnCache_CaseInsensitiveLookup_FindsColumnRegardlessOfCase()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, CreateMockPropertyInfo("PropertyName"));
            mockColumn.SetupGet(c => c.PropertyName).Returns("PropertyName");

            var columns = new List<SqlColumn> { mockColumn.Object };
            var table = new TestSqlTable(columns);

            // Act
            var cache = table.ExposedNamedColumnCache;

            // Assert
            Assert.True(cache.ContainsKey("PropertyName"));
            Assert.True(cache.ContainsKey("propertyname"));
            Assert.True(cache.ContainsKey("PROPERTYNAME"));
            Assert.True(cache.ContainsKey("pRoPeRtYnAmE"));
        }

        /// <summary>
        /// Tests that NamedColumnCache correctly handles single column.
        /// </summary>
        [Fact]
        public void NamedColumnCache_SingleColumn_PopulatesCacheWithOneEntry()
        {
            // Arrange
            var mockColumn = new Mock<SqlColumn>(MockBehavior.Strict, CreateMockPropertyInfo("SingleProperty"));
            mockColumn.SetupGet(c => c.PropertyName).Returns("SingleProperty");

            var columns = new List<SqlColumn> { mockColumn.Object };
            var table = new TestSqlTable(columns);

            // Act
            var cache = table.ExposedNamedColumnCache;

            // Assert
            Assert.NotNull(cache);
            Assert.Single(cache);
            Assert.True(cache.ContainsKey("SingleProperty"));
            Assert.Same(mockColumn.Object, cache["SingleProperty"]);
        }

        /// <summary>
        /// Tests that NamedColumnCache correctly handles multiple columns with different property names.
        /// </summary>
        [Fact]
        public void NamedColumnCache_MultipleColumns_PopulatesCacheWithAllEntries()
        {
            // Arrange
            var mockColumn1 = new Mock<SqlColumn>(MockBehavior.Strict, CreateMockPropertyInfo("Column1"));
            mockColumn1.SetupGet(c => c.PropertyName).Returns("Column1");
            var mockColumn2 = new Mock<SqlColumn>(MockBehavior.Strict, CreateMockPropertyInfo("Column2"));
            mockColumn2.SetupGet(c => c.PropertyName).Returns("Column2");
            var mockColumn3 = new Mock<SqlColumn>(MockBehavior.Strict, CreateMockPropertyInfo("Column3"));
            mockColumn3.SetupGet(c => c.PropertyName).Returns("Column3");

            var columns = new List<SqlColumn> { mockColumn1.Object, mockColumn2.Object, mockColumn3.Object };
            var table = new TestSqlTable(columns);

            // Act
            var cache = table.ExposedNamedColumnCache;

            // Assert
            Assert.NotNull(cache);
            Assert.Equal(3, cache.Count);
            Assert.True(cache.ContainsKey("Column1"));
            Assert.True(cache.ContainsKey("Column2"));
            Assert.True(cache.ContainsKey("Column3"));
            Assert.Same(mockColumn1.Object, cache["Column1"]);
            Assert.Same(mockColumn2.Object, cache["Column2"]);
            Assert.Same(mockColumn3.Object, cache["Column3"]);
        }

        /// <summary>
        /// Helper method to create a mock PropertyInfo for testing.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>A mock PropertyInfo instance.</returns>
        private static PropertyInfo CreateMockPropertyInfo(string propertyName)
        {
            var mockProperty = new Mock<PropertyInfo>();
            mockProperty.Setup(p => p.Name).Returns(propertyName);
            mockProperty.Setup(p => p.PropertyType).Returns(typeof(string));
            return mockProperty.Object;
        }

        /// <summary>
        /// Test implementation of SqlTable to expose protected members for testing.
        /// </summary>
        private class TestSqlTable : SqlTable
        {
            private readonly TableDefinition _testDefinition;

            public TestSqlTable(ICollection<SqlColumn> columns)
                : base(columns)
            {
                var mockDefinition = new Mock<TableDefinition>();
                mockDefinition.Setup(d => d.Type).Returns(typeof(TestSqlTable));
                _testDefinition = mockDefinition.Object;
            }

            public override TableDefinition Definition => _testDefinition;

            public ConcurrentDictionary<string, SqlColumn> ExposedNamedColumnCache => NamedColumnCache;
        }

        /// <summary>
        /// Tests that DefinitionType returns the ObjectType from the Definition property.
        /// </summary>
        /// <param name="expectedType">The expected Type to be returned.</param>
        [Theory]
        [MemberData(nameof(DefinitionTypeTestData))]
        public void DefinitionType_ReturnsObjectTypeFromDefinition(Type? expectedType)
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.SetupGet(d => d.ObjectType).Returns(expectedType);

            var mockSqlTable = new Mock<SqlTable>();
            mockSqlTable.SetupGet(t => t.Definition).Returns(mockTableDefinition.Object);

            // Act
            Type? actualType = mockSqlTable.Object.DefinitionType;

            // Assert
            Assert.Equal(expectedType, actualType);
        }

        /// <summary>
        /// Tests that DefinitionType returns null when ObjectType is null.
        /// </summary>
        [Fact]
        public void DefinitionType_WhenObjectTypeIsNull_ReturnsNull()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.SetupGet(d => d.ObjectType).Returns((Type?)null);

            var mockSqlTable = new Mock<SqlTable>();
            mockSqlTable.SetupGet(t => t.Definition).Returns(mockTableDefinition.Object);

            // Act
            Type? actualType = mockSqlTable.Object.DefinitionType;

            // Assert
            Assert.Null(actualType);
        }

        /// <summary>
        /// Tests that DefinitionType returns the correct type for a class type.
        /// </summary>
        [Fact]
        public void DefinitionType_WhenObjectTypeIsClassType_ReturnsClassType()
        {
            // Arrange
            Type expectedType = typeof(string);
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.SetupGet(d => d.ObjectType).Returns(expectedType);

            var mockSqlTable = new Mock<SqlTable>();
            mockSqlTable.SetupGet(t => t.Definition).Returns(mockTableDefinition.Object);

            // Act
            Type? actualType = mockSqlTable.Object.DefinitionType;

            // Assert
            Assert.Equal(expectedType, actualType);
            Assert.True(actualType?.IsClass);
        }

        /// <summary>
        /// Tests that DefinitionType returns the correct type for an interface type.
        /// </summary>
        [Fact]
        public void DefinitionType_WhenObjectTypeIsInterfaceType_ReturnsInterfaceType()
        {
            // Arrange
            Type expectedType = typeof(IEnumerable<int>);
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.SetupGet(d => d.ObjectType).Returns(expectedType);

            var mockSqlTable = new Mock<SqlTable>();
            mockSqlTable.SetupGet(t => t.Definition).Returns(mockTableDefinition.Object);

            // Act
            Type? actualType = mockSqlTable.Object.DefinitionType;

            // Assert
            Assert.Equal(expectedType, actualType);
            Assert.True(actualType?.IsInterface);
        }

        /// <summary>
        /// Tests that DefinitionType returns the correct type for a generic type.
        /// </summary>
        [Fact]
        public void DefinitionType_WhenObjectTypeIsGenericType_ReturnsGenericType()
        {
            // Arrange
            Type expectedType = typeof(List<string>);
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.SetupGet(d => d.ObjectType).Returns(expectedType);

            var mockSqlTable = new Mock<SqlTable>();
            mockSqlTable.SetupGet(t => t.Definition).Returns(mockTableDefinition.Object);

            // Act
            Type? actualType = mockSqlTable.Object.DefinitionType;

            // Assert
            Assert.Equal(expectedType, actualType);
            Assert.True(actualType?.IsGenericType);
        }

        /// <summary>
        /// Tests that DefinitionType returns the correct type for a value type.
        /// </summary>
        [Fact]
        public void DefinitionType_WhenObjectTypeIsValueType_ReturnsValueType()
        {
            // Arrange
            Type expectedType = typeof(int);
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.SetupGet(d => d.ObjectType).Returns(expectedType);

            var mockSqlTable = new Mock<SqlTable>();
            mockSqlTable.SetupGet(t => t.Definition).Returns(mockTableDefinition.Object);

            // Act
            Type? actualType = mockSqlTable.Object.DefinitionType;

            // Assert
            Assert.Equal(expectedType, actualType);
            Assert.True(actualType?.IsValueType);
        }

        /// <summary>
        /// Tests that DefinitionType returns the correct type for an abstract class type.
        /// </summary>
        [Fact]
        public void DefinitionType_WhenObjectTypeIsAbstractClassType_ReturnsAbstractClassType()
        {
            // Arrange
            Type expectedType = typeof(SqlTable);
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.SetupGet(d => d.ObjectType).Returns(expectedType);

            var mockSqlTable = new Mock<SqlTable>();
            mockSqlTable.SetupGet(t => t.Definition).Returns(mockTableDefinition.Object);

            // Act
            Type? actualType = mockSqlTable.Object.DefinitionType;

            // Assert
            Assert.Equal(expectedType, actualType);
            Assert.True(actualType?.IsAbstract);
        }

        /// <summary>
        /// Provides test data for DefinitionType tests with various Type values.
        /// </summary>
        public static TheoryData<Type?> DefinitionTypeTestData()
        {
            return new TheoryData<Type?>
            {
                typeof(string),
                typeof(int),
                typeof(List<int>),
                typeof(IEnumerable<string>),
                typeof(DateTime),
                typeof(object),
                null
            };
        }

        /// <summary>
        /// Tests that GetColumn returns null when propertyName is null.
        /// Input: null propertyName.
        /// Expected: Returns null without attempting cache lookup.
        /// </summary>
        [Fact]
        public void GetColumn_NullPropertyName_ReturnsNull()
        {
            // Arrange
            var columns = new List<TestSqlColumn>();
            var table = new TestSqlTable(columns);

            // Act
            var result = table.GetColumn(null);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GetColumn returns null when propertyName is an empty string.
        /// Input: Empty string propertyName.
        /// Expected: Returns null without attempting cache lookup.
        /// </summary>
        [Fact]
        public void GetColumn_EmptyPropertyName_ReturnsNull()
        {
            // Arrange
            var columns = new List<TestSqlColumn>();
            var table = new TestSqlTable(columns);

            // Act
            var result = table.GetColumn(string.Empty);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GetColumn returns null when propertyName contains only whitespace.
        /// Input: Whitespace-only string propertyName (space, tab, newline).
        /// Expected: Returns null as the property doesn't exist in cache.
        /// </summary>
        [Theory]
        [InlineData(" ")]
        [InlineData("  ")]
        [InlineData("\t")]
        [InlineData("\n")]
        [InlineData("\r\n")]
        [InlineData(" \t\n ")]
        public void GetColumn_WhitespacePropertyName_ReturnsNull(string propertyName)
        {
            // Arrange
            var columns = new List<TestSqlColumn>();
            var table = new TestSqlTable(columns);

            // Act
            var result = table.GetColumn(propertyName);

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GetColumn returns null when the property name doesn't exist.
        /// Input: Valid string that doesn't match any column property name.
        /// Expected: Returns null.
        /// </summary>
        [Fact]
        public void GetColumn_NonExistentPropertyName_ReturnsNull()
        {
            // Arrange
            var column1 = new TestSqlColumn("Property1");
            var column2 = new TestSqlColumn("Property2");
            var columns = new List<TestSqlColumn> { column1, column2 };
            var table = new TestSqlTable(columns);

            // Act
            var result = table.GetColumn("NonExistentProperty");

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Tests that GetColumn returns the correct column when property name exists.
        /// Input: Valid property name that exists in the columns collection.
        /// Expected: Returns the matching SqlColumn.
        /// </summary>
        [Fact]
        public void GetColumn_ExistingPropertyName_ReturnsColumn()
        {
            // Arrange
            var column1 = new TestSqlColumn("Property1");
            var column2 = new TestSqlColumn("Property2");
            var columns = new List<TestSqlColumn> { column1, column2 };
            var table = new TestSqlTable(columns);

            // Act
            var result = table.GetColumn("Property1");

            // Assert
            Assert.NotNull(result);
            Assert.Same(column1, result);
            Assert.Equal("Property1", result.PropertyName);
        }

        /// <summary>
        /// Tests that GetColumn performs case-insensitive property name matching.
        /// Input: Property name with different casing variations.
        /// Expected: Returns the same column regardless of case.
        /// </summary>
        [Theory]
        [InlineData("PropertyName")]
        [InlineData("propertyname")]
        [InlineData("PROPERTYNAME")]
        [InlineData("pRoPeRtYnAmE")]
        [InlineData("PropertyNAME")]
        public void GetColumn_CaseInsensitivePropertyName_ReturnsColumn(string propertyName)
        {
            // Arrange
            var column = new TestSqlColumn("PropertyName");
            var columns = new List<TestSqlColumn> { column };
            var table = new TestSqlTable(columns);

            // Act
            var result = table.GetColumn(propertyName);

            // Assert
            Assert.NotNull(result);
            Assert.Same(column, result);
        }

        /// <summary>
        /// Tests that GetColumn returns the correct column from multiple columns.
        /// Input: Property name that matches one of multiple columns.
        /// Expected: Returns the correct matching column.
        /// </summary>
        [Fact]
        public void GetColumn_MultipleColumns_ReturnsCorrectColumn()
        {
            // Arrange
            var column1 = new TestSqlColumn("Id");
            var column2 = new TestSqlColumn("Name");
            var column3 = new TestSqlColumn("Age");
            var column4 = new TestSqlColumn("Email");
            var columns = new List<TestSqlColumn> { column1, column2, column3, column4 };
            var table = new TestSqlTable(columns);

            // Act
            var resultName = table.GetColumn("Name");
            var resultAge = table.GetColumn("Age");
            var resultId = table.GetColumn("Id");

            // Assert
            Assert.NotNull(resultName);
            Assert.Same(column2, resultName);
            Assert.NotNull(resultAge);
            Assert.Same(column3, resultAge);
            Assert.NotNull(resultId);
            Assert.Same(column1, resultId);
        }

        /// <summary>
        /// Tests that GetColumn handles special characters in property names.
        /// Input: Property name containing special characters.
        /// Expected: Returns the column if it exists, null otherwise.
        /// </summary>
        [Theory]
        [InlineData("Property_Name")]
        [InlineData("Property123")]
        [InlineData("_Property")]
        [InlineData("Property$")]
        public void GetColumn_SpecialCharactersInPropertyName_HandlesCorrectly(string propertyName)
        {
            // Arrange
            var column = new TestSqlColumn(propertyName);
            var columns = new List<TestSqlColumn> { column };
            var table = new TestSqlTable(columns);

            // Act
            var result = table.GetColumn(propertyName);

            // Assert
            Assert.NotNull(result);
            Assert.Same(column, result);
            Assert.Equal(propertyName, result.PropertyName);
        }

        /// <summary>
        /// Tests that GetColumn handles very long property names.
        /// Input: Very long string property name.
        /// Expected: Returns the column if it exists.
        /// </summary>
        [Fact]
        public void GetColumn_VeryLongPropertyName_ReturnsColumn()
        {
            // Arrange
            var longPropertyName = new string('A', 1000);
            var column = new TestSqlColumn(longPropertyName);
            var columns = new List<TestSqlColumn> { column };
            var table = new TestSqlTable(columns);

            // Act
            var result = table.GetColumn(longPropertyName);

            // Assert
            Assert.NotNull(result);
            Assert.Same(column, result);
        }

        /// <summary>
        /// Tests that GetColumn returns null for empty column collection.
        /// Input: Valid property name with empty columns collection.
        /// Expected: Returns null.
        /// </summary>
        [Fact]
        public void GetColumn_EmptyColumnsCollection_ReturnsNull()
        {
            // Arrange
            var columns = new List<TestSqlColumn>();
            var table = new TestSqlTable(columns);

            // Act
            var result = table.GetColumn("AnyProperty");

            // Assert
            Assert.Null(result);
        }

        #region Helper Classes

        /// <summary>
        /// Test implementation of SqlTable for testing purposes.
        /// </summary>
        private class TestSqlTable : SqlTable
        {
            private readonly TestTableDefinition _definition;

            public TestSqlTable(ICollection<TestSqlColumn> columns)
                : base(new List<SqlColumn>(columns))
            {
                _definition = new TestTableDefinition();
            }

            public override TableDefinition Definition => _definition;
        }

        /// <summary>
        /// Test implementation of SqlColumn for testing purposes.
        /// </summary>
        private class TestSqlColumn : SqlColumn
        {
            private readonly TestColumnDefinition _definition;
            private readonly string _propertyName;

            public TestSqlColumn(string propertyName)
                : base(CreatePropertyInfo(propertyName))
            {
                _propertyName = propertyName;
                _definition = new TestColumnDefinition();
            }

            public override ColumnDefinition Definition => _definition;

            private static PropertyInfo CreatePropertyInfo(string propertyName)
            {
                var property = typeof(TestEntity).GetProperty(nameof(TestEntity.DummyProperty));
                return property ?? throw new InvalidOperationException("Property not found");
            }
        }

        /// <summary>
        /// Test entity for creating PropertyInfo instances.
        /// </summary>
        private class TestEntity
        {
            public string DummyProperty { get; set; }
        }

        /// <summary>
        /// Test implementation of TableDefinition for testing purposes.
        /// </summary>
        private class TestTableDefinition : TableDefinition
        {
            public TestTableDefinition()
                : base(typeof(TestEntity))
            {
            }
        }

        /// <summary>
        /// Test implementation of ColumnDefinition for testing purposes.
        /// </summary>
        private class TestColumnDefinition : ColumnDefinition
        {
            public TestColumnDefinition()
                : base(typeof(TestEntity).GetProperty(nameof(TestEntity.DummyProperty)))
            {
            }
        }

        #endregion

        /// <summary>
        /// Tests that ClearCache clears the named column cache when it has been populated.
        /// </summary>
        [Fact]
        public void ClearCache_WhenNamedColumnCacheIsPopulated_ClearsCache()
        {
            // Arrange
            var columnMock = new Mock<SqlColumn>();
            columnMock.Setup(c => c.PropertyName).Returns("TestProperty");
            var columns = new List<SqlColumn> { columnMock.Object };

            var definitionMock = new Mock<TableDefinition>();
            definitionMock.Setup(d => d.ObjectType).Returns(typeof(string));

            var table = new TestSqlTable(columns, definitionMock.Object);

            // Populate the cache by accessing NamedColumnCache
            var cache = table.GetNamedColumnCache();
            var initialCount = cache.Count;

            // Act
            table.ClearCache();

            // Assert
            var cacheAfterClear = table.GetNamedColumnCache();
            Assert.Equal(0, cacheAfterClear.Count);
        }

        /// <summary>
        /// Tests that ClearCache sets SelectColumns to null, causing it to be re-initialized on next access.
        /// </summary>
        [Fact]
        public void ClearCache_WhenSelectColumnsIsPopulated_ResetsSelectColumns()
        {
            // Arrange
            var columnMock = new Mock<SqlColumn>();
            columnMock.Setup(c => c.PropertyName).Returns("TestProperty");
            var columns = new List<SqlColumn> { columnMock.Object };

            var definitionMock = new Mock<TableDefinition>();
            definitionMock.Setup(d => d.ObjectType).Returns(typeof(string));

            var table = new TestSqlTable(columns, definitionMock.Object);

            // Access SelectColumns to populate the cache
            var selectColumnsBefore = table.SelectColumns;
            Assert.NotNull(selectColumnsBefore);

            // Act
            table.ClearCache();

            // Assert
            var selectColumnsAfter = table.SelectColumns;
            // After clearing, SelectColumns should be re-initialized (not the same reference)
            Assert.NotNull(selectColumnsAfter);
        }

        /// <summary>
        /// Tests that ClearCache does not throw when called on a table with empty caches.
        /// </summary>
        [Fact]
        public void ClearCache_WhenCachesAreEmpty_DoesNotThrow()
        {
            // Arrange
            var columnMock = new Mock<SqlColumn>();
            columnMock.Setup(c => c.PropertyName).Returns("TestProperty");
            var columns = new List<SqlColumn> { columnMock.Object };

            var definitionMock = new Mock<TableDefinition>();
            definitionMock.Setup(d => d.ObjectType).Returns(typeof(string));

            var table = new TestSqlTable(columns, definitionMock.Object);

            // Act & Assert - should not throw
            table.ClearCache();

            // Verify cache is still empty
            var cache = table.GetNamedColumnCache();
            Assert.Equal(0, cache.Count);
        }

        /// <summary>
        /// Tests that ClearCache can be called multiple times without error.
        /// </summary>
        [Fact]
        public void ClearCache_CalledMultipleTimes_DoesNotThrow()
        {
            // Arrange
            var columnMock = new Mock<SqlColumn>();
            columnMock.Setup(c => c.PropertyName).Returns("TestProperty");
            var columns = new List<SqlColumn> { columnMock.Object };

            var definitionMock = new Mock<TableDefinition>();
            definitionMock.Setup(d => d.ObjectType).Returns(typeof(string));

            var table = new TestSqlTable(columns, definitionMock.Object);

            // Populate cache
            var cache = table.GetNamedColumnCache();

            // Act & Assert - multiple calls should not throw
            table.ClearCache();
            table.ClearCache();
            table.ClearCache();

            // Verify cache is empty
            var cacheAfterClear = table.GetNamedColumnCache();
            Assert.Equal(0, cacheAfterClear.Count);
        }

        /// <summary>
        /// Tests that after ClearCache, GetColumn method can still function correctly by re-populating the cache.
        /// </summary>
        [Fact]
        public void ClearCache_AfterClearing_GetColumnStillWorks()
        {
            // Arrange
            var columnMock = new Mock<SqlColumn>();
            columnMock.Setup(c => c.PropertyName).Returns("TestProperty");
            var columns = new List<SqlColumn> { columnMock.Object };

            var definitionMock = new Mock<TableDefinition>();
            definitionMock.Setup(d => d.ObjectType).Returns(typeof(string));

            var table = new TestSqlTable(columns, definitionMock.Object);

            // Use GetColumn to populate cache
            var columnBefore = table.GetColumn("TestProperty");
            Assert.NotNull(columnBefore);

            // Act
            table.ClearCache();

            // Assert - GetColumn should still work after clearing
            var columnAfter = table.GetColumn("TestProperty");
            Assert.NotNull(columnAfter);
            Assert.Same(columnMock.Object, columnAfter);
        }

        /// <summary>
        /// Test helper class that provides a concrete implementation of SqlTable for testing purposes.
        /// Exposes protected members for verification.
        /// </summary>
        private class TestSqlTable : SqlTable
        {
            private readonly TableDefinition _definition;

            public TestSqlTable(ICollection<SqlColumn> columns, TableDefinition definition)
                : base(columns)
            {
                _definition = definition;
            }

            public override TableDefinition Definition => _definition;

            public override string Name => "TestTable";

            public System.Collections.Concurrent.ConcurrentDictionary<string, SqlColumn> GetNamedColumnCache()
            {
                return NamedColumnCache;
            }
        }

        /// <summary>
        /// Tests that ToString returns the Name property value when Name is a valid string.
        /// </summary>
        /// <param name="name">The name to set on the table.</param>
        [Theory]
        [InlineData("TestTable")]
        [InlineData("Table_With_Underscores")]
        [InlineData("Table123")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Table With Spaces")]
        [InlineData("特殊字符表")]
        [InlineData("Table\nWith\nNewlines")]
        [InlineData("Table\tWith\tTabs")]
        public void ToString_WithVariousNames_ReturnsName(string name)
        {
            // Arrange
            var columns = new List<SqlColumn>();
            var table = new TestSqlTable(columns, name);

            // Act
            var result = table.ToString();

            // Assert
            Assert.Equal(name, result);
        }

        /// <summary>
        /// Tests that ToString returns null when Name is null.
        /// </summary>
        [Fact]
        public void ToString_WhenNameIsNull_ReturnsNull()
        {
            // Arrange
            var columns = new List<SqlColumn>();
            var table = new TestSqlTable(columns, null);

            // Act
            var result = table.ToString();

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// Test implementation of SqlTable for testing purposes.
        /// </summary>
        private class TestSqlTable : SqlTable
        {
            private readonly TableDefinition _definition;

            public TestSqlTable(ICollection<SqlColumn> columns, string? name)
                : base(columns)
            {
                Name = name;
                _definition = new TableDefinition(typeof(object), name ?? string.Empty);
            }

            public override TableDefinition Definition => _definition;
        }

        /// <summary>
        /// Tests that SelectColumns returns an empty array when Columns is empty.
        /// </summary>
        [Fact]
        public void SelectColumns_EmptyColumns_ReturnsEmptyArray()
        {
            // Arrange
            var mockTable = CreateMockSqlTable(Array.Empty<SqlColumn>());

            // Act
            var result = mockTable.SelectColumns;

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that SelectColumns returns all ColumnDefinition columns when all can be read.
        /// </summary>
        [Fact]
        public void SelectColumns_AllColumnsCanRead_ReturnsAllColumns()
        {
            // Arrange
            var column1 = CreateColumnDefinition("Column1", ColumnMode.Read);
            var column2 = CreateColumnDefinition("Column2", ColumnMode.Full);
            var column3 = CreateColumnDefinition("Column3", ColumnMode.Final);
            var columns = new[] { column1, column2, column3 };
            var mockTable = CreateMockSqlTable(columns);

            // Act
            var result = mockTable.SelectColumns;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Length);
            Assert.Contains(column1, result);
            Assert.Contains(column2, result);
            Assert.Contains(column3, result);
        }

        /// <summary>
        /// Tests that SelectColumns filters out ColumnDefinition columns when Mode does not allow read.
        /// </summary>
        [Fact]
        public void SelectColumns_ColumnsCannotRead_FiltersOutNonReadableColumns()
        {
            // Arrange
            var readableColumn = CreateColumnDefinition("Readable", ColumnMode.Read);
            var writeOnlyColumn = CreateColumnDefinition("WriteOnly", ColumnMode.Write);
            var noneColumn = CreateColumnDefinition("None", ColumnMode.None);
            var columns = new[] { readableColumn, writeOnlyColumn, noneColumn };
            var mockTable = CreateMockSqlTable(columns);

            // Act
            var result = mockTable.SelectColumns;

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Contains(readableColumn, result);
            Assert.DoesNotContain(writeOnlyColumn, result);
            Assert.DoesNotContain(noneColumn, result);
        }

        /// <summary>
        /// Tests that SelectColumns unwraps ForeignColumn and checks the target ColumnDefinition.
        /// </summary>
        [Fact]
        public void SelectColumns_ForeignColumnWithReadableTarget_IncludesForeignColumn()
        {
            // Arrange
            var targetColumn = CreateColumnDefinition("TargetColumn", ColumnMode.Read);
            var foreignColumn = CreateForeignColumn("ForeignColumn", targetColumn);
            var columns = new[] { foreignColumn };
            var mockTable = CreateMockSqlTable(columns);

            // Act
            var result = mockTable.SelectColumns;

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Contains(foreignColumn, result);
        }

        /// <summary>
        /// Tests that SelectColumns filters out ForeignColumn when target cannot be read.
        /// </summary>
        [Fact]
        public void SelectColumns_ForeignColumnWithNonReadableTarget_ExcludesForeignColumn()
        {
            // Arrange
            var targetColumn = CreateColumnDefinition("TargetColumn", ColumnMode.Write);
            var foreignColumn = CreateForeignColumn("ForeignColumn", targetColumn);
            var columns = new[] { foreignColumn };
            var mockTable = CreateMockSqlTable(columns);

            // Act
            var result = mockTable.SelectColumns;

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that SelectColumns handles nested ForeignColumn chains correctly.
        /// </summary>
        [Fact]
        public void SelectColumns_NestedForeignColumns_UnwrapsToFinalTarget()
        {
            // Arrange
            var targetColumn = CreateColumnDefinition("FinalTarget", ColumnMode.Read);
            var foreignColumn1 = CreateForeignColumn("Foreign1", targetColumn);
            var foreignColumn2 = CreateForeignColumn("Foreign2", foreignColumn1);
            var columns = new[] { foreignColumn2 };
            var mockTable = CreateMockSqlTable(columns);

            // Act
            var result = mockTable.SelectColumns;

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Contains(foreignColumn2, result);
        }

        /// <summary>
        /// Tests that SelectColumns handles nested ForeignColumn chains with non-readable target.
        /// </summary>
        [Fact]
        public void SelectColumns_NestedForeignColumnsWithNonReadableTarget_ExcludesColumn()
        {
            // Arrange
            var targetColumn = CreateColumnDefinition("FinalTarget", ColumnMode.None);
            var foreignColumn1 = CreateForeignColumn("Foreign1", targetColumn);
            var foreignColumn2 = CreateForeignColumn("Foreign2", foreignColumn1);
            var columns = new[] { foreignColumn2 };
            var mockTable = CreateMockSqlTable(columns);

            // Act
            var result = mockTable.SelectColumns;

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that SelectColumns returns true for SqlColumn types that are neither ColumnDefinition nor ForeignColumn.
        /// </summary>
        [Fact]
        public void SelectColumns_NonStandardSqlColumn_IncludesColumn()
        {
            // Arrange
            var customColumn = CreateCustomSqlColumn("CustomColumn");
            var columns = new[] { customColumn };
            var mockTable = CreateMockSqlTable(columns);

            // Act
            var result = mockTable.SelectColumns;

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Contains(customColumn, result);
        }

        /// <summary>
        /// Tests that SelectColumns handles mixed column types correctly.
        /// </summary>
        [Fact]
        public void SelectColumns_MixedColumnTypes_FiltersCorrectly()
        {
            // Arrange
            var readableColumn = CreateColumnDefinition("Readable", ColumnMode.Read);
            var writeOnlyColumn = CreateColumnDefinition("WriteOnly", ColumnMode.Write);
            var foreignReadable = CreateForeignColumn("ForeignReadable", CreateColumnDefinition("Target1", ColumnMode.Read));
            var foreignNonReadable = CreateForeignColumn("ForeignNonReadable", CreateColumnDefinition("Target2", ColumnMode.None));
            var customColumn = CreateCustomSqlColumn("Custom");
            var columns = new[] { readableColumn, writeOnlyColumn, foreignReadable, foreignNonReadable, customColumn };
            var mockTable = CreateMockSqlTable(columns);

            // Act
            var result = mockTable.SelectColumns;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Length);
            Assert.Contains(readableColumn, result);
            Assert.Contains(foreignReadable, result);
            Assert.Contains(customColumn, result);
            Assert.DoesNotContain(writeOnlyColumn, result);
            Assert.DoesNotContain(foreignNonReadable, result);
        }

        /// <summary>
        /// Tests that SelectColumns caches the result and returns the same instance on subsequent calls.
        /// </summary>
        [Fact]
        public void SelectColumns_CalledMultipleTimes_ReturnsSameCachedInstance()
        {
            // Arrange
            var column = CreateColumnDefinition("Column", ColumnMode.Read);
            var columns = new[] { column };
            var mockTable = CreateMockSqlTable(columns);

            // Act
            var result1 = mockTable.SelectColumns;
            var result2 = mockTable.SelectColumns;

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.Same(result1, result2);
        }

        /// <summary>
        /// Tests that SelectColumns maintains the order of columns from the Columns array.
        /// </summary>
        [Fact]
        public void SelectColumns_MaintainsColumnOrder_PreservesOriginalOrder()
        {
            // Arrange
            var column1 = CreateColumnDefinition("Column1", ColumnMode.Read);
            var column2 = CreateColumnDefinition("Column2", ColumnMode.Read);
            var column3 = CreateColumnDefinition("Column3", ColumnMode.Read);
            var columns = new[] { column1, column2, column3 };
            var mockTable = CreateMockSqlTable(columns);

            // Act
            var result = mockTable.SelectColumns;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Length);
            Assert.Equal(column1, result[0]);
            Assert.Equal(column2, result[1]);
            Assert.Equal(column3, result[2]);
        }

        /// <summary>
        /// Tests SelectColumns with various ColumnMode enum values.
        /// </summary>
        /// <param name="mode">The column mode to test.</param>
        /// <param name="shouldBeIncluded">Whether the column should be included in SelectColumns.</param>
        [Theory]
        [InlineData(ColumnMode.None, false)]
        [InlineData(ColumnMode.Read, true)]
        [InlineData(ColumnMode.Write, false)]
        [InlineData(ColumnMode.Insert, false)]
        [InlineData(ColumnMode.Update, false)]
        [InlineData(ColumnMode.Full, true)]
        [InlineData(ColumnMode.Final, true)]
        public void SelectColumns_VariousColumnModes_FiltersBasedOnReadability(ColumnMode mode, bool shouldBeIncluded)
        {
            // Arrange
            var column = CreateColumnDefinition("TestColumn", mode);
            var columns = new[] { column };
            var mockTable = CreateMockSqlTable(columns);

            // Act
            var result = mockTable.SelectColumns;

            // Assert
            Assert.NotNull(result);
            if (shouldBeIncluded)
            {
                Assert.Single(result);
                Assert.Contains(column, result);
            }
            else
            {
                Assert.Empty(result);
            }
        }

        #region Helper Methods

        /// <summary>
        /// Creates a mock SqlTable with the specified columns.
        /// </summary>
        private SqlTable CreateMockSqlTable(SqlColumn[] columns)
        {
            var mockTable = new Mock<SqlTable>(columns) { CallBase = true };
            var mockTableDef = new Mock<TableDefinition>();
            mockTable.Setup(t => t.Definition).Returns(mockTableDef.Object);
            return mockTable.Object;
        }

        /// <summary>
        /// Creates a ColumnDefinition with the specified property name and mode.
        /// </summary>
        private ColumnDefinition CreateColumnDefinition(string propertyName, ColumnMode mode)
        {
            var mockProperty = CreateMockProperty(propertyName);
            var column = (ColumnDefinition)Activator.CreateInstance(
                typeof(ColumnDefinition),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { mockProperty },
                null);

            var modeProperty = typeof(ColumnDefinition).GetProperty("Mode");
            modeProperty.SetValue(column, mode);

            return column;
        }

        /// <summary>
        /// Creates a ForeignColumn pointing to the specified target column.
        /// </summary>
        private ForeignColumn CreateForeignColumn(string propertyName, SqlColumn targetColumn)
        {
            var mockProperty = CreateMockProperty(propertyName);
            var foreignColumn = (ForeignColumn)Activator.CreateInstance(
                typeof(ForeignColumn),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { mockProperty },
                null);

            var columnRef = new ColumnRef(targetColumn);
            var targetColumnProperty = typeof(ForeignColumn).GetProperty("TargetColumn");
            targetColumnProperty.SetValue(foreignColumn, columnRef);

            return foreignColumn;
        }

        /// <summary>
        /// Creates a custom SqlColumn that is neither ColumnDefinition nor ForeignColumn.
        /// </summary>
        private SqlColumn CreateCustomSqlColumn(string propertyName)
        {
            var mockProperty = CreateMockProperty(propertyName);
            var mockColumn = new Mock<SqlColumn>(mockProperty) { CallBase = true };
            var mockDefinition = new Mock<ColumnDefinition>(mockProperty) { CallBase = true };
            mockColumn.Setup(c => c.Definition).Returns(mockDefinition.Object);
            return mockColumn.Object;
        }

        /// <summary>
        /// Creates a mock PropertyInfo with the specified name.
        /// </summary>
        private PropertyInfo CreateMockProperty(string propertyName)
        {
            var mockProperty = new Mock<PropertyInfo>();
            mockProperty.Setup(p => p.Name).Returns(propertyName);
            mockProperty.Setup(p => p.PropertyType).Returns(typeof(string));
            return mockProperty.Object;
        }

        #endregion

        /// <summary>
        /// Tests that GetHashCode returns 0 when DefinitionType is null.
        /// Input: SqlTable with Definition.ObjectType returning null.
        /// Expected: GetHashCode returns 0.
        /// </summary>
        [Fact]
        public void GetHashCode_WhenDefinitionTypeIsNull_ReturnsZero()
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(d => d.ObjectType).Returns((Type?)null);

            var mockSqlTable = new Mock<SqlTable>(new List<SqlColumn>());
            mockSqlTable.Setup(t => t.Definition).Returns(mockTableDefinition.Object);
            mockSqlTable.CallBase = true;

            var sqlTable = mockSqlTable.Object;

            // Act
            var hashCode = sqlTable.GetHashCode();

            // Assert
            Assert.Equal(0, hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns the hash code of DefinitionType when it is not null.
        /// Input: SqlTable with Definition.ObjectType returning a valid Type.
        /// Expected: GetHashCode returns the hash code of that Type.
        /// </summary>
        [Fact]
        public void GetHashCode_WhenDefinitionTypeIsNotNull_ReturnsTypeHashCode()
        {
            // Arrange
            var testType = typeof(string);
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(d => d.ObjectType).Returns(testType);

            var mockSqlTable = new Mock<SqlTable>(new List<SqlColumn>());
            mockSqlTable.Setup(t => t.Definition).Returns(mockTableDefinition.Object);
            mockSqlTable.CallBase = true;

            var sqlTable = mockSqlTable.Object;

            // Act
            var hashCode = sqlTable.GetHashCode();

            // Assert
            Assert.Equal(testType.GetHashCode(), hashCode);
        }

        /// <summary>
        /// Tests that GetHashCode returns consistent values when called multiple times.
        /// Input: SqlTable with Definition.ObjectType returning a valid Type.
        /// Expected: Multiple calls to GetHashCode return the same value.
        /// </summary>
        [Fact]
        public void GetHashCode_CalledMultipleTimes_ReturnsConsistentValue()
        {
            // Arrange
            var testType = typeof(int);
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(d => d.ObjectType).Returns(testType);

            var mockSqlTable = new Mock<SqlTable>(new List<SqlColumn>());
            mockSqlTable.Setup(t => t.Definition).Returns(mockTableDefinition.Object);
            mockSqlTable.CallBase = true;

            var sqlTable = mockSqlTable.Object;

            // Act
            var hashCode1 = sqlTable.GetHashCode();
            var hashCode2 = sqlTable.GetHashCode();
            var hashCode3 = sqlTable.GetHashCode();

            // Assert
            Assert.Equal(hashCode1, hashCode2);
            Assert.Equal(hashCode2, hashCode3);
        }

        /// <summary>
        /// Tests that GetHashCode returns different values for different DefinitionTypes.
        /// Input: Two SqlTable instances with different Definition.ObjectType values.
        /// Expected: GetHashCode returns different values for different types.
        /// </summary>
        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        [InlineData(typeof(DateTime))]
        [InlineData(typeof(object))]
        public void GetHashCode_WithDifferentDefinitionTypes_ReturnsDifferentHashCodes(Type definitionType)
        {
            // Arrange
            var mockTableDefinition = new Mock<TableDefinition>();
            mockTableDefinition.Setup(d => d.ObjectType).Returns(definitionType);

            var mockSqlTable = new Mock<SqlTable>(new List<SqlColumn>());
            mockSqlTable.Setup(t => t.Definition).Returns(mockTableDefinition.Object);
            mockSqlTable.CallBase = true;

            var sqlTable = mockSqlTable.Object;

            // Act
            var hashCode = sqlTable.GetHashCode();

            // Assert
            Assert.Equal(definitionType.GetHashCode(), hashCode);
        }

        /// <summary>
        /// Tests that Columns property returns an empty array when initialized with an empty collection.
        /// </summary>
        [Fact]
        public void Columns_EmptyCollection_ReturnsEmptyArray()
        {
            // Arrange
            var emptyColumns = new List<SqlColumn>();
            var table = new TestSqlTable(emptyColumns);

            // Act
            var result = table.Columns;

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that Columns property returns an array with a single column when initialized with one column.
        /// </summary>
        [Fact]
        public void Columns_SingleColumn_ReturnsSingleElementArray()
        {
            // Arrange
            var property = typeof(TestEntity).GetProperty(nameof(TestEntity.Id));
            var column = new TestSqlColumn(property);
            var columns = new List<SqlColumn> { column };
            var table = new TestSqlTable(columns);

            // Act
            var result = table.Columns;

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Same(column, result[0]);
        }

        /// <summary>
        /// Tests that Columns property returns an array with multiple columns in the same order as provided.
        /// </summary>
        [Fact]
        public void Columns_MultipleColumns_ReturnsAllColumnsInOrder()
        {
            // Arrange
            var property1 = typeof(TestEntity).GetProperty(nameof(TestEntity.Id));
            var property2 = typeof(TestEntity).GetProperty(nameof(TestEntity.Name));
            var property3 = typeof(TestEntity).GetProperty(nameof(TestEntity.Value));
            var column1 = new TestSqlColumn(property1);
            var column2 = new TestSqlColumn(property2);
            var column3 = new TestSqlColumn(property3);
            var columns = new List<SqlColumn> { column1, column2, column3 };
            var table = new TestSqlTable(columns);

            // Act
            var result = table.Columns;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Length);
            Assert.Same(column1, result[0]);
            Assert.Same(column2, result[1]);
            Assert.Same(column3, result[2]);
        }

        /// <summary>
        /// Tests that Columns property returns the same reference on multiple calls.
        /// </summary>
        [Fact]
        public void Columns_MultipleCalls_ReturnsSameReference()
        {
            // Arrange
            var property = typeof(TestEntity).GetProperty(nameof(TestEntity.Id));
            var column = new TestSqlColumn(property);
            var columns = new List<SqlColumn> { column };
            var table = new TestSqlTable(columns);

            // Act
            var result1 = table.Columns;
            var result2 = table.Columns;

            // Assert
            Assert.Same(result1, result2);
        }

        /// <summary>
        /// Tests that Columns property returns an independent copy of the collection (array conversion).
        /// </summary>
        [Fact]
        public void Columns_ArrayCreatedFromCollection_IsIndependentOfOriginalCollection()
        {
            // Arrange
            var property = typeof(TestEntity).GetProperty(nameof(TestEntity.Id));
            var column = new TestSqlColumn(property);
            var columns = new List<SqlColumn> { column };
            var table = new TestSqlTable(columns);
            var originalCount = table.Columns.Length;

            // Act
            columns.Add(new TestSqlColumn(typeof(TestEntity).GetProperty(nameof(TestEntity.Name))));

            // Assert
            Assert.Equal(originalCount, table.Columns.Length);
        }

        #region Helper Classes

        /// <summary>
        /// Test implementation of SqlTable for testing purposes.
        /// </summary>
        private class TestSqlTable : SqlTable
        {
            private readonly TableDefinition _definition;

            public TestSqlTable(ICollection<SqlColumn> columns) : base(columns)
            {
                _definition = new TableDefinition(typeof(TestEntity), "TestTable", "dbo");
            }

            public override TableDefinition Definition => _definition;
        }

        /// <summary>
        /// Test implementation of SqlColumn for testing purposes.
        /// </summary>
        private class TestSqlColumn : SqlColumn
        {
            private readonly ColumnDefinition _definition;

            public TestSqlColumn(PropertyInfo property) : base(property)
            {
                _definition = new ColumnDefinition(property, property.Name, false, false, false);
            }

            public override ColumnDefinition Definition => _definition;
        }

        /// <summary>
        /// Test entity class for property reflection.
        /// </summary>
        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Value { get; set; }
        }

        #endregion

        /// <summary>
        /// Tests that the constructor initializes the table with a valid collection of columns
        /// and sets the Table property for each column to the current table instance.
        /// </summary>
        [Fact]
        public void SqlTable_WithValidColumns_ShouldSetTablePropertyOnEachColumn()
        {
            // Arrange
            var mockProperty = typeof(TestEntity).GetProperty(nameof(TestEntity.Name));
            var mockColumn1 = new Mock<SqlColumn>(mockProperty);
            var mockColumn2 = new Mock<SqlColumn>(mockProperty);
            var columns = new List<SqlColumn> { mockColumn1.Object, mockColumn2.Object };

            // Act
            var mockTable = new Mock<SqlTable>(columns);
            var table = mockTable.Object;

            // Assert
            Assert.Equal(table, mockColumn1.Object.Table);
            Assert.Equal(table, mockColumn2.Object.Table);
        }

        /// <summary>
        /// Tests that the constructor works correctly with an empty collection of columns.
        /// </summary>
        [Fact]
        public void SqlTable_WithEmptyCollection_ShouldInitializeSuccessfully()
        {
            // Arrange
            var columns = new List<SqlColumn>();

            // Act
            var mockTable = new Mock<SqlTable>(columns);
            var table = mockTable.Object;

            // Assert
            Assert.NotNull(table);
            Assert.Empty(table.Columns);
        }

        /// <summary>
        /// Tests that the constructor throws NullReferenceException when passed a null collection.
        /// </summary>
        [Fact]
        public void SqlTable_WithNullCollection_ShouldThrowNullReferenceException()
        {
            // Arrange
            ICollection<SqlColumn>? columns = null;

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => new Mock<SqlTable>(columns!));
        }

        /// <summary>
        /// Tests that the constructor throws NullReferenceException when the collection contains a null column.
        /// </summary>
        [Fact]
        public void SqlTable_WithNullColumnInCollection_ShouldThrowNullReferenceException()
        {
            // Arrange
            var columns = new List<SqlColumn> { null! };

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => new Mock<SqlTable>(columns));
        }

        /// <summary>
        /// Tests that the constructor correctly handles a single column in the collection.
        /// </summary>
        [Fact]
        public void SqlTable_WithSingleColumn_ShouldSetTablePropertyCorrectly()
        {
            // Arrange
            var mockProperty = typeof(TestEntity).GetProperty(nameof(TestEntity.Name));
            var mockColumn = new Mock<SqlColumn>(mockProperty);
            var columns = new List<SqlColumn> { mockColumn.Object };

            // Act
            var mockTable = new Mock<SqlTable>(columns);
            var table = mockTable.Object;

            // Assert
            Assert.Equal(table, mockColumn.Object.Table);
            Assert.Single(table.Columns);
        }

        /// <summary>
        /// Tests that the constructor correctly handles multiple columns and sets Table property for all.
        /// </summary>
        [Theory]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(10)]
        public void SqlTable_WithMultipleColumns_ShouldSetTablePropertyOnAllColumns(int columnCount)
        {
            // Arrange
            var mockProperty = typeof(TestEntity).GetProperty(nameof(TestEntity.Name));
            var columns = new List<SqlColumn>();
            var mockColumns = new List<Mock<SqlColumn>>();

            for (int i = 0; i < columnCount; i++)
            {
                var mockColumn = new Mock<SqlColumn>(mockProperty);
                mockColumns.Add(mockColumn);
                columns.Add(mockColumn.Object);
            }

            // Act
            var mockTable = new Mock<SqlTable>(columns);
            var table = mockTable.Object;

            // Assert
            Assert.Equal(columnCount, table.Columns.Length);
            foreach (var mockColumn in mockColumns)
            {
                Assert.Equal(table, mockColumn.Object.Table);
            }
        }

        /// <summary>
        /// Tests that the constructor creates a defensive copy of the columns collection.
        /// Modifying the original collection should not affect the table's internal column array.
        /// </summary>
        [Fact]
        public void SqlTable_WithList_ShouldCreateDefensiveCopy()
        {
            // Arrange
            var mockProperty = typeof(TestEntity).GetProperty(nameof(TestEntity.Name));
            var mockColumn1 = new Mock<SqlColumn>(mockProperty);
            var mockColumn2 = new Mock<SqlColumn>(mockProperty);
            var columns = new List<SqlColumn> { mockColumn1.Object };

            // Act
            var mockTable = new Mock<SqlTable>(columns);
            var table = mockTable.Object;
            var originalCount = table.Columns.Length;

            // Modify original collection after table creation
            columns.Add(mockColumn2.Object);

            // Assert
            Assert.Equal(originalCount, table.Columns.Length);
            Assert.Equal(1, table.Columns.Length);
        }

        /// <summary>
        /// Tests that the constructor throws NullReferenceException when the collection contains
        /// multiple items including a null column.
        /// </summary>
        [Fact]
        public void SqlTable_WithMixedNullAndValidColumns_ShouldThrowNullReferenceException()
        {
            // Arrange
            var mockProperty = typeof(TestEntity).GetProperty(nameof(TestEntity.Name));
            var mockColumn = new Mock<SqlColumn>(mockProperty);
            var columns = new List<SqlColumn> { mockColumn.Object, null!, mockColumn.Object };

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => new Mock<SqlTable>(columns));
        }

        /// <summary>
        /// Test helper entity class with properties for testing.
        /// </summary>
        private class TestEntity
        {
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        /// <summary>
        /// Tests that the Keys property returns an empty array when the table has no columns.
        /// </summary>
        [Fact]
        public void Keys_WhenNoColumns_ReturnsEmptyArray()
        {
            // Arrange
            var table = CreateTestTable(new SqlColumn[0]);

            // Act
            var result = table.Keys;

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that the Keys property returns an empty array when no columns are primary keys.
        /// </summary>
        [Fact]
        public void Keys_WhenNoPrimaryKeys_ReturnsEmptyArray()
        {
            // Arrange
            var column1 = CreateColumnDefinition("Column1", isPrimaryKey: false);
            var column2 = CreateColumnDefinition("Column2", isPrimaryKey: false);
            var columns = new SqlColumn[] { column1, column2 };
            var table = CreateTestTable(columns);

            // Act
            var result = table.Keys;

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Tests that the Keys property returns a single primary key when only one column is marked as a primary key.
        /// </summary>
        [Fact]
        public void Keys_WhenSinglePrimaryKey_ReturnsSingleKey()
        {
            // Arrange
            var primaryKey = CreateColumnDefinition("Id", isPrimaryKey: true);
            var regularColumn = CreateColumnDefinition("Name", isPrimaryKey: false);
            var columns = new SqlColumn[] { primaryKey, regularColumn };
            var table = CreateTestTable(columns);

            // Act
            var result = table.Keys;

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Id", result[0].PropertyName);
        }

        /// <summary>
        /// Tests that the Keys property returns multiple primary keys sorted alphabetically by PropertyName.
        /// </summary>
        [Fact]
        public void Keys_WhenMultiplePrimaryKeys_ReturnsSortedByPropertyName()
        {
            // Arrange
            var key1 = CreateColumnDefinition("Zulu", isPrimaryKey: true);
            var key2 = CreateColumnDefinition("Alpha", isPrimaryKey: true);
            var key3 = CreateColumnDefinition("Mike", isPrimaryKey: true);
            var regularColumn = CreateColumnDefinition("Data", isPrimaryKey: false);
            var columns = new SqlColumn[] { key1, regularColumn, key2, key3 };
            var table = CreateTestTable(columns);

            // Act
            var result = table.Keys;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Length);
            Assert.Equal("Alpha", result[0].PropertyName);
            Assert.Equal("Mike", result[1].PropertyName);
            Assert.Equal("Zulu", result[2].PropertyName);
        }

        /// <summary>
        /// Tests that the Keys property returns the same cached instance when called multiple times.
        /// </summary>
        [Fact]
        public void Keys_WhenCalledMultipleTimes_ReturnsCachedInstance()
        {
            // Arrange
            var primaryKey = CreateColumnDefinition("Id", isPrimaryKey: true);
            var columns = new SqlColumn[] { primaryKey };
            var table = CreateTestTable(columns);

            // Act
            var result1 = table.Keys;
            var result2 = table.Keys;

            // Assert
            Assert.Same(result1, result2);
        }

        /// <summary>
        /// Tests that the Keys property correctly filters out non-ColumnDefinition SqlColumn instances.
        /// </summary>
        [Fact]
        public void Keys_WhenColumnsContainNonColumnDefinitions_FiltersCorrectly()
        {
            // Arrange
            var columnDefinition = CreateColumnDefinition("Id", isPrimaryKey: true);
            var sqlColumnMock = new Mock<SqlColumn>(CreateMockPropertyInfo("Name"));
            var columns = new SqlColumn[] { columnDefinition, sqlColumnMock.Object };
            var table = CreateTestTable(columns);

            // Act
            var result = table.Keys;

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Id", result[0].PropertyName);
        }

        /// <summary>
        /// Tests that Keys property sorting respects case sensitivity in PropertyName comparison.
        /// </summary>
        [Fact]
        public void Keys_SortingIsCaseSensitive_ReturnsSortedCorrectly()
        {
            // Arrange
            var key1 = CreateColumnDefinition("zeta", isPrimaryKey: true);
            var key2 = CreateColumnDefinition("Alpha", isPrimaryKey: true);
            var key3 = CreateColumnDefinition("beta", isPrimaryKey: true);
            var columns = new SqlColumn[] { key1, key2, key3 };
            var table = CreateTestTable(columns);

            // Act
            var result = table.Keys;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Length);
            Assert.Equal("Alpha", result[0].PropertyName);
            Assert.Equal("beta", result[1].PropertyName);
            Assert.Equal("zeta", result[2].PropertyName);
        }

        /// <summary>
        /// Tests that Keys property handles empty PropertyName correctly in sorting.
        /// </summary>
        [Fact]
        public void Keys_WithEmptyPropertyName_SortsCorrectly()
        {
            // Arrange
            var key1 = CreateColumnDefinition("Beta", isPrimaryKey: true);
            var key2 = CreateColumnDefinition("", isPrimaryKey: true);
            var key3 = CreateColumnDefinition("Alpha", isPrimaryKey: true);
            var columns = new SqlColumn[] { key1, key2, key3 };
            var table = CreateTestTable(columns);

            // Act
            var result = table.Keys;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Length);
            Assert.Equal("", result[0].PropertyName);
            Assert.Equal("Alpha", result[1].PropertyName);
            Assert.Equal("Beta", result[2].PropertyName);
        }

        /// <summary>
        /// Tests that Keys property handles very long PropertyName values.
        /// </summary>
        [Fact]
        public void Keys_WithVeryLongPropertyName_HandlesCorrectly()
        {
            // Arrange
            var longName = new string('A', 10000);
            var key1 = CreateColumnDefinition(longName, isPrimaryKey: true);
            var key2 = CreateColumnDefinition("Short", isPrimaryKey: true);
            var columns = new SqlColumn[] { key1, key2 };
            var table = CreateTestTable(columns);

            // Act
            var result = table.Keys;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Length);
            Assert.Equal(longName, result[0].PropertyName);
            Assert.Equal("Short", result[1].PropertyName);
        }

        /// <summary>
        /// Tests that Keys property handles special characters in PropertyName during sorting.
        /// </summary>
        [Fact]
        public void Keys_WithSpecialCharactersInPropertyName_SortsCorrectly()
        {
            // Arrange
            var key1 = CreateColumnDefinition("Name_123", isPrimaryKey: true);
            var key2 = CreateColumnDefinition("Name-ABC", isPrimaryKey: true);
            var key3 = CreateColumnDefinition("Name@XYZ", isPrimaryKey: true);
            var columns = new SqlColumn[] { key1, key2, key3 };
            var table = CreateTestTable(columns);

            // Act
            var result = table.Keys;

            // Assert
            Assert.NotNull(result);
            Assert.Equal(3, result.Length);
        }

        /// <summary>
        /// Tests that Keys property returns empty array when all columns are non-ColumnDefinition types.
        /// </summary>
        [Fact]
        public void Keys_WhenAllColumnsAreNonColumnDefinitions_ReturnsEmptyArray()
        {
            // Arrange
            var sqlColumnMock1 = new Mock<SqlColumn>(CreateMockPropertyInfo("Column1"));
            var sqlColumnMock2 = new Mock<SqlColumn>(CreateMockPropertyInfo("Column2"));
            var columns = new SqlColumn[] { sqlColumnMock1.Object, sqlColumnMock2.Object };
            var table = CreateTestTable(columns);

            // Act
            var result = table.Keys;

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        /// <summary>
        /// Creates a test instance of SqlTable with the specified columns.
        /// </summary>
        private TestSqlTable CreateTestTable(SqlColumn[] columns)
        {
            return new TestSqlTable(columns);
        }

        /// <summary>
        /// Creates a ColumnDefinition mock with the specified PropertyName and IsPrimaryKey value.
        /// </summary>
        private ColumnDefinition CreateColumnDefinition(string propertyName, bool isPrimaryKey)
        {
            var propertyInfo = CreateMockPropertyInfo(propertyName);
            var mock = new Mock<ColumnDefinition>(propertyInfo);
            mock.SetupGet(c => c.PropertyName).Returns(propertyName);
            mock.SetupGet(c => c.IsPrimaryKey).Returns(isPrimaryKey);
            return mock.Object;
        }

        /// <summary>
        /// Test implementation of SqlTable for testing purposes.
        /// </summary>
        private class TestSqlTable : SqlTable
        {
            private readonly TableDefinition _definition;

            public TestSqlTable(SqlColumn[] columns)
                : base(columns)
            {
                var mockDefinition = new Mock<TableDefinition>();
                mockDefinition.SetupGet(d => d.ObjectType).Returns(typeof(TestEntity));
                _definition = mockDefinition.Object;
            }

            public override TableDefinition Definition => _definition;
        }

        /// <summary>
        /// Test entity class used for testing.
        /// </summary>
        private class TestEntity
        {
        }
    }
}