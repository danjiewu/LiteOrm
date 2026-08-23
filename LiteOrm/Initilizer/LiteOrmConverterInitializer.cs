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

            // bool ↔ 数值列（数据库以数值存储布尔时双向转换）
            foreach (DbValueType numeric in _numericDbValueTypes)
                sqlBuilder.RegisterDbValueConverter(numeric, ConvertToBool, static b => b ? 1 : 0);
            sqlBuilder.RegisterDbValueConverter(DbValueType.Boolean, ConvertToBool, static b => b);

            // 整型族 ↩ 整型 DB 值（标识/主键列 ExecuteScalar 回读 long 等整型值时 Convert.ChangeType 到属性整型）：
            // 通过预注册实现整型间的数值转换，严格模式下不依赖运行时 ChangeType 兜底
            foreach (DbValueType intDbType in _integerDbValueTypes)
            {
                RegisterIntegerConverters<byte>(sqlBuilder, intDbType);
                RegisterIntegerConverters<sbyte>(sqlBuilder, intDbType);
                RegisterIntegerConverters<short>(sqlBuilder, intDbType);
                RegisterIntegerConverters<ushort>(sqlBuilder, intDbType);
                RegisterIntegerConverters<int>(sqlBuilder, intDbType);
                RegisterIntegerConverters<uint>(sqlBuilder, intDbType);
                RegisterIntegerConverters<long>(sqlBuilder, intDbType);
                RegisterIntegerConverters<ulong>(sqlBuilder, intDbType);
            }

            // Guid ↔ Guid/Binary/字符串列
            sqlBuilder.RegisterDbValueConverter(DbValueType.Guid, ConvertToGuid, static g => g);
            sqlBuilder.RegisterDbValueConverter(DbValueType.Binary, ConvertToGuid, static g => g.ToByteArray());
            foreach (DbValueType stringType in _stringDbValueTypes)
                sqlBuilder.RegisterDbValueConverter(stringType, ConvertToGuid, static g => g.ToString());

            // string → Guid 列（字符串值写入 Guid 列时解析；读取方向同类型直返）
            sqlBuilder.RegisterDbValueConverter(DbValueType.Guid,
                static o => (string)o,
                static s => Guid.TryParse(s, out Guid g) ? g : s);

            // DateTime / DateTimeOffset ↔ 日期时间类列
            foreach (DbValueType dateType in _dateDbValueTypes)
            {
                sqlBuilder.RegisterDbValueConverter(dateType, ConvertToDateTime, static d => d);
                sqlBuilder.RegisterDbValueConverter(dateType, ConvertToDateTimeOffset, static d => d.DateTime);
            }
            sqlBuilder.RegisterDbValueConverter(DbValueType.DateTimeOffset, ConvertToDateTimeOffset, static d => d);
            sqlBuilder.RegisterDbValueConverter(DbValueType.DateTimeOffset, ConvertToDateTime, static d => new DateTimeOffset(d));

            // TimeSpan ↔ Time/Int64(ticks)/字符串列
            sqlBuilder.RegisterDbValueConverter(DbValueType.Time, ConvertToTimeSpan, static t => t);
            sqlBuilder.RegisterDbValueConverter(DbValueType.Int64, ConvertToTimeSpan, static t => t.Ticks);
            foreach (DbValueType stringType in _stringDbValueTypes)
                sqlBuilder.RegisterDbValueConverter(stringType, ConvertToTimeSpan, static t => t.ToString());

            // string ↔ 字符串列（读方向同类型直接赋值，此处注册保证写方向语义完整）
            foreach (DbValueType stringType in _stringDbValueTypes)
                sqlBuilder.RegisterDbValueConverter(stringType, static o => (string)o, static s => s);

            // Oracle 方言：bool 以整数 1/0 写入（Oracle 无布尔类型）。
            // 通过基类型遍历，这些注册优先于 SqlBuilder 上的默认注册。
            OracleBuilder.Instance.RegisterDbValueConverter(DbValueType.Boolean, ConvertToBool, static b => b ? 1 : 0);

            // SQLite 方言：DateTime/DateTimeOffset/TimeSpan 以字符串存储（SQLite 无原生日期/时间类型）。
            foreach (DbValueType dateType in _dateDbValueTypes)
            {
                SQLiteBuilder.Instance.RegisterDbValueConverter(dateType,
                    ConvertToDateTime, static d => d.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                SQLiteBuilder.Instance.RegisterDbValueConverter(dateType,
                    ConvertToDateTimeOffset, static d => d.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            }
            SQLiteBuilder.Instance.RegisterDbValueConverter(DbValueType.DateTimeOffset,
                ConvertToDateTime, static d => d.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            SQLiteBuilder.Instance.RegisterDbValueConverter(DbValueType.DateTimeOffset,
                ConvertToDateTimeOffset, static d => d.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            SQLiteBuilder.Instance.RegisterDbValueConverter(DbValueType.Time,
                ConvertToTimeSpan, static t => t.ToString("c"));
        }

        private static readonly DbValueType[] _numericDbValueTypes =
        {
            DbValueType.Int16, DbValueType.Int32, DbValueType.Int64,
            DbValueType.Byte, DbValueType.SByte, DbValueType.UInt16, DbValueType.UInt32, DbValueType.UInt64
        };

        private static readonly DbValueType[] _integerDbValueTypes =
        {
            DbValueType.Int16, DbValueType.Int32, DbValueType.Int64,
            DbValueType.Byte, DbValueType.SByte, DbValueType.UInt16, DbValueType.UInt32, DbValueType.UInt64
        };

        private static readonly DbValueType[] _stringDbValueTypes =
        {
            DbValueType.String, DbValueType.AnsiString, DbValueType.StringFixedLength, DbValueType.AnsiStringFixedLength
        };

        private static readonly DbValueType[] _dateDbValueTypes =
        {
            DbValueType.Date, DbValueType.DateTime, DbValueType.DateTime2
        };

        /// <summary>
        /// 注册整型值类型 <typeparamref name="T"/> 与指定整型 DB 取值类型的双向数值转换（读取用 <see cref="Convert.ChangeType(object, Type)"/>）。
        /// </summary>
        private static void RegisterIntegerConverters<T>(SqlBuilder sqlBuilder, DbValueType dbType) where T : struct
        {
            sqlBuilder.RegisterDbValueConverter<SqlBuilder, T>(dbType,
                o => (T)Convert.ChangeType(o, typeof(T)),
                v => v);
        }

        /// <summary>
        /// 将数据库值转换为 <see cref="bool"/>（字符串 1/Y/T/0/N/F 或数值非零）。
        /// </summary>
        private static bool ConvertToBool(object value)
        {
            if (value is string strBool)
            {
                if (bool.TryParse(strBool, out bool result)) return result;
                if (strBool == "1" || strBool.Equals("Y", StringComparison.OrdinalIgnoreCase) || strBool.Equals("T", StringComparison.OrdinalIgnoreCase)) return true;
                if (strBool == "0" || strBool.Equals("N", StringComparison.OrdinalIgnoreCase) || strBool.Equals("F", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return Convert.ToInt64(value) != 0;
        }

        /// <summary>
        /// 将数据库值转换为 <see cref="Guid"/>（字符串或 16 字节数组）。
        /// </summary>
        private static Guid ConvertToGuid(object value)
        {
            if (value is string strGuid && Guid.TryParse(strGuid, out Guid guid)) return guid;
            if (value is byte[] bytesGuid && bytesGuid.Length == 16) return new Guid(bytesGuid);
            return (Guid)Convert.ChangeType(value, typeof(Guid));
        }

        /// <summary>
        /// 将数据库值转换为 <see cref="TimeSpan"/>（ticks、字符串或 Oracle 区间格式）。
        /// </summary>
        private static TimeSpan ConvertToTimeSpan(object value)
        {
            if (value is TimeSpan ts) return ts;
            if (value is long ticks) return TimeSpan.FromTicks(ticks);
            if (value is string strTs)
            {
                if (TimeSpan.TryParse(strTs, out TimeSpan ts2)) return ts2;
                // Oracle interval format: "+DD HH:MM:SS.FFFFFF"
                if (strTs.Length > 3 && (strTs[0] == '+' || strTs[0] == '-') && strTs.IndexOf(' ') >= 0)
                {
                    var parts = strTs.Substring(1).Split(new[] { ' ' }, 2);
                    if (int.TryParse(parts[0], out int days) && parts.Length > 1 && TimeSpan.TryParse(parts[1], out TimeSpan time))
                        return new TimeSpan(days, time.Hours, time.Minutes, time.Seconds, time.Milliseconds);
                }
            }
            return (TimeSpan)Convert.ChangeType(value, typeof(TimeSpan));
        }

        /// <summary>
        /// 将数据库值转换为 <see cref="DateTimeOffset"/>（DateTime 或字符串）。
        /// </summary>
        private static DateTimeOffset ConvertToDateTimeOffset(object value)
        {
            if (value is DateTimeOffset dto) return dto;
            if (value is DateTime dt) return new DateTimeOffset(dt);
            if (value is string strDto && DateTimeOffset.TryParse(strDto, out DateTimeOffset dto2)) return dto2;
            return (DateTimeOffset)Convert.ChangeType(value, typeof(DateTimeOffset));
        }

        /// <summary>
        /// 将数据库值转换为 <see cref="DateTime"/>（DateTimeOffset、字符串或数值）。
        /// </summary>
        private static DateTime ConvertToDateTime(object value)
        {
            if (value is DateTime dt) return dt;
            if (value is DateTimeOffset dto) return dto.DateTime;
            if (value is string strDt && DateTime.TryParse(strDt, out DateTime dt2)) return dt2;
            return (DateTime)Convert.ChangeType(value, typeof(DateTime));
        }
    }
}
