using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Threading;

namespace LiteOrm
{

    /// <summary>
    /// 批量插入提供程序工厂
    /// </summary>
    public class BulkProviderFactory
    {
        private static Lazy<BulkProviderFactory?> _instance = new Lazy<BulkProviderFactory?>(
            () => null,
            LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// 获取全局单例实例。实例在首次访问时通过工厂委托延迟创建并缓存。
        /// 默认返回空。
        /// </summary>
        public static BulkProviderFactory? Instance => _instance.Value;

        /// <summary>
        /// 设置全局单例的工厂委托。工厂委托在 <see cref="Instance"/> 首次访问时通过 <see cref="Lazy{T}"/> 延迟执行并缓存结果。
        /// 传入 null 时恢复为空。
        /// </summary>
        /// <param name="factory">返回批量提供者工厂实例的工厂委托；传入 null 时返回为空</param>
        public static void Set(Func<BulkProviderFactory>? factory)
        {
            _instance = factory is null
                ? new Lazy<BulkProviderFactory?>(() => null, LazyThreadSafetyMode.ExecutionAndPublication)
                : new Lazy<BulkProviderFactory?>(factory, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private readonly Dictionary<Type, IBulkProvider> _keyedProviders;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="providers">所有注册的批量插入提供程序</param>
        public BulkProviderFactory(IEnumerable<IBulkProvider> providers)
        {
            _keyedProviders = new Dictionary<Type, IBulkProvider>();
            if (providers != null)
            {
                foreach (var provider in providers)
                {
                    var attr = provider.GetType().GetCustomAttribute<BulkProviderAttribute>(true);
                    if (attr?.DbConnectionType is Type keyType)
                    {
                        _keyedProviders[keyType] = provider;
                    }
                }
            }
        }

        /// <summary>
        /// 获取批量插入提供程序
        /// </summary>
        /// <param name="dbConnectionType">数据库连接类型</param>
        /// <returns>对应的批量插入提供程序</returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public IBulkProvider? GetProvider(Type dbConnectionType)
        {
            if (dbConnectionType == null)
                throw new ArgumentNullException(nameof(dbConnectionType));

            if (!typeof(IDbConnection).IsAssignableFrom(dbConnectionType))
                throw new ArgumentException($"Type must implement IDbConnection: {dbConnectionType.Name}");

            // 尝试直接查找
            if (_keyedProviders.TryGetValue(dbConnectionType, out var provider))
            {
                return provider;
            }
            return null;
        }
    }
}
