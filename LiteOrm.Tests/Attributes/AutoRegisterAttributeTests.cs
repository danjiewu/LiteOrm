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
            Assert.Equal(RegisterPolicy.All, attribute.Policy);
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
            Assert.Equal(RegisterPolicy.All, attribute.Policy);
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

        [Theory]
        [InlineData(RegisterPolicy.All)]
        [InlineData(RegisterPolicy.Self)]
        [InlineData(RegisterPolicy.Interface)]
        public void Constructor_WithServiceTypes_SetsServiceTypes(RegisterPolicy serviceTypes)
        {
            var attribute = new AutoRegisterAttribute(serviceTypes);

            Assert.Equal(serviceTypes, attribute.Policy);
        }

        [Theory]
        [InlineData(Lifetime.Scoped, RegisterPolicy.Self)]
        [InlineData(Lifetime.Transient, RegisterPolicy.Interface)]
        [InlineData(Lifetime.Singleton, RegisterPolicy.All)]
        public void Constructor_WithLifetimeAndServiceTypes_SetsBoth(Lifetime lifetime, RegisterPolicy serviceTypes)
        {
            var attribute = new AutoRegisterAttribute(lifetime, serviceTypes);

            Assert.Equal(lifetime, attribute.Lifetime);
            Assert.Equal(serviceTypes, attribute.Policy);
        }
    }
}