using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LiteOrm.Common;
using LiteOrm.Service;

namespace LiteOrm.AspNetCore
{
    /// <summary>
    /// LiteOrm服务范围中间件 - 为每个HTTP请求管理会话生命周期
    /// </summary>
    /// <remarks>
    /// MyServiceScopeMiddleware 是一个 ASP.NET Core 中间件，用于在 HTTP 请求处理管道中
    /// 为每个请求创建和管理 LiteOrm 会话的生命周期。
    /// 
    /// 主要功能包括：
    /// 1. 会话上下文创建 - 在请求开始时为每个请求创建独立的会话上下文
    /// 2. 会话隔离 - 确保每个请求有独立的数据库连接和事务上下文
    /// 3. 会话清理 - 在请求结束时正确释放所有会话资源
    /// 4. 异常处理 - 确保即使发生异常也能正确清理资源
    /// 5. 日志记录 - 记录请求的关键信息和异常日志
    /// 6. 请求追踪 - 使用请求ID追踪请求的处理
    /// 
    /// 该中间件应在 Startup.ConfigureServices 中注册为 ASP.NET Core 中间件，
    /// 通常在管道的较早位置，以确保所有后续中间件都在正确的会话上下文中运行。
    /// 
    /// 使用示例：
    /// <code>
    /// public void Configure(IApplicationBuilder app)
    /// {
    ///     // 注册 LiteOrm 会话中间件
    ///     app.UseMiddleware&lt;MyServiceScopeMiddleware&gt;();
    ///     
    ///     // 其他中间件...
    ///     app.UseRouting();
    ///     app.UseEndpoints(endpoints => { });
    /// }
    /// </code>
    /// </remarks>
    public class LiteOrmScopeMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceProvider _rootProvider;
        private readonly ILogger<LiteOrmScopeMiddleware> _logger;

        /// <summary>
        /// 初始化 <see cref="LiteOrmScopeMiddleware"/> 类的新实例。
        /// </summary>
        /// <param name="next">请求管道中的下一个中间件。</param>
        /// <param name="rootProvider">根服务提供程序。</param>
        /// <param name="logger">日志记录器。</param>
        public LiteOrmScopeMiddleware(
            RequestDelegate next,
            IServiceProvider rootProvider,
            ILogger<LiteOrmScopeMiddleware> logger)
        {
            _next = next;
            _rootProvider = rootProvider;
            _logger = logger;
        }

        /// <summary>
        /// 处理 HTTP 请求并管理 LiteOrm 会话。
        /// </summary>
        /// <param name="context">HTTP 上下文。</param>
        /// <param name="sessionManager">会话管理器。</param>
        /// <returns>表示异步操作的任务。</returns>
        public async Task InvokeAsync(HttpContext context, SessionManager sessionManager)
        {
            var requestId = context.TraceIdentifier;
            var method = context.Request.Method;
            var path = context.Request.Path;
            var queryString = context.Request.QueryString.Value;
            using var scope = sessionManager.Enter(false);
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

    /// <summary>
    /// <see cref="LiteOrmScopeMiddleware"/> 的扩展方法。
    /// </summary>
    public static class LiteOrmScopeMiddlewareExtensions
    {
        /// <summary>
        /// 在 HTTP 请求管道中启用 LiteOrm 服务范围中间件。
        /// </summary>
        /// <param name="builder">应用程序生成器。</param>
        /// <returns>配置后的应用程序生成器。</returns>
        public static IApplicationBuilder UseLiteOrm(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<LiteOrmScopeMiddleware>();
        }
    }
}