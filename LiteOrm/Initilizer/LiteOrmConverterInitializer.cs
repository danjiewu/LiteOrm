using LiteOrm.Common;
using System;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm 默认值转换器初始化器，用于在 <see cref="SqlBuilder"/> 类型上注册默认的双向值转换器。
    /// 通过静态构造函数在首次访问时自动注册，供 <see cref="IDbConverter.GetDbValueConverter"/> 查找。
    /// 注册主键为 (值类型, DbValueType)，读取与写入共用同一注册表；
    /// 转换经注册转换器的 <see cref="IDbValueConverter.DbReadConverter"/> / <see cref="IDbValueConverter.DbWriteConverter"/> 委托应用，委托为 null 时直接赋值。
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

            // 数值族 ↩ 数值 DB 值（整型/浮点/Decimal 互转，如 Decimal↔Int32、float↔Int64 等）
            // 按各数值 DbValueType 的 CLR 类型严格注册 TDbType，读取时严格区分 int/long/decimal/…。
            RegisterNumericInt16(sqlBuilder, DbValueType.Int16);
            RegisterNumericInt32(sqlBuilder, DbValueType.Int32);
            RegisterNumericInt64(sqlBuilder, DbValueType.Int64);
            RegisterNumericByte(sqlBuilder, DbValueType.Byte);
            RegisterNumericSByte(sqlBuilder, DbValueType.SByte);
            RegisterNumericUInt16(sqlBuilder, DbValueType.UInt16);
            RegisterNumericUInt32(sqlBuilder, DbValueType.UInt32);
            RegisterNumericUInt64(sqlBuilder, DbValueType.UInt64);
            RegisterNumericDecimal(sqlBuilder, DbValueType.Decimal);
            RegisterNumericSingle(sqlBuilder, DbValueType.Single);
            RegisterNumericDouble(sqlBuilder, DbValueType.Double);

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
        /// 以传入委托方式注册数值值类型 <typeparamref name="TValueType"/> 与数值 DB 列 <typeparamref name="TDbType"/> 的双向转换。
        /// 读取转委托 <paramref name="fromDb"/> 由调用方针对具体类型提供（编译期特化强转，避免 <see cref="Convert.ChangeType(object, Type)"/> 的反射与装箱）；
        /// 写入委托返回 <typeparamref name="TValueType"/> 值（dbType 绑定交由驱动/上层）。
        /// </summary>
        private static void RegisterNumericConverters<TDbType, TValueType>(SqlBuilder sqlBuilder, DbValueType dbType,
            DbConvertHandler<TDbType, TValueType> fromDb)
            where TDbType : struct where TValueType : struct
        {
            sqlBuilder.RegisterDbValueConverter<SqlBuilder, TDbType, TValueType>(dbType, fromDb, v => v);
        }

        // 以下按数值列 CLR 类型分别注册到全部数值值类型，fromDb 均为编译期强转委托（无反射、无装箱）。
        private static void RegisterNumericInt16(SqlBuilder sb, DbValueType t)
        {
            RegisterNumericConverters<short, byte>(sb, t, o => (byte)o);
            RegisterNumericConverters<short, sbyte>(sb, t, o => (sbyte)o);
            RegisterNumericConverters<short, short>(sb, t, o => o);
            RegisterNumericConverters<short, ushort>(sb, t, o => (ushort)o);
            RegisterNumericConverters<short, int>(sb, t, o => o);
            RegisterNumericConverters<short, uint>(sb, t, o => (uint)o);
            RegisterNumericConverters<short, long>(sb, t, o => o);
            RegisterNumericConverters<short, ulong>(sb, t, o => (ulong)o);
            RegisterNumericConverters<short, decimal>(sb, t, o => o);
            RegisterNumericConverters<short, float>(sb, t, o => o);
            RegisterNumericConverters<short, double>(sb, t, o => o);
        }

        private static void RegisterNumericInt32(SqlBuilder sb, DbValueType t)
        {
            RegisterNumericConverters<int, byte>(sb, t, o => (byte)o);
            RegisterNumericConverters<int, sbyte>(sb, t, o => (sbyte)o);
            RegisterNumericConverters<int, short>(sb, t, o => (short)o);
            RegisterNumericConverters<int, ushort>(sb, t, o => (ushort)o);
            RegisterNumericConverters<int, int>(sb, t, o => o);
            RegisterNumericConverters<int, uint>(sb, t, o => (uint)o);
            RegisterNumericConverters<int, long>(sb, t, o => o);
            RegisterNumericConverters<int, ulong>(sb, t, o => (ulong)o);
            RegisterNumericConverters<int, decimal>(sb, t, o => o);
            RegisterNumericConverters<int, float>(sb, t, o => o);
            RegisterNumericConverters<int, double>(sb, t, o => o);
        }

        private static void RegisterNumericInt64(SqlBuilder sb, DbValueType t)
        {
            RegisterNumericConverters<long, byte>(sb, t, o => (byte)o);
            RegisterNumericConverters<long, sbyte>(sb, t, o => (sbyte)o);
            RegisterNumericConverters<long, short>(sb, t, o => (short)o);
            RegisterNumericConverters<long, ushort>(sb, t, o => (ushort)o);
            RegisterNumericConverters<long, int>(sb, t, o => (int)o);
            RegisterNumericConverters<long, uint>(sb, t, o => (uint)o);
            RegisterNumericConverters<long, long>(sb, t, o => o);
            RegisterNumericConverters<long, ulong>(sb, t, o => (ulong)o);
            RegisterNumericConverters<long, decimal>(sb, t, o => o);
            RegisterNumericConverters<long, float>(sb, t, o => o);
            RegisterNumericConverters<long, double>(sb, t, o => o);
        }

        private static void RegisterNumericByte(SqlBuilder sb, DbValueType t)
        {
            RegisterNumericConverters<byte, byte>(sb, t, o => o);
            RegisterNumericConverters<byte, sbyte>(sb, t, o => (sbyte)o);
            RegisterNumericConverters<byte, short>(sb, t, o => o);
            RegisterNumericConverters<byte, ushort>(sb, t, o => o);
            RegisterNumericConverters<byte, int>(sb, t, o => o);
            RegisterNumericConverters<byte, uint>(sb, t, o => o);
            RegisterNumericConverters<byte, long>(sb, t, o => o);
            RegisterNumericConverters<byte, ulong>(sb, t, o => o);
            RegisterNumericConverters<byte, decimal>(sb, t, o => o);
            RegisterNumericConverters<byte, float>(sb, t, o => o);
            RegisterNumericConverters<byte, double>(sb, t, o => o);
        }

        private static void RegisterNumericSByte(SqlBuilder sb, DbValueType t)
        {
            RegisterNumericConverters<sbyte, byte>(sb, t, o => (byte)o);
            RegisterNumericConverters<sbyte, sbyte>(sb, t, o => o);
            RegisterNumericConverters<sbyte, short>(sb, t, o => o);
            RegisterNumericConverters<sbyte, ushort>(sb, t, o => (ushort)o);
            RegisterNumericConverters<sbyte, int>(sb, t, o => o);
            RegisterNumericConverters<sbyte, uint>(sb, t, o => (uint)o);
            RegisterNumericConverters<sbyte, long>(sb, t, o => o);
            RegisterNumericConverters<sbyte, ulong>(sb, t, o => (ulong)o);
            RegisterNumericConverters<sbyte, decimal>(sb, t, o => o);
            RegisterNumericConverters<sbyte, float>(sb, t, o => o);
            RegisterNumericConverters<sbyte, double>(sb, t, o => o);
        }

        private static void RegisterNumericUInt16(SqlBuilder sb, DbValueType t)
        {
            RegisterNumericConverters<ushort, byte>(sb, t, o => (byte)o);
            RegisterNumericConverters<ushort, sbyte>(sb, t, o => (sbyte)o);
            RegisterNumericConverters<ushort, short>(sb, t, o => (short)o);
            RegisterNumericConverters<ushort, ushort>(sb, t, o => o);
            RegisterNumericConverters<ushort, int>(sb, t, o => o);
            RegisterNumericConverters<ushort, uint>(sb, t, o => o);
            RegisterNumericConverters<ushort, long>(sb, t, o => o);
            RegisterNumericConverters<ushort, ulong>(sb, t, o => o);
            RegisterNumericConverters<ushort, decimal>(sb, t, o => o);
            RegisterNumericConverters<ushort, float>(sb, t, o => o);
            RegisterNumericConverters<ushort, double>(sb, t, o => o);
        }

        private static void RegisterNumericUInt32(SqlBuilder sb, DbValueType t)
        {
            RegisterNumericConverters<uint, byte>(sb, t, o => (byte)o);
            RegisterNumericConverters<uint, sbyte>(sb, t, o => (sbyte)o);
            RegisterNumericConverters<uint, short>(sb, t, o => (short)o);
            RegisterNumericConverters<uint, ushort>(sb, t, o => (ushort)o);
            RegisterNumericConverters<uint, int>(sb, t, o => (int)o);
            RegisterNumericConverters<uint, uint>(sb, t, o => o);
            RegisterNumericConverters<uint, long>(sb, t, o => o);
            RegisterNumericConverters<uint, ulong>(sb, t, o => o);
            RegisterNumericConverters<uint, decimal>(sb, t, o => o);
            RegisterNumericConverters<uint, float>(sb, t, o => o);
            RegisterNumericConverters<uint, double>(sb, t, o => o);
        }

        private static void RegisterNumericUInt64(SqlBuilder sb, DbValueType t)
        {
            RegisterNumericConverters<ulong, byte>(sb, t, o => (byte)o);
            RegisterNumericConverters<ulong, sbyte>(sb, t, o => (sbyte)o);
            RegisterNumericConverters<ulong, short>(sb, t, o => (short)o);
            RegisterNumericConverters<ulong, ushort>(sb, t, o => (ushort)o);
            RegisterNumericConverters<ulong, int>(sb, t, o => (int)o);
            RegisterNumericConverters<ulong, uint>(sb, t, o => (uint)o);
            RegisterNumericConverters<ulong, long>(sb, t, o => (long)o);
            RegisterNumericConverters<ulong, ulong>(sb, t, o => o);
            RegisterNumericConverters<ulong, decimal>(sb, t, o => o);
            RegisterNumericConverters<ulong, float>(sb, t, o => o);
            RegisterNumericConverters<ulong, double>(sb, t, o => o);
        }

        private static void RegisterNumericDecimal(SqlBuilder sb, DbValueType t)
        {
            RegisterNumericConverters<decimal, byte>(sb, t, o => (byte)o);
            RegisterNumericConverters<decimal, sbyte>(sb, t, o => (sbyte)o);
            RegisterNumericConverters<decimal, short>(sb, t, o => (short)o);
            RegisterNumericConverters<decimal, ushort>(sb, t, o => (ushort)o);
            RegisterNumericConverters<decimal, int>(sb, t, o => (int)o);
            RegisterNumericConverters<decimal, uint>(sb, t, o => (uint)o);
            RegisterNumericConverters<decimal, long>(sb, t, o => (long)o);
            RegisterNumericConverters<decimal, ulong>(sb, t, o => (ulong)o);
            RegisterNumericConverters<decimal, decimal>(sb, t, o => o);
            RegisterNumericConverters<decimal, float>(sb, t, o => (float)o);
            RegisterNumericConverters<decimal, double>(sb, t, o => (double)o);
        }

        private static void RegisterNumericSingle(SqlBuilder sb, DbValueType t)
        {
            RegisterNumericConverters<float, byte>(sb, t, o => (byte)o);
            RegisterNumericConverters<float, sbyte>(sb, t, o => (sbyte)o);
            RegisterNumericConverters<float, short>(sb, t, o => (short)o);
            RegisterNumericConverters<float, ushort>(sb, t, o => (ushort)o);
            RegisterNumericConverters<float, int>(sb, t, o => (int)o);
            RegisterNumericConverters<float, uint>(sb, t, o => (uint)o);
            RegisterNumericConverters<float, long>(sb, t, o => (long)o);
            RegisterNumericConverters<float, ulong>(sb, t, o => (ulong)o);
            RegisterNumericConverters<float, decimal>(sb, t, o => (decimal)o);
            RegisterNumericConverters<float, float>(sb, t, o => o);
            RegisterNumericConverters<float, double>(sb, t, o => o);
        }

        private static void RegisterNumericDouble(SqlBuilder sb, DbValueType t)
        {
            RegisterNumericConverters<double, byte>(sb, t, o => (byte)o);
            RegisterNumericConverters<double, sbyte>(sb, t, o => (sbyte)o);
            RegisterNumericConverters<double, short>(sb, t, o => (short)o);
            RegisterNumericConverters<double, ushort>(sb, t, o => (ushort)o);
            RegisterNumericConverters<double, int>(sb, t, o => (int)o);
            RegisterNumericConverters<double, uint>(sb, t, o => (uint)o);
            RegisterNumericConverters<double, long>(sb, t, o => (long)o);
            RegisterNumericConverters<double, ulong>(sb, t, o => (ulong)o);
            RegisterNumericConverters<double, decimal>(sb, t, o => (decimal)o);
            RegisterNumericConverters<double, float>(sb, t, o => (float)o);
            RegisterNumericConverters<double, double>(sb, t, o => o);
        }

        /// <summary>
        /// 将字符串解析为 <see cref="TimeSpan"/>（支持常规格式与 Oracle interval "+DD HH:MM:SS.FFFFFF"）。
        /// </summary>
        private static TimeSpan ParseToTimeSpan(string value)
        {
            if (TimeSpan.TryParse(value, out TimeSpan ts)) return ts;
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
