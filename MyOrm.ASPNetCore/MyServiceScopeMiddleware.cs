using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyOrm.Common;
using MyOrm.Service;

namespace MyOrm.AspNetCore
{
    public class MyServiceScopeMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceProvider _rootProvider;

        // 注入自定义根ServiceProvider（需提前注册为服务）
        public MyServiceScopeMiddleware(RequestDelegate next, IServiceProvider rootProvider)
        {
            _next = next;
            _rootProvider = rootProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 使用自定义根Provider创建Scope
            using var customScope = _rootProvider.CreateScope();
            using var sessionManager = customScope.ServiceProvider.GetRequiredService<SessionManager>();
            using var sessionScope = sessionManager.EnterContext();
            // 替换请求的RequestServices为自定义Scope的Provider
            var originalRequestServices = context.RequestServices;
            context.RequestServices = customScope.ServiceProvider;
            try
            {
                    await _next(context);
            }
            finally
            {
                // 恢复原始RequestServices（可选，防止后续中间件异常）
                context.RequestServices = originalRequestServices;
            }
        }
    }

    public static class MyServiceScopeMiddlewareExtensions
    {
        public static IApplicationBuilder UseMyOrm(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<MyServiceScopeMiddleware>(builder.ApplicationServices);
        }
    }
}
