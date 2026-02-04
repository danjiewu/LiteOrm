using Autofac;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm 核心组件初始化器，负责初始化 SessionManager 和 TableInfoProvider。
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
    public class LiteOrmCoreInitializer : IStartable
    {
        private readonly SessionManager _sessionManager;
        private readonly TableInfoProvider _tableInfoProvider;

        public LiteOrmCoreInitializer(SessionManager sessionManager, TableInfoProvider tableInfoProvider)
        {
            _sessionManager = sessionManager;
            _tableInfoProvider = tableInfoProvider;
        }

        /// <summary>
        /// 启动时初始化核心单例组件。
        /// </summary>
        public void Start()
        {
            // 设置全局单例引用的核心组件
            SessionManager.Current = _sessionManager;
            TableInfoProvider.Default = _tableInfoProvider;
        }
    }
}
