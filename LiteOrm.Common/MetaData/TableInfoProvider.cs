using System;
using System.Threading;
using System.Diagnostics.CodeAnalysis;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表信息提供类
    /// </summary>
    public abstract class TableInfoProvider
    {
        private static Lazy<TableInfoProvider> _instance = new Lazy<TableInfoProvider>(
            () => new AttributeTableInfoProvider(),
            LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// 获取全局单例实例。实例在首次访问时通过工厂委托延迟创建并缓存。
        /// 默认返回 <see cref="AttributeTableInfoProvider"/> 实例。
        /// </summary>
        public static TableInfoProvider Instance => _instance.Value;

        /// <summary>
        /// 设置全局单例的工厂委托。工厂委托在 <see cref="Instance"/> 首次访问时通过 <see cref="Lazy{T}"/> 延迟执行并缓存结果。
        /// 传入 null 时恢复为默认工厂（创建 <see cref="AttributeTableInfoProvider"/>）。
        /// </summary>
        /// <param name="factory">返回表信息提供者实例的工厂委托；传入 null 时恢复默认工厂</param>
        public static void Set(Func<TableInfoProvider>? factory)
        {
            _instance = factory is null
                ? new Lazy<TableInfoProvider>(() => new AttributeTableInfoProvider(), LazyThreadSafetyMode.ExecutionAndPublication)
                : new Lazy<TableInfoProvider>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// 获取对象类型所对应的表定义。
        /// </summary>
        /// <param name="objectType">实体对象类型。</param>
        /// <returns>返回对应的 <see cref="TableDefinition"/> 信息。</returns>
        public abstract TableDefinition? GetTableDefinition(
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
            Type objectType);

        /// <summary>
        /// 获取指定类型的视图信息（包含关联查询信息）。
        /// </summary>
        /// <param name="objectType">实体对象类型。</param>
        /// <returns>返回对应的 <see cref="TableView"/> 信息。</returns>
        public abstract TableView? GetTableView(
            [DynamicallyAccessedMembers(Constants.RegistedMemberTypes)]
            Type objectType);
    }
}
