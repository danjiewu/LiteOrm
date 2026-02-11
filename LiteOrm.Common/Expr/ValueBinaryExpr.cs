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
        /// <summary>
        /// 映射值运算符到对应的运算符文本
        /// </summary>
        private static readonly Dictionary<ValueOperator, string> operatorTexts = new()
        {
            { ValueOperator.Add,"+"  },
            { ValueOperator.Subtract,"-" },
            { ValueOperator.Multiply,"*" },
            { ValueOperator.Divide,"/" },
            { ValueOperator.Concat,"||" }
        };

        /// <summary>
        /// 初始化默认的值二元表达式
        /// </summary>
        public ValueBinaryExpr() { }

        /// <summary>
        /// 使用指定的左操作数、运算符和右操作数初始化值二元表达式
        /// </summary>
        /// <param name="left">左操作数表达式</param>
        /// <param name="oper">值运算符</param>
        /// <param name="right">右操作数表达式</param>
        public ValueBinaryExpr(ValueTypeExpr left, ValueOperator oper, ValueTypeExpr right)
        {
            Left = left;
            Operator = oper;
            Right = right;
        }

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
            return obj is ValueBinaryExpr b &&
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
