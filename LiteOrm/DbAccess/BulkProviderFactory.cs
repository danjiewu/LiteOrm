using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace LiteOrm
{

    /// <summary>
    /// 批量插入提供程序工厂
    /// </summary>
    [AutoRegister(Lifetime.Singleton)]
    public class BulkProviderFactory
    {
        private readonly Dictionary<object, IBulkProvider> _keyedProviders;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="bulkProviders">已注册的批量插入提供程序</param>
        public BulkProviderFactory(IEnumerable<IBulkProvider> bulkProviders)
        {
            if (bulkProviders is null) throw new ArgumentNullException(nameof(bulkProviders));

            _keyedProviders = bulkProviders
                .Select(provider => new
                {
                    Provider = provider,
                    Attribute = provider.GetType().GetCustomAttribute<AutoRegisterAttribute>(true)
                })
                .Where(x => x.Attribute?.Key != null)
                .ToDictionary(x => x.Attribute.Key, x => x.Provider);
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
