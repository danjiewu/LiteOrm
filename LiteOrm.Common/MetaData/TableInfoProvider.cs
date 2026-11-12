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
        private static volatile Lazy<TableInfoProvider> _instance = new Lazy<TableInfoProvider>(
            () => new AttributeTableInfoProvider(),
            LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// 获取全局单例实例。实例在首次访问时通过工厂委托延迟创建并缓存。
        /// 默认返回 <see cref="AttributeTableInfoProvider"/> 实例。
        /// </summary>
        public static TableInfoProvider Instance => _instance.Value;

        /// <summary>
        /// 设置全局单例的工厂委托。工厂委托在 <see cref="Instance"/> 首次访问时通过 <see cref="Lazy{T}"/> 延迟执行并缓存结果。
        /// <para>
        /// 该方法可能被多个程序集生成的模块初始化器在加载时调用，也可能与读取 <see cref="Instance"/> 的线程并发，
        /// 因此以原子方式替换整个 <see cref="Lazy{T}"/>，读者要么看到旧实例、要么看到新实例，不会看到半初始化状态。
        /// </para>
        /// </summary>
        /// <param name="factory">返回表信息提供者实例的工厂委托，不能为 null。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="factory"/> 为 null 时抛出。</exception>
        public static void Set(Func<TableInfoProvider> factory)
        {
            if (factory is null) throw new ArgumentNullException(nameof(factory));
            Interlocked.Exchange(ref _instance, new Lazy<TableInfoProvider>(factory, LazyThreadSafetyMode.ExecutionAndPublication));
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
