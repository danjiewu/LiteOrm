using LiteOrm.Common;
using System;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm 默认值转换器初始化器，用于在 <see cref="SqlBuilder"/> 类型上注册默认的双向值转换器。
    /// 通过静态构造函数在首次访问时自动注册，供 <see cref="IDbConverter.GetDbValueConverter"/> 查找。
    /// 注册主键为 (值类型, DbValueType)，读取与写入共用同一注册表；
    /// 读取经 <see cref="DbConverterHelper.ApplyRead"/>，写入经 <see cref="DbConverterHelper.ApplyWrite"/> 应用注册的转换器。
    /// </summary>
    /// <remarks>
    /// 调用 <see cref="Initialize"/> 方法可显式触发静态构造函数，确保转换器在应用启动时完成注册。
    /// 静态构造函数只会执行一次，因此多次调用 <see cref="Initialize"/> 是安全的。
    /// 各方言子类可通过 <see cref="SqlBuilderExtensions"/> 的 RegisterDbValueConverter 扩展方法在自身类型上覆盖默认注册
    /// （通过继承链查找，方言注册优先于基类注册）。
    /// </remarks>
    public static class LiteOrmConverterInitializer
    {
        /// <summary>
        /// 静态构造函数，在首次访问类时自动注册默认转换器。
        /// </summary>
        static LiteOrmConverterInitializer()
        {
            RegisterDefaultConverters();
        }

        /// <summary>
        /// 触发静态构造函数以注册默认转换器。可在应用启动时调用以确保转换器已注册。
        /// 多次调用是安全的——静态构造函数只会执行一次。
        /// </summary>
        public static void Initialize() { }

        /// <summary>
        /// 在 SqlBuilder 类型上注册默认的双向值转换器。
        /// </summary>
        private static void RegisterDefaultConverters()
        {
            SqlBuilder sqlBuilder = SqlBuilder.Instance;

            // bool ↔ 数值列（数据库以数值存储布尔）：按各数值 DbValueType 的 CLR 类型严格注册 TDbType（int/long/…），
            // 读取时严格区分类型，避免 int/long 等被强行互转。
            RegisterBoolConverter<short>(sqlBuilder, DbValueType.Int16);
            RegisterBoolConverter<int>(sqlBuilder, DbValueType.Int32);
            RegisterBoolConverter<long>(sqlBuilder, DbValueType.Int64);
            RegisterBoolConverter<byte>(sqlBuilder, DbValueType.Byte);
            RegisterBoolConverter<sbyte>(sqlBuilder, DbValueType.SByte);
            RegisterBoolConverter<ushort>(sqlBuilder, DbValueType.UInt16);
            RegisterBoolConverter<uint>(sqlBuilder, DbValueType.UInt32);
            RegisterBoolConverter<ulong>(sqlBuilder, DbValueType.UInt64);
            sqlBuilder.RegisterDbValueConverter<SqlBuilder, bool, bool>(DbValueType.Boolean, null, null);

            // 整型族 ↩ 整型 DB 值（标识/主键列 ExecuteScalar 回读、或列读取时的整型值）
            // 按各整型 DbValueType 的 CLR 类型严格注册 TDbType，读取时严格区分 int/long/…。
            RegisterIntegerConverters<short>(sqlBuilder, DbValueType.Int16);
            RegisterIntegerConverters<int>(sqlBuilder, DbValueType.Int32);
            RegisterIntegerConverters<long>(sqlBuilder, DbValueType.Int64);
            RegisterIntegerConverters<byte>(sqlBuilder, DbValueType.Byte);
            RegisterIntegerConverters<sbyte>(sqlBuilder, DbValueType.SByte);
            RegisterIntegerConverters<ushort>(sqlBuilder, DbValueType.UInt16);
            RegisterIntegerConverters<uint>(sqlBuilder, DbValueType.UInt32);
            RegisterIntegerConverters<ulong>(sqlBuilder, DbValueType.UInt64);

            // Guid ↔ Guid/Binary/字符串列
            sqlBuilder.RegisterDbValueConverter<SqlBuilder, Guid, Guid>(DbValueType.Guid, null, null);
            sqlBuilder.RegisterDbValueConverter(DbValueType.Binary, (byte[] b) => new Guid(b), g => g.ToByteArray());
            foreach (DbValueType stringType in _stringDbValueTypes)
                sqlBuilder.RegisterDbValueConverter(stringType, (string s) => Guid.Parse(s), g => g.ToString());

            // string → Guid 列（字符串值写入 Guid 列时解析；读取方向 Guid 直转字符串）
            sqlBuilder.RegisterDbValueConverter(DbValueType.Guid,
                (Guid g) => g.ToString(),
                s => Guid.TryParse(s, out Guid guid) ? guid : s);

            // DateTime / DateTimeOffset ↔ 日期时间类列
            foreach (DbValueType dateType in _dateDbValueTypes)
            {
                sqlBuilder.RegisterDbValueConverter<SqlBuilder, DateTime, DateTime>(dateType, null, null);
                sqlBuilder.RegisterDbValueConverter(dateType, (DateTime d) => new DateTimeOffset(d), d => d.DateTime);
            }
            sqlBuilder.RegisterDbValueConverter<SqlBuilder, DateTimeOffset, DateTimeOffset>(DbValueType.DateTimeOffset, null, null);
            sqlBuilder.RegisterDbValueConverter(DbValueType.DateTimeOffset, (DateTimeOffset d) => d.DateTime, d => new DateTimeOffset(d));

            // TimeSpan ↔ Time/Int64(ticks)/字符串列
            sqlBuilder.RegisterDbValueConverter<SqlBuilder, TimeSpan, TimeSpan>(DbValueType.Time, null, null);
            sqlBuilder.RegisterDbValueConverter(DbValueType.Int64, (long t) => TimeSpan.FromTicks(t), t => t.Ticks);
            foreach (DbValueType stringType in _stringDbValueTypes)
                sqlBuilder.RegisterDbValueConverter(stringType, (string s) => ParseToTimeSpan(s), t => t.ToString());

            // string ↔ 字符串列（读方向同类型直接赋值，此处注册保证写方向语义完整）
            foreach (DbValueType stringType in _stringDbValueTypes)
                sqlBuilder.RegisterDbValueConverter<SqlBuilder, string, string>(stringType, null, null);

            // Oracle 方言：bool 以整数 1/0 写入（Oracle 无布尔类型）。
            // 通过派生类型注册，这些注册优先于 SqlBuilder 上的默认注册。
            OracleBuilder.Instance.RegisterDbValueConverter(DbValueType.Boolean, (long v) => v != 0, b => b ? 1 : 0);

            // SQLite 方言：DateTime/DateTimeOffset/TimeSpan 以字符串存储（SQLite 无原生日期/时间类型）。
            foreach (DbValueType dateType in _dateDbValueTypes)
            {
                SQLiteBuilder.Instance.RegisterDbValueConverter(dateType,
                    (string s) => DateTime.Parse(s), d => d.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                SQLiteBuilder.Instance.RegisterDbValueConverter(dateType,
                    (string s) => DateTimeOffset.Parse(s), d => d.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            }
            SQLiteBuilder.Instance.RegisterDbValueConverter(DbValueType.DateTimeOffset,
                (string s) => DateTime.Parse(s), d => d.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            SQLiteBuilder.Instance.RegisterDbValueConverter(DbValueType.DateTimeOffset,
                (string s) => DateTimeOffset.Parse(s), d => d.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            SQLiteBuilder.Instance.RegisterDbValueConverter(DbValueType.Time,
                (string s) => ParseToTimeSpan(s), t => t.ToString("c"));
        }

        private static readonly DbValueType[] _stringDbValueTypes =
        {
            DbValueType.String, DbValueType.AnsiString, DbValueType.StringFixedLength, DbValueType.AnsiStringFixedLength
        };

        private static readonly DbValueType[] _dateDbValueTypes =
        {
            DbValueType.Date, DbValueType.DateTime, DbValueType.DateTime2
        };

        /// <summary>
        /// 注册 <see cref="bool"/> 与指定整型 DB 取值类型的双向数值转换。
        /// <typeparamref name="TDbType"/> 为该 DbValueType 驱动的 CLR 类型（如 Int32→int、Int64→long），严格区分数值类型。
        /// </summary>
        private static void RegisterBoolConverter<TDbType>(SqlBuilder sqlBuilder, DbValueType dbType) where TDbType : struct
        {
            sqlBuilder.RegisterDbValueConverter<SqlBuilder, TDbType, bool>(dbType,
                o => Convert.ToInt64(o) != 0,
                b => b ? 1 : 0);
        }

        /// <summary>
        /// 为指定整型 DB 取值类型注册全部整型值类型 <typeparamref name="TValueType"/> 的双向数值转换
        /// （读取经 <see cref="Convert.ChangeType(object, Type)"/> 转到属性整型）。
        /// <typeparamref name="TDbType"/> 为该 DbValueType 驱动的 CLR 类型，严格区分数值类型。
        /// </summary>
        private static void RegisterIntegerConverters<TDbType>(SqlBuilder sqlBuilder, DbValueType dbType) where TDbType : struct
        {
            RegisterIntegerConverters<TDbType, byte>(sqlBuilder, dbType);
            RegisterIntegerConverters<TDbType, sbyte>(sqlBuilder, dbType);
            RegisterIntegerConverters<TDbType, short>(sqlBuilder, dbType);
            RegisterIntegerConverters<TDbType, ushort>(sqlBuilder, dbType);
            RegisterIntegerConverters<TDbType, int>(sqlBuilder, dbType);
            RegisterIntegerConverters<TDbType, uint>(sqlBuilder, dbType);
            RegisterIntegerConverters<TDbType, long>(sqlBuilder, dbType);
            RegisterIntegerConverters<TDbType, ulong>(sqlBuilder, dbType);
        }

        private static void RegisterIntegerConverters<TDbType, TValueType>(SqlBuilder sqlBuilder, DbValueType dbType)
            where TDbType : struct where TValueType : struct
        {
            sqlBuilder.RegisterDbValueConverter<SqlBuilder, TDbType, TValueType>(dbType,
                o => (TValueType)(object)Convert.ChangeType(o, typeof(TValueType)),
                v => v);
        }

        /// <summary>
        /// 将字符串解析为 <see cref="TimeSpan"/>（支持常规格式与 Oracle interval "+DD HH:MM:SS.FFFFFF"）。
        /// </summary>
        private static TimeSpan ParseToTimeSpan(string value)
        {
            if (TimeSpan.TryParse(value, out TimeSpan ts)) return ts;
            // Oracle interval format: "+DD HH:MM:SS.FFFFFF"
            if (value.Length > 3 && (value[0] == '+' || value[0] == '-') && value.IndexOf(' ') >= 0)
            {
                var parts = value.Substring(1).Split(new[] { ' ' }, 2);
                if (int.TryParse(parts[0], out int days) && parts.Length > 1 && TimeSpan.TryParse(parts[1], out TimeSpan time))
                    return new TimeSpan(days, time.Hours, time.Minutes, time.Seconds, time.Milliseconds);
            }
            return (TimeSpan)Convert.ChangeType(value, typeof(TimeSpan));
        }
    }
}
