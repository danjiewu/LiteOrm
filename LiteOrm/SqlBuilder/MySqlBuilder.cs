using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;


namespace LiteOrm
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
        /// 连接各字符串的SQL语句
        /// </summary>
        /// <param name="strs">需要连接的sql字符串</param>
        /// <returns>SQL语句</returns>
        public override string BuildConcatSql(params string[] strs)
        {
            return $"CONCAT({String.Join(",", strs)})";
        }

        /// <summary>
        /// 是否支持带自增列的批量插入并返回首个 ID。
        /// </summary>
        public override bool SupportBatchInsertWithIdentity => true;

        /// <summary>
        /// 转化为数据库合法名称
        /// </summary>

        /// <param name="name">字符串名称</param>
        /// <returns>数据库合法名称</returns>
        public override string ToSqlName(string name)
        {
            if (name is null) throw new ArgumentNullException("name");
            return String.Join(".", Array.ConvertAll(name.Split('.'), n => $"`{n}`"));
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
        public override string BuildIdentityInsertSql(IDbCommand command, ColumnDefinition identityColumn, string tableName, string strColumns, string strValues)
        {
            return $"insert into {ToSqlName(tableName)} ({strColumns}) \nvalues ({strValues});\nselect LAST_INSERT_ID() as `ID`;";
        }

        /// <summary>
        /// 生成带标识列的批量插入 SQL，返回首个插入的 ID。
        /// </summary>
        public override string BuildBatchIdentityInsertSql(IDbCommand command, ColumnDefinition identityColumn, string tableName, string columns, List<string> valuesList)
        {
            return $"{BuildBatchInsertSql(tableName, columns, valuesList)};\nselect LAST_INSERT_ID() as `ID`;";
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
            return $"select {select} \nfrom {from} {where} Order by {orderBy} limit {startIndex},{sectionSize}";
        }

        /// <summary>
        /// 获取自增标识 SQL 片段。
        /// </summary>
        protected override string GetAutoIncrementSql() => "AUTO_INCREMENT";

        /// <summary>
        /// 生成加多个列的 SQL 语句。
        /// </summary>
        public override string BuildAddColumnsSql(string tableName, IEnumerable<ColumnDefinition> columns)
        {
            var colSqls = columns.Select(c => $"ADD {ToSqlName(c.Name)} {GetSqlType(c)}{(c.AllowNull ? " NULL" : (c.IsIdentity ? "" : " NOT NULL"))}");
            return $"ALTER TABLE {ToSqlName(tableName)} {string.Join(", ", colSqls)}";
        }

        /// <summary>
        /// 生成更新或插入（Upsert）的 SQL 语句 (MySQL 风格)。
        /// 使用 INSERT ... ON DUPLICATE KEY UPDATE 语法实现。
        /// </summary>
        /// <param name="command">数据库命令。</param>
        /// <param name="tableName">目标表名。</param>
        /// <param name="insertColumns">插入列。</param>
        /// <param name="insertValues">插入值。</param>
        /// <param name="updateSets">更新集。</param>
        /// <param name="keyColumns">关键列。</param>
        /// <param name="identityColumn">标识列。</param>
        /// <returns>返回 MySQL Upsert SQL 字符串。成功插入返回自增 ID，更新则返回 -1。</returns>
        public override string BuildUpsertSql(IDbCommand command, string tableName, string insertColumns, string insertValues, string updateSets, IEnumerable<ColumnDefinition> keyColumns, ColumnDefinition identityColumn)
        {
            return $"INSERT INTO {ToSqlName(tableName)} ({insertColumns}) VALUES ({insertValues}) ON DUPLICATE KEY UPDATE {updateSets}; SELECT IF(ROW_COUNT() = 1, LAST_INSERT_ID(), -1);";
        }
    }
}
