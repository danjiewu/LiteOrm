using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 支持的一元操作符枚举。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UnaryOperator
    {
        /// <summary>
        /// 逻辑非 (NOT)
        /// </summary>
        Not = 0,
        /// <summary>
        /// 算术负号 (-)
        /// </summary>
        Nagive = 1,
        /// <summary>
        /// 按位取反 (~)
        /// </summary>
        BitwiseNot = 2,
    }
}
