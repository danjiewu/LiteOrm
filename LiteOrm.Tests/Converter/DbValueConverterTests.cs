using LiteOrm.Common;
using System;
using System.Text.Json;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// DbValueConverterMap / IDbConverter 委托式转换器机制单元测试。
    /// 读写共用 (Type, DbValueType) 主键；<see cref="IDbConverter.GetDbValueConverter"/> 仅查注册表（可空返回）；
    /// 读取经 <see cref="IDbValueConverter.DbReadConverter"/>，写入经 <see cref="IDbValueConverter.DbWriteConverter"/> 委托应用，
    /// 二者均为严格无兜底：未注册或委托为 null 时直接赋值 / 直返。
    /// 纯内存测试，无需数据库连接。
    /// 注意：DbValueConverterMap 为静态按构建器类型共享，各测试使用互不冲突的注册键。
    /// </summary>
    [Collection("Database")]
    public class DbValueConverterTests
    {
        /// <summary>测试用自定义构建器（拥有独立的 DbValueConverterMap，不污染全局注册）。</summary>
        private class ConverterTestBuilder : SqlBuilder
        {
        }

        /// <summary>继承 ConverterTestBuilder 的子构建器（验证方言覆盖基类注册）。</summary>
        private sealed class OverrideTestBuilder : ConverterTestBuilder
        {
        }

        /// <summary>测试用自定义值类型（避免与默认注册的 bool/Guid/DateTime 等类型冲突）。</summary>
        private sealed class CustomValue
        {
            public string? Text { get; set; }
        }

        /// <summary>严格无兜底读取：解析转换器后经 <see cref="IDbValueConverter.DbReadConverter"/> 委托应用；未注册或委托为 null 时原样返回 raw。</summary>
        private static object? Read(IDbConverter builder, object raw, Type targetType, DbValueType dbType)
        {
            var conv = builder.GetDbValueConverter(targetType, dbType);
            return conv?.DbReadConverter is { } handler ? handler(raw) : raw;
        }

        /// <summary>严格无兜底写入：解析转换器后经 <see cref="IDbValueConverter.DbWriteConverter"/> 委托应用；未注册或委托为 null 时原样返回 value。</summary>
        private static object Write(IDbConverter builder, object value, DbValueType dbType)
        {
            var conv = builder.GetDbValueConverter(value.GetType(), dbType);
            return conv?.DbWriteConverter is { } handler ? handler(value) : value;
        }

        #region 注册与查找（(Type, DbValueType) 主键，可空返回）

        [Fact]
        public void GetDbValueConverter_RegisteredInstance_FoundByTypeAndDbValueType()
        {
            var builder = new ConverterTestBuilder();
            var converter = new FuncDbValueConverter<object, CustomValue>(
                o => new CustomValue { Text = (string)o },
                v => v.Text!);
            builder.RegisterDbValueConverter(DbValueType.Guid, converter);

            // 命中注册返回同一实例
            Assert.Same(converter, builder.GetDbValueConverter(typeof(CustomValue), DbValueType.Guid));
            // 未注册的 (类型, 取值类型) 组合返回 null
            Assert.Null(builder.GetDbValueConverter(typeof(CustomValue), DbValueType.DateTime));
        }

        [Fact]
        public void RegisterDbValueConverter_SameTypeDifferentDbValueType_ResolvesIndependently()
        {
            var builder = new ConverterTestBuilder();
            builder.RegisterDbValueConverter<ConverterTestBuilder, string, CustomValue>(DbValueType.String,
                o => new CustomValue { Text = "S:" + o },
                v => v.Text!.Substring(2));
            builder.RegisterDbValueConverter<ConverterTestBuilder, object, CustomValue>(DbValueType.Binary,
                o => new CustomValue { Text = "B:" + o },
                v => v.Text!.Substring(2));

            Assert.Equal("S:abc", ((CustomValue)Read(builder, "abc", typeof(CustomValue), DbValueType.String)!).Text);
            Assert.Equal("B:xyz", ((CustomValue)Read(builder, "xyz", typeof(CustomValue), DbValueType.Binary)!).Text);
        }

        #endregion

        #region 读写双向复用（单一注册同时服务读、写）

        [Fact]
        public void RegisterDbValueConverter_ByDelegates_ServesBothReadAndWrite()
        {
            var builder = new ConverterTestBuilder();
            builder.RegisterDbValueConverter<ConverterTestBuilder, string, CustomValue>(DbValueType.AnsiString,
                o => new CustomValue { Text = (string)o },
                v => "[" + v.Text + "]");

            Assert.Equal("abc", ((CustomValue)Read(builder, "abc", typeof(CustomValue), DbValueType.AnsiString)!).Text);
            Assert.Equal("[abc]", Write(builder, new CustomValue { Text = "abc" }, DbValueType.AnsiString));
        }

        [Fact]
        public void RegisteredIntConverter_RoundTripsThroughBothDirections()
        {
            var builder = new ConverterTestBuilder();
            builder.RegisterDbValueConverter<ConverterTestBuilder, long, int>(DbValueType.Int32,
                o => Convert.ToInt32(o) * 2,
                v => v / 2);

            Assert.Equal(20, Read(builder, 10L, typeof(int), DbValueType.Int32));
            Assert.Equal(10, Write(builder, 20, DbValueType.Int32));
        }

        #endregion

        #region 默认注册（SqlBuilder.Instance 基础注册表）

        [Fact]
        public void DefaultRegistration_BoolFromNumeric_ReadsBothDirections()
        {
            var builder = SqlBuilder.Instance;

            Assert.True((bool)Read(builder, 1, typeof(bool), DbValueType.Int32)!);
            Assert.False((bool)Read(builder, 0, typeof(bool), DbValueType.Int32)!);

            Assert.Equal(1, Write(builder, true, DbValueType.Int32));
            Assert.Equal(0, Write(builder, false, DbValueType.Int32));
        }

        [Fact]
        public void DefaultRegistration_GuidRoundTrips_StringAndBinary()
        {
            var builder = SqlBuilder.Instance;
            Guid guid = Guid.NewGuid();

            Assert.Equal(guid, Read(builder, guid.ToString(), typeof(Guid), DbValueType.String));
            Assert.Equal(guid.ToString(), Write(builder, guid, DbValueType.String));

            Assert.Equal(guid, Read(builder, guid.ToByteArray(), typeof(Guid), DbValueType.Binary));
            Assert.Equal(guid.ToByteArray(), Write(builder, guid, DbValueType.Binary));
        }

        [Fact]
        public void DefaultRegistration_TimeSpanFromInt64Ticks_BothDirections()
        {
            var builder = SqlBuilder.Instance;
            TimeSpan time = TimeSpan.FromMinutes(90);

            Assert.Equal(time, Read(builder, time.Ticks, typeof(TimeSpan), DbValueType.Int64));
            Assert.Equal(time.Ticks, Write(builder, time, DbValueType.Int64));
        }

        [Fact]
        public void DefaultRegistration_DateTimeOffsetFromDateTime_BothDirections()
        {
            var builder = SqlBuilder.Instance;
            var dateTime = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Unspecified);

            Assert.Equal(new DateTimeOffset(dateTime), Read(builder, dateTime, typeof(DateTimeOffset), DbValueType.DateTime));
            Assert.Equal(dateTime, Write(builder, new DateTimeOffset(dateTime), DbValueType.DateTime));
        }

        [Fact]
        public void DefaultRegistration_StringToGuidColumn_ParsesOnWrite()
        {
            var builder = SqlBuilder.Instance;
            Guid guid = Guid.NewGuid();

            // 字符串值写入 Guid 列：解析为 Guid
            Assert.Equal(guid, Write(builder, guid.ToString(), DbValueType.Guid));
            // 无效 Guid 字符串原样返回交由驱动处理
            Assert.Equal("not-a-guid", Write(builder, "not-a-guid", DbValueType.Guid));
        }

        #endregion

        #region 方言覆盖（继承链查找：子类注册优先于基类）

        [Fact]
        public void DialectRegistration_OverridesBaseRegistration()
        {
            var builder = new OverrideTestBuilder();
            builder.RegisterDbValueConverter<OverrideTestBuilder, string, CustomValue>(DbValueType.String,
                o => new CustomValue { Text = "override:" + o },
                v => v.Text!.Substring("override:".Length));

            // 子构建器命中自身的注册
            Assert.Equal("override:abc", ((CustomValue)Read(builder, "abc", typeof(CustomValue), DbValueType.String)!).Text);

            // 基类 SqlBuilder 查不到子构建器的注册（继承链不向下查找）
            Assert.Null(SqlBuilder.Instance.GetDbValueConverter(typeof(CustomValue), DbValueType.String));
        }

        [Fact]
        public void OracleBuilder_BoolAsInteger_ForBooleanDbType()
        {
            Assert.Equal(1, Write(OracleBuilder.Instance, true, DbValueType.Boolean));
            Assert.Equal(0, Write(OracleBuilder.Instance, false, DbValueType.Boolean));

            // 基类 SqlBuilder 对 Boolean 列直返 bool
            Assert.Equal(true, Write(SqlBuilder.Instance, true, DbValueType.Boolean));
        }

        [Fact]
        public void SQLiteBuilder_DateTimeStoredAsString()
        {
            var dateTime = new DateTime(2024, 6, 1, 8, 30, 15, 123);

            Assert.Equal("2024-06-01 08:30:15.123", Write(SQLiteBuilder.Instance, dateTime, DbValueType.DateTime));
            Assert.Equal(dateTime, Read(SQLiteBuilder.Instance, "2024-06-01 08:30:15.123", typeof(DateTime), DbValueType.DateTime));
        }

        #endregion

        #region 严格无兜底与空值处理

        [Fact]
        public void Read_UnregisteredType_ReturnsRawValue()
        {
            var builder = SqlBuilder.Instance;

            // 未注册的 (int, Object) 组合不做 ChangeType，直返原始 long
            Assert.Equal(5L, Read(builder, 5L, typeof(int), DbValueType.Object));
            // 字符串直返（不做 JSON 反序列化等处理）
            Assert.Equal("42", Read(builder, "42", typeof(int), DbValueType.Object));
        }

        [Fact]
        public void Read_NullColumnConverter_PassesNullRawThrough()
        {
            var builder = SqlBuilder.Instance;

            // 读取委托不做空值短路：未注册时 DBNull.Value 原样返回，交由调用方/列级处理
            Assert.Same(DBNull.Value, Read(builder, DBNull.Value, typeof(int), DbValueType.Object));
            Assert.Same(DBNull.Value, Read(builder, DBNull.Value, typeof(string), DbValueType.Object));
        }

        [Fact]
        public void Write_UnregisteredType_ReturnsRawValue()
        {
            var builder = SqlBuilder.Instance;

            // DbValueType 未指定注册时，按值类型无法匹配到转换器 → 直返
            Assert.Equal(true, Write(builder, true, DbValueType.Boolean));
            Assert.Equal(TimeSpan.FromHours(1), Write(builder, TimeSpan.FromHours(1), DbValueType.Time));
        }

        [Fact]
        public void Write_NullValue_IsCallerResponsibility()
        {
            var builder = SqlBuilder.Instance;

            // 空值由调用方（列级 ToDbValue）处理为 DBNull；此处要求值非 null
            Assert.Equal("abc", Write(builder, "abc", DbValueType.Object));
        }

        [Fact]
        public void Read_JsonColumn_ComplexUnregisteredPassedThrough()
        {
            var builder = SqlBuilder.Instance;
            var value = new CustomValue { Text = "abc" };

            // 未注册的复杂类型不做 JSON 反序列化，原样返回
            Assert.Same(value, Read(builder, value, typeof(CustomValue), DbValueType.Json));
        }

        [Fact]
        public void RegisterDbValueConverter_Json_AppliesInBothDirections()
        {
            // 使用独立构建器，避免污染 SqlBuilder.Instance 的静态注册表
            var builder = new ConverterTestBuilder();
            var value = new CustomValue { Text = "abc" };

            // 预注册复杂类型的 Json 转换器
            var jsonConverter = new FuncDbValueConverter<object, CustomValue>(
                o => new CustomValue { Text = JsonSerializer.Deserialize<CustomValue>((string)o)!.Text! },
                v => JsonSerializer.Serialize(v));
            builder.RegisterDbValueConverter(DbValueType.Json, jsonConverter);

            // 写入走注册转换器（序列化为 JSON 字符串）
            Assert.Equal("{\"Text\":\"abc\"}", Write(builder, value, DbValueType.Json));
            // 读取走注册转换器（反序列化）
            Assert.Equal("abc", ((CustomValue)Read(builder, "{\"Text\":\"abc\"}", typeof(CustomValue), DbValueType.Json)!).Text);
        }

        #endregion
    }
}