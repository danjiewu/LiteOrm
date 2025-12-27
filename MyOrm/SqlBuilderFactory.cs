using MyOrm.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm
{
    public class SqlBuilderFactory:ISqlBuilderFactory
    {
        public Dictionary<Type, SqlBuilder> RegisteredSqlBuilders { get; } = new Dictionary<Type, SqlBuilder>();

        public void RegisterSqlBuilder(Type providerType, SqlBuilder sqlBuilder)
        {
            RegisteredSqlBuilders[providerType] = sqlBuilder;
        }
        public virtual SqlBuilder GetSqlBuilder(Type providerType)
        {
            if (providerType == null) throw new ArgumentNullException("providerType");
            if (RegisteredSqlBuilders.ContainsKey(providerType)) return RegisteredSqlBuilders[providerType];
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
    }
}
