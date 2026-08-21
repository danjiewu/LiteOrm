using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

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
        public static DbType ToDbType(this ColumnDefinition column, IDbConverter dbConverter)
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
            if (IsCollectionType(column.PropertyType)) return DbValueTypeMap.InferFromPropertyType(column.PropertyType);
            return dbConverter.GetDbValueType(column.PropertyType);
        }

        /// <summary>
        /// 从 <paramref name="target"/> 取列值并转换为数据库可接受的值（写入方向的列级入口，从实体取值场景）：
        /// 等价于 <see cref="ToDbValue(ColumnDefinition, object?, IDbConverter?)"/>(<see cref="SqlColumn.GetValue"/> 的结果)。
        /// </summary>
        /// <param name="column">列定义。</param>
        /// <param name="target">实体对象（从中取列值）。</param>
        /// <param name="dbConverter">数据库值转换器；为 null 时仅做列级转换与裸值直返。</param>
        /// <returns>数据库可接受的值。</returns>
        public static object GetToDbValue(this ColumnDefinition column, object? target, IDbConverter? dbConverter = null)
        {
            if (column is null) throw new ArgumentNullException(nameof(column));
            return column.ToDbValue(column.GetValue(target), dbConverter);
        }

        /// <summary>
        /// 将裸值按列上下文转换为数据库可接受的值（写入方向的列级入口，裸值场景，如主键查询条件、时间戳条件）：
        /// null 返回 <see cref="DBNull.Value"/>；列级转换器（<see cref="SqlColumn.DbValueConverter"/>）优先；
        /// 否则委托 <see cref="DbConverterHelper.ToDbValue(IDbConverter, object?, DbValueType?)"/> 统一链路
        /// （注册转换器优先 + 枚举/bool/DateTimeOffset/TimeSpan 适配 + <see cref="Convert.ChangeType(object, Type)"/> 兜底；
        /// 复杂类型需按 (值类型, DbValueType) 预注册转换器，未预注册的复杂类型不处理）。
        /// </summary>
        /// <param name="column">列定义（提供列级转换器与列取值类型上下文）。</param>
        /// <param name="value">要转换的裸值（非从实体属性取得）。</param>
        /// <param name="dbConverter">数据库值转换器；为 null 时仅做列级转换与裸值直返。</param>
        /// <returns>数据库可接受的值。</returns>
        public static object ToDbValue(this ColumnDefinition column, object? value, IDbConverter? dbConverter = null)
        {
            if (column is null) throw new ArgumentNullException(nameof(column));
            if (value is null) return DBNull.Value;

            // 列级转换器优先（与读取方向的 FromDbValue 对称）
            if (column.DbValueConverter is IDbValueConverter columnConverter)
                return columnConverter.ConvertToDbValue(value);

            return dbConverter != null
                ? dbConverter.ToDbValue(value, column.GetDbValueType(dbConverter))
                : value;
        }

        /// <summary>
        /// 将数据库取得的值转换为列属性类型的值（读取方向的列级入口，裸值场景，如批量存在性检查的主键比较值）：
        /// 空值短路（null / <see cref="DBNull"/> / 空字符串 → 属性类型默认值）后，
        /// 列级转换器（<see cref="SqlColumn.DbValueConverter"/>）优先；
        /// 否则委托 <see cref="DbConverterHelper.ConvertFromDbValue(IDbConverter, object?, Type, DbValueType)"/> 统一链路
        /// （注册转换器优先 + 同类型直返 + 运行时类型注册命中 + 枚举解析 + <see cref="Convert.ChangeType(object, Type)"/> 兜底；
        /// 复杂类型需按 (值类型, DbValueType) 预注册转换器，未预注册的复杂类型不处理）。
        /// </summary>
        /// <param name="column">列定义。</param>
        /// <param name="dbValue">数据库取得的原始值。</param>
        /// <param name="dbConverter">数据库值转换器；为 null 时退化为列级转换 + <see cref="Convert.ChangeType(object, Type)"/>。</param>
        /// <returns>列属性类型的值。</returns>
        public static object? FromDbValue(this ColumnDefinition column, object? dbValue, IDbConverter? dbConverter = null)
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

            // 列级转换器优先
            if (column.DbValueConverter is IDbValueConverter columnConverter)
                return columnConverter.ConvertFromDbValue(dbValue);

            if (dbConverter is null)
                return Convert.ChangeType(dbValue, column.PropertyType.GetUnderlyingType());

            return DbConverterHelper.ConvertFromDbValue(dbConverter, dbValue, column.PropertyType, column.GetDbValueType(dbConverter));
        }

        /// <summary>
        /// 将数据库取得的值转换为列属性类型的值后直接写入 <paramref name="target"/> 的对应属性
        /// （读取方向的列级入口，写入实体场景，如自增主键回填；与写入方向的
        /// <see cref="GetToDbValue(ColumnDefinition, object?, IDbConverter?)"/> 对称）：
        /// 转换链路同 <see cref="FromDbValue(ColumnDefinition, object?, IDbConverter?)"/>。
        /// </summary>
        /// <param name="column">列定义。</param>
        /// <param name="target">实体对象（转换结果写入其对应属性）。</param>
        /// <param name="dbValue">数据库取得的原始值。</param>
        /// <param name="dbConverter">数据库值转换器；为 null 时退化为列级转换 + <see cref="Convert.ChangeType(object, Type)"/>。</param>
        public static void SetFromDbValue(this ColumnDefinition column, object? target, object? dbValue, IDbConverter? dbConverter = null)
        {
            if (column is null) throw new ArgumentNullException(nameof(column));
            column.SetValue(target, FromDbValue(column, dbValue, dbConverter));
        }

        /// <summary>
        /// 判断指定类型是否为集合类型（数组、<see cref="System.Collections.Generic.IEnumerable{T}"/> 等），
        /// 排除 <see cref="string"/> 与 <see cref="byte"/>[]。
        /// </summary>
        /// <param name="type">要判断的类型。</param>
        /// <returns>如果类型是集合类型则返回 true。</returns>
        public static bool IsCollectionType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        {
            if (type is null) return false;
            type = type.GetUnderlyingType();
            if (type == typeof(string) || type == typeof(byte[])) return false;
            return typeof(IEnumerable).IsAssignableFrom(type);
        }

        /// <summary>
        /// 解析集合类型的元素类型。
        /// 数组返回 <see cref="Type.GetElementType"/>；<c>IEnumerable&lt;T&gt;/ICollection&lt;T&gt;/IList&lt;T&gt;</c>
        /// 返回泛型参数 <c>T</c>；非泛型集合返回 <see cref="object"/>；无法解析返回 <see langword="null"/>。
        /// </summary>
        /// <param name="type">集合类型。</param>
        /// <returns>元素类型；无法解析时为 null。</returns>
        public static Type? GetCollectionElementType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        {
            if (type is null) return null;
            type = type.GetUnderlyingType();
            if (type == typeof(string) || type == typeof(byte[])) return null;

            if (type.IsArray) return type.GetElementType();

            if (type.IsGenericType)
            {
                Type genericDef = type.GetGenericTypeDefinition();
                if (genericDef == typeof(IEnumerable<>) || genericDef == typeof(ICollection<>)
                    || genericDef == typeof(IList<>) || genericDef == typeof(List<>)
                    || genericDef == typeof(IReadOnlyCollection<>) || genericDef == typeof(IReadOnlyList<>)
                    || genericDef == typeof(ISet<>) || genericDef == typeof(HashSet<>))
                {
                    return type.GetGenericArguments()[0];
                }

                // 其他泛型集合接口实现：查找其实现的 IEnumerable<T>
                Type? enumerable = type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                if (enumerable is not null) return enumerable.GetGenericArguments()[0];
            }

            if (typeof(IEnumerable).IsAssignableFrom(type)) return typeof(object);

            return null;
        }
    }
}
