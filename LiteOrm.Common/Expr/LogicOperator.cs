using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 支持的逻辑二元操作符。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LogicOperator
    {
        /// <summary>
        /// 等于
        /// </summary>
        Equal = 0,
        /// <summary>
        /// 大于
        /// </summary>
        GreaterThan = 1,
        /// <summary>
        /// 小于
        /// </summary>
        LessThan = 2,
        /// <summary>
        /// 以指定字符串为开头
        /// </summary>
        StartsWith = 3,
        /// <summary>
        /// 以指定字符串为结尾
        /// </summary>
        EndsWith = 4,
        /// <summary>
        /// 包含指定字符串
        /// </summary>
        Contains = 5,
        /// <summary>
        /// 匹配字符串通配符
        /// </summary>
        Like = 6,
        /// <summary>
        /// 是否在集合内
        /// </summary>
        In = 7,
        /// <summary>
        /// 正则表达式匹配
        /// </summary>
        RegexpLike = 8,
        /// <summary>
        /// 逻辑非标识。
        /// </summary>
        Not = 64,
        /// <summary>
        /// 不等于
        /// </summary>
        NotEqual = Equal | Not,
        /// <summary>
        /// 大于等于
        /// </summary>
        GreaterThanOrEqual = LessThan | Not,
        /// <summary>
        /// 小于等于
        /// </summary>
        LessThanOrEqual = GreaterThan | Not,
        /// <summary>
        /// 不以指定字符串为开头
        /// </summary>
        NotStartsWith = StartsWith | Not,
        /// <summary>
        /// 不以指定字符串为结尾
        /// </summary>
        NotEndsWith = EndsWith | Not,
        /// <summary>
        /// 不包含指定字符串
        /// </summary>
        NotContains = Contains | Not,
        /// <summary>
        /// 不匹配字符串通配符
        /// </summary>
        NotLike = Like | Not,
        /// <summary>
        /// 不在集合内
        /// </summary>
        NotIn = In | Not,
        /// <summary>
        /// 不匹配正则表达式
        /// </summary>
        NotRegexpLike = RegexpLike | Not
    }
}
