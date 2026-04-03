using System;
using System.Collections.Generic;
using System.Reflection;

using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class SqlColumnTests
    {
        [Fact]
        public void Constructor_InitializesNamePropertyNameAndPropertyType()
        {
            var column = CreateColumn(nameof(TestEntity.Name));

            Assert.Equal(nameof(TestEntity.Name), column.Name);
            Assert.Equal(nameof(TestEntity.Name), column.PropertyName);
            Assert.Equal(typeof(string), column.PropertyType);
            Assert.Equal(typeof(TestEntity).GetProperty(nameof(TestEntity.Name)), column.Property);
        }

        [Fact]
        public void GetValue_WhenTargetProvided_ReturnsPropertyValue()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var entity = new TestEntity { Name = "Alice" };

            var result = column.GetValue(entity);

            Assert.Equal("Alice", result);
        }

        [Fact]
        public void GetValue_WhenTargetIsNull_ThrowsArgumentNullException()
        {
            var column = CreateColumn(nameof(TestEntity.Name));

            Assert.Throws<ArgumentNullException>(() => column.GetValue(null));
        }

        [Fact]
        public void SetValue_WhenTargetProvided_UpdatesProperty()
        {
            var column = CreateColumn(nameof(TestEntity.Name));
            var entity = new TestEntity();

            column.SetValue(entity, "Bob");

            Assert.Equal("Bob", entity.Name);
        }

        [Fact]
        public void SetValue_WhenTargetIsNull_ThrowsArgumentNullException()
        {
            var column = CreateColumn(nameof(TestEntity.Name));

            Assert.Throws<ArgumentNullException>(() => column.SetValue(null, "Bob"));
        }

        [Fact]
        public void SetValue_WhenValueTypeIsInvalid_ThrowsInvalidOperationException()
        {
            var column = CreateColumn(nameof(TestEntity.Age));
            var entity = new TestEntity();

            Assert.Throws<InvalidOperationException>(() => column.SetValue(entity, "invalid"));
        }

        [Fact]
        public void Equals_WithSameTableAndPropertyName_ReturnsTrue()
        {
            var left = CreateColumn(nameof(TestEntity.Name));
            var right = CreateColumn(nameof(TestEntity.Name));
            _ = new TableView(
                (TableDefinition)Activator.CreateInstance(
                    typeof(TableDefinition),
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    args: new object[] { typeof(TestEntity), new List<ColumnDefinition>() },
                    culture: null)!,
                new List<JoinedTable>(),
                new SqlColumn[] { left, right });

            Assert.True(left.Equals(right));
        }

        [Fact]
        public void ForeignAlias_WhenForeignTableIsNull_ReturnsNull()
        {
            var column = CreateColumn(nameof(TestEntity.Name));

            Assert.Null(column.ForeignAlias);
            Assert.Null(column.ForeignType);
        }

        private static ColumnDefinition CreateColumn(string propertyName)
        {
            var property = typeof(TestEntity).GetProperty(propertyName)!;
            return (ColumnDefinition)Activator.CreateInstance(
                typeof(ColumnDefinition),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { property },
                culture: null)!;
        }

        private static Mock<SqlTable> CreateTableMock()
        {
            var mock = new Mock<SqlTable>(new List<SqlColumn>()) { CallBase = true };
            var definition = (TableDefinition)Activator.CreateInstance(
                typeof(TableDefinition),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { typeof(TestEntity), new List<ColumnDefinition>() },
                culture: null)!;
            mock.SetupGet(t => t.Definition).Returns(definition);
            return mock;
        }

        private static void SetTable(SqlColumn column, SqlTable table)
        {
            typeof(SqlColumn)
                .GetProperty(nameof(SqlColumn.Table), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(column, table);
        }

        private class TestEntity
        {
            public string Name { get; set; } = string.Empty;
            public int Age { get; set; }
        }
    }
}
