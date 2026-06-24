using System;
using System.Threading;

namespace LiteOrm
{
    /// <summary>
    /// 服务提供者持有器，用于支持属性注入的后备机制。
    /// 当类型无法通过构造函数注入获取服务时，可通过此持有器从当前作用域的 ServiceProvider 解析。
    /// </summary>
    internal static class ServiceProviderHolder
    {
        private static IServiceProvider _rootServiceProvider;
        private static readonly AsyncLocal<IServiceProvider> _currentScope = new();

        /// <summary>
        /// 获取当前异步上下文作用域的服务提供者；若未设置作用域，则返回根服务提供者。
        /// 设置时更新根服务提供者作为后备。
        /// </summary>
        public static IServiceProvider ServiceProvider
        {
            get => _currentScope.Value ?? _rootServiceProvider;
            set => _rootServiceProvider = value;
        }

        /// <summary>
        /// 设置当前异步上下文的作用域服务提供者。
        /// 通过 AsyncLocal 向下传递，子异步任务将继承此值。
        /// </summary>
        /// <param name="serviceProvider">当前作用域的服务提供者</param>
        public static void SetCurrentScope(IServiceProvider serviceProvider)
        {
            _currentScope.Value = serviceProvider;
        }
    }
}