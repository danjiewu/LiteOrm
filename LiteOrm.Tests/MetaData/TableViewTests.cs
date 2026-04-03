using System;
using System.Collections.Generic;
using System.Reflection;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class TableViewTests
    {
        [Fact]
        public void Constructor_SetsDefinitionAndColumns()
        {
            var definition = CreateTableDefinition(typeof(TestEntity));
            var column = CreateColumn(nameof(TestEntity.Name));
            var view = new TableView(definition, new List<JoinedTable>(), new SqlColumn[] { column });

            Assert.Same(definition, view.Definition);
            Assert.Single(view.Columns);
            Assert.Same(column, view.Columns[0]);
        }

        [Fact]
        public void JoinedTables_WithEmptyList_ReturnsCachedEmptyCollection()
        {
            var definition = CreateTableDefinition(typeof(TestEntity));
            var view = new TableView(definition, new List<JoinedTable>(), Array.Empty<SqlColumn>());

            var first = view.JoinedTables;
            var second = view.JoinedTables;

            Assert.Empty(first);
            Assert.Same(first, second);
        }

        private static TableDefinition CreateTableDefinition(Type type)
        {
            return (TableDefinition)Activator.CreateInstance(
                typeof(TableDefinition),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { type, new List<ColumnDefinition>() },
                culture: null)!;
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

        private class TestEntity
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
