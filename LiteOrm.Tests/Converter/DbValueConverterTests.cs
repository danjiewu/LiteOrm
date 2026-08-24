using LiteOrm.Common;
using System;
using System.Text.Json;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// DbValueConverterMap / IDbConverter 委托式转换器机制单元测试。
    /// 读写共用 (Type, DbValueType) 主键；GetDbValueConverter 仅查注册表（可空返回）；
    /// 读取经 <see cref="DbConverterHelper.ApplyRead"/>，写入经 <see cref="DbConverterHelper.ApplyWrite"/>，
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

            Assert.Equal("S:abc", ((CustomValue)DbConverterHelper.ApplyRead(builder, null, "abc", typeof(CustomValue), DbValueType.String)!).Text);
            Assert.Equal("B:xyz", ((CustomValue)DbConverterHelper.ApplyRead(builder, null, "xyz", typeof(CustomValue), DbValueType.Binary)!).Text);
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

            Assert.Equal("abc", ((CustomValue)DbConverterHelper.ApplyRead(builder, null, "abc", typeof(CustomValue), DbValueType.AnsiString)!).Text);
            Assert.Equal("[abc]", DbConverterHelper.ApplyWrite(builder, null, new CustomValue { Text = "abc" }, DbValueType.AnsiString));
        }

        [Fact]
        public void RegisteredIntConverter_RoundTripsThroughBothDirections()
        {
            var builder = new ConverterTestBuilder();
            builder.RegisterDbValueConverter<ConverterTestBuilder, long, int>(DbValueType.Int32,
                o => Convert.ToInt32(o) * 2,
                v => v / 2);

            Assert.Equal(20, DbConverterHelper.ApplyRead(builder, null, 10L, typeof(int), DbValueType.Int32));
            Assert.Equal(10, DbConverterHelper.ApplyWrite(builder, null, 20, DbValueType.Int32));
        }

        #endregion

        #region 默认注册（SqlBuilder.Instance 基础注册表）

        [Fact]
        public void DefaultRegistration_BoolFromNumeric_ReadsBothDirections()
        {
            var builder = SqlBuilder.Instance;

            Assert.True((bool)DbConverterHelper.ApplyRead(builder, null, 1, typeof(bool), DbValueType.Int32)!);
            Assert.False((bool)DbConverterHelper.ApplyRead(builder, null, 0, typeof(bool), DbValueType.Int32)!);

            Assert.Equal(1, DbConverterHelper.ApplyWrite(builder, null, true, DbValueType.Int32));
            Assert.Equal(0, DbConverterHelper.ApplyWrite(builder, null, false, DbValueType.Int32));
        }

        [Fact]
        public void DefaultRegistration_GuidRoundTrips_StringAndBinary()
        {
            var builder = SqlBuilder.Instance;
            Guid guid = Guid.NewGuid();

            Assert.Equal(guid, DbConverterHelper.ApplyRead(builder, null, guid.ToString(), typeof(Guid), DbValueType.String));
            Assert.Equal(guid.ToString(), DbConverterHelper.ApplyWrite(builder, null, guid, DbValueType.String));

            Assert.Equal(guid, DbConverterHelper.ApplyRead(builder, null, guid.ToByteArray(), typeof(Guid), DbValueType.Binary));
            Assert.Equal(guid.ToByteArray(), DbConverterHelper.ApplyWrite(builder, null, guid, DbValueType.Binary));
        }

        [Fact]
        public void DefaultRegistration_TimeSpanFromInt64Ticks_BothDirections()
        {
            var builder = SqlBuilder.Instance;
            TimeSpan time = TimeSpan.FromMinutes(90);

            Assert.Equal(time, DbConverterHelper.ApplyRead(builder, null, time.Ticks, typeof(TimeSpan), DbValueType.Int64));
            Assert.Equal(time.Ticks, DbConverterHelper.ApplyWrite(builder, null, time, DbValueType.Int64));
        }

        [Fact]
        public void DefaultRegistration_DateTimeOffsetFromDateTime_BothDirections()
        {
            var builder = SqlBuilder.Instance;
            var dateTime = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Unspecified);

            Assert.Equal(new DateTimeOffset(dateTime), DbConverterHelper.ApplyRead(builder, null, dateTime, typeof(DateTimeOffset), DbValueType.DateTime));
            Assert.Equal(dateTime, DbConverterHelper.ApplyWrite(builder, null, new DateTimeOffset(dateTime), DbValueType.DateTime));
        }

        [Fact]
        public void DefaultRegistration_StringToGuidColumn_ParsesOnWrite()
        {
            var builder = SqlBuilder.Instance;
            Guid guid = Guid.NewGuid();

            // 字符串值写入 Guid 列：解析为 Guid
            Assert.Equal(guid, DbConverterHelper.ApplyWrite(builder, null, guid.ToString(), DbValueType.Guid));
            // 无效 Guid 字符串原样返回交由驱动处理
            Assert.Equal("not-a-guid", DbConverterHelper.ApplyWrite(builder, null, "not-a-guid", DbValueType.Guid));
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
            Assert.Equal("override:abc", ((CustomValue)DbConverterHelper.ApplyRead(builder, null, "abc", typeof(CustomValue), DbValueType.String)!).Text);

            // 基类 SqlBuilder 查不到子构建器的注册（继承链不向下查找）
            Assert.Null(SqlBuilder.Instance.GetDbValueConverter(typeof(CustomValue), DbValueType.String));
        }

        [Fact]
        public void OracleBuilder_BoolAsInteger_ForBooleanDbType()
        {
            Assert.Equal(1, DbConverterHelper.ApplyWrite(OracleBuilder.Instance, null, true, DbValueType.Boolean));
            Assert.Equal(0, DbConverterHelper.ApplyWrite(OracleBuilder.Instance, null, false, DbValueType.Boolean));

            // 基类 SqlBuilder 对 Boolean 列直返 bool
            Assert.Equal(true, DbConverterHelper.ApplyWrite(SqlBuilder.Instance, null, true, DbValueType.Boolean));
        }

        [Fact]
        public void SQLiteBuilder_DateTimeStoredAsString()
        {
            var dateTime = new DateTime(2024, 6, 1, 8, 30, 15, 123);

            Assert.Equal("2024-06-01 08:30:15.123", DbConverterHelper.ApplyWrite(SQLiteBuilder.Instance, null, dateTime, DbValueType.DateTime));
            Assert.Equal(dateTime, DbConverterHelper.ApplyRead(SQLiteBuilder.Instance, null, "2024-06-01 08:30:15.123", typeof(DateTime), DbValueType.DateTime));
        }

        #endregion

        #region 严格无兜底与空值处理

        [Fact]
        public void ApplyRead_UnregisteredType_ReturnsRawValue()
        {
            var builder = SqlBuilder.Instance;

            // 未注册的 (int, Object) 组合不做 ChangeType，直返原始 long
            Assert.Equal(5L, DbConverterHelper.ApplyRead(builder, null, 5L, typeof(int), DbValueType.Object));
            // 字符串直返（不做 JSON 反序列化等处理）
            Assert.Equal("42", DbConverterHelper.ApplyRead(builder, null, "42", typeof(int), DbValueType.Object));
        }

        [Fact]
        public void ApplyRead_NullColumnConverter_UsesNullRaw()
        {
            var builder = SqlBuilder.Instance;

            // 无列级转换器且无注册：null 直接透传（空值短路属于列级 FromDbValue 职责）
            Assert.Null(DbConverterHelper.ApplyRead(builder, null, null, typeof(int), DbValueType.Object));
            // ApplyRead 自身不做空值短路：DBNull.Value 原样返回，交由调用方/列级处理
            Assert.Same(DBNull.Value, DbConverterHelper.ApplyRead(builder, null, DBNull.Value, typeof(string), DbValueType.Object));
        }

        [Fact]
        public void ApplyWrite_UnregisteredType_ReturnsRawValue()
        {
            var builder = SqlBuilder.Instance;

            // DbValueType 未指定注册时，按值类型无法匹配到转换器 → 直返
            Assert.Equal(true, DbConverterHelper.ApplyWrite(builder, null, true, DbValueType.Boolean));
            Assert.Equal(TimeSpan.FromHours(1), DbConverterHelper.ApplyWrite(builder, null, TimeSpan.FromHours(1), DbValueType.Time));
        }

        [Fact]
        public void ApplyWrite_NullValue_IsCallerResponsibility()
        {
            var builder = SqlBuilder.Instance;

            // 空值由调用方（列级 ToDbValue）处理为 DBNull；此处要求值非 null
            Assert.Equal("abc", DbConverterHelper.ApplyWrite(builder, null, "abc", DbValueType.Object));
        }

        [Fact]
        public void ApplyRead_JsonColumn_ComplexUnregisteredPassedThrough()
        {
            var builder = SqlBuilder.Instance;
            var value = new CustomValue { Text = "abc" };

            // 未注册的复杂类型不做 JSON 反序列化，原样返回
            Assert.Same(value, DbConverterHelper.ApplyRead(builder, null, value, typeof(CustomValue), DbValueType.Json));
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
            Assert.Equal("{\"Text\":\"abc\"}", DbConverterHelper.ApplyWrite(builder, null, value, DbValueType.Json));
            // 读取走注册转换器（反序列化）
            Assert.Equal("abc", ((CustomValue)DbConverterHelper.ApplyRead(builder, null, "{\"Text\":\"abc\"}", typeof(CustomValue), DbValueType.Json)!)!.Text);
        }

        #endregion
    }
}