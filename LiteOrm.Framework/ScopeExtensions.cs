using Autofac;
using Autofac.Extensions.DependencyInjection;
using LiteOrm;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm.Framework
{
    /// <summary>
    /// Autofac 生命周期作用域跟踪扩展。
    /// </summary>
    /// <remarks>
    /// 通过监听 Autofac 的 <see cref="ILifetimeScope"/> 事件，
    /// 自动更新 <see cref="SessionManager"/> 的当前作用域服务提供者，
    /// 确保异步上下文中能正确解析当前 scope 的 SessionManager 实例。
    /// </remarks>
    public static class ScopeExtensions
    {
        /// <summary>
        /// 注册 Autofac 作用域跟踪，在子作用域创建和销毁时自动更新 SessionManager 的服务提供者。
        /// </summary>
        /// <param name="rootScope">根作用域。</param>
        public static void RegisterScope(ILifetimeScope rootScope)
        {
            rootScope.ChildLifetimeScopeBeginning += (sender, e) =>
            {
                // 子作用域开始时，设置当前服务提供者为子作用域
                SessionManager.SetCurrentServiceProvider(new AutofacServiceProvider(e.LifetimeScope));

                // 子作用域结束时，恢复为根作用域
                e.LifetimeScope.CurrentScopeEnding += (s, args) =>
                {
                    SessionManager.SetCurrentServiceProvider(new AutofacServiceProvider(rootScope));
                };

                // 递归注册子作用域的子作用域跟踪
                RegisterScope(e.LifetimeScope);
            };
        }
    }
}
