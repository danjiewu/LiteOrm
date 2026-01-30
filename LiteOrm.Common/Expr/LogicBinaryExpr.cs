using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 逻辑二元表达式，用于布尔判断条件（如 id = 1, name LIKE '%abc%'）。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class LogicBinaryExpr : LogicExpr
    {
        // 映射操作符到基础 SQL 符号
        private static readonly Dictionary<LogicOperator, string> operatorTexts = new()
        {
            { LogicOperator.Equal,"=" },
            { LogicOperator.GreaterThan,">" },
            { LogicOperator.LessThan,"<" },
            { LogicOperator.NotEqual,"!=" },
            { LogicOperator.GreaterThanOrEqual,">=" },
            { LogicOperator.LessThanOrEqual,"<=" },
            { LogicOperator.Like,"LIKE" },
            { LogicOperator.StartsWith,"LIKE" },
            { LogicOperator.EndsWith,"LIKE" },
            { LogicOperator.Contains,"LIKE" },
            { LogicOperator.In,"IN" },
            { LogicOperator.RegexpLike,"REGEXP_LIKE" }
        };

        /// <summary>
        /// 创建空的逻辑二元表达式。
        /// </summary>
        public LogicBinaryExpr() { }

        /// <summary>
        /// 使用指定的左右操作数和操作符初始化逻辑二元表达式。
        /// </summary>
        public LogicBinaryExpr(ValueTypeExpr left, LogicOperator oper, ValueTypeExpr right)
        {
            Left = left;
            Operator = oper;
            Right = right;
        }

        /// <summary>
        /// 获取或设置左侧子表达式。
        /// </summary>
        public ValueTypeExpr Left { get; set; }

        /// <summary>
        /// 获取或设置右侧子表达式。
        /// </summary>
        public ValueTypeExpr Right { get; set; }

        /// <summary>
        /// 获取或设置二元操作符。
        /// </summary>
        public LogicOperator Operator { get; set; }

        /// <summary>
        /// 获取去掉 NOT 标志后的原始操作符（例如 Not|In => In）。
        /// </summary>
        public LogicOperator OriginOperator => Operator.Positive();

        /// <summary>
        /// 反转当前表达式的左右表达式位置，并尽可能保持原本的逻辑结果（例如 "a &gt; b" 变为 "b &lt; a"）。
        /// </summary>
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

        public override string ToString()
        {
            if (!operatorTexts.TryGetValue(Operator, out string op)) op = Operator.ToString();
            return $"{Left} {op} {Right}";
        }

        public override bool Equals(object obj)
        {
            return obj is LogicBinaryExpr b &&
                   b.Operator == Operator &&
                   Equals(b.Left, Left) &&
                   Equals(b.Right, Right);
        }

        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), (int)Operator, (Left?.GetHashCode() ?? 0), (Right?.GetHashCode() ?? 0));
        }
    }
}
