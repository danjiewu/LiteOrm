using LiteOrm.Common;
using System;
using Xunit;

namespace LiteOrm.Tests
{
    /// <summary>
    /// DbValueConverterMap / IDbConverter 统一转换器机制单元测试。
    /// 读写共用 (Type, DbValueType) 主键；GetDbValueConverter 仅查注册表（可空返回）；
    /// 读取分发见 <see cref="DbConverterHelper.ConvertFromDbValue(IDbConverter, object?, Type)"/>，
    /// 写入分发见 <see cref="SqlBuilderExtensions.ConvertToDbValue(IDbConverter, object?, DbValueType?)"/>。
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
            builder.RegisterDbValueConverter<ConverterTestBuilder, CustomValue>(DbValueType.String,
                o => new CustomValue { Text = "S:" + o },
                v => v.Text!.Substring(2));
            builder.RegisterDbValueConverter<ConverterTestBuilder, CustomValue>(DbValueType.Binary,
                o => new CustomValue { Text = "B:" + o },
                v => v.Text!.Substring(2));

            Assert.Equal("S:abc", ((CustomValue)DbConverterHelper.ConvertFromDbValue(builder, "abc", typeof(CustomValue), DbValueType.String)!).Text);
            Assert.Equal("B:xyz", ((CustomValue)DbConverterHelper.ConvertFromDbValue(builder, "xyz", typeof(CustomValue), DbValueType.Binary)!).Text);
        }

        #endregion

        #region 读写双向复用（单一注册同时服务读、写）

        [Fact]
        public void RegisterDbValueConverter_ByDelegates_ServesBothReadAndWrite()
        {
            var builder = new ConverterTestBuilder();
            builder.RegisterDbValueConverter<ConverterTestBuilder, CustomValue>(DbValueType.AnsiString,
                o => new CustomValue { Text = (string)o },
                v => "[" + v.Text + "]");

            Assert.Equal("abc", ((CustomValue)DbConverterHelper.ConvertFromDbValue(builder, "abc", typeof(CustomValue), DbValueType.AnsiString)!).Text);
            Assert.Equal("[abc]", builder.ConvertToDbValue(new CustomValue { Text = "abc" }, DbValueType.AnsiString));
        }

        [Fact]
        public void RegisteredIntConverter_RoundTripsThroughBothDirections()
        {
            var builder = new ConverterTestBuilder();
            builder.RegisterDbValueConverter<ConverterTestBuilder, int>(DbValueType.Int32,
                o => Convert.ToInt32(o) * 2,
                v => v / 2);

            Assert.Equal(20, DbConverterHelper.ConvertFromDbValue(builder, 10L, typeof(int), DbValueType.Int32));
            Assert.Equal(10, builder.ConvertToDbValue(20, DbValueType.Int32));
        }

        #endregion

        #region 默认注册（SqlBuilder.Instance 基础注册表）

        [Fact]
        public void DefaultRegistration_BoolFromNumeric_ReadsBothDirections()
        {
            var builder = SqlBuilder.Instance;

            Assert.True((bool)DbConverterHelper.ConvertFromDbValue(builder, 1, typeof(bool), DbValueType.Int32)!);
            Assert.False((bool)DbConverterHelper.ConvertFromDbValue(builder, 0, typeof(bool), DbValueType.Int32)!);

            Assert.Equal(1, builder.ConvertToDbValue(true, DbValueType.Int32));
            Assert.Equal(0, builder.ConvertToDbValue(false, DbValueType.Int32));
        }

        [Fact]
        public void DefaultRegistration_GuidRoundTrips_StringAndBinary()
        {
            var builder = SqlBuilder.Instance;
            Guid guid = Guid.NewGuid();

            Assert.Equal(guid, DbConverterHelper.ConvertFromDbValue(builder, guid.ToString(), typeof(Guid), DbValueType.String));
            Assert.Equal(guid.ToString(), builder.ConvertToDbValue(guid, DbValueType.String));

            Assert.Equal(guid, DbConverterHelper.ConvertFromDbValue(builder, guid.ToByteArray(), typeof(Guid), DbValueType.Binary));
            Assert.Equal(guid.ToByteArray(), builder.ConvertToDbValue(guid, DbValueType.Binary));
        }

        [Fact]
        public void DefaultRegistration_TimeSpanFromInt64Ticks_BothDirections()
        {
            var builder = SqlBuilder.Instance;
            TimeSpan time = TimeSpan.FromMinutes(90);

            Assert.Equal(time, DbConverterHelper.ConvertFromDbValue(builder, time.Ticks, typeof(TimeSpan), DbValueType.Int64));
            Assert.Equal(time.Ticks, builder.ConvertToDbValue(time, DbValueType.Int64));
        }

        [Fact]
        public void DefaultRegistration_DateTimeOffsetFromDateTime_BothDirections()
        {
            var builder = SqlBuilder.Instance;
            var dateTime = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Unspecified);

            Assert.Equal(new DateTimeOffset(dateTime), DbConverterHelper.ConvertFromDbValue(builder, dateTime, typeof(DateTimeOffset), DbValueType.DateTime));
            Assert.Equal(dateTime, builder.ConvertToDbValue(new DateTimeOffset(dateTime), DbValueType.DateTime));
        }

        [Fact]
        public void DefaultRegistration_StringToGuidColumn_ParsesOnWrite()
        {
            var builder = SqlBuilder.Instance;
            Guid guid = Guid.NewGuid();

            // 字符串值写入 Guid 列：解析为 Guid
            Assert.Equal(guid, builder.ConvertToDbValue(guid.ToString(), DbValueType.Guid));
            // 无效 Guid 字符串原样返回交由驱动处理
            Assert.Equal("not-a-guid", builder.ConvertToDbValue("not-a-guid", DbValueType.Guid));
        }

        #endregion

        #region 方言覆盖（继承链查找：子类注册优先于基类）

        [Fact]
        public void DialectRegistration_OverridesBaseRegistration()
        {
            var builder = new OverrideTestBuilder();
            builder.RegisterDbValueConverter<OverrideTestBuilder, CustomValue>(DbValueType.String,
                o => new CustomValue { Text = "override:" + o },
                v => v.Text!.Substring("override:".Length));

            // 子构建器命中自身的注册
            Assert.Equal("override:abc", ((CustomValue)DbConverterHelper.ConvertFromDbValue(builder, "abc", typeof(CustomValue), DbValueType.String)!).Text);

            // 基类 SqlBuilder 查不到子构建器的注册（继承链不向下查找）
            Assert.Null(SqlBuilder.Instance.GetDbValueConverter(typeof(CustomValue), DbValueType.String));
        }

        [Fact]
        public void OracleBuilder_BoolAsInteger_ForBooleanDbType()
        {
            Assert.Equal(1, OracleBuilder.Instance.ConvertToDbValue(true, DbValueType.Boolean));
            Assert.Equal(0, OracleBuilder.Instance.ConvertToDbValue(false, DbValueType.Boolean));

            // 基类 SqlBuilder 对 Boolean 列直返 bool
            Assert.Equal(true, SqlBuilder.Instance.ConvertToDbValue(true, DbValueType.Boolean));
        }

        [Fact]
        public void SQLiteBuilder_DateTimeStoredAsString()
        {
            var dateTime = new DateTime(2024, 6, 1, 8, 30, 15, 123);

            Assert.Equal("2024-06-01 08:30:15.123", SQLiteBuilder.Instance.ConvertToDbValue(dateTime, DbValueType.DateTime));
            Assert.Equal(dateTime, DbConverterHelper.ConvertFromDbValue(SQLiteBuilder.Instance, "2024-06-01 08:30:15.123", typeof(DateTime), DbValueType.DateTime));
        }

        #endregion

        #region 默认兜底与空值短路

        [Fact]
        public void DefaultFallback_UnregisteredType_UsesChangeType()
        {
            var builder = SqlBuilder.Instance;

            Assert.Equal(5, DbConverterHelper.ConvertFromDbValue(builder, 5L, typeof(int), DbValueType.Object));
            Assert.Equal(42, DbConverterHelper.ConvertFromDbValue(builder, "42", typeof(int), DbValueType.Object));
        }

        [Fact]
        public void ConvertFromDbValue_NullAndDbNull_ReturnTargetDefault()
        {
            var builder = SqlBuilder.Instance;

            Assert.Equal(0, DbConverterHelper.ConvertFromDbValue(builder, DBNull.Value, typeof(int), DbValueType.Object));
            Assert.Equal(0, DbConverterHelper.ConvertFromDbValue(builder, null, typeof(int), DbValueType.Object));

            Assert.Null(DbConverterHelper.ConvertFromDbValue(builder, DBNull.Value, typeof(string), DbValueType.Object));

            Assert.Null(DbConverterHelper.ConvertFromDbValue(builder, DBNull.Value, typeof(int?), DbValueType.Object));
        }

        [Fact]
        public void ConvertFromDbValue_EmptyString_ReturnTargetDefault()
        {
            var builder = SqlBuilder.Instance;

            Assert.Equal(0, DbConverterHelper.ConvertFromDbValue(builder, string.Empty, typeof(int), DbValueType.Object));
            Assert.Equal(Guid.Empty, DbConverterHelper.ConvertFromDbValue(builder, string.Empty, typeof(Guid), DbValueType.String));
        }

        [Fact]
        public void ConvertToDbValue_NullDbType_InfersFromSourceType()
        {
            var builder = SqlBuilder.Instance;

            // DbValueType 未指定按源类型推断为 Boolean → bool 直返
            Assert.Equal(true, builder.ConvertToDbValue(true, null));

            // TimeSpan 推断为 Time → 直返 TimeSpan
            Assert.Equal(TimeSpan.FromHours(1), builder.ConvertToDbValue(TimeSpan.FromHours(1), null));
        }

        [Fact]
        public void ConvertToDbValue_NullValue_ReturnsDbNull()
        {
            Assert.Equal(DBNull.Value, SqlBuilder.Instance.ConvertToDbValue(null, DbValueType.Object));
        }

        [Fact]
        public void ConvertToDbValue_JsonColumn_StringPassthroughComplexSerialized()
        {
            var builder = SqlBuilder.Instance;

            // 标量字符串直返（不序列化为带引号 JSON）
            Assert.Equal("abc", builder.ConvertToDbValue("abc", DbValueType.Json));

            // 复杂对象序列化为 JSON 字符串
            Assert.Equal("{\"Text\":\"abc\"}", builder.ConvertToDbValue(new CustomValue { Text = "abc" }, DbValueType.Json));
        }

        #endregion
    }
}
