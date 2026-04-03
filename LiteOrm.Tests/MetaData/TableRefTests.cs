using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class TableRefTests
    {
        [Fact]
        public void Constructor_SetsNameAndColumns()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var definition = CreateTableDefinition("Users", column);
            var tableRef = new TestTableRef(definition);

            Assert.Equal("Users", tableRef.Name);
            Assert.Single(tableRef.Columns);
            Assert.Same(column, tableRef.Columns[0].Column);
        }

        [Fact]
        public void GetColumn_WithNullOrEmpty_ReturnsNull()
        {
            var tableRef = new TestTableRef(CreateTableDefinition("Users"));

            Assert.Null(tableRef.GetColumn(null));
            Assert.Null(tableRef.GetColumn(string.Empty));
        }

        [Fact]
        public void GetColumn_WithExistingPropertyName_ReturnsColumnRef()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var tableRef = new TestTableRef(CreateTableDefinition("Users", column));

            var result = tableRef.GetColumn(nameof(TestEntity.Name));

            Assert.NotNull(result);
            Assert.Same(column, result.Column);
        }

        [Fact]
        public void GetColumn_IsCaseSensitive()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var tableRef = new TestTableRef(CreateTableDefinition("Users", column));

            Assert.Null(tableRef.GetColumn("name"));
            Assert.NotNull(tableRef.GetColumn("Name"));
        }

        [Fact]
        public void Equals_WithSameDefinitionAndName_ReturnsTrue()
        {
            var definition = CreateTableDefinition("Users");
            var left = new TestTableRef(definition);
            var right = new TestTableRef(definition);

            Assert.True(left.Equals(right));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void Equals_WithDifferentNames_ReturnsFalse()
        {
            var definition = CreateTableDefinition("Users");
            var left = new TestTableRef(definition);
            var right = new TestTableRef(definition);
            typeof(SqlObject).GetProperty(nameof(SqlObject.Name), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(right, "Other");

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void NamedColumnCache_ReturnsSameInstance()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var tableRef = new TestTableRef(CreateTableDefinition("Users", column));

            var first = tableRef.GetNamedColumnCache();
            var second = tableRef.GetNamedColumnCache();

            Assert.Same(first, second);
            Assert.Single(first);
        }

        private static ColumnDefinition CreateColumn(string propertyName)
        {
            return (ColumnDefinition)Activator.CreateInstance(
                typeof(ColumnDefinition),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { typeof(TestEntity).GetProperty(propertyName)! },
                culture: null)!;
        }

        private static TableDefinition CreateTableDefinition(string name, params ColumnDefinition[] columns)
        {
            var table = (TableDefinition)Activator.CreateInstance(
                typeof(TableDefinition),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { typeof(TestEntity), new List<ColumnDefinition>(columns) },
                culture: null)!;
            typeof(SqlObject).GetProperty(nameof(SqlObject.Name), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(table, name);
            return table;
        }

        private class TestTableRef : TableRef
        {
            public TestTableRef(TableDefinition table) : base(table)
            {
            }

            public ConcurrentDictionary<string, ColumnRef> GetNamedColumnCache() => NamedColumnCache;
        }

        private class TestEntity
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
