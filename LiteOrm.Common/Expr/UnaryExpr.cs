using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示单目（一元）运算表达式，例如 NOT 逻辑取反、负号（-）或按位取反（~）。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class UnaryExpr : Expr
    {
        /// <summary>
        /// 无参构造，供序列化使用。
        /// </summary>
        public UnaryExpr()
        {
        }
        /// <summary>
        /// 使用操作符和操作数初始化一元表达式。
        /// </summary>
        /// <param name="oper">一元操作符（如 NOT）。</param>
        /// <param name="operand">要操作的目标表达式。</param>
        public UnaryExpr(UnaryOperator oper, Expr operand)
        {
            Operator = oper;
            Operand = operand;
        }

        /// <summary>
        /// 获取或设置一元操作符。
        /// </summary>
        public UnaryOperator Operator { get; set; }

        /// <summary>
        /// 获取或设置该操作符作用的子表达式。
        /// </summary>
        public Expr Operand { get; set; }

        /// <summary>
        /// 返回当前一元运算的字符串预览。
        /// </summary>
        /// <returns>格式如 "NOT (operand)" 的 SQL 片段预览。</returns>
        public override string ToString()
        {
            switch (Operator)
            {
                case UnaryOperator.Not:
                    return $"NOT {Operand?.ToString()}";
                case UnaryOperator.Nagive:
                    return $"-{Operand?.ToString()}";
                case UnaryOperator.BitwiseNot:
                    return $"~{Operand?.ToString()}";
                default:
                    return Operand?.ToString();
            }
        }


        /// <summary>
        /// 比较两个 UnaryExpr 是否逻辑一致。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is UnaryExpr p && p.Operator == Operator && Equals(p.Operand, Operand);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), Operator.GetHashCode(), Operand.GetHashCode());
        }
    }
}
