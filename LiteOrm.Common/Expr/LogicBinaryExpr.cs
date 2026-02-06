using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 逻辑二元表达式类，用于表示二元比较操作，例如 id = 1, name LIKE '%abc%' 等
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class LogicBinaryExpr : LogicExpr
    {
        /// <summary>
        /// 映射逻辑运算符到对应的 SQL 运算符文本
        /// </summary>
        private static readonly Dictionary<LogicOperator, string> operatorTexts = new()
        {
            { LogicOperator.Equal,"=" },
            { LogicOperator.GreaterThan,">" },
            { LogicOperator.LessThan,"<" },
            { LogicOperator.NotEqual,"!=" },
            { LogicOperator.GreaterThanOrEqual,">=" },
            { LogicOperator.LessThanOrEqual,"<=" }
        };

        /// <summary>
        /// 初始化默认的逻辑二元表达式
        /// </summary>
        public LogicBinaryExpr() { }

        /// <summary>
        /// 使用指定的左操作数、运算符和右操作数初始化逻辑二元表达式
        /// </summary>
        /// <param name="left">左操作数表达式</param>
        /// <param name="oper">逻辑运算符</param>
        /// <param name="right">右操作数表达式</param>
        public LogicBinaryExpr(ValueTypeExpr left, LogicOperator oper, ValueTypeExpr right)
        {
            Left = left;
            Operator = oper;
            Right = right;
        }

        /// <summary>
        /// 获取左操作数表达式
        /// </summary>
        public ValueTypeExpr Left { get; set; }

        /// <summary>
        /// 获取右操作数表达式
        /// </summary>
        public ValueTypeExpr Right { get; set; }

        /// <summary>
        /// 获取逻辑运算符
        /// </summary>
        public LogicOperator Operator { get; set; }

        /// <summary>
        /// 获取去除 NOT 标记后的原始运算符（Not|In => In）
        /// </summary>
        public LogicOperator OriginOperator => Operator.Positive();

        /// <summary>
        /// 反转当前表达式，将左右操作数位置互换
        /// 例如 "a &gt; b" 变为 "b &lt; a"
        /// </summary>
        /// <param name="keepEquivalent">是否保持等价（不反转运算符方向）</param>
        /// <returns>反转后的新表达式</returns>
        public LogicBinaryExpr Reverse(bool keepEquivalent = false)
        {
            LogicBinaryExpr newExpr = new LogicBinaryExpr(Right, Operator, Left);
            if (!keepEquivalent)
            {
                newExpr.Operator = Operator switch
                {
                    LogicOperator.GreaterThan => LogicOperator.LessThan,
                    LogicOperator.LessThan => LogicOperator.GreaterThan,
                    LogicOperator.GreaterThanOrEqual => LogicOperator.LessThanOrEqual,
                    LogicOperator.LessThanOrEqual => LogicOperator.GreaterThanOrEqual,
                    LogicOperator.Equal => LogicOperator.Equal,
                    LogicOperator.NotEqual => LogicOperator.NotEqual,
                    _ => throw new InvalidOperationException($"Operator: {Operator} does not support equivalent reversal")
                };
            }
            return newExpr;
        }

        /// <summary>
        /// 返回表达式的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            if (!operatorTexts.TryGetValue(Operator, out string op)) op = Operator.ToString();
            return $"{Left} {op} {Right}";
        }

        /// <summary>
        /// 判断当前对象是否与指定对象相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj)
        {
            return obj is LogicBinaryExpr b &&
                   b.Operator == Operator &&
                   Equals(b.Left, Left) &&
                   Equals(b.Right, Right);
        }

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), (int)Operator, (Left?.GetHashCode() ?? 0), (Right?.GetHashCode() ?? 0));
        }
    }
}
