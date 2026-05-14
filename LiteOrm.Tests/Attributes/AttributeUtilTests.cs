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

        private class PropertyOrderTestModel
        {
            [PropertyOrder(2)]
            public string Alpha { get; set; }

            [PropertyOrder(1)]
            public string Beta { get; set; }

            [PropertyOrder(10, After = nameof(Beta))]
            public string Gamma { get; set; }

            [PropertyOrder(100, Before = nameof(Epsilon))]
            public string Delta { get; set; }

            [PropertyOrder(0)]
            public string Epsilon { get; set; }
        }

        private class CircularPropertyOrderTestModel
        {
            [PropertyOrder(Before = nameof(Second))]
            public string First { get; set; }

            [PropertyOrder(Before = nameof(First))]
            public string Second { get; set; }
        }
    }
}
