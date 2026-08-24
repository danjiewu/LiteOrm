using System;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace LiteOrm.Common
{
    /// <summary>
    /// <see cref="ColumnDefinition"/> 的扩展方法。
    /// </summary>
    public static class ColumnDefinitionExtensions
    {
        /// <summary>
        /// 获取列的有效 <see cref="System.Data.DbType"/>（调用数据库时的映射结果，用于
        /// <see cref="System.Data.Common.DbParameter.DbType"/> 或读取器方法选择）。
        /// 优先使用 <see cref="ColumnDefinition.DbType"/>；未显式指定时：
        /// 集合类型属性推断为 <see cref="DbValueType.Array"/>，
        /// 其余类型委托 <paramref name="dbConverter"/> 根据属性类型推断。
        /// </summary>
        /// <param name="column">列定义。</param>
        /// <param name="dbConverter">数据库类型转换器，用于在 <see cref="ColumnDefinition.DbType"/> 为 <see cref="DbValueType.Default"/> 时推断类型。</param>
        /// <returns>有效的 <see cref="DbType"/> 值。</returns>
        public static DbType GetDbType(this ColumnDefinition column, IDbConverter dbConverter)
        {
            if (column is null) throw new ArgumentNullException(nameof(column));
            if (dbConverter is null) throw new ArgumentNullException(nameof(dbConverter));
            return dbConverter.ToDbType(GetDbValueType(column, dbConverter));
        }

        /// <summary>
        /// 获取列的有效 <see cref="DbValueType"/>。
        /// 优先使用 <see cref="ColumnDefinition.DbType"/>；未显式指定时，
        /// 集合类型属性按 <see cref="DbValueType.Array"/> 推断，其余委托
        /// <paramref name="dbConverter"/> 推断。
        /// </summary>
        /// <param name="column">列定义。</param>
        /// <param name="dbConverter">数据库类型转换器，用于在 <see cref="ColumnDefinition.DbType"/> 为 <see cref="DbValueType.Default"/> 时推断类型。</param>
        /// <returns>有效的 <see cref="DbValueType"/> 值。</returns>
        public static DbValueType GetDbValueType(this ColumnDefinition column, IDbConverter dbConverter)
        {
            if (column is null) throw new ArgumentNullException(nameof(column));
            if (dbConverter is null) throw new ArgumentNullException(nameof(dbConverter));
            if (column.DbType != DbValueType.Default) return column.DbType;
            if (DbConverterHelper.IsCollectionType(column.PropertyType)) return DbValueTypeMap.InferFromPropertyType(column.PropertyType);
            return dbConverter.GetDbValueType(column.PropertyType);
        }


        /// <summary>
        /// 从 <paramref name="target"/> 取列值并转换为数据库可接受的值（写入方向的列级入口，从实体取值场景）：
        /// 等价于 <see cref="ToDbValue(ColumnDefinition, object?)"/>(<see cref="SqlColumn.GetValue"/> 的结果)。
        /// </summary>
        /// <param name="column">列定义。</param>
        /// <param name="target">实体对象（从中取列值）。</param>
        /// <returns>数据库可接受的值。</returns>
        public static object GetToDbValue(this ColumnDefinition column, object? target)
        {
            if (column is null) throw new ArgumentNullException(nameof(column));
            return column.ToDbValue(column.GetValue(target));
        }

        /// <summary>
        /// 将裸值按列上下文转换为数据库可接受的值（写入方向的列级入口，裸值场景，如主键查询条件、时间戳条件）：
        /// null 返回 <see cref="DBNull.Value"/>；列级转换器（<see cref="SqlColumn.DbValueConverter"/>）解析并返回结果。
        /// </summary>
        /// <param name="column">列定义（提供列级转换器与列取值类型上下文）。</param>
        /// <param name="value">要转换的裸值（非从实体属性取得）。</param>
        /// <returns>数据库可接受的值。</returns>
        public static object ToDbValue(this ColumnDefinition column, object? value)
        {
            if (column is null) throw new ArgumentNullException(nameof(column));
            if (value is null) return DBNull.Value;
            return column.DbValueConverter?.DbWriteConverter is DbConvertHandler write ? write(value) : value;
        }

        /// <summary>
        /// 将数据库取得的值转换为列属性类型的值（读取方向的列级入口，裸值场景，如批量存在性检查的主键比较值）：
        /// 空值短路（null / <see cref="DBNull"/> / 空字符串 → 属性类型默认值）后，
        /// 列级转换器（<see cref="SqlColumn.DbValueConverter"/>）解析并回填列；
        /// 取 <see cref="IDbValueConverter.DbReadConverter"/> 委托执行，委托为 null 时直接返回原值（严格无兜底）。
        /// </summary>
        /// <param name="column">列定义。</param>
        /// <param name="dbValue">数据库取得的原始值。</param>
        /// <returns>列属性类型的值。</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "GetUnderlyingType only checks Nullable<T> and is safe for known property types.")]
        public static object? FromDbValue(this ColumnDefinition column, object? dbValue)
        {
            if (column is null) throw new ArgumentNullException(nameof(column));

            // 空值短路：null / DBNull / 空字符串 → 属性类型默认值（引用类型与可空类型为 null，非可空值类型为零值）。
            // 列级与注册转换器均不应收到空值（如 SQLite ALTER TABLE 加列产生的 DEFAULT '' 旧数据）。
            if (dbValue is null || dbValue == DBNull.Value || (dbValue is string empty && empty.Length == 0))
            {
                return column.PropertyType.IsValueType && Nullable.GetUnderlyingType(column.PropertyType) is null
                    ? DbConverterHelper.CreateDefaultValue(column.PropertyType.GetUnderlyingType())
                    : null;
            }

            return column.DbValueConverter?.DbReadConverter is DbConvertHandler read ? read(dbValue) : dbValue;
        }

        /// <summary>
        /// 将数据库取得的值转换为列属性类型的值后直接写入 <paramref name="target"/> 的对应属性
        /// （读取方向的列级入口，写入实体场景，如自增主键回填；与写入方向的
        /// <see cref="GetToDbValue(ColumnDefinition, object?)"/> 对称）：
        /// 转换链路同 <see cref="FromDbValue(ColumnDefinition, object?)"/>。
        /// </summary>
        /// <param name="column">列定义。</param>
        /// <param name="target">实体对象（转换结果写入其对应属性）。</param>
        /// <param name="dbValue">数据库取得的原始值。</param>
        public static void SetFromDbValue(this ColumnDefinition column, object? target, object? dbValue)
        {
            if (column is null) throw new ArgumentNullException(nameof(column));
            column.SetValue(target, FromDbValue(column, dbValue));
        }
    }
}
