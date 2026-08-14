using System;
using System.Data;

namespace LiteOrm.Common
{
    /// <summary>
    /// 提供 <see cref="DbValueType"/> 与 <see cref="DbType"/> 之间的映射，
    /// 以及基于属性类型的自动推断辅助方法。
    /// </summary>
    public static class DbValueTypeMap
    {
        /// <summary>
        /// 判断指定取值类型是否为数组类型（<see cref="DbValueType.Array"/>）。
        /// </summary>
        /// <param name="dbValueType">取值类型。</param>
        /// <returns>为数组类型时返回 true。</returns>
        public static bool HasArray(this DbValueType dbValueType)
        {
            return dbValueType == DbValueType.Array;
        }

        /// <summary>
        /// 将 <see cref="DbValueType"/> 映射为 <see cref="DbType"/>。
        /// <see cref="DbValueType.Json"/> / <see cref="DbValueType.Jsonb"/> 映射为
        /// <see cref="DbType.String"/>，<see cref="DbValueType.Array"/> 映射为
        /// <see cref="DbType.Object"/>，其余直接按对齐的枚举值转换。
        /// </summary>
        /// <param name="dbValueType">取值类型。</param>
        /// <returns>对应的 <see cref="DbType"/>。</returns>
        public static DbType ToDbType(DbValueType dbValueType)
        {
            if (dbValueType == DbValueType.Default) return DbType.Object;
            if (dbValueType == DbValueType.Json || dbValueType == DbValueType.Jsonb) return DbType.String;
            if (dbValueType == DbValueType.Array) return DbType.Object;
            return (DbType)dbValueType;
        }

        /// <summary>
        /// 将 <see cref="DbType"/> 映射为对应的 <see cref="DbValueType"/>（枚举值对齐，直接转换）。
        /// </summary>
        /// <param name="dbType">数据库标量类型。</param>
        /// <returns>对应的 <see cref="DbValueType"/>。</returns>
        public static DbValueType FromDbType(DbType dbType)
        {
            return (DbValueType)dbType;
        }

        /// <summary>
        /// 根据属性 CLR 类型推断 <see cref="DbValueType"/>：
        /// <list type="bullet">
        /// <item><see cref="byte"/>[] → <see cref="DbValueType.Binary"/></item>
        /// <item>数组/集合 → <see cref="DbValueType.Array"/></item>
        /// <item>其余类型 → 通过 <see cref="DbTypeMap"/> 映射</item>
        /// </list>
        /// </summary>
        /// <param name="type">属性类型。</param>
        /// <returns>推断出的取值类型。</returns>
        public static DbValueType InferFromPropertyType(Type type)
        {
            if (type is null) return DbValueType.Object;
            type = type.GetUnderlyingType();
            if (type == typeof(byte[])) return DbValueType.Binary;
            if (ColumnDefinitionExtensions.IsCollectionType(type)) return DbValueType.Array;
            return FromDbType(DbTypeMap.GetDbType(type));
        }
    }
}
