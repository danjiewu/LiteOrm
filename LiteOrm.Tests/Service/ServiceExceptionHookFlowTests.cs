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
        public void NotifyHook_ShouldObserveAndRethrow()
        {
            var recorder = ServiceProvider.GetRequiredService<ExceptionHookRecorder>();
            var service = ServiceProvider.GetRequiredService<IExceptionHookTestService>();

            var ex = Assert.Throws<InvalidOperationException>(() => service.ThrowWithNotifyHook());

            Assert.Equal("notify", ex.Message);
            Assert.Equal(1, recorder.NotifyCount);
        }

        [Fact]
        public void HandleHook_ShouldReturnHandledResult()
        {
            var recorder = ServiceProvider.GetRequiredService<ExceptionHookRecorder>();
            var service = ServiceProvider.GetRequiredService<IExceptionHookTestService>();

            var result = service.ThrowWithHandleHook();

            Assert.Equal(123, result);
            Assert.Equal(1, recorder.HandleCount);
        }

        [Fact]
        public async Task HandleHookAsync_ShouldReturnHandledResult()
        {
            var recorder = ServiceProvider.GetRequiredService<ExceptionHookRecorder>();
            var service = ServiceProvider.GetRequiredService<IExceptionHookTestService>();

            var result = await service.ThrowAsyncWithHandleHook();

            Assert.Equal(456, result);
            Assert.Equal(1, recorder.AsyncHandleCount);
        }

        [Fact]
        public void GlobalExceptionHandlingEvent_ShouldHandleException()
        {
            var service = ServiceProvider.GetRequiredService<IExceptionHookTestService>();
            EventHandler<ServiceExceptionContext> handler = (sender, context) =>
            {
                if (context.MethodName == nameof(IExceptionHookTestService.ThrowWithGlobalHandler))
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
        public void NotifyHook_HandlingException_ShouldThrowInvalidOperationException()
        {
            var service = ServiceProvider.GetRequiredService<IExceptionHookTestService>();

            var ex = Assert.Throws<InvalidOperationException>(() => service.ThrowWithInvalidNotifyHook());

            Assert.Contains("configured as Notify", ex.Message);
        }

        [Fact]
        public void NestedServiceCall_ShouldRethrowOriginalException()
        {
            var service = ServiceProvider.GetRequiredService<IOuterExceptionHookTestService>();

            var ex = Assert.Throws<InvalidOperationException>(() => service.CallInnerThrow());

            Assert.Equal("nested", ex.Message);
        }
    }

    public interface IExceptionHookTestService
    {
        int ThrowWithHandleHook();
        void ThrowWithNotifyHook();
        Task<int> ThrowAsyncWithHandleHook();
        int ThrowWithGlobalHandler();
        int ThrowWithInvalidNotifyHook();
    }

    public interface IOuterExceptionHookTestService
    {
        void CallInnerThrow();
    }

    [AutoRegister(Lifetime = Lifetime.Scoped)]
    [Intercept(typeof(ServiceInvokeInterceptor))]
    public class ExceptionHookTestService : IExceptionHookTestService
    {
        [ExceptionHook(typeof(NotifyOnlyHook), Mode = ServiceExceptionHookMode.Notify)]
        public void ThrowWithNotifyHook()
        {
            throw new InvalidOperationException("notify");
        }

        [ExceptionHook(typeof(HandleHook), Mode = ServiceExceptionHookMode.Handle)]
        public int ThrowWithHandleHook()
        {
            throw new InvalidOperationException("handle");
        }

        [ExceptionHook(typeof(AsyncHandleHook), Mode = ServiceExceptionHookMode.Handle)]
        public Task<int> ThrowAsyncWithHandleHook()
        {
            throw new InvalidOperationException("async");
        }

        public int ThrowWithGlobalHandler()
        {
            throw new InvalidOperationException("global");
        }

        [ExceptionHook(typeof(InvalidNotifyHook), Mode = ServiceExceptionHookMode.Notify)]
        public int ThrowWithInvalidNotifyHook()
        {
            throw new InvalidOperationException("invalid");
        }
    }

    [AutoRegister(Lifetime = Lifetime.Scoped)]
    [Intercept(typeof(ServiceInvokeInterceptor))]
    public class OuterExceptionHookTestService : IOuterExceptionHookTestService
    {
        private readonly IInnerExceptionHookTestService _innerService;

        public OuterExceptionHookTestService(IInnerExceptionHookTestService innerService)
        {
            _innerService = innerService;
        }

        public void CallInnerThrow()
        {
            _innerService.ThrowNested();
        }
    }

    public interface IInnerExceptionHookTestService
    {
        void ThrowNested();
    }

    [AutoRegister(Lifetime = Lifetime.Scoped)]
    [Intercept(typeof(ServiceInvokeInterceptor))]
    public class InnerExceptionHookTestService : IInnerExceptionHookTestService
    {
        public void ThrowNested()
        {
            throw new InvalidOperationException("nested");
        }
    }

    [AutoRegister(Lifetime = Lifetime.Scoped)]
    public class ExceptionHookRecorder
    {
        public int NotifyCount { get; set; }
        public int HandleCount { get; set; }
        public int AsyncHandleCount { get; set; }
    }

    [AutoRegister(Lifetime.Scoped, typeof(IServiceExceptionHook))]
    public class NotifyOnlyHook : IServiceExceptionHook
    {
        private readonly ExceptionHookRecorder _recorder;

        public NotifyOnlyHook(ExceptionHookRecorder recorder)
        {
            _recorder = recorder;
        }

        public void OnException(ServiceExceptionContext context)
        {
            _recorder.NotifyCount++;
        }
    }

    [AutoRegister(Lifetime.Scoped, typeof(IServiceExceptionHook))]
    public class HandleHook : IServiceExceptionHook
    {
        private readonly ExceptionHookRecorder _recorder;

        public HandleHook(ExceptionHookRecorder recorder)
        {
            _recorder = recorder;
        }

        public void OnException(ServiceExceptionContext context)
        {
            _recorder.HandleCount++;
            context.Handle(123);
        }
    }

    [AutoRegister(Lifetime.Scoped, typeof(IServiceExceptionHook))]
    public class AsyncHandleHook : IServiceExceptionHook
    {
        private readonly ExceptionHookRecorder _recorder;

        public AsyncHandleHook(ExceptionHookRecorder recorder)
        {
            _recorder = recorder;
        }

        public void OnException(ServiceExceptionContext context)
        {
            _recorder.AsyncHandleCount++;
            context.Handle(456);
        }
    }

    [AutoRegister(Lifetime.Scoped, typeof(IServiceExceptionHook))]
    public class InvalidNotifyHook : IServiceExceptionHook
    {
        public void OnException(ServiceExceptionContext context)
        {
            context.Handle(999);
        }
    }
}
