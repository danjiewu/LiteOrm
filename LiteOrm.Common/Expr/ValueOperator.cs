using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 支持的值二元操作符。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ValueOperator
    {
        /// <summary>
        /// 加法
        /// </summary>
        Add = 0,
        /// <summary>
        /// 减法
        /// </summary>
        Subtract = 1,
        /// <summary>
        /// 乘法
        /// </summary>
        Multiply = 2,
        /// <summary>
        /// 除法
        /// </summary>
        Divide = 3,
        /// <summary>
        /// 字符串连接
        /// </summary>
        Concat = 4
    }
}
