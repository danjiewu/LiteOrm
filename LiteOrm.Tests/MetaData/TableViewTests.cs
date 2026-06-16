using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
            var view = new TableView(definition, new List<SqlColumn> { column }, new List<JoinedTable>());

            Assert.Same(definition, view.Definition);
            Assert.Single(view.Columns);
            Assert.Same(column, view.Columns[0]);
        }

        [Fact]
        public void JoinedTables_WithEmptyList_ReturnsCachedEmptyCollection()
        {
            var definition = CreateTableDefinition(typeof(TestEntity));
            var view = new TableView(definition, Array.Empty<SqlColumn>(), new List<JoinedTable>());

            var first = view.JoinedTables;
            var second = view.JoinedTables;

            Assert.Empty(first);
            Assert.Same(first, second);
        }

        [Fact]
        public void JoinedTables_WithDependencyChain_ReturnsTopologicallySortedTables()
        {
            var definition = CreateTableDefinition(typeof(TestEntity));
            var tableA = CreateJoinedTable(typeof(TestEntityA));
            var tableB = CreateJoinedTable(typeof(TestEntityB));
            var tableC = CreateJoinedTable(typeof(TestEntityC));

            SetForeignKeys(tableB, tableA.GetColumn(nameof(TestEntityA.Id))!);
            SetForeignKeys(tableC, tableB.GetColumn(nameof(TestEntityB.Id))!);

            var view = new TableView(definition, Array.Empty<SqlColumn>(), new List<JoinedTable> { tableC, tableB, tableA });

            var joinedTables = view.JoinedTables;

            Assert.Equal(new[] { tableA, tableB, tableC }, joinedTables.ToArray());
        }

        [Fact]
        public void JoinedTables_WithCircularDependency_ThrowsInvalidOperationException()
        {
            var definition = CreateTableDefinition(typeof(TestEntity));
            var tableA = CreateJoinedTable(typeof(TestEntityA));
            var tableB = CreateJoinedTable(typeof(TestEntityB));

            SetForeignKeys(tableA, tableB.GetColumn(nameof(TestEntityB.Id))!);
            SetForeignKeys(tableB, tableA.GetColumn(nameof(TestEntityA.Id))!);

            var view = new TableView(definition, Array.Empty<SqlColumn>(), new List<JoinedTable> { tableA, tableB });

            Assert.Throws<InvalidOperationException>(() => _ = view.JoinedTables);
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

        private static JoinedTable CreateJoinedTable(Type type)
        {
            var keyColumn = (ColumnDefinition)Activator.CreateInstance(
                typeof(ColumnDefinition),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { type.GetProperty("Id")! },
                culture: null)!;
            var definition = (TableDefinition)Activator.CreateInstance(
                typeof(TableDefinition),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { type, new List<ColumnDefinition> { keyColumn } },
                culture: null)!;
            return new JoinedTable(definition, new[] { keyColumn });
        }

        private static void SetForeignKeys(JoinedTable table, params ColumnRef[] foreignKeys)
        {
            var setter = typeof(JoinedTable)
                .GetProperty(nameof(JoinedTable.ForeignKeys), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .GetSetMethod(true)!;
            setter.Invoke(table, new object[] { new ReadOnlyCollection<ColumnRef>(foreignKeys.ToList()) });
        }

        private class TestEntity
        {
            public string Name { get; set; } = string.Empty;
        }

        private class TestEntityA
        {
            public int Id { get; set; }
        }

        private class TestEntityB
        {
            public int Id { get; set; }
        }

        private class TestEntityC
        {
            public int Id { get; set; }
        }
    }
}
