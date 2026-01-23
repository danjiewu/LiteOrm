using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 支持的二元操作符列表，包括比较、模糊匹配、包含及基本算术运算。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BinaryOperator
    {
        /// <summary>
        /// 相等
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
        /// 以指定字符串为开始（作为字符串比较）
        /// </summary>
        StartsWith = 3,
        /// <summary>
        /// 以指定字符串为结尾（作为字符串比较）
        /// </summary>
        EndsWith = 4,
        /// <summary>
        /// 包含指定字符串（作为字符串比较）
        /// </summary>
        Contains = 5,
        /// <summary>
        /// 匹配字符串格式（作为字符串比较）
        /// </summary>
        Like = 6,
        /// <summary>
        /// 包含在集合中
        /// </summary>
        In = 7,
        /// <summary>
        /// 正则表达式匹配
        /// </summary>
        RegexpLike = 8,
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
        Concat = 13,
        /// <summary>
        /// 逻辑非标志（用于组合生成 NOT IN, NOT LIKE 等）。
        /// </summary>
        Not = 64,
        /// <summary>
        /// 不等于
        /// </summary>
        NotEqual = Equal | Not,
        /// <summary>
        /// 不小于
        /// </summary>
        GreaterThanOrEqual = LessThan | Not,
        /// <summary>
        /// 不大于
        /// </summary>
        LessThanOrEqual = GreaterThan | Not,
        /// <summary>
        /// 不以指定字符串为开始
        /// </summary>
        NotStartsWith = StartsWith | Not,
        /// <summary>
        /// 不以指定字符串为结尾
        /// </summary>
        NotEndsWith = EndsWith | Not,
        /// <summary>
        /// 不包含指定字符串（作为字符串比较）
        /// </summary>
        NotContains = Contains | Not,
        /// <summary>
        /// 不匹配字符串格式（作为字符串比较）
        /// </summary>
        NotLike = Like | Not,
        /// <summary>
        /// 不包含在集合中
        /// </summary>
        NotIn = In | Not,
        /// <summary>
        /// 不匹配正则表达式
        /// </summary>
        NotRegexpLike = RegexpLike | Not
    }
}
