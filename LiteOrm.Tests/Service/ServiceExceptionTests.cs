using System;

using LiteOrm.Service;
using Xunit;

namespace LiteOrm.Service.UnitTests
{
    public class ServiceExceptionTests
    {
        [Fact]
        public void Constructor_Default_CreatesException()
        {
            var ex = new ServiceException();

            Assert.NotNull(ex.Message);
        }

        [Fact]
        public void Constructor_WithMessage_SetsMessage()
        {
            var ex = new ServiceException("failed");

            Assert.Equal("failed", ex.Message);
        }

        [Fact]
        public void Constructor_WithMessageAndInner_SetsBoth()
        {
            var inner = new InvalidOperationException("inner");
            var ex = new ServiceException("failed", inner);

            Assert.Equal("failed", ex.Message);
            Assert.Same(inner, ex.InnerException);
        }
    }
}
