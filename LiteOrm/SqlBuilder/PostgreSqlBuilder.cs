using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;


namespace LiteOrm
{
    /// <summary>
    /// PostgreSql 生成 SQL 语句的辅助类。
    /// </summary>
    public class PostgreSqlBuilder : SqlBuilder
    {
        /// <summary>
        /// PostgreSql SQL 构建器实例。
        /// </summary>
        public static readonly new PostgreSqlBuilder Instance = new PostgreSqlBuilder();

        /// <summary>
        /// 构建带有标识列或序列插入的 SQL 语句。
        /// </summary>
        /// <param name="command">数据库命令对象。</param>
        /// <param name="identityColumn">标识列或含有序列的列定义。</param>
        /// <param name="tableName">表名。</param>
        /// <param name="strColumns">插入列名部分。</param>
        /// <param name="strValues">插入值名部分。</param>
        /// <returns>构建后的 SQL 语句。</returns>
        public override string BuildIdentityInsertSql(IDbCommand command, ColumnDefinition identityColumn, string tableName, string strColumns, string strValues)
        {
            return $"INSERT INTO {ToSqlName(tableName)} ({strColumns}) \nVALUES ({strValues}) RETURNING {ToSqlName(identityColumn.Name)}";
        }

        /// <summary>
        /// 将通用名称转换为 PostgreSql 限定的参数名称。
        /// </summary>
        /// <param name="nativeName">通用名称</param>
        /// <returns>带@前缀的参数名称</returns>
        public override string ToParamName(string nativeName)
        {
            return $"@{nativeName}";
        }

        /// <summary>
        /// 名称转化为PostgreSql数据库合法名称"mytable"."col"
        /// </summary>
        /// <param name="name">字符串名称</param>
        /// <returns>数据库合法名称</returns>
        public override string ToSqlName(string name)
        {
            if (name is null) throw new ArgumentNullException("name");
            return String.Join(".", Array.ConvertAll(name.Split('.'), n => $"\"{n.ToLower()}\""));
        }

        /// <summary>
        /// 获取自增标识 SQL 片段。
        /// </summary>
        protected override string GetAutoIncrementSql() => "";

        /// <summary>
        /// 获取 PostgreSql 列类型。
        /// </summary>
        protected override string GetSqlType(ColumnDefinition column)
        {
            if (column.IsIdentity)
            {
                return column.DbType == DbType.Int64 ? "BIGSERIAL" : "SERIAL";
            }
            return base.GetSqlType(column);
        }

        /// <summary>
        /// 生成添加多个列的 SQL 语句。
        /// </summary>
        public override string BuildAddColumnsSql(string tableName, IEnumerable<ColumnDefinition> columns)
        {
            var colSqls = columns.Select(c => $"ADD COLUMN {ToSqlName(c.Name)} {GetSqlType(c)}{(c.AllowNull ? " NULL" : (c.IsIdentity ? "" : " NOT NULL"))}");
            return $"ALTER TABLE {ToSqlName(tableName)} {string.Join(", ", colSqls)}";
        }

        /// <summary>
        /// 生成 PostgreSql 专用的批量更新 SQL 语句（采用 JOIN 方式）。
        /// </summary>
        public override string BuildBatchUpdateSql(string tableName, ColumnDefinition[] updatableColumns, ColumnDefinition[] keyColumns, int batchSize)
        {
            var sb = ValueStringBuilder.Create(2048);
            string sqlTableName = ToSqlName(tableName);
            int paramsPerRecord = updatableColumns.Length + keyColumns.Length;

            sb.Append("UPDATE ");
            sb.Append(sqlTableName);
            sb.Append(" T SET ");
            for (int i = 0; i < updatableColumns.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(ToSqlName(updatableColumns[i].Name));
                sb.Append(" = S.");
                sb.Append(ToSqlName("v" + i));
            }

            sb.Append("\nFROM (");
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
            sb.Append(") S WHERE ");
            for (int k = 0; k < keyColumns.Length; k++)
            {
                if (k > 0) sb.Append(" AND ");
                sb.Append("T.");
                sb.Append(ToSqlName(keyColumns[k].Name));
                sb.Append(" = S.");
                sb.Append(ToSqlName("v" + (updatableColumns.Length + k)));
            }

            string result = sb.ToString();
            sb.Dispose();
            return result;
        }
    }
}

