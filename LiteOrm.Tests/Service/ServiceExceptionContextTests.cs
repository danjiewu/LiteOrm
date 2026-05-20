using LiteOrm.Service;
using System;
using System.Reflection;
using Xunit;

namespace LiteOrm.Service.UnitTests
{
    public class ServiceExceptionContextTests
    {
        [Fact]
        public void Handle_WithResult_SetsHandledState()
        {
            var context = CreateContext(typeof(int));

            context.Handle(123);

            Assert.True(context.Handled);
            Assert.True(context.ResultAssigned);
            Assert.Equal(123, context.Result);
        }

        [Fact]
        public void Handle_WithoutResult_OnValueReturningMethod_ThrowsInvalidOperationException()
        {
            var context = CreateContext(typeof(int));

            Assert.Throws<InvalidOperationException>(() => context.Handle());
        }

        [Fact]
        public void Handle_WithIncompatibleResult_ThrowsInvalidOperationException()
        {
            var context = CreateContext(typeof(int));

            Assert.Throws<InvalidOperationException>(() => context.Handle("bad"));
        }

        [Fact]
        public void Handle_WithoutResult_OnVoidMethod_Succeeds()
        {
            var context = CreateContext(null, typeof(void), nameof(DummyVoidMethod));

            context.Handle();

            Assert.True(context.Handled);
            Assert.False(context.ResultAssigned);
        }

        private static ServiceExceptionContext CreateContext(Type resultType, Type methodReturnType = null, string methodName = nameof(DummyMethod))
        {
            return new ServiceExceptionContext(
                new InvalidOperationException("boom"),
                new object(),
                typeof(ServiceExceptionContextTests),
                nameof(ServiceExceptionContextTests),
                typeof(ServiceExceptionContextTests).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static),
                Array.Empty<object>(),
                Array.Empty<object>(),
                "session",
                Array.Empty<string>(),
                methodReturnType ?? resultType,
                resultType);
        }

        private static int DummyMethod() => 0;
        private static void DummyVoidMethod() { }
    }
}
