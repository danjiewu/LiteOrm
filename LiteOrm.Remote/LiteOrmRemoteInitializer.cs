using Autofac;
using LiteOrm.Common;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm.Remote 客户端初始化器，参照 <see cref="LiteOrmCoreInitializer"/> 使用 IStartable 方式
    /// 在容器构建时自动设置 <see cref="TableInfoProvider.Default"/>。
    /// 不需要触发 <see cref="TableInfoProvider"/> 解析也能正确设置。
    /// </summary>
    [AutoRegister(Lifetime = Lifetime.Singleton)]
    public class LiteOrmRemoteInitializer : IStartable
    {
        private readonly TableInfoProvider _tableInfoProvider;

        /// <summary>
        /// 初始化 <see cref="LiteOrmRemoteInitializer"/> 类的新实例。
        /// </summary>
        /// <param name="tableInfoProvider">表信息提供者实例（由 <see cref="AutoRegisterAttribute"/> 自动注册的 <see cref="AttributeTableInfoProvider"/>）。</param>
        public LiteOrmRemoteInitializer(TableInfoProvider tableInfoProvider)
        {
            _tableInfoProvider = tableInfoProvider;
        }

        /// <summary>
        /// 容器构建时自动调用，设置 <see cref="TableInfoProvider.Default"/> 全局实例。
        /// </summary>
        public void Start()
        {
            TableInfoProvider.Default = _tableInfoProvider;
        }
    }
}