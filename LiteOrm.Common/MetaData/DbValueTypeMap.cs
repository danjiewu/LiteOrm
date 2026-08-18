using System;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace LiteOrm.Common
{
    /// <summary>
    /// 提供 <see cref="DbValueType"/> 与 <see cref="Type"/> / <see cref="DbType"/> 之间的映射，
    /// 以及基于属性类型的自动推断辅助方法。
    /// <para>
    /// 内部代码统一使用 <see cref="DbValueType"/>；仅在数据库操作边界
    ///（<c>DbParameter.DbType</c> 赋值、<c>DataReader</c> 类型化读取方法选择）时
    /// 通过 <see cref="ToDbType(DbValueType)"/> 转换为 <see cref="DbType"/>。
    /// </para>
    /// </summary>
    public static class DbValueTypeMap
    {
        private static readonly ConcurrentDictionary<Type, DbValueType> _typeToDbValueType = new();
        private static readonly ConcurrentDictionary<DbValueType, Type> _dbValueTypeToType = new();

        static DbValueTypeMap()
        {
            Set(typeof(Enum), DbValueType.Int32);
            Set(typeof(byte), DbValueType.Byte);
            Set(typeof(byte[]), DbValueType.Binary);
            Set(typeof(char), DbValueType.String);
            Set(typeof(bool), DbValueType.Boolean);
            Set(typeof(DateTime), DbValueType.DateTime);
            Set(typeof(decimal), DbValueType.Decimal);
            Set(typeof(double), DbValueType.Double);
            Set(typeof(Guid), DbValueType.Guid);
            Set(typeof(short), DbValueType.Int16);
            Set(typeof(ushort), DbValueType.UInt16);
            Set(typeof(int), DbValueType.Int32);
            Set(typeof(uint), DbValueType.UInt32);
            Set(typeof(long), DbValueType.Int64);
            Set(typeof(ulong), DbValueType.UInt64);
            Set(typeof(sbyte), DbValueType.SByte);
            Set(typeof(float), DbValueType.Single);
            Set(typeof(string), DbValueType.String);
            Set(typeof(TimeSpan), DbValueType.Time);
            Set(typeof(DateTimeOffset), DbValueType.DateTimeOffset);
        }

        /// <summary>
        /// 注册双向映射关系。
        /// </summary>
        public static void Set(Type type, DbValueType dbValueType)
        {
            _typeToDbValueType[type] = dbValueType;
            _dbValueTypeToType[dbValueType] = type;
        }

        /// <summary>
        /// 获取 Type 对应的 <see cref="DbValueType"/>。
        /// </summary>
        public static DbValueType GetDbValueType(Type type)
        {
            if (!_typeToDbValueType.ContainsKey(type) && type.IsEnum)
                type = typeof(Enum);
            return _typeToDbValueType.TryGetValue(type, out var dbValueType) ? dbValueType : DbValueType.Object;
        }

        /// <summary>
        /// 获取 <see cref="DbValueType"/> 对应的 Type。
        /// </summary>
        public static Type GetType(DbValueType dbValueType)
        {
            return _dbValueTypeToType.TryGetValue(dbValueType, out var type) ? type : typeof(object);
        }

        /// <summary>
        /// 将 <see cref="DbValueType"/> 转换为对应的 .NET 类型。
        /// </summary>
        public static Type ToType(this DbValueType dbValueType) => GetType(dbValueType);

        ///<summary>
        /// 获取类型的基础类型。如果是 Nullable&lt;T&gt; 则返回 T，否则返回原类型。
        /// </summary>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2073", Justification = "Nullable.GetUnderlyingType returns a Type without annotation; the caller-supplied Type already has members preserved per context requirements; this method only unwraps Nullable<T>.")]
#endif
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        public static Type GetUnderlyingType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] this Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return Nullable.GetUnderlyingType(type) ?? type;
            return type;
        }

        /// <summary>
        /// 判断指定取值类型是否包含数组掩码（<see cref="DbValueType.Array"/>）。
        /// </summary>
        public static bool HasArray(this DbValueType dbValueType)
            => (dbValueType & DbValueType.Array) == DbValueType.Array;

        /// <summary>
        /// 剥离 <see cref="DbValueType.Array"/> 掩码，返回标量部分。
        /// </summary>
        public static DbValueType StripArray(this DbValueType dbValueType)
            => dbValueType & ~DbValueType.Array;

        /// <summary>
        /// 将 <see cref="DbValueType"/> 映射为 <see cref="DbType"/>（数据库操作边界转换）。
        /// 先剥离 <see cref="DbValueType.Array"/> 掩码，再将标量部分映射为 <see cref="DbType"/>：
        /// 若剥离后无标量类型（仅为掩码）或为 <see cref="DbValueType.Default"/>，映射为 <see cref="DbType.Object"/>；
        /// <see cref="DbValueType.Json"/> / <see cref="DbValueType.Jsonb"/> 映射为 <see cref="DbType.String"/>；
        /// 其余直接按对齐的枚举值转换。
        /// </summary>
        public static DbType ToDbType(this DbValueType dbValueType)
        {
            var scalar = dbValueType.StripArray();
            if (scalar == DbValueType.Default || scalar == 0) return DbType.Object;
            if (scalar == DbValueType.Json || scalar == DbValueType.Jsonb) return DbType.String;
            return (DbType)scalar;
        }

        /// <summary>
        /// 将 <see cref="DbType"/> 映射为对应的 <see cref="DbValueType"/>（枚举值对齐，直接转换）。
        /// </summary>
        public static DbValueType FromDbType(DbType dbType) => (DbValueType)dbType;

        /// <summary>
        /// 根据属性 CLR 类型推断 <see cref="DbValueType"/>：
        /// <list type="bullet">
        /// <item><see cref="byte"/>[] → <see cref="DbValueType.Binary"/></item>
        /// <item>数组/集合 → 标量类型 | <see cref="DbValueType.Array"/> 掩码</item>
        /// <item>其余类型 → 通过 <see cref="GetDbValueType"/> 映射</item>
        /// </list>
        /// </summary>
        public static DbValueType InferFromPropertyType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
        {
            if (type is null) return DbValueType.Object;
            type = type.GetUnderlyingType();
            if (type == typeof(byte[])) return DbValueType.Binary;
            if (ColumnDefinitionExtensions.IsCollectionType(type))
            {
                Type? elementType = ColumnDefinitionExtensions.GetCollectionElementType(type);
                if (elementType != null)
                    return GetDbValueType(elementType) | DbValueType.Array;
                return DbValueType.Object | DbValueType.Array;
            }
            return GetDbValueType(type);
        }
    }
}
