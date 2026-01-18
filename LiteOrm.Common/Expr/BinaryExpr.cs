using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// 二元条件表达式。
    /// 代表 [Left] [Operator] [Right] 结构，例如：
    /// - <c>id = 1</c>
    /// - <c>name LIKE '%abc%'</c>
    /// - <c>score &gt; 90</c>
    /// - <c>id IN (1, 2, 3)</c>
    /// </summary>
    /// <remarks>
    /// 支持位运算标志（Not）来表示反向操作（如 NOT IN, NOT LIKE）。
    /// </remarks>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class BinaryExpr : Expr
    {
        // 映射操作符到基础 SQL 符号
        private static Dictionary<BinaryOperator, string> operatorTexts = new Dictionary<BinaryOperator, string>()
        {
            { BinaryOperator.Equal,"=" },
            { BinaryOperator.GreaterThan,">" },
            { BinaryOperator.LessThan,"<" },
            { BinaryOperator.NotEqual,"!=" },
            { BinaryOperator.GreaterThanOrEqual,">=" },
            { BinaryOperator.LessThanOrEqual,"<=" },
            { BinaryOperator.Add,"+"  },
            { BinaryOperator.Subtract,"-" },
            { BinaryOperator.Multiply,"*" },
            { BinaryOperator.Divide,"/" },
            { BinaryOperator.Concat,"||" }
        };

        /// <summary>
        /// 创建空的二元表达式。
        /// </summary>
        public BinaryExpr() { }

        /// <summary>
        /// 使用指定的左右操作数和操作符初始化二元表达式。
        /// </summary>
        /// <param name="left">左侧表达式（通常是 PropertyExpr）</param>
        /// <param name="oper">二元操作符</param>
        /// <param name="right">右侧表达式（通常是 ValueExpr 或另一个 PropertyExpr）</param>
        public BinaryExpr(Expr left, BinaryOperator oper, Expr right)
        {
            Left = left;
            Operator = oper;
            Right = right;
        }

        /// <summary>
        /// 指示该表达式是否为返回值的表达式（如加减乘除、字符串拼接），而非布尔判断条件。
        /// </summary>
        public override bool IsValue =>
            Operator >= BinaryOperator.Add && Operator <= BinaryOperator.Concat;

        /// <summary>
        /// 获取或设置左侧子表达式。
        /// </summary>
        public Expr Left { get; set; }

        /// <summary>
        /// 获取或设置右侧子表达式。
        /// </summary>
        public Expr Right { get; set; }

        /// <summary>
        /// 获取或设置二元操作符。
        /// </summary>
        public BinaryOperator Operator { get; set; }

        /// <summary>
        /// 获取去掉 NOT 标志后的原始操作符（例如 Not|In => In）。
        /// </summary>
        public BinaryOperator OriginOperator => Operator.Positive();

        /// <summary>
        /// 反转当前表达式的左右表达式位置，并尽可能保持原本的逻辑结果（例如 "a &gt; b" 变为 "b &lt; a"）。
        /// </summary>
        /// <param name="keepResult">若为 true，则根据对称性调整操作符以确保逻辑结果不变。</param>
        /// <returns>反转后的新 BinaryExpr。</returns>
        /// <exception cref="InvalidOperationException">当操作符不支持逻辑反转（如 StartsWith）时抛出。</exception>
        public BinaryExpr Reverse(bool keepResult = false)
        {
            BinaryExpr newExpr = new BinaryExpr(Right, Operator, Left);
            if (!keepResult)
            {
                newExpr.Operator = Operator switch
                {
                    BinaryOperator.GreaterThan => BinaryOperator.LessThan,
                    BinaryOperator.LessThan => BinaryOperator.GreaterThan,
                    BinaryOperator.GreaterThanOrEqual => BinaryOperator.LessThanOrEqual,
                    BinaryOperator.LessThanOrEqual => BinaryOperator.GreaterThanOrEqual,
                    BinaryOperator.Equal => BinaryOperator.Equal,
                    BinaryOperator.NotEqual => BinaryOperator.NotEqual,
                    BinaryOperator.Add => BinaryOperator.Add,
                    BinaryOperator.Multiply => BinaryOperator.Multiply,  
                    _ => throw new InvalidOperationException($"操作符: {Operator}不支持等价反转")
                };
            }
            return newExpr;
        }

        /// <summary>
        /// 返回当前表达式的字符串预览（非最终 SQL）。
        /// </summary>
        public override string ToString()
        {
            if (!operatorTexts.TryGetValue(Operator, out string op)) op = Operator.ToString();
            return $"{Left} {op} {Right}";
        }

        /// <summary>
        /// 比较两个 BinaryExpr 是否相等。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is BinaryExpr b &&
                   b.Operator == Operator &&
                   Equals(b.Left, Left) &&
                   Equals(b.Right, Right);
        }

        /// <summary>
        /// 作为默认哈希函数。
        /// </summary>
        /// <returns>当前对象的哈希代码。</returns>
        public override int GetHashCode()
        {
            return
                
                OrderedHashCodes(GetType().GetHashCode(), Operator.GetHashCode(), (Left?.GetHashCode() ?? 0), (Right?.GetHashCode() ?? 0));
        }
    }

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

    /// <summary>
    /// 为二元操作符提供的便捷扩展工具。
    /// </summary>
    public static class BinaryOperatorExt
    {
        /// <summary>
        /// 检查指定的操作符是否带有 NOT 标志。
        /// </summary>
        public static bool IsNot(this BinaryOperator oper)
        {
            return (oper & BinaryOperator.Not) == BinaryOperator.Not;
        }

        /// <summary>
        /// 提取剥离了 NOT 标志的正向操作符。
        /// </summary>
        public static BinaryOperator Positive(this BinaryOperator oper)
        {
            return oper & ~BinaryOperator.Not;
        }

        /// <summary>
        /// 获取当前操作符的对立版本（即取反或撤销取反）。
        /// </summary>
        public static BinaryOperator Opposite(this BinaryOperator oper)
        {
            return oper ^ BinaryOperator.Not;
        }
    }
}
