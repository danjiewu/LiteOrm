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

        public static object GetAsDbValue(this ColumnDefinition column, object target, IDbConverter? dbConverter = null)
        {
            object? value = column.GetValue(target);
            if (value is null) return DBNull.Value;
            if (dbConverter != null)
            {
                var dbType = column.GetDbValueType(dbConverter);
                IDbValueConverter? converter = column.DbValueConverter ?? dbConverter.GetDbValueConverter(column.PropertyType, dbType);
                if (converter != null)
                {
                    return converter.ConvertToDbValue(value);
                }
                // 最后兜底：目标类型一致直返；否则 ChangeType，失败时原样返回交由驱动绑定
                Type targetType = dbType.ToType();
                if (targetType.IsInstanceOfType(value))
                    return value;
                try
                {
                    return Convert.ChangeType(value, targetType);
                }
                catch (InvalidCastException)
                {
                    return value;
                }
            }
            return value;
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
