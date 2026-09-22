using LiteOrm;
using LiteOrm.Common;
using LiteOrm.DependencyInjection;
using LiteOrm.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// 验证 <see cref="ServiceInvokeInterceptor"/> 基于角色的服务鉴权行为：
    /// 用户主体（<see cref="IPrincipal"/>）从 DI 注入的 <see cref="IUserContext"/> 获取，
    /// 权限由 [ServicePermission] 声明；
    /// 同时验证 <c>RegisterLiteOrm</c> 提供的用户上下文注入方式。纯 DI 测试，无需数据库。
    /// </summary>
    public class ServicePermissionInterceptorTests
    {
        private static IHost BuildHost(
            Action<LiteOrm.DependencyInjection.LiteOrmServiceExtensions.LiteOrmOptions>? configureOptions = null,
            Action<IServiceCollection>? configureServices = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["LiteOrm:Default"] = "SQLite",
                    ["LiteOrm:DataSources:0:Name"] = "SQLite",
                    ["LiteOrm:DataSources:0:ConnectionString"] = "Data Source=permission.db",
                    ["LiteOrm:DataSources:0:Provider"] = "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite"
                })
                .Build();

            return Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((_, config) => config.AddConfiguration(configuration))
                .RegisterLiteOrm(configureOptions)
                .ConfigureServices(services => configureServices?.Invoke(services))
                .Build();
        }

        /// <summary>
        /// 构造已认证的用户主体，角色写入 Role 声明（与 ASP.NET Core 一致的形态）。
        /// </summary>
        private static ClaimsPrincipal CreatePrincipal(params string[] roles)
        {
            var claims = new List<Claim>();
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuthentication"));
        }

        [Fact]
        public void Permission_NotDeclared_AllowsWithoutUserContext()
        {
            using var host = BuildHost();

            using var scope = host.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionDemoService>();
            Assert.Equal(nameof(IPermissionDemoService.Open), service.Open());
        }

        [Fact]
        public void Permission_AllowAnonymous_AllowsWithoutUserContext()
        {
            using var host = BuildHost();

            using var scope = host.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionDemoService>();
            Assert.Equal(nameof(IPermissionDemoService.AnonymousAllowed), service.AnonymousAllowed());
        }

        [Fact]
        public void Permission_AllowAnonymousWithRoles_AllowsWithoutUserContext()
        {
            // 声明 AllowAnonymous 的方法直接放行，即便声明了角色也不校验
            using var host = BuildHost();

            using var scope = host.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionDemoService>();
            Assert.Equal(nameof(IPermissionDemoService.AnonymousWithRoles), service.AnonymousWithRoles());
        }

        [Fact]
        public void Permission_AllowAnonymousWithRoles_IgnoresRoleCheck()
        {
            // 已注册用户上下文且角色不匹配，AllowAnonymous 仍然放行
            var user = new TestUserContext { UserPrincipal = CreatePrincipal("Guest") };

            using var host = BuildHost(options => options.RegisterUserContext(user));

            using var scope = host.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionDemoService>();
            Assert.Equal(nameof(IPermissionDemoService.AnonymousWithRoles), service.AnonymousWithRoles());
        }

        [Fact]
        public void Permission_RequireAuthentication_WithoutUserContext_AllowsLegacyMode()
        {
            // 未注册 IUserContext 且未声明角色：兼容未接入用户体系的应用，放行
            using var host = BuildHost();

            using var scope = host.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionDemoService>();
            Assert.Equal(nameof(IPermissionDemoService.RequireAuthentication), service.RequireAuthentication());
        }

        [Fact]
        public void Permission_Roles_WithoutUserContext_Allows()
        {
            // 未注册 IUserContext：没有身份来源，直接放行，不校验角色
            using var host = BuildHost();

            using var scope = host.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionDemoService>();
            Assert.Equal(nameof(IPermissionDemoService.AdminOnly), service.AdminOnly());
        }

        [Fact]
        public void Permission_RequireAuthentication_NullPrincipal_Throws()
        {
            // 已注册用户上下文但当前无主体（如后台任务无请求上下文）：视为未认证
            var user = new TestUserContext();

            using var host = BuildHost(options => options.RegisterUserContext(user));

            using var scope = host.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionDemoService>();
            Assert.Throws<ServicePermissionException>(() => service.RequireAuthentication());
        }

        [Fact]
        public void Permission_RequireAuthentication_UnauthenticatedIdentity_Throws()
        {
            // 主体存在但 Identity 未认证
            var user = new TestUserContext { UserPrincipal = new ClaimsPrincipal(new ClaimsIdentity()) };

            using var host = BuildHost(options => options.RegisterUserContext(user));

            using var scope = host.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionDemoService>();
            Assert.Throws<ServicePermissionException>(() => service.RequireAuthentication());
        }

        [Fact]
        public void Permission_RequireAuthentication_Authenticated_Allows()
        {
            var user = new TestUserContext { UserPrincipal = CreatePrincipal("Manager") };

            using var host = BuildHost(options => options.RegisterUserContext(user));

            using var scope = host.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionDemoService>();
            Assert.Equal(nameof(IPermissionDemoService.RequireAuthentication), service.RequireAuthentication());
        }

        [Fact]
        public void Permission_Roles_NotMatched_Throws()
        {
            var user = new TestUserContext { UserPrincipal = CreatePrincipal("Guest") };

            using var host = BuildHost(options => options.RegisterUserContext(user));

            using var scope = host.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionDemoService>();
            Assert.Throws<ServicePermissionException>(() => service.AdminOnly());
        }

        [Fact]
        public void Permission_Roles_Matched_TrimsDeclaredRole()
        {
            // 允许角色 "Admin, Manager" 逗号切分后带空白，声明侧去空白后命中 "Manager"；
            // 角色匹配语义由 IPrincipal.IsInRole 决定（ClaimsPrincipal 按声明值精确比较，大小写敏感）
            var user = new TestUserContext { UserPrincipal = CreatePrincipal("Manager") };

            using var host = BuildHost(options => options.RegisterUserContext(user));

            using var scope = host.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionDemoService>();
            Assert.Equal(nameof(IPermissionDemoService.AdminOnly), service.AdminOnly());
        }

        [Fact]
        public async Task Permission_Roles_AsyncMethod_Throws()
        {
            var user = new TestUserContext { UserPrincipal = CreatePrincipal("Guest") };

            using var host = BuildHost(options => options.RegisterUserContext(user));

            using var scope = host.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionDemoService>();
            await Assert.ThrowsAsync<ServicePermissionException>(() => service.AdminOnlyAsync());
        }

        [Fact]
        public void Permission_Denied_FiresExceptionEvent()
        {
            var user = new TestUserContext();

            using var host = BuildHost(
                options => options.RegisterUserContext(user),
                services =>
                {
                    services.AddScoped<RecordingExceptionEvent>();
                    services.AddScoped<IServiceExceptionEvent>(sp => sp.GetRequiredService<RecordingExceptionEvent>());
                });

            using var scope = host.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPermissionDemoService>();
            var evt = scope.ServiceProvider.GetRequiredService<RecordingExceptionEvent>();

            Assert.Throws<ServicePermissionException>(() => service.RequireAuthentication());
            Assert.Contains(evt.Calls, c => c.StartsWith("OnException:") && c.Contains(nameof(IPermissionDemoService.RequireAuthentication)));
        }

        [Fact]
        public void RegisterUserContext_GenericType_ResolvesFromScope()
        {
            using var host = BuildHost(options => options.RegisterUserContext<TestUserContext>(Lifetime.Singleton));

            using var scope = host.Services.CreateScope();
            var userContext = scope.ServiceProvider.GetRequiredService<IUserContext>();
            Assert.IsType<TestUserContext>(userContext);
            Assert.Same(userContext, scope.ServiceProvider.GetRequiredService<IUserContext>());
        }

        [Fact]
        public void RegisterUserContext_Instance_ResolvesSameInstance()
        {
            var user = new TestUserContext { UserPrincipal = CreatePrincipal("Admin") };

            using var host = BuildHost(options => options.RegisterUserContext(user));

            using var scope = host.Services.CreateScope();
            Assert.Same(user, scope.ServiceProvider.GetRequiredService<IUserContext>());
        }

        [Fact]
        public void RegisterUserContext_Factory_ResolvesFactoryResult()
        {
            using var host = BuildHost(options => options.RegisterUserContext(_ => new TestUserContext { UserPrincipal = CreatePrincipal("Admin") }));

            using var scope = host.Services.CreateScope();
            var userContext = Assert.IsType<TestUserContext>(scope.ServiceProvider.GetRequiredService<IUserContext>());
            Assert.True(userContext.UserPrincipal?.Identity?.IsAuthenticated);
        }

        /// <summary>
        /// 权限演示服务接口，[ServicePermission] 声明在接口方法上（拦截器读取接口方法的特性）。
        /// </summary>
        public interface IPermissionDemoService
        {
            string Open();

            [ServicePermission(true)]
            string AnonymousAllowed();

            [ServicePermission(true, AllowRoles = "Admin")]
            string AnonymousWithRoles();

            [ServicePermission]
            string RequireAuthentication();

            [ServicePermission(AllowRoles = "Admin, Manager")]
            string AdminOnly();

            [ServicePermission(AllowRoles = "Admin")]
            Task<string> AdminOnlyAsync();
        }

        /// <summary>
        /// 权限演示服务实现，方法不触碰数据库，仅返回方法名。
        /// </summary>
        [AutoRegister(Lifetime = Lifetime.Scoped)]
        [Service]
        public sealed class PermissionDemoService : IPermissionDemoService
        {
            public string Open() => nameof(Open);

            public string AnonymousAllowed() => nameof(AnonymousAllowed);

            public string AnonymousWithRoles() => nameof(AnonymousWithRoles);

            public string RequireAuthentication() => nameof(RequireAuthentication);

            public string AdminOnly() => nameof(AdminOnly);

            public Task<string> AdminOnlyAsync() => Task.FromResult(nameof(AdminOnlyAsync));
        }

        /// <summary>
        /// 可变测试用户上下文，便于在各用例中切换当前主体。
        /// </summary>
        private sealed class TestUserContext : IUserContext
        {
            public IPrincipal? UserPrincipal { get; set; }
        }

        /// <summary>
        /// 记录异常事件回调的测试订阅者。
        /// </summary>
        private sealed class RecordingExceptionEvent : IServiceExceptionEvent
        {
            public List<string> Calls { get; } = new List<string>();

            public void OnException(ServiceExceptionContext context) =>
                Calls.Add($"{nameof(OnException)}:{context.ServiceName}.{context.MethodName}");
        }
    }
}
