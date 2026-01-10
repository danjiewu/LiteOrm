using MyOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace MyOrm.MySql
{
    /// <summary>
    /// MySql生成Sql语句的辅助类
    /// </summary>
    public class MySqlBuilder : SqlBuilder
    {
        public static readonly new MySqlBuilder Instance = new MySqlBuilder();

        protected override void InitializeFunctionMappings(Dictionary<string, string> functionMappings)
        {
            // 只需添加名称不同的映射
            functionMappings["Length"] = "CHAR_LENGTH";  // 字符数
            functionMappings["IndexOf"] = "LOCATE";      // LOCATE(substr, str)
        }
        /// <summary>
        /// 连接各字符串的SQL语句
        /// </summary>
        /// <param name="strs">需要连接的sql字符串</param>
        /// <returns>SQL语句</returns>
        public override string BuildConcatSql(params string[] strs)
        {
            return $"CONCAT({String.Join(",", strs)})";
        }

        public override string BuildIdentityInsertSQL(IDbCommand command, ColumnDefinition identityColumn, string tableName, string strColumns, string strValues)
        {
            return $"insert into {ToSqlName(tableName)} ({strColumns}) \nvalues ({strValues});\nselect @@IDENTITY as [ID];";
        }

        /// <summary>
        /// 参数名称转化为原始名称
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <returns>原始名称</returns>
        public override string ToNativeName(string paramName)
        {
            return paramName.TrimStart('@');
        }

        /// <summary>
        /// 原始名称转化为参数名称
        /// </summary>
        /// <param name="nativeName">原始名称</param>
        /// <returns>参数名称</returns>
        public override string ToParamName(string nativeName)
        {
            return $"@{nativeName}";
        }

        /// <summary>
        /// 将列名、表名等替换为数据库合法名称
        /// </summary>
        /// <param name="sql">sql语句</param>
        /// <returns></returns>
        public override string ReplaceSqlName(string sql)
        {
            return ReplaceSqlName(sql, '`', '`');
        }

        /// <summary>
        /// 生成分页查询的SQL语句
        /// </summary>
        /// <param name="select">select内容</param>
        /// <param name="from">from块</param>
        /// <param name="where">where条件</param>
        /// <param name="orderBy">排序</param>
        /// <param name="startIndex">起始位置，从0开始</param>
        /// <param name="sectionSize">查询条数</param>
        /// <returns></returns>
        public override string GetSelectSectionSql(string select, string from, string where, string orderBy, int startIndex, int sectionSize)
        {
            return $"select {select} \nfrom {from} \nwhere {where} Order by {orderBy} limit {startIndex},{sectionSize}";
        }
    }
}
