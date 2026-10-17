using System;
using System.Collections.Generic;
using System.Reflection;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class ColumnRefTests
    {
        [Fact]
        public void Constructor_WithColumn_SetsNameAndColumn()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var columnRef = new ColumnRef(column);

            Assert.Equal(column.Name, columnRef.Name);
            Assert.Same(column, columnRef.Column);
            Assert.Null(columnRef.Table);
        }

        [Fact]
        public void Constructor_WithTableAndColumn_SetsAllProperties()
        {
            var table = CreateJoinedTable();
            var column = CreateColumn(nameof(TestEntity.Name));
            var columnRef = new ColumnRef(table, column);

            Assert.Same(table, columnRef.Table);
            Assert.Same(column, columnRef.Column);
        }

        [Fact]
        public void Constructor_WithNullTable_LeavesTableNull()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var columnRef = new ColumnRef(null, column);

            Assert.Null(columnRef.Table);
            Assert.Same(column, columnRef.Column);
        }

        [Fact]
        public void Equals_WithSameTableAndColumn_ReturnsTrue()
        {
            var table = CreateJoinedTable();
            var column = CreateColumn(nameof(TestEntity.Name));
            var left = new ColumnRef(table, column);
            var right = new ColumnRef(table, column);

            Assert.True(left.Equals(right));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void Equals_WithSameTableDefinitionAndName_ReturnsTrue()
        {
            var left = new ColumnRef(CreateJoinedTable(), CreateColumn(nameof(TestEntity.Name)));
            var right = new ColumnRef(CreateJoinedTable(), CreateColumn(nameof(TestEntity.Name)));

            Assert.NotSame(left.Table, right.Table);
            Assert.True(left.Equals(right));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void Equals_WithDifferentTableAlias_ReturnsFalse()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var definition = CreateTableDefinition(new List<ColumnDefinition> { column });
            var left = new ColumnRef(new JoinedTable(definition, new[] { column }), column);
            var right = new ColumnRef(new JoinedTable(definition, new[] { column }) { Name = "Other" }, column);

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_WithDifferentColumns_ReturnsFalse()
        {
            var table = CreateJoinedTable();
            var left = new ColumnRef(table, CreateColumn(nameof(TestEntity.Name)));
            var right = new ColumnRef(table, CreateColumn(nameof(TestEntity.Age)));

            Assert.False(left.Equals(right));
        }

        private static ColumnDefinition CreateColumn(string propertyName)
        {
            return (ColumnDefinition)Activator.CreateInstance(
                typeof(ColumnDefinition),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { typeof(TestEntity).GetProperty(propertyName)! },
                culture: null)!;
        }

        private static TableDefinition CreateTableDefinition(List<ColumnDefinition> columns)
        {
            return (TableDefinition)Activator.CreateInstance(
                typeof(TableDefinition),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { typeof(TestEntity), columns },
                culture: null)!;
        }

        private static JoinedTable CreateJoinedTable()
        {
            var keyColumn = CreateColumn(nameof(TestEntity.Age));
            return new JoinedTable(CreateTableDefinition(new List<ColumnDefinition> { keyColumn }), new[] { keyColumn });
        }

        private class TestEntity
        {
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }
    }
}
