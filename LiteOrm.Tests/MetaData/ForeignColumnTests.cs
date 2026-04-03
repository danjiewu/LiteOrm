using System;
using System.Collections.Generic;
using System.Reflection;

using LiteOrm.Common;
using Moq;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class ForeignColumnTests
    {
        [Fact]
        public void Name_WhenTargetColumnIsNull_ReturnsNull()
        {
            var foreignColumn = CreateForeignColumn();

            Assert.Null(foreignColumn.Name);
        }

        [Fact]
        public void Name_WhenTargetColumnSet_ReturnsTargetColumnName()
        {
            var target = CreateColumn(nameof(TestEntity.Name));
            var foreignColumn = CreateForeignColumn();
            SetTargetColumn(foreignColumn, new ColumnRef(target));

            Assert.Equal(target.Name, foreignColumn.Name);
        }

        [Fact]
        public void Definition_WhenTargetColumnSet_ReturnsTargetDefinition()
        {
            var target = CreateColumn(nameof(TestEntity.Name));
            var foreignColumn = CreateForeignColumn();
            SetTargetColumn(foreignColumn, new ColumnRef(target));

            Assert.Same(target.Definition, foreignColumn.Definition);
        }

        private static ForeignColumn CreateForeignColumn()
        {
            return (ForeignColumn)Activator.CreateInstance(
                typeof(ForeignColumn),
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { typeof(TestEntity).GetProperty(nameof(TestEntity.Name))! },
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

        private static void SetTargetColumn(ForeignColumn foreignColumn, ColumnRef target)
        {
            typeof(ForeignColumn)
                .GetProperty(nameof(ForeignColumn.TargetColumn), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .SetValue(foreignColumn, target);
        }

        private class TestEntity
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
