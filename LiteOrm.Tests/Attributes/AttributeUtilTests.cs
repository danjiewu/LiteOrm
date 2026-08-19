using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class AttributeUtilTests
    {
        [Fact]
        public void SortProperty_PrioritizesBeforeAndAfterThenAppliesOrder()
        {
            Dictionary<string, PropertyInfo> properties = typeof(PropertyOrderTestModel)
                .GetProperties()
                .ToDictionary(property => property.Name);

            List<PropertyInfo> sortedProperties = new List<PropertyInfo>
            {
                properties[nameof(PropertyOrderTestModel.Epsilon)],
                properties[nameof(PropertyOrderTestModel.Delta)],
                properties[nameof(PropertyOrderTestModel.Gamma)],
                properties[nameof(PropertyOrderTestModel.Alpha)],
                properties[nameof(PropertyOrderTestModel.Beta)]
            };

            sortedProperties.SortProperty();

            Assert.Equal(
                new[]
                {
                    nameof(PropertyOrderTestModel.Beta),
                    nameof(PropertyOrderTestModel.Alpha),
                    nameof(PropertyOrderTestModel.Gamma),
                    nameof(PropertyOrderTestModel.Delta),
                    nameof(PropertyOrderTestModel.Epsilon)
                },
                sortedProperties.Select(property => property.Name));
        }

        [Fact]
        public void SortProperty_WhenCircularDependencyExists_ThrowsInvalidOperationException()
        {
            List<PropertyInfo> properties = typeof(CircularPropertyOrderTestModel)
                .GetProperties()
                .ToList();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => properties.SortProperty());

            Assert.Contains("circular property order dependency", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SortProperty_IndexerDoesNotParticipate_NoFalseCircularOnDuplicateItemName()
        {
            List<PropertyInfo> properties = typeof(DuplicateItemNameTestModel)
                .GetProperties()
                .ToList();

            Assert.Equal(2, properties.Count(property => property.Name == nameof(DuplicateItemNameTestModel.Item)));
            Assert.Single(properties, property => property.GetIndexParameters().Length > 0);

            List<PropertyInfo> sortedProperties = properties.SortProperty() ?? new List<PropertyInfo>();

            Assert.Equal(properties.Count, sortedProperties.Count);
            Assert.Equal(
                properties.Select(property => property.Name).OrderBy(name => name),
                sortedProperties.Select(property => property.Name).OrderBy(name => name));

            int originalIndex = properties.FindIndex(property => property.GetIndexParameters().Length > 0);
            int sortedIndex = sortedProperties.FindIndex(property => property.GetIndexParameters().Length > 0);
            Assert.Equal(originalIndex, sortedIndex);
            Assert.Same(properties[originalIndex], sortedProperties[sortedIndex]);
        }

        [Fact]
        public void SortProperty_IndexerDoesNotParticipate_AppliesOrderingWithoutFalseCycle()
        {
            List<PropertyInfo> properties = typeof(DuplicateItemNameWithOrderTestModel)
                .GetProperties()
                .ToList();

            List<PropertyInfo> sortedProperties = properties.SortProperty() ?? new List<PropertyInfo>();

            Assert.Equal(properties.Count, sortedProperties.Count);

            int itemIndex = sortedProperties.FindIndex(property => property.Name == nameof(DuplicateItemNameWithOrderTestModel.Item) && property.GetIndexParameters().Length == 0);
            int dataIndex = sortedProperties.FindIndex(property => property.Name == nameof(DuplicateItemNameWithOrderTestModel.Data));

            Assert.NotEqual(-1, itemIndex);
            Assert.NotEqual(-1, dataIndex);
            Assert.True(itemIndex < dataIndex);
        }

        private class PropertyOrderTestModel
        {
            [PropertyOrder(2)]
            public string? Alpha { get; set; }

            [PropertyOrder(1)]
            public string? Beta { get; set; }

            [PropertyOrder(10, After = nameof(Beta))]
            public string? Gamma { get; set; }

            [PropertyOrder(100, Before = nameof(Epsilon))]
            public string? Delta { get; set; }

            [PropertyOrder(0)]
            public string? Epsilon { get; set; }
        }

        private class CircularPropertyOrderTestModel
        {
            [PropertyOrder(Before = nameof(Second))]
            public string? First { get; set; }

            [PropertyOrder(Before = nameof(First))]
            public string? Second { get; set; }
        }

        private class IndexerItemTestBase
        {
            public object? this[int i] => i;
        }

        private class DuplicateItemNameTestModel : IndexerItemTestBase
        {
            public string? Item { get; set; }

            public string? Data { get; set; }
        }

        private class DuplicateItemNameWithOrderTestModel : IndexerItemTestBase
        {
            [PropertyOrder(Before = nameof(Data))]
            public string? Item { get; set; }

            public string? Data { get; set; }
        }
    }
}
