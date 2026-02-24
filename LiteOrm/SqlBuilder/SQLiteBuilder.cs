using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;


namespace LiteOrm
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
        /// 返回指定类型对应的Sqlite数据库类型。
        /// </summary>
        /// <param name="type">要转换的类型。</param>
        /// <returns></returns>
        /// <remarks>Sqlite不支持DateTime、TimeSpan类型，将被映射为DbType.String类型</remarks>
        public override DbType GetDbType(Type type)
        {
            Type underlyingType = type.GetUnderlyingType();
            if (underlyingType == typeof(DateTime) || underlyingType == typeof(TimeSpan) || underlyingType == typeof(DateTimeOffset)) return DbType.String;
            return base.GetDbType(type);
        }

        /// <summary>
        /// 将对象值转换为数据库值，Sqlite 中 DateTime、TimeSpan 类型将被转换为字符串存储。
        /// </summary>
        /// <param name="value">要转换的对象值。</param>
        /// <param name="dbType">要转换的数据库类型。</param>
        /// <returns>转换后的数据库值。</returns>
        public override object ConvertToDbValue(object value, DbType dbType = DbType.Object)
        {
            if (value is DateTime dt)
            {
                // SQLite中存储为字符串格式 "yyyy-MM-dd HH:mm:ss.fff"
                return dt.ToString("yyyy-MM-dd HH:mm:ss.fff");
            }
            else if (value is DateTimeOffset dto)
            {
                return dto.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
            }
            else if (value is TimeSpan ts)
            {
                // SQLite中存储为字符串格式 "hh:mm:ss.fff"
                return ts.ToString("c");
            }
            return base.ConvertToDbValue(value, dbType);
        }
        /// <summary>
        /// 构建插入并返回自增标识的 SQL 语句。
        /// </summary>
        public override string BuildIdentityInsertSql(IDbCommand command, ColumnDefinition identityColumn, string tableName, string strColumns, string strValues)
        {
            return $"INSERT INTO {ToSqlName(tableName)} ({strColumns}) \nVALUES ({strValues});\nSELECT LAST_INSERT_ROWID() AS [ID];";
        }

        /// <summary>
        /// 获取自增标识 SQL 片段。
        /// </summary>
        protected override string GetAutoIncrementSql() => "AUTOINCREMENT";

        /// <summary>
        /// 获取 SQLite 列类型。对于主键自增列，必须使用 INTEGER。
        /// </summary>
        protected override string GetSqlType(ColumnDefinition column)
        {
            if (column.IsPrimaryKey && column.IsIdentity) return "INTEGER";
            return base.GetSqlType(column);
        }

        /// <summary>
        /// 是否支持带自增列的批量插入并返回首个 ID。
        /// </summary>
        public override bool SupportBatchInsertWithIdentity => true;

        /// <summary>
        /// 生成带标识列的批量插入 SQL，返回首个插入的 ID。
        /// </summary>
        public override string BuildBatchIdentityInsertSql(IDbCommand command, ColumnDefinition identityColumn, string tableName, string columns, List<string> valuesList)
        {
            return $"{BuildBatchInsertSql(tableName, columns, valuesList)}; SELECT LAST_INSERT_ROWID() - ({valuesList.Count - 1}) AS [ID];";
        }

        /// <summary>
        /// 生成添加多个列的 SQL 语句。
        /// </summary>

        public override string BuildAddColumnsSql(string tableName, IEnumerable<ColumnDefinition> columns)
        {
            var sqlName = ToSqlName(tableName);
            var colSqls = columns.Select(c => $"ALTER TABLE {sqlName} ADD COLUMN {ToSqlName(c.Name)} {GetSqlType(c)}{(c.AllowNull ? " NULL" : (c.IsIdentity ? "" : " NOT NULL"))}");
            return string.Join("; ", colSqls);
        }

        /// <summary>
        /// 将结构化的 SQL 片段组装成最终的 SELECT 语句 (SQLite 实现)。
        /// 使用 LIMIT n OFFSET m 语法进行分页。
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
                result.Append(subSelect.Take.ToString());
                if (subSelect.Skip > 0)
                {
                    result.Append(" OFFSET ");
                    result.Append(subSelect.Skip.ToString());
                }
            }
        }

        /// <summary>
        /// 生成 SQLite 专用的批量更新 SQL 语句，使用 CTE + Values 方式批量更新。
        /// </summary>
        public override string BuildBatchUpdateSql(string tableName, ColumnDefinition[] updatableColumns, ColumnDefinition[] keyColumns, int batchSize)
        {
            if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0");
            if (keyColumns.Length == 0) throw new ArgumentException("At least one key column is required", nameof(keyColumns));
            if (updatableColumns.Length == 0) throw new ArgumentException("At least one updatable column is required", nameof(updatableColumns));

            int paramsPerRecord = updatableColumns.Length + keyColumns.Length;
            var sb = ValueStringBuilder.Create(256 + batchSize * paramsPerRecord * 10);
            string sqlTableName = ToSqlName(tableName);

            // 1. 构建CTE部分 (WITH clause)
            sb.Append("WITH batch_data(");

            // 先更新列，后键列
            for (int i = 0; i < updatableColumns.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(ToSqlName(updatableColumns[i].Name));
            }

            for (int k = 0; k < keyColumns.Length; k++)
            {
                sb.Append(", ");
                sb.Append(ToSqlName(keyColumns[k].Name));
            }

            sb.Append(") AS (\n    VALUES ");

            // 2. 构建VALUES部分，使用数字参数名
            for (int b = 0; b < batchSize; b++)
            {
                if (b > 0) sb.Append(",\n           ");
                sb.Append("(");

                int paramBase = b * paramsPerRecord;

                // 更新列参数
                for (int i = 0; i < updatableColumns.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(ToSqlParam($"p{paramBase + i}"));
                }

                // 键列参数 
                for (int k = 0; k < keyColumns.Length; k++)
                {
                    sb.Append(", ");
                    sb.Append(ToSqlParam($"p{paramBase + updatableColumns.Length + k}"));
                }

                sb.Append(")");
            }

            sb.Append("\n)\n");

            // 3. 构建UPDATE语句
            sb.Append("UPDATE ");
            sb.Append(sqlTableName);
            sb.Append("\nSET\n");

            // 构建SET子句：每个可更新列对应一个子查询
            for (int i = 0; i < updatableColumns.Length; i++)
            {
                if (i > 0) sb.Append(",\n");
                sb.Append("    ");
                sb.Append(ToSqlName(updatableColumns[i].Name));
                sb.Append(" = (\n        SELECT ");
                sb.Append(ToSqlName(updatableColumns[i].Name));
                sb.Append("\n        FROM batch_data\n        WHERE ");

                // 构建WHERE条件连接主键
                for (int k = 0; k < keyColumns.Length; k++)
                {
                    if (k > 0) sb.Append("\n          AND ");
                    sb.Append(sqlTableName);
                    sb.Append(".");
                    sb.Append(ToSqlName(keyColumns[k].Name));
                    sb.Append(" = batch_data.");
                    sb.Append(ToSqlName(keyColumns[k].Name));
                }

                sb.Append("\n    )");
            }

            // 4. 构建WHERE子句
            sb.Append("\nWHERE EXISTS (\n    SELECT 1\n    FROM batch_data\n    WHERE ");

            for (int k = 0; k < keyColumns.Length; k++)
            {
                if (k > 0) sb.Append("\n      AND ");
                sb.Append(sqlTableName);
                sb.Append(".");
                sb.Append(ToSqlName(keyColumns[k].Name));
                sb.Append(" = batch_data.");
                sb.Append(ToSqlName(keyColumns[k].Name));
            }

            sb.Append("\n);");

            string result = sb.ToString();
            sb.Dispose();
            return result;
        }

        /// <summary>
        /// 生成创建表的 SQL 语句。SQLite 要求 AUTOINCREMENT 必须在 PRIMARY KEY 之后。
        /// </summary>
        public override string BuildCreateTableSql(string tableName, IEnumerable<ColumnDefinition> columns)
        {
            var sb = ValueStringBuilder.Create(512);
            sb.Append("CREATE TABLE ");
            sb.Append(ToSqlName(tableName));
            sb.Append(" (");
            bool first = true;
            foreach (var column in columns)
            {
                if (!first) sb.Append(",");
                sb.Append("\n  ");
                sb.Append(ToSqlName(column.Name));
                sb.Append(" ");
                sb.Append(GetSqlType(column));
                if (column.IsPrimaryKey) sb.Append(" PRIMARY KEY");
                if (column.IsIdentity)
                {
                    sb.Append(" ");
                    sb.Append(GetAutoIncrementSql());
                }
                if (!column.AllowNull && !column.IsPrimaryKey) sb.Append(" NOT NULL");
                first = false;
            }
            sb.Append("\n)");
            string result = sb.ToString();
            sb.Dispose();
            return result;
        }
    }
}