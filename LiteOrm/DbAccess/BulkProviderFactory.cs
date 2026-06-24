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
        private readonly Dictionary<Type, IBulkProvider> _providers;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serviceProvider">服务提供者，用于解析所有 IBulkProvider 实现</param>
        public BulkProviderFactory(
            IServiceProvider serviceProvider)
        {
            var providers = serviceProvider.GetServices<IBulkProvider>();
            _providers = new Dictionary<Type, IBulkProvider>();

            foreach (var provider in providers)
            {
                var key = GetProviderKey(provider.GetType());
                if (key != null && !_providers.ContainsKey(key))
                {
                    _providers[key] = provider;
                }
            }
        }

        private static Type GetProviderKey(Type providerType)
        {
            var attr = providerType.GetCustomAttribute<AutoRegisterAttribute>(true);
            if (attr?.Key is Type keyType)
            {
                return keyType;
            }
            return null;
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
            if (_providers.TryGetValue(dbConnectionType, out var provider))
            {
                return provider;
            }
            return null;
        }
    }
}