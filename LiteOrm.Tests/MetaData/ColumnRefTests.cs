using System;
using System.Collections.Generic;
using System.Reflection;

using LiteOrm.Common;
using Moq;
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
            var table = CreateTableRef();
            var column = CreateColumn(nameof(TestEntity.Name));
            var columnRef = new ColumnRef(table, column);

            Assert.Same(table, columnRef.Table);
            Assert.Same(column, columnRef.Column);
        }

        [Fact]
        public void Equals_WithSameTableAndColumn_ReturnsTrue()
        {
            var table = CreateTableRef();
            var column = CreateColumn(nameof(TestEntity.Name));
            var left = new ColumnRef(table, column);
            var right = new ColumnRef(table, column);

            Assert.True(left.Equals(right));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void Equals_WithDifferentColumns_ReturnsFalse()
        {
            var table = CreateTableRef();
            var left = new ColumnRef(table, CreateColumn(nameof(TestEntity.Name)));
            var right = new ColumnRef(table, CreateColumn(nameof(TestEntity.Age)));

            Assert.False(left.Equals(right));
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

        private static TableRef CreateTableRef()
        {
            var definition = (TableDefinition)Activator.CreateInstance(
                typeof(TableDefinition),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { typeof(TestEntity), new List<ColumnDefinition>() },
                culture: null)!;
            return new TestTableRef(definition);
        }

        private class TestTableRef : TableRef
        {
            public TestTableRef(TableDefinition table) : base(table)
            {
            }
        }

        private class TestEntity
        {
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }
    }
}
