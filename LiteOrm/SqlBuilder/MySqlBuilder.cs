using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;


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
            return $"INSERT INTO {ToSqlName(tableName)} ({strColumns}) \nVALUES ({strValues});\nSELECT LAST_INSERT_ID() AS `ID`;";
        }

        /// <summary>
        /// 生成带标识列的批量插入 SQL，返回首个插入的 ID。
        /// </summary>
        public override string BuildBatchIdentityInsertSql(IDbCommand command, ColumnDefinition identityColumn, string tableName, string columns, List<string> valuesList)
        {
            return $"{BuildBatchInsertSql(tableName, columns, valuesList)};\nSELECT LAST_INSERT_ID() AS `ID`;";
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
            return $"SELECT {select} \nFROM {from} {where} ORDER BY {orderBy} LIMIT {startIndex},{sectionSize}";
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
        /// 生成 MySQL 专用的批量更新 SQL 语句（采用 JOIN 方式）。
        /// </summary>
        public override string BuildBatchUpdateSql(string tableName, ColumnDefinition[] updatableColumns, ColumnDefinition[] keyColumns, int batchSize)
        {
            
            string sqlTableName = ToSqlName(tableName);
            int paramsPerRecord = updatableColumns.Length + keyColumns.Length;
            var sb = ValueStringBuilder.Create(128+paramsPerRecord*batchSize*8);
            
            sb.Append("UPDATE ");
            sb.Append(sqlTableName);
            sb.Append(" T");
            sb.Append("\nINNER JOIN (");
            for (int b = 0; b < batchSize; b++)
            {
                if (b > 0) sb.Append("\n  UNION ALL ");
                sb.Append("SELECT ");
                for (int i = 0; i < paramsPerRecord; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(ToSqlParam("p" + (b * paramsPerRecord + i)));
                    if (b == 0)
                    {
                        sb.Append(" AS ");
                        sb.Append(ToSqlName("v" + i));
                    }
                }
            }
            sb.Append(") S ON ");
            for (int k = 0; k < keyColumns.Length; k++)
            {
                if (k > 0) sb.Append(" AND ");
                sb.Append("T.");
                sb.Append(ToSqlName(keyColumns[k].Name));
                sb.Append(" = S.");
                sb.Append(ToSqlName("v" + (updatableColumns.Length + k)));
            }

            sb.Append("\nSET ");
            for (int i = 0; i < updatableColumns.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("T.");
                sb.Append(ToSqlName(updatableColumns[i].Name));
                sb.Append(" = S.");
                sb.Append(ToSqlName("v" + i));
            }

            string result = sb.ToString();
            sb.Dispose();
            return result;
        }
    }
}
