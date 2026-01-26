using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库连接配置
    /// </summary>
    public class DataSourceConfig
    {
        /// <summary>
        /// 数据源名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        public string ConnectionString { get; set; }
        
        /// <summary>
        /// 数据库提供程序类型全名
        /// </summary>
        public string Provider { get; set; }
        
        /// <summary>
        /// 连接保活时长
        /// </summary>
        public TimeSpan KeepAliveDuration { get; set; }
        
        /// <summary>
        /// 连接池大小，默认为16
        /// </summary>
        public int PoolSize { get; set; } = 16;

        /// <summary>
        /// 数据库参数最大数量限制，为0表示无限制，默认为1000
        /// </summary>
        public int ParamCountLimit { get; set; } = 1000;

        /// <summary>
        /// 是否开启自动建表同步
        /// </summary>
        public bool SyncTable { get; set; }

        /// <summary>
        /// 获取提供程序类型
        /// </summary>
        public Type ProviderType
        {
            get
            {
                if (string.IsNullOrEmpty(Provider))
                    throw new InvalidOperationException("数据库提供程序未指定");

                var type = Type.GetType(Provider);
                if (type == null)
                    throw new TypeLoadException($"无法加载数据库提供程序类型: {Provider}");

                return type;
            }
        }
    }

    /// <summary>
    /// 数据源提供程序接口，用于管理数据库连接配置
    /// </summary>
    public interface IDataSourceProvider: IEnumerable<DataSourceConfig>
    {
        /// <summary>
        /// 获取默认数据源名称
        /// </summary>
        string DefaultDataSourceName { get; }
        
        /// <summary>
        /// 根据名称获取数据源配置
        /// </summary>
        /// <param name="name">数据源名称</param>
        /// <returns>数据源配置</returns>
        DataSourceConfig GetDataSource(string name);
    }

}
