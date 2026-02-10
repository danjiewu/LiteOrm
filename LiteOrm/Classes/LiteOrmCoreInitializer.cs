using Autofac;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm 核心初始化器，用于初始化 SessionManager 和 TableInfoProvider
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
    public class LiteOrmCoreInitializer : IStartable
    {
        private readonly SessionManager _sessionManager;
        private readonly TableInfoProvider _tableInfoProvider;

        /// <summary>
        /// 使用指定的会话管理器和表信息提供者初始化 LiteOrmCoreInitializer 类的新实例
        /// </summary>
        /// <param name="sessionManager">会话管理器实例</param>
        /// <param name="tableInfoProvider">表信息提供者实例</param>
        public LiteOrmCoreInitializer(SessionManager sessionManager, TableInfoProvider tableInfoProvider)
        {
            _sessionManager = sessionManager;
            _tableInfoProvider = tableInfoProvider;
        }

        /// <summary>
        /// 启动时的初始化方法
        /// </summary>
        public void Start()
        {
            // 设置全局的会话管理器和表信息提供者
            SessionManager.Current = _sessionManager;
            TableInfoProvider.Default = _tableInfoProvider;
        }
    }
}
