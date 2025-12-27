using System;
using System.Collections.Generic;
using System.Text;
using MyOrm.Common;
using System.Collections;
using System.Data;

namespace MyOrm.SQLite
{
    /// <summary>
    /// Oracle生成Sql语句的辅助类
    /// </summary>
    public class SQLiteBuilder : SqlBuilder
    {
        public static readonly new SQLiteBuilder Instance = new SQLiteBuilder();
        public override string BuildIdentityInsertSQL(IDbCommand command, ColumnDefinition identityColumn, string tableName, string strColumns, string strValues)
        {
            return String.Format("insert into {0} ({1}) \nvalues ({2});\n{3};", ToSqlName(tableName), strColumns, strValues, "SELECT last_insert_rowid() as [ID];");
        }
    }
}
