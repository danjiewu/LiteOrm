using Autofac;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm 核心组件初始化器，负责初始化 SessionManager 和 TableInfoProvider。
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
    public class LiteOrmCoreInitializer : IComponentInitializer
    {
        /// <summary>
        /// 使用指定的组件上下文初始化核心单例组件。
        /// </summary>
        /// <param name="componentContext">用于解析服务的组件上下文。</param>
        public void Initialize(IComponentContext componentContext)
        {
            // 设置全局单例引用的核心组件
            SessionManager.Current = componentContext.Resolve<SessionManager>();
            TableInfoProvider.Default = componentContext.Resolve<TableInfoProvider>();
        }
    }
}
