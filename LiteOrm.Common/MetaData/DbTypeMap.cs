using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;

namespace LiteOrm.Common
{
    /// <summary>
    /// 提供 Type 与 DbType 之间的双向映射工具类。
    /// </summary>
    public static class DbTypeMap
    {
        private static readonly ConcurrentDictionary<Type, DbType> _typeToDbType = new();
        private static readonly ConcurrentDictionary<DbType, Type> _dbTypeToType = new();

        /// <summary>
        /// 初始化 <see cref="DbTypeMap"/> 类的静态构造函数。
        /// </summary>
        static DbTypeMap()
        {
            InitializeDefaults();
        }

        private static void InitializeDefaults()
        {
            Set(typeof(Enum), DbType.Int32);
            Set(typeof(byte), DbType.Byte);
            Set(typeof(byte[]), DbType.Binary);
            Set(typeof(char), DbType.String);
            Set(typeof(bool), DbType.Boolean);
            Set(typeof(DateTime), DbType.DateTime);
            Set(typeof(decimal), DbType.Decimal);
            Set(typeof(double), DbType.Double);
            Set(typeof(Guid), DbType.Guid);
            Set(typeof(short), DbType.Int16);
            Set(typeof(ushort), DbType.UInt16);
            Set(typeof(int), DbType.Int32);
            Set(typeof(uint), DbType.UInt32);
            Set(typeof(long), DbType.Int64);
            Set(typeof(ulong), DbType.UInt64);
            Set(typeof(sbyte), DbType.SByte);
            Set(typeof(float), DbType.Single);
            Set(typeof(string), DbType.String);
            Set(typeof(TimeSpan), DbType.Time);
            Set(typeof(ushort), DbType.UInt16);
            Set(typeof(uint), DbType.UInt32);
            Set(typeof(ulong), DbType.UInt64);
            Set(typeof(DateTimeOffset), DbType.DateTimeOffset);
        }

        /// <summary>
        /// 注册双向映射关系。
        /// </summary>
        public static void Set(Type type, DbType dbType)
        {
            _typeToDbType[type] = dbType;
            // 反向映射：如果存在多个类型映射到同一个 DbType，后者注册的将覆盖之前的映射
            _dbTypeToType[dbType] = type;
        }

        /// <summary>
        /// 获取 Type 对应的 DbType。
        /// </summary>
        public static DbType GetDbType(Type type)
        {
            Type underlyingType = type.GetUnderlyingType() ;

            if (!_typeToDbType.ContainsKey(underlyingType) && underlyingType.IsEnum)
                underlyingType = typeof(Enum);

            return _typeToDbType.TryGetValue(underlyingType, out var dbType) ? dbType : DbType.Object;
        }

        /// <summary>
        /// 获取 DbType 对应的 Type。
        /// </summary>
        public static Type GetType(DbType dbType)
        {
            return _dbTypeToType.TryGetValue(dbType, out var type) ? type : typeof(object);
        }

        /// <summary>
        /// 获取指定数据库类型的默认长度。
        /// </summary>
        /// <param name="columnType">数据库列的数据类型。</param>
        /// <returns>默认存储长度。</returns>
        public static int GetDefaultLength(DbType columnType)
        {
            switch (columnType)
            {
                case DbType.Byte:
                case DbType.SByte:
                case DbType.Boolean: return 1;
                case DbType.Int16:
                case DbType.UInt16: return 2;
                case DbType.Single:
                case DbType.UInt32:
                case DbType.Int32: return 4;
                case DbType.Int64:
                case DbType.UInt64:
                case DbType.Double: return 8;
                case DbType.String:
                case DbType.AnsiString:
                case DbType.AnsiStringFixedLength:
                case DbType.StringFixedLength: return 255;
                case DbType.Xml: return 1 << 16;
                case DbType.Binary: return Int32.MaxValue;
                default: return 0;
            }
        }

        /// <summary>
        /// 将 DbType 转换为对应的 .NET 类型。
        /// </summary>
        public static Type ToType(this DbType dbType)
        {
            return GetType(dbType);
        }

        ///<summary>
        /// 获取类型的基础类型。如果是 Nullable&lt;T&gt; 则返回 T，否则返回原类型。
        /// </summary>
        public static Type GetUnderlyingType(this Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                return Nullable.GetUnderlyingType(type);
            }
            return type;
        }
    }
}