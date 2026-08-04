using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 为 <see cref="Expr"/> 及其子类、关联集合类型和原生类型提供源生成的 JSON 序列化上下文。
    /// 替代反射式 <see cref="JsonSerializer"/> 调用，满足 NativeAOT 兼容性要求。
    /// <para>
    /// <see cref="ExprJsonConverterFactory"/> 通过 <see cref="JsonConverterAttribute"/> 标注在
    /// <see cref="Expr"/> 上自动注册，无需在 Options 中显式添加。
    /// </para>
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = false)]
    [JsonSerializable(typeof(Expr))]
    [JsonSerializable(typeof(LogicExpr))]
    [JsonSerializable(typeof(ValueTypeExpr))]
    [JsonSerializable(typeof(SqlSegment))]
    [JsonSerializable(typeof(SourceExpr))]
    [JsonSerializable(typeof(SelectExpr))]
    [JsonSerializable(typeof(TableExpr))]
    [JsonSerializable(typeof(TableJoinExpr))]
    [JsonSerializable(typeof(List<LogicExpr>))]
    [JsonSerializable(typeof(HashSet<LogicExpr>))]
    [JsonSerializable(typeof(List<Expr>))]
    [JsonSerializable(typeof(List<ValueTypeExpr>))]
    [JsonSerializable(typeof(string[]))]
    [JsonSerializable(typeof(DateTime))]
    [JsonSerializable(typeof(DateTimeOffset))]
    [JsonSerializable(typeof(TimeSpan))]
    [JsonSerializable(typeof(Guid))]
    [JsonSerializable(typeof(byte[]))]
    [JsonSerializable(typeof(object))]
    internal partial class ExprJsonSerializerContext : JsonSerializerContext
    {
        /// <summary>
        /// 预配置实例，使用 <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/>
        /// 编码器以避免 HTML 字符转义，提高 SQL 表达式的可读性。
        /// </summary>
        public static readonly ExprJsonSerializerContext Instance = CreateInstance();

        private static ExprJsonSerializerContext CreateInstance()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            // ExprJsonConverterFactory 通过 [JsonConverter] 特性标注在 Expr 上，
            // JsonSerializerOptions 会自动发现并注册该工厂，无需在此显式添加。
            return new ExprJsonSerializerContext(options);
        }
    }
}
