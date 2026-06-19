using LiteOrm.Common;

namespace LiteOrm.SqlToExpr;

public enum SqlDialect
{
    SQLite,
    SqlServer,
    MySql,
    PostgreSql,
    Oracle
}

internal static class SqlDialectExtensions
{
    public static ISqlBuilder GetSqlBuilder(this SqlDialect dialect)
    {
        return dialect switch
        {
            SqlDialect.SqlServer => SqlServerBuilder.Instance,
            SqlDialect.MySql => MySqlBuilder.Instance,
            SqlDialect.PostgreSql => PostgreSqlBuilder.Instance,
            SqlDialect.Oracle => OracleBuilder.Instance,
            _ => SQLiteBuilder.Instance
        };
    }
}