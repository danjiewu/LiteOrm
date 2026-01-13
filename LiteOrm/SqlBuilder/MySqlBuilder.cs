using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace LiteOrm.MySql
{
    /// <summary>
    /// MySQL 生成 SQL 语句的辅助类。
    /// </summary>
    public class MySqlBuilder : SqlBuilder
    {
        /// <summary>
        /// 获取 <see cref="MySqlBuilder"/> 的单例实例。
        /// </summary>
        public static readonly new MySqlBuilder Instance = new MySqlBuilder();

        /// <summary>
        /// 初始化函数映射关系。
        /// </summary>
        /// <param name="functionMappings">函数映射字典。</param>
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
        /// <summary>
        /// 构建插入并返回自增标识的 SQL 语句。
        /// </summary>
        /// <param name="command">数据库命令对象。</param>
        /// <param name="identityColumn">标识列定义。</param>
        /// <param name="tableName">表名。</param>
        /// <param name="strColumns">插入列名部分。</param>
        /// <param name="strValues">插入值名部分。</param>
        /// <returns>构建后的 SQL 语句。</returns>
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
