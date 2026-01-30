using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 支持的值二元操作符。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ValueBinaryOperator
    {
        /// <summary>
        /// 加法
        /// </summary>
        Add = 9,
        /// <summary>
        /// 减法
        /// </summary>
        Subtract = 10,
        /// <summary>
        /// 乘法
        /// </summary>
        Multiply = 11,
        /// <summary>
        /// 除法
        /// </summary>
        Divide = 12,
        /// <summary>
        /// 字符串连接
        /// </summary>
        Concat = 13
    }
}
