using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class SqlTableTests
    {
        [Fact]
        public void Constructor_AssignsTableToEachColumn()
        {
            var column1 = CreateColumn(nameof(TestEntity.Id));
            var column2 = CreateColumn(nameof(TestEntity.Name));
            var table = CreateTable(new SqlColumn[] { column1, column2 });

            Assert.Same(table, column1.Table);
            Assert.Same(table, column2.Table);
        }

        [Fact]
        public void GetColumn_IsCaseInsensitive()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var table = CreateTable(new SqlColumn[] { column });

            Assert.Same(column, table.GetColumn("name"));
            Assert.Same(column, table.GetColumn("NAME"));
        }

        [Fact]
        public void GetColumn_WithNullOrEmpty_ReturnsNull()
        {
            var table = CreateTable(Array.Empty<SqlColumn>());

            Assert.Null(table.GetColumn(null));
            Assert.Null(table.GetColumn(string.Empty));
        }

        [Fact]
        public void Keys_ReturnsPrimaryKeysSortedByPropertyName()
        {
            var keyB = CreateColumn(nameof(TestEntity.Name), isPrimaryKey: true);
            var keyA = CreateColumn(nameof(TestEntity.Id), isPrimaryKey: true);
            var nonKey = CreateColumn(nameof(TestEntity.Age));
            var table = CreateTable(new SqlColumn[] { keyB, nonKey, keyA });

            var keys = table.Keys;

            Assert.Equal(2, keys.Length);
            Assert.Equal(nameof(TestEntity.Id), keys[0].PropertyName);
            Assert.Equal(nameof(TestEntity.Name), keys[1].PropertyName);
        }

        [Fact]
        public void SelectColumns_FiltersByReadableMode()
        {
            var readable = CreateColumn(nameof(TestEntity.Id), mode: ColumnMode.Read);
            var writeOnly = CreateColumn(nameof(TestEntity.Name), mode: ColumnMode.Write);
            var full = CreateColumn(nameof(TestEntity.Age), mode: ColumnMode.Full);
            var table = CreateTable(new SqlColumn[] { readable, writeOnly, full });

            Assert.Equal(new SqlColumn[] { readable, full }, table.SelectColumns);
        }

        [Fact]
        public void ClearCache_RebuildsNamedColumnCacheOnNextLookup()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var table = CreateTable(new SqlColumn[] { column });

            var cacheBefore = GetNamedColumnCache(table);
            table.ClearCache();
            var cacheAfter = GetNamedColumnCache(table);

            Assert.Single(cacheBefore);
            Assert.Single(cacheAfter);
            Assert.Same(cacheBefore, cacheAfter);
        }

        [Fact]
        public void Equals_WithSameDefinitionType_ReturnsTrue()
        {
            var left = CreateTable(Array.Empty<SqlColumn>(), typeof(TestEntity));
            var right = CreateTable(Array.Empty<SqlColumn>(), typeof(TestEntity));

            Assert.True(left.Equals(right));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void Equals_WithDifferentDefinitionType_ReturnsFalse()
        {
            var left = CreateTable(Array.Empty<SqlColumn>(), typeof(TestEntity));
            var right = CreateTable(Array.Empty<SqlColumn>(), typeof(string));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void ToString_ReturnsName()
        {
            var table = CreateTable(Array.Empty<SqlColumn>(), typeof(TestEntity), "Users");

            Assert.Equal("Users", table.ToString());
        }

        private static TableView CreateTable(ICollection<SqlColumn> columns, Type? objectType = null, string name = "TestTable")
        {
            var definition = (TableDefinition)Activator.CreateInstance(
                typeof(TableDefinition),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { objectType ?? typeof(TestEntity), new List<ColumnDefinition>() },
                culture: null)!;

            var table = new TableView(definition, new List<JoinedTable>(), columns);
            typeof(SqlObject).GetProperty(nameof(SqlObject.Name), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(table, name);
            return table;
        }

        private static ColumnDefinition CreateColumn(string propertyName, bool isPrimaryKey = false, ColumnMode mode = ColumnMode.Full)
        {
            var property = typeof(TestEntity).GetProperty(propertyName)!;
            var column = (ColumnDefinition)Activator.CreateInstance(
                typeof(ColumnDefinition),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { property },
                culture: null)!;

            typeof(ColumnDefinition).GetProperty(nameof(ColumnDefinition.IsPrimaryKey), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(column, isPrimaryKey);
            typeof(ColumnDefinition).GetProperty(nameof(ColumnDefinition.Mode), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(column, mode);

            return column;
        }

        private static ConcurrentDictionary<string, SqlColumn> GetNamedColumnCache(SqlTable table)
        {
            return (ConcurrentDictionary<string, SqlColumn>)typeof(SqlTable)
                .GetProperty("NamedColumnCache", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(table)!;
        }

        private class TestEntity
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }
    }
}
