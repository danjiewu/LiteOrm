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

        public override void ToSqlName(ref ValueStringBuilder sb, ReadOnlySpan<char> simpleName)
        {
            simpleName = simpleName.Trim();
            if (simpleName.IsEmpty) return;
            if (simpleName[0] != '`') sb.Append('`');
            sb.Append(simpleName);
            if (simpleName[simpleName.Length - 1] != '`') sb.Append('`');
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
        /// 将结构化的 SQL 片段组装成最终的 SELECT 语句 (MySQL 实现)。
        /// 使用 LIMIT [offset,] row_count 语法进行分页。
        /// </summary>
        public override void BuildSelectSql(ref SqlValueStringBuilder subSelect, ref ValueStringBuilder result)
        {
            if (subSelect.Select.Length == 0) result.Append("SELECT *");
            else
            {
                result.Append("SELECT ");
                result.Append(subSelect.Select.AsSpan());
            }

            if (subSelect.From.Length > 0)
            {
                result.Append(" \nFROM ");
                result.Append(subSelect.From.AsSpan());
            }

            if (subSelect.Where.Length > 0)
            {
                result.Append(" \nWHERE ");
                result.Append(subSelect.Where.AsSpan());
            }

            if (subSelect.GroupBy.Length > 0)
            {
                result.Append(" \nGROUP BY ");
                result.Append(subSelect.GroupBy.AsSpan());
            }

            if (subSelect.Having.Length > 0)
            {
                result.Append(" \nHAVING ");
                result.Append(subSelect.Having.AsSpan());
            }

            if (subSelect.OrderBy.Length > 0)
            {
                result.Append(" \nORDER BY ");
                result.Append(subSelect.OrderBy.AsSpan());
            }

            if (subSelect.Take > 0)
            {
                result.Append(" \nLIMIT ");
                if (subSelect.Skip > 0)
                {
                    result.Append(subSelect.Skip.ToString());
                    result.Append(",");
                }
                result.Append(subSelect.Take.ToString());
            }
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
