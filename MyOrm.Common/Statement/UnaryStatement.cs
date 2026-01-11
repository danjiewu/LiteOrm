using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    public sealed class UnaryStatement : Statement
    {
        /// <summary>
        /// 无参构造。
        /// </summary>
        public UnaryStatement()
        {
        }
        /// <summary>
        /// 使用单目操作符与操作对象构造语句。
        /// </summary>
        /// <param name="oper">单目操作符</param>
        /// <param name="operand">操作对象</param>
        public UnaryStatement(UnaryOperator oper, Statement operand)
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
        public Statement Operand { get; set; }
        /// <inheritdoc/>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            switch (Operator)
            {
                case UnaryOperator.Not:
                    return $"NOT {Operand.ToSql(context, sqlBuilder, outputParams)}";
                case UnaryOperator.Nagive:
                    return $"-{Operand.ToSql(context, sqlBuilder, outputParams)}";
                case UnaryOperator.BitwiseNot:
                    return $"~{Operand.ToSql(context, sqlBuilder, outputParams)}";
                default:
                    return Operand.ToSql(context, sqlBuilder, outputParams);
            }
        }

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

        public override bool Equals(object obj)
        {
            return obj is UnaryStatement p && p.Operator == Operator && Equals(p.Operand, Operand);
        }

        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), Operator.GetHashCode(), Operand.GetHashCode());
        }
    }

    /// <summary>
    /// 单目操作符
    /// </summary>
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
