using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示单目运算表达式（如 NOT 运算）。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class UnaryExpr : Expr
    {
        /// <summary>
        /// 无参构造。
        /// </summary>
        public UnaryExpr()
        {
        }
        /// <summary>
        /// 使用单目操作符与操作对象构造表达式。
        /// </summary>
        /// <param name="oper">单目操作符</param>
        /// <param name="operand">操作对象</param>
        public UnaryExpr(UnaryOperator oper, Expr operand)
        {
            Operator = oper;
            Operand = operand;
        }
        /// <summary>
        /// 单目操作符
        /// </summary>
        public UnaryOperator Operator { get; set; }
        /// <summary>
        /// 操作对象
        /// </summary>
        public Expr Operand { get; set; }

        /// <inheritdoc/>
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


        /// <inheritdoc/>
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

    /// <summary>
    /// 单目操作符
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UnaryOperator
    {
        /// <summary>
        /// 逻辑取反
        /// </summary>
        Not = 0,
        /// <summary>
        /// 负号
        /// </summary>
        Nagive = 1,
        /// <summary>
        /// 按位取反
        /// </summary>
        BitwiseNot = 2,
    }
}
