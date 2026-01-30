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
        /// 算术负号 (-)
        /// </summary>
        Nagive = 0,
        /// <summary>
        /// 按位取反 (~)
        /// </summary>
        BitwiseNot = 1,
    }
}
