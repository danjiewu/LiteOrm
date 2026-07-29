using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace LiteOrm
{

    /// <summary>
    /// 批量插入提供程序工厂
    /// </summary>
    [AutoRegister(Lifetime.Singleton)]
    public class BulkProviderFactory
    {
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
                    var attr = provider.GetType().GetCustomAttribute<AutoRegisterAttribute>(true);
                    if (attr?.Key is Type keyType)
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
        public IBulkProvider GetProvider(Type dbConnectionType)
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
