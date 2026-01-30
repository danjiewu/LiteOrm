using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 逻辑表达式集合的连接方式。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LogicJoinType
    {
        /// <summary>
        /// 逻辑 AND 连接
        /// </summary>
        And = 1,
        /// <summary>
        /// 逻辑 OR 连接
        /// </summary>
        Or = 2
    }
}
