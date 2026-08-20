using LiteOrm.Common;
using System;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm 默认值转换器初始化器，用于在 <see cref="SqlBuilder"/> 类型上注册默认的读取/写入转换器。
    /// 通过静态构造函数在首次访问时自动注册，供 GetFromDbValueConverter/GetToDbValueConverter 的委托分发使用。
    /// </summary>
    /// <remarks>
    /// 调用 <see cref="Initialize"/> 方法可显式触发静态构造函数，确保转换器在应用启动时完成注册。
    /// 静态构造函数只会执行一次，因此多次调用 <see cref="Initialize"/> 是安全的。
    /// 各方言子类可通过 <see cref="SqlBuilderExtensions"/> 的 RegisterDbReadConverter/RegisterDbWriteConverter 扩展方法在自身类型上覆盖默认注册。
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
        /// 在 SqlBuilder 类型上注册默认的读取/写入转换器（预注册的方法委托）。
        /// </summary>
        private static void RegisterDefaultConverters()
        {
            // 读取转换器：源类型统一为 object，使 DataReaderConverter 的 GetFromDbValueConverter 兜底路径可直接命中
            SqlBuilder.Instance.RegisterDbReadConverter<SqlBuilder, object, bool>(ConvertToBool);
            SqlBuilder.Instance.RegisterDbReadConverter<SqlBuilder, object, Guid>(ConvertToGuid);
            SqlBuilder.Instance.RegisterDbReadConverter<SqlBuilder, object, TimeSpan>(ConvertToTimeSpan);
            SqlBuilder.Instance.RegisterDbReadConverter<SqlBuilder, object, DateTimeOffset>(ConvertToDateTimeOffset);

            // 写入转换器：按 (源类型, DbValueType) 注册
            foreach (DbValueType numeric in _numericDbValueTypes)
                SqlBuilder.Instance.RegisterDbWriteConverter(numeric, static (bool b) => b ? 1 : 0);
            SqlBuilder.Instance.RegisterDbWriteConverter(DbValueType.Boolean, static (bool b) => Convert.ToBoolean(b));

            SqlBuilder.Instance.RegisterDbWriteConverter(DbValueType.Guid, static (Guid g) => g);
            SqlBuilder.Instance.RegisterDbWriteConverter(DbValueType.Binary, static (Guid g) => g.ToByteArray());
            foreach (DbValueType stringType in _stringDbValueTypes)
                SqlBuilder.Instance.RegisterDbWriteConverter(stringType, static (Guid g) => g.ToString());

            foreach (DbValueType dateType in _dateDbValueTypes)
            {
                SqlBuilder.Instance.RegisterDbWriteConverter(dateType, static (DateTime d) => d);
                SqlBuilder.Instance.RegisterDbWriteConverter(dateType, static (DateTimeOffset d) => d.DateTime);
            }
            SqlBuilder.Instance.RegisterDbWriteConverter(DbValueType.DateTimeOffset, static (DateTimeOffset d) => d);
            SqlBuilder.Instance.RegisterDbWriteConverter(DbValueType.DateTimeOffset, static (DateTime d) => new DateTimeOffset(d));

            SqlBuilder.Instance.RegisterDbWriteConverter(DbValueType.Time, static (TimeSpan t) => t);
            foreach (DbValueType stringType in _stringDbValueTypes)
                SqlBuilder.Instance.RegisterDbWriteConverter(stringType, static (TimeSpan t) => t.ToString());

            foreach (DbValueType stringType in _stringDbValueTypes)
                SqlBuilder.Instance.RegisterDbWriteConverter(stringType, static (string s) => s);

            // Oracle 方言：在 OracleBuilder 自身的 DbValueConverterMap 上注册 bool 写入转换为整数 1/0
            // （Oracle 无布尔类型）。通过基类型遍历，这些注册优先于 SqlBuilder 上的默认注册。
            OracleBuilder.Instance.RegisterDbWriteConverter(DbValueType.Boolean, static (bool b) => b ? 1 : 0);
            foreach (DbValueType numeric in _numericDbValueTypes)
                OracleBuilder.Instance.RegisterDbWriteConverter(numeric, static (bool b) => b ? 1 : 0);

            // SQLite 方言：在 SQLiteBuilder 自身的 DbValueConverterMap 上注册 DateTime/DateTimeOffset/TimeSpan
            // 写入转换为字符串存储（SQLite 无原生日期/时间类型）。
            foreach (DbValueType dateType in _dateDbValueTypes)
            {
                SQLiteBuilder.Instance.RegisterDbWriteConverter(dateType, static (DateTime d) => d.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                SQLiteBuilder.Instance.RegisterDbWriteConverter(dateType, static (DateTimeOffset d) => d.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            }
            SQLiteBuilder.Instance.RegisterDbWriteConverter(DbValueType.DateTimeOffset, static (DateTime d) => d.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            SQLiteBuilder.Instance.RegisterDbWriteConverter(DbValueType.DateTimeOffset, static (DateTimeOffset d) => d.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            SQLiteBuilder.Instance.RegisterDbWriteConverter(DbValueType.Time, static (TimeSpan t) => t.ToString("c"));
        }

        private static readonly DbValueType[] _numericDbValueTypes =
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
            if (value is long ticks) return TimeSpan.FromTicks(ticks);
            if (value is string strTs)
            {
                if (TimeSpan.TryParse(strTs, out TimeSpan ts)) return ts;
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
            if (value is DateTime dt) return new DateTimeOffset(dt);
            if (value is string strDto && DateTimeOffset.TryParse(strDto, out DateTimeOffset dto)) return dto;
            return (DateTimeOffset)Convert.ChangeType(value, typeof(DateTimeOffset));
        }
    }
}