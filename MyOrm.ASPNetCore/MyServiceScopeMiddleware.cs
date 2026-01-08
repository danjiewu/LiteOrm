using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyOrm.Common;
using MyOrm.Service;

namespace MyOrm.AspNetCore
{
    public class MyServiceScopeMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceProvider _rootProvider;
        private readonly ILogger<MyServiceScopeMiddleware> _logger;

        public MyServiceScopeMiddleware(
            RequestDelegate next,
            IServiceProvider rootProvider,
            ILogger<MyServiceScopeMiddleware> logger)
        {
            _next = next;
            _rootProvider = rootProvider;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, SessionManager sessionManager)
        {
            var requestId = context.TraceIdentifier;
            var method = context.Request.Method;
            var path = context.Request.Path;
            var queryString = context.Request.QueryString.Value;
            using var scope = sessionManager.EnterContext(false);
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // 简化日志：只记录异常类型、请求ID和路径
                _logger.LogError(
                    "异常: {ExceptionType} - {RequestId} {Method} {Path}{QueryString}",
                    ex.GetType().Name,
                    requestId,
                    method,
                    path,
                    queryString);

                throw; // 重新抛出异常
            }
            finally
            {
                scope.Dispose();
            }
        }
    }
    public static class MyServiceScopeMiddlewareExtensions
    {
        public static IApplicationBuilder UseMyOrm(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<MyServiceScopeMiddleware>();
        }
    }
}