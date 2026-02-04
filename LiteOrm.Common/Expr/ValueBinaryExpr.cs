using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 值类型二元表达式，用于数值计算和字符串拼接等，如 a + b, str1 || str2 等
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class ValueBinaryExpr : ValueTypeExpr
    {
        private static readonly Dictionary<ValueOperator, string> operatorTexts = new()
        {
            { ValueOperator.Add,"+"  },
            { ValueOperator.Subtract,"-" },
            { ValueOperator.Multiply,"*" },
            { ValueOperator.Divide,"/" },
            { ValueOperator.Concat,"||" }
        };

        public ValueBinaryExpr() { }

        public ValueBinaryExpr(ValueTypeExpr left, ValueOperator oper, ValueTypeExpr right)
        {
            Left = left;
            Operator = oper;
            Right = right;
        }

        public override bool IsValue => true;

        /// <summary>
        /// 获取或设置左操作数表达式
        /// </summary>
        public ValueTypeExpr Left { get; set; }

        /// <summary>
        /// 获取或设置右操作数表达式
        /// </summary>
        public ValueTypeExpr Right { get; set; }

        /// <summary>
        /// 获取或设置二元运算符
        /// </summary>
        public ValueOperator Operator { get; set; }

        public override string ToString()
        {
            if (!operatorTexts.TryGetValue(Operator, out string op)) op = Operator.ToString();
            return $"{Left} {op} {Right}";
        }

        public override bool Equals(object obj)
        {
            return obj is ValueBinaryExpr b &&
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
