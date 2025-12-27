using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 数据库连接配置
    /// </summary>
    public class DataSourceConfig
    {
        public string Name { get; set; }
        public string ConnectionString { get; set; }
        public string Provider { get; set; }
        public TimeSpan KeepAliveDuration { get; set; }
        public int PoolSize { get; set; } = 16;

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

    public interface IDataSourceProvider: IEnumerable<DataSourceConfig>
    {
        string DefaultDataSourceName { get; }
        DataSourceConfig GetDataSource(string name);
    }

}
