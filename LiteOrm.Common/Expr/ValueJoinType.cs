using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 值类型表达式集合的连接方式。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ValueJoinType
    {
        /// <summary>
        /// 逗号分隔列表（如 IN (@p1, @p2)）
        /// </summary>
        List = 0,
        /// <summary>
        /// 字符串连接方式（如 CONCAT(s1, s2)）
        /// </summary>
        Concat = 3
    }
}
