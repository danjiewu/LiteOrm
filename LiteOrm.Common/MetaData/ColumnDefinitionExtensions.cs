using System;
using System.Data;

namespace LiteOrm.Common
{
    /// <summary>
    /// <see cref="ColumnDefinition"/> 的扩展方法。
    /// </summary>
    public static class ColumnDefinitionExtensions
    {
        /// <summary>
        /// 获取列的有效 <see cref="DbType"/>。
        /// 当 <see cref="ColumnDefinition.DbType"/> 已显式指定时返回该值；
        /// 否则使用 <paramref name="dbConverter"/> 根据列的属性类型自动推断。
        /// </summary>
        /// <param name="column">列定义。</param>
        /// <param name="dbConverter">数据库类型转换器，用于在 <see cref="ColumnDefinition.DbType"/> 为 null 时推断类型。</param>
        /// <returns>有效的 <see cref="DbType"/> 值。</returns>
        public static DbType ToDbType(this ColumnDefinition column, IDbConverter dbConverter)
        {
            if (column is null) throw new ArgumentNullException(nameof(column));
            if (dbConverter is null) throw new ArgumentNullException(nameof(dbConverter));
            return column.DbType ?? dbConverter.GetDbType(column.PropertyType);
        }
    }
}
