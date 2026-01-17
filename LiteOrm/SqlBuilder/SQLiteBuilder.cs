using System;
using System.Collections.Generic;
using System.Text;
using LiteOrm.Common;
using System.Collections;
using System.Data;

namespace LiteOrm.SqlBuilder
{
    /// <summary>
    /// SQLite SQL 构建器。
    /// </summary>
    public class SQLiteBuilder : BaseSqlBuilder
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
    }
}
