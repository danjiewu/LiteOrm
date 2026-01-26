using System;
using System.Collections.Generic;
using System.Text;
using LiteOrm.Common;
using System.Collections;
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
        /// 生成更新或插入（Upsert）的 SQL 语句 (SQLite 风格)。
        /// 使用 INSERT ... ON CONFLICT (...) DO UPDATE SET 语法。
        /// </summary>
        /// <param name="command">数据库命令。</param>
        /// <param name="tableName">目标表名。</param>
        /// <param name="insertColumns">插入列。</param>
        /// <param name="insertValues">插入值。</param>
        /// <param name="updateSets">更新集。</param>
        /// <param name="keyColumns">冲突关键列。</param>
        /// <param name="identityColumn">标识列。</param>
        /// <returns>返回 SQLite Upsert SQL 字符串。</returns>
        public override string BuildUpsertSql(IDbCommand command, string tableName, string insertColumns, string insertValues, string updateSets, IEnumerable<ColumnDefinition> keyColumns, ColumnDefinition identityColumn)
        {
            string keys = string.Join(",", keyColumns.Select(c => ToSqlName(c.Name)));
            return $"INSERT INTO {ToSqlName(tableName)} ({insertColumns}) VALUES ({insertValues}) ON CONFLICT ({keys}) DO UPDATE SET {updateSets}; SELECT last_insert_rowid();";
        }

        /// <summary>
        /// 将对象值转换为数据库值，Sqlite 中 DateTime、TimeSpan 类型将被转换为字符串存储。
        /// </summary>
        /// <param name="value">要转换的对象值。</param>
        /// <param name="dbType">要转换的数据库类型。</param>
        /// <returns>转换后的数据库值。</returns>
        public override object ConvertToDbValue(object value, DbType dbType = DbType.Object)
        {
            if(value is DateTime dt)
            {
                // SQLite中存储为字符串格式 "yyyy-MM-dd HH:mm:ss.fff"
                return dt.ToString("yyyy-MM-dd HH:mm:ss.fff");
            }
            else if(value is DateTimeOffset dto)
            {
                return dto.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
            }
            else if(value is TimeSpan ts)
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
            return $"insert into {ToSqlName(tableName)} ({strColumns}) \nvalues ({strValues});\nSELECT last_insert_rowid() as [ID];";
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
            return $"{BuildBatchInsertSql(tableName, columns, valuesList)}; select last_insert_rowid() - ({valuesList.Count - 1}) as [ID];";
        }

        /// <summary>
        /// 生成添加多个列的 SQL 语句。
        /// </summary>

        public override string BuildAddColumnsSql(string tableName, IEnumerable<ColumnDefinition> columns)
        {
            var sqlName = ToSqlName(tableName);
            var colSqls = columns.Select(c => $"ALTER TABLE {sqlName} ADD COLUMN {ToSqlName(c.Name)} {GetSqlType(c)}{(c.AllowNull ? " NULL" : (c.IsIdentity ? "" : " NOT NULL"))}");
            return string.Join(";", colSqls);
        }
    }
}
