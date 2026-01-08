using Microsoft.Extensions.DependencyInjection;
using MyOrm.Common;
using System;
using System.Collections.Concurrent;


namespace MyOrm
{
    [AutoRegister(ServiceLifetime.Singleton)]
    public class SqlBuilderFactory : ISqlBuilderFactory
    {
        public static readonly SqlBuilderFactory Instance = new SqlBuilderFactory();
        public  ConcurrentDictionary<Type, SqlBuilder> registeredSqlBuilders { get; } = new();

        public void RegisterSqlBuilder(Type providerType, SqlBuilder sqlBuilder)
        {
            registeredSqlBuilders[providerType] = sqlBuilder;
        }

        public virtual SqlBuilder GetSqlBuilder(Type providerType)
        {
            if (providerType == null) throw new ArgumentNullException("providerType");
            if (registeredSqlBuilders.ContainsKey(providerType)) return registeredSqlBuilders[providerType];
            var connectionTypeName = providerType.Name;
            connectionTypeName = connectionTypeName.ToUpper();
            if (connectionTypeName.Contains("ORACLE"))
                return Oracle.OracleBuilder.Instance;
            else if (connectionTypeName.Contains("MYSQL"))
                return MySql.MySqlBuilder.Instance;
            else if (connectionTypeName.Contains("SQLSERVER"))
                return SqlServer.SqlServerBuilder.Instance;
            else if (connectionTypeName.Contains("SQLITE"))
                return SQLite.SQLiteBuilder.Instance;
            else return SqlBuilder.Instance;
        }

        ISqlBuilder ISqlBuilderFactory.GetSqlBuilder(Type providerType)
        {
            return GetSqlBuilder(providerType);
        }
    }
}
