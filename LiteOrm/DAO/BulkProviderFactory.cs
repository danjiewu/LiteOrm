using Autofac.Features.Indexed;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace LiteOrm
{

    /// <summary>
    /// 批量插入提供程序工厂
    /// </summary>
    [AutoRegister(ServiceLifetime.Singleton)]
    public class BulkProviderFactory
    {
        private readonly IIndex<Type, IBulkProvider> _keyedProviders;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="keyedProviders">用于查找的批量插入提供程序索引</param>
        public BulkProviderFactory(
            IIndex<Type, IBulkProvider> keyedProviders)
        {
            _keyedProviders = keyedProviders;
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

            // 1. 尝试直接查找
            if (_keyedProviders.TryGetValue(dbConnectionType, out var provider))
            {
                return provider;
            }
            return null;
        }
    }
}
