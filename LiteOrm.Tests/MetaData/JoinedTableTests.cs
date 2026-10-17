using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class JoinedTableTests
    {
        [Fact]
        public void Constructor_SetsNameAndTableDefinition()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var definition = CreateTableDefinition(typeof(TestEntity), "Users", column);
            var table = new JoinedTable(definition, new[] { column });

            Assert.Equal("Users", table.Name);
            Assert.Same(definition, table.TableDefinition);
            Assert.Equal(TableJoinType.Left, table.JoinType);
            Assert.False(table.Used);
            Assert.Single(table.ForeignPrimeKeys);
            Assert.Same(column, table.ForeignPrimeKeys[0].Column);
            Assert.Same(table, table.ForeignPrimeKeys[0].Table);
        }

        [Fact]
        public void Constructor_WithNullDefinition_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new JoinedTable(null!));
        }

        [Fact]
        public void Constructor_WithNoPrimeKeys_ThrowsArgumentException()
        {
            var definition = CreateTableDefinition(typeof(TestEntity), "Users");

            Assert.Throws<ArgumentException>(() => new JoinedTable(definition, Array.Empty<ColumnDefinition>()));
        }

        [Fact]
        public void GetColumn_WithNullOrEmpty_ReturnsNull()
        {
            var table = CreateJoinedTable(CreateTableDefinition(typeof(TestEntity), "Users", CreateColumn(nameof(TestEntity.Name))));

            Assert.Null(table.GetColumn(null!));
            Assert.Null(table.GetColumn(string.Empty));
        }

        [Fact]
        public void GetColumn_WithExistingPropertyName_ReturnsColumnRef()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var table = CreateJoinedTable(CreateTableDefinition(typeof(TestEntity), "Users", column));

            var result = table.GetColumn(nameof(TestEntity.Name));

            Assert.NotNull(result);
            Assert.Same(column, result!.Column);
            Assert.Same(table, result.Table);
        }

        [Fact]
        public void GetColumn_WithUnknownPropertyName_ReturnsNull()
        {
            var table = CreateJoinedTable(CreateTableDefinition(typeof(TestEntity), "Users", CreateColumn(nameof(TestEntity.Name))));

            Assert.Null(table.GetColumn(nameof(TestEntity.Age)));
        }

        [Fact]
        public void GetColumn_IsCaseSensitive()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var table = CreateJoinedTable(CreateTableDefinition(typeof(TestEntity), "Users", column));

            Assert.Null(table.GetColumn("name"));
            Assert.NotNull(table.GetColumn("Name"));
        }

        [Fact]
        public void Equals_WithSameDefinitionAndName_ReturnsTrue()
        {
            var definition = CreateTableDefinition(typeof(TestEntity), "Users", CreateColumn(nameof(TestEntity.Name)));
            var left = CreateJoinedTable(definition);
            var right = CreateJoinedTable(definition);

            Assert.True(left.Equals(right));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void Equals_WithDifferentNames_ReturnsFalse()
        {
            var definition = CreateTableDefinition(typeof(TestEntity), "Users", CreateColumn(nameof(TestEntity.Name)));
            var left = CreateJoinedTable(definition);
            var right = CreateJoinedTable(definition);
            right.Name = "Other";

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void Equals_WithDifferentDefinitions_ReturnsFalse()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var left = CreateJoinedTable(CreateTableDefinition(typeof(TestEntity), "Users", column));

            var otherColumn = (ColumnDefinition)Activator.CreateInstance(
                typeof(ColumnDefinition),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { typeof(OtherEntity).GetProperty(nameof(OtherEntity.Name))! },
                culture: null)!;
            var right = CreateJoinedTable(CreateTableDefinition(typeof(OtherEntity), "Users", otherColumn));

            Assert.False(left.Equals(right));
        }

        [Fact]
        public void ForeignKeys_WithMismatchedCount_ThrowsArgumentException()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var table = CreateJoinedTable(CreateTableDefinition(typeof(TestEntity), "Users", column));
            var setter = typeof(JoinedTable)
                .GetProperty(nameof(JoinedTable.ForeignKeys), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .GetSetMethod(true)!;

            var keys = new ReadOnlyCollection<ColumnRef>(new List<ColumnRef>
            {
                new ColumnRef(table, column),
                new ColumnRef(table, column)
            });

            Assert.Throws<TargetInvocationException>(() => setter.Invoke(table, new object[] { keys }));
        }

        [Fact]
        public void NamedColumnCache_ReturnsSameInstance()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var table = new TestJoinedTable(CreateTableDefinition(typeof(TestEntity), "Users", column), new[] { column });

            var first = table.GetNamedColumnCache();
            var second = table.GetNamedColumnCache();

            Assert.Same(first, second);
            Assert.Single(first);
            Assert.Same(table, first[nameof(TestEntity.Name)].Table);
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

        private static TableDefinition CreateTableDefinition(Type objectType, string name, params ColumnDefinition[] columns)
        {
            var table = (TableDefinition)Activator.CreateInstance(
                typeof(TableDefinition),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { objectType, new List<ColumnDefinition>(columns) },
                culture: null)!;
            table.Name = name;
            return table;
        }

        private static JoinedTable CreateJoinedTable(TableDefinition definition)
        {
            var primeKeys = new List<ColumnDefinition>();
            foreach (ColumnDefinition column in definition.Columns) primeKeys.Add(column);
            if (primeKeys.Count == 0) primeKeys.Add(CreateColumn(nameof(TestEntity.Name)));
            return new JoinedTable(definition, primeKeys);
        }

        private class TestJoinedTable : JoinedTable
        {
            public TestJoinedTable(TableDefinition table, IEnumerable<ColumnDefinition> primeKeys) : base(table, primeKeys)
            {
            }

            public ConcurrentDictionary<string, ColumnRef> GetNamedColumnCache() => NamedColumnCache;
        }

        private class TestEntity
        {
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }

        private class OtherEntity
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
