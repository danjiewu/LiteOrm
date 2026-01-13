using System;
using System.Collections.Generic;
using System.Text;
using LiteOrm.Common;
using System.Collections;
using System.Data;

namespace LiteOrm.SQLite
{
    /// <summary>
    /// SQLite SQL 构建器。
    /// </summary>
    public class SQLiteBuilder : SqlBuilder
    {
        /// <summary>
        /// SQLite SQL 构建器实例。
        /// </summary>
        public static readonly new SQLiteBuilder Instance = new SQLiteBuilder();

        /// <summary>
        /// 初始化函数映射关系。
        /// </summary>
        /// <param name="functionMappings">函数映射字典。</param>
        protected override void InitializeFunctionMappings(Dictionary<string, string> functionMappings)
        {
            functionMappings["Length"] = "LENGTH";
            functionMappings["IndexOf"] = "INSTR";       // INSTR(str, substr)
            functionMappings["Substring"] = "SUBSTR";
        }
        /// <summary>
        /// 构建插入并返回自增标识的SQL语句
        /// </summary>
        public override string BuildIdentityInsertSQL(IDbCommand command, ColumnDefinition identityColumn, string tableName, string strColumns, string strValues)
        {
            return $"insert into {ToSqlName(tableName)} ({strColumns}) \nvalues ({strValues});\nSELECT last_insert_rowid() as [ID];";
        }
    }
}
