using LiteOrm.Common;
using LiteOrm.DependencyInjection;
using LiteOrm.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests.Service
{
    public class ServiceExceptionHookFlowTests
    {
        private static IHost BuildHost(Action<IServiceCollection> configureServices)
        {
            return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(cfg =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["LiteOrm:Default"] = "SQLite",
                        ["LiteOrm:DataSources:0:Name"] = "SQLite",
                        ["LiteOrm:DataSources:0:ConnectionString"] = "Data Source=excevent.db",
                        ["LiteOrm:DataSources:0:Provider"] = "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite",
                        ["LiteOrm:DataSources:0:SyncTable"] = "true"
                    });
                })
                .RegisterLiteOrm()
                .ConfigureServices(configureServices)
                .Build();
        }

        private static Action<IServiceCollection> ConfigureExceptionEvent() => s =>
        {
            s.AddScoped<RecordingExceptionEvent>();
            s.AddScoped<IServiceExceptionEvent>(sp => sp.GetRequiredService<RecordingExceptionEvent>());
        };

        /// <summary>
        /// 记录异常事件回调调用的测试订阅者。
        /// </summary>
        private sealed class RecordingExceptionEvent : IServiceExceptionEvent
        {
            public List<string> Calls { get; } = new List<string>();

            public void OnException(ServiceExceptionContext context) => Calls.Add(context.MethodName);
        }

        [Fact]
        public async Task ServiceExceptionEvent_ShouldFireAndRethrow()
        {
            using var host = BuildHost(ConfigureExceptionEvent());
            await host.StartAsync(TestContext.Current.CancellationToken);
            try
            {
                using var scope = host.Services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IExceptionHandlingTestService>();
                var evt = scope.ServiceProvider.GetRequiredService<RecordingExceptionEvent>();

                var ex = Assert.Throws<InvalidOperationException>(() => service.ThrowUnhandled());
                Assert.Equal("unhandled", ex.Message);
                Assert.Contains(nameof(IExceptionHandlingTestService.ThrowUnhandled), evt.Calls);

                evt.Calls.Clear();
                var asyncEx = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ThrowAsyncWithGlobalHandler());
                Assert.Equal("async", asyncEx.Message);
                Assert.Contains(nameof(IExceptionHandlingTestService.ThrowAsyncWithGlobalHandler), evt.Calls);
            }
            finally
            {
                await host.StopAsync(TestContext.Current.CancellationToken);
                host.Dispose();
            }
        }

        [Fact]
        public async Task ServiceExceptionEvent_Nested_ShouldNotifyAndRethrowOriginal()
        {
            using var host = BuildHost(ConfigureExceptionEvent());
            await host.StartAsync(TestContext.Current.CancellationToken);
            try
            {
                using var scope = host.Services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IOuterExceptionHandlingTestService>();
                var evt = scope.ServiceProvider.GetRequiredService<RecordingExceptionEvent>();

                var ex = Assert.Throws<InvalidOperationException>(() => service.CallInnerThrow());
                Assert.Equal("nested", ex.Message);
                Assert.Contains(nameof(IInnerExceptionHandlingTestService.ThrowNested), evt.Calls);
            }
            finally
            {
                await host.StopAsync(TestContext.Current.CancellationToken);
                host.Dispose();
            }
        }
    }

    public interface IExceptionHandlingTestService
    {
        int ThrowUnhandled();
        Task<int> ThrowAsyncWithGlobalHandler();
    }

    public interface IOuterExceptionHandlingTestService
    {
        void CallInnerThrow();
    }

    [AutoRegister(Lifetime = Lifetime.Scoped)]
    [Intercept(typeof(ServiceInvokeInterceptor))]
    public class ExceptionHandlingTestService : IExceptionHandlingTestService
    {
        public int ThrowUnhandled()
        {
            throw new InvalidOperationException("unhandled");
        }

        public Task<int> ThrowAsyncWithGlobalHandler()
        {
            throw new InvalidOperationException("async");
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
}