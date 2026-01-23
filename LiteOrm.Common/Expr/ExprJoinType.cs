using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表达式集合的逻辑连接模式。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ExprJoinType
    {
        /// <summary>
        /// 逗号分隔的列表（通常用于构建 IN (@p1, @p2, ...)）
        /// </summary>
        List = 0,
        /// <summary>
        /// 逻辑 AND 连接
        /// </summary>
        And = 1,
        /// <summary>
        /// 逻辑 OR 连接
        /// </summary>
        Or = 2,
        /// <summary>
        /// 字符串或其它值的 Concat 连接
        /// </summary>
        Concat = 3
    }
}
