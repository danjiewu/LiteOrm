using Castle.DynamicProxy;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace LiteOrm.Service
{
    /// <summary>
    /// 使用 Scope 服务自动生成接口示例的拦截器，将接口的调用转发至服务提供者
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Scoped)]
    public class ServiceGenerateInterceptor : IInterceptor
    {
        private IServiceProvider _serviceProvider;
        /// <summary>
        /// 初始化 <see cref="ServiceGenerateInterceptor"/> 类的新实例。
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        public ServiceGenerateInterceptor(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>  
        /// 根据接口返回类型自动从服务提供者获取对应服务
        /// </summary>  
        /// <param name="invocation">目标方法</param>
        /// <returns>目标方法的返回值。</returns>  
        public void Intercept(IInvocation invocation)
        {
            invocation.ReturnValue = _serviceProvider.GetRequiredService(invocation.Method.ReturnType);
        }
    }
}