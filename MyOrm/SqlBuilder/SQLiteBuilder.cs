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
        protected override void InitializeFunctionMappings(Dictionary<string, string> functionMappings)
        {
            functionMappings["Length"] = "LENGTH";
            functionMappings["IndexOf"] = "INSTR";       // INSTR(str, substr)
            functionMappings["Substring"] = "SUBSTR";
        }
        public override string BuildIdentityInsertSQL(IDbCommand command, ColumnDefinition identityColumn, string tableName, string strColumns, string strValues)
        {
            return $"insert into {ToSqlName(tableName)} ({strColumns}) \nvalues ({strValues});\nSELECT last_insert_rowid() as [ID];";
        }
    }
}
