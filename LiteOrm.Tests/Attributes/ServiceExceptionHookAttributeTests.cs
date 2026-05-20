using LiteOrm;
using LiteOrm.Service;
using System;
using Xunit;

namespace LiteOrm.UnitTests
{
    public class ServiceExceptionHookAttributeTests
    {
        [Fact]
        public void Constructor_WithHookType_SetsDefaults()
        {
            var attribute = new ExceptionHookAttribute(typeof(TestHook));

            Assert.Equal(typeof(TestHook), attribute.HookType);
            Assert.Equal(ServiceExceptionHookMode.Notify, attribute.Mode);
        }

        [Fact]
        public void Constructor_WithNullHookType_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new ExceptionHookAttribute(null));
        }

        [Fact]
        public void Constructor_WithNonHookType_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ExceptionHookAttribute(typeof(string)));
        }

        private class TestHook : IServiceExceptionHook
        {
            public void OnException(ServiceExceptionContext context)
            {
            }
        }
    }
}
