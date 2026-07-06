using Autofac.Extras.DynamicProxy;
using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests.Service
{
    [Collection("Database")]
    public class ServiceExceptionHookFlowTests : TestBase
    {
        public ServiceExceptionHookFlowTests(DatabaseFixture fixture) : base(fixture)
        {
        }

        [Fact]
        public void GlobalExceptionHandlingEvent_ShouldHandleExceptionWithResult()
        {
            var service = ServiceProvider.GetRequiredService<IExceptionHandlingTestService>();
            EventHandler<ServiceExceptionContext> handler = (sender, context) =>
            {
                if (context.MethodName == nameof(IExceptionHandlingTestService.ThrowWithGlobalHandler))
                    context.Handle(789);
            };

            ServiceInvokeInterceptor.ExceptionHandling += handler;
            try
            {
                var result = service.ThrowWithGlobalHandler();

                Assert.Equal(789, result);
            }
            finally
            {
                ServiceInvokeInterceptor.ExceptionHandling -= handler;
            }
        }

        [Fact]
        public void GlobalExceptionHandlingEvent_ShouldHandleVoidException()
        {
            var service = ServiceProvider.GetRequiredService<IExceptionHandlingTestService>();
            EventHandler<ServiceExceptionContext> handler = (sender, context) =>
            {
                if (context.MethodName == nameof(IExceptionHandlingTestService.ThrowVoid))
                    context.Handle();
            };

            ServiceInvokeInterceptor.ExceptionHandling += handler;
            try
            {
                service.ThrowVoid();
            }
            finally
            {
                ServiceInvokeInterceptor.ExceptionHandling -= handler;
            }
        }

        [Fact]
        public async Task GlobalExceptionHandlingEvent_ShouldHandleAsyncExceptionWithResult()
        {
            var service = ServiceProvider.GetRequiredService<IExceptionHandlingTestService>();
            EventHandler<ServiceExceptionContext> handler = (sender, context) =>
            {
                if (context.MethodName == nameof(IExceptionHandlingTestService.ThrowAsyncWithGlobalHandler))
                    context.Handle(456);
            };

            ServiceInvokeInterceptor.ExceptionHandling += handler;
            try
            {
                var result = await service.ThrowAsyncWithGlobalHandler();

                Assert.Equal(456, result);
            }
            finally
            {
                ServiceInvokeInterceptor.ExceptionHandling -= handler;
            }
        }

        [Fact]
        public void UnhandledException_ShouldRethrow()
        {
            var service = ServiceProvider.GetRequiredService<IExceptionHandlingTestService>();

            var ex = Assert.Throws<InvalidOperationException>(() => service.ThrowUnhandled());

            Assert.Equal("unhandled", ex.Message);
        }

        [Fact]
        public void NestedServiceCall_ShouldRethrowOriginalException()
        {
            var service = ServiceProvider.GetRequiredService<IOuterExceptionHandlingTestService>();

            var ex = Assert.Throws<InvalidOperationException>(() => service.CallInnerThrow());

            Assert.Equal("nested", ex.Message);
        }
    }

    public interface IExceptionHandlingTestService
    {
        int ThrowWithGlobalHandler();
        void ThrowVoid();
        Task<int> ThrowAsyncWithGlobalHandler();
        int ThrowUnhandled();
    }

    public interface IOuterExceptionHandlingTestService
    {
        void CallInnerThrow();
    }

    [AutoRegister(Lifetime = Lifetime.Scoped)]
    [Intercept(typeof(ServiceInvokeInterceptor))]
    public class ExceptionHandlingTestService : IExceptionHandlingTestService
    {
        public int ThrowWithGlobalHandler()
        {
            throw new InvalidOperationException("global");
        }

        public void ThrowVoid()
        {
            throw new InvalidOperationException("void");
        }

        public Task<int> ThrowAsyncWithGlobalHandler()
        {
            throw new InvalidOperationException("async");
        }

        public int ThrowUnhandled()
        {
            throw new InvalidOperationException("unhandled");
        }
    }

    [AutoRegister(Lifetime = Lifetime.Scoped)]
    [Intercept(typeof(ServiceInvokeInterceptor))]
    public class OuterExceptionHandlingTestService : IOuterExceptionHandlingTestService
    {
        private readonly IInnerExceptionHandlingTestService _innerService;

        public OuterExceptionHandlingTestService(IInnerExceptionHandlingTestService innerService)
        {
            _innerService = innerService;
        }

        public void CallInnerThrow()
        {
            _innerService.ThrowNested();
        }
    }

    public interface IInnerExceptionHandlingTestService
    {
        void ThrowNested();
    }

    [AutoRegister(Lifetime = Lifetime.Scoped)]
    [Intercept(typeof(ServiceInvokeInterceptor))]
    public class InnerExceptionHandlingTestService : IInnerExceptionHandlingTestService
    {
        public void ThrowNested()
        {
            throw new InvalidOperationException("nested");
        }
    }
}
