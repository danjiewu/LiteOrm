using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;

namespace LiteOrm
{

    /// <summary>
    /// SQL Server 生成 SQL 语句的辅助类。
    /// </summary>
    public class SqlServerBuilder : SqlBuilder
    {
        /// <summary>
        /// 获取 <see cref="SqlServerBuilder"/> 的单例实例。
        /// </summary>
        public static readonly new SqlServerBuilder Instance = new SqlServerBuilder();

        /// <summary>
        /// 是否支持带自增列的批量插入并返回首个 ID。
        /// </summary>
        public override bool SupportBatchInsertWithIdentity => true;


        /// <summary>
        /// 使用传入的 <see cref="ValueStringBuilder"/> 构建字符串连接 SQL 片段。
        /// </summary>
        /// <param name="sb">用于接收 SQL 片段的字符串构建器。</param>
        /// <param name="strs">需要连接的sql字符串</param>
        public override void BuildConcatSql(ref ValueStringBuilder sb, params string[] strs)
        {
            for (int i = 0; i < strs.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(strs[i]);
            }
        }

        /// <summary>
        /// 生成带标识列的批量插入 SQL，返回首个插入的 ID。
        /// </summary>
        public override string BuildBatchIdentityInsertSql(ColumnDefinition identityColumn, string tableName, string columns, List<string> valuesList)
        {
            return $"{BuildBatchInsertSql(tableName, columns, valuesList)}; SELECT SCOPE_IDENTITY() - ({valuesList.Count - 1}) * {identityColumn.IdentityIncreasement} AS [ID];";
        }

        /// <summary>
        /// 生成 SQL Server 专用的批量更新 SQL 语句（使用 UPDATE ... SET ... FROM ... INNER JOIN (VALUES ...) 方式）。
        /// 针对 SQL Server 优化：使用 VALUES 子句和 INNER JOIN 批量处理更新操作，提高性能。
        /// </summary>
        /// <param name="tableName">目标表名。</param>
        /// <param name="updatableColumns">可更新列集合。</param>
        /// <param name="keyColumns">主键列集合。</param>
        /// <param name="batchSize">批次大小。</param>
        /// <returns>返回 SQL Server 可执行的批量更新 SQL 字符串。</returns>
        public override string BuildBatchUpdateSql(string tableName, ColumnDefinition[] updatableColumns, ColumnDefinition[] keyColumns, int batchSize)
        {
            if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than 0");
            if (keyColumns.Length == 0) throw new ArgumentException("At least one key column is required", nameof(keyColumns));
            if (updatableColumns.Length == 0) throw new ArgumentException("At least one updatable column is required", nameof(updatableColumns));

           
            string sqlTableName = ToSqlName(tableName);
            int paramsPerRecord = updatableColumns.Length + keyColumns.Length;
            var sb = ValueStringBuilder.Create(128+paramsPerRecord*batchSize*8);

            // 构建 UPDATE 语句
            sb.Append("UPDATE T SET ");
            for (int i = 0; i < updatableColumns.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(ToSqlName(updatableColumns[i].Name));
                sb.Append(" = S.v");
                sb.Append(i.ToString());
            }

            // 构建 FROM 子句
            sb.Append("\nFROM ");
            sb.Append(sqlTableName);
            sb.Append(" T");

            // 构建 INNER JOIN 子句和 VALUES 子查询
            sb.Append("\nINNER JOIN (");
            sb.Append("VALUES ");
            for (int b = 0; b < batchSize; b++)
            {
                if (b > 0) sb.Append(", ");
                sb.Append("(");
                for (int i = 0; i < paramsPerRecord; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(ToSqlParam("p" + (b * paramsPerRecord + i)));
                }
                sb.Append(")");
            }
            sb.Append(") AS S (");

            // 定义 VALUES 子句的列名
            for (int i = 0; i < updatableColumns.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(ToSqlName("v" + i));
            }
            for (int k = 0; k < keyColumns.Length; k++)
            {
                if (updatableColumns.Length > 0 || k > 0) sb.Append(", ");
                sb.Append(ToSqlName("k" + k));
            }
            sb.Append(") ON (");

            // 构建 ON 子句
            for (int k = 0; k < keyColumns.Length; k++)
            {
                if (k > 0) sb.Append(" AND ");
                sb.Append("T.");
                sb.Append(ToSqlName(keyColumns[k].Name));
                sb.Append(" = S.k");
                sb.Append(k.ToString());
            }
            sb.Append(")");

            string result = sb.ToString();
            sb.Dispose();
            return result;
        }
    }
}

