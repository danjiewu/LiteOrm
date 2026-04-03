using System;

using LiteOrm.Common;
using Xunit;

namespace LiteOrm.Common.UnitTests
{
    public class AutoRegisterAttributeTests
    {
        [Fact]
        public void DefaultConstructor_SetsExpectedDefaults()
        {
            var attribute = new AutoRegisterAttribute();

            Assert.Equal(Lifetime.Singleton, attribute.Lifetime);
            Assert.True(attribute.Enabled);
            Assert.Null(attribute.ServiceTypes);
            Assert.Null(attribute.Key);
            Assert.False(attribute.AutoActivate);
        }

        [Theory]
        [InlineData(Lifetime.Singleton)]
        [InlineData(Lifetime.Scoped)]
        [InlineData(Lifetime.Transient)]
        public void Constructor_WithLifetime_SetsLifetime(Lifetime lifetime)
        {
            var attribute = new AutoRegisterAttribute(lifetime);

            Assert.Equal(lifetime, attribute.Lifetime);
            Assert.Null(attribute.ServiceTypes);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Constructor_WithEnabled_SetsEnabled(bool enabled)
        {
            var attribute = new AutoRegisterAttribute(enabled);

            Assert.Equal(enabled, attribute.Enabled);
            Assert.Equal(Lifetime.Singleton, attribute.Lifetime);
        }

        [Fact]
        public void Constructor_WithServiceTypes_SetsArrayReference()
        {
            var serviceTypes = new[] { typeof(string), typeof(int) };
            var attribute = new AutoRegisterAttribute(serviceTypes);

            Assert.Same(serviceTypes, attribute.ServiceTypes);
        }

        [Fact]
        public void Constructor_WithLifetimeAndServiceTypes_SetsBoth()
        {
            var serviceTypes = new[] { typeof(string) };
            var attribute = new AutoRegisterAttribute(Lifetime.Scoped, serviceTypes);

            Assert.Equal(Lifetime.Scoped, attribute.Lifetime);
            Assert.Same(serviceTypes, attribute.ServiceTypes);
        }

        [Fact]
        public void Constructor_WithNullServiceTypes_LeavesServiceTypesNull()
        {
            var attribute = new AutoRegisterAttribute(serviceTypes: null);

            Assert.Null(attribute.ServiceTypes);
        }
    }
}
