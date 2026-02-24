using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示逻辑类型的表达式基类。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public abstract class LogicExpr : Expr
    {
        /// <summary>
        /// 逻辑与运算符 &amp; 的重载。
        /// 允许使用 expr1 &amp; expr2 构建复合条件。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>组合后的 AND 表达式。</returns>
        public static LogicExpr operator &(LogicExpr left, LogicExpr right)
        {
            if (left is null) return right;
            else if (right is null) return left;
            else return left.And(right);
        }

        /// <summary>
        /// 逻辑或运算符 | 的重载。
        /// 允许使用 expr1 | expr2 构建复合条件。
        /// </summary>
        /// <param name="left">左操作数。</param>
        /// <param name="right">右操作数。</param>
        /// <returns>组合后的 OR 表达式。</returns>
        public static LogicExpr operator |(LogicExpr left, LogicExpr right)
        {
            if (left is null) return null;
            else if (right is null) return null;
            else return left.Or(right);
        }

        /// <summary>
        /// 逻辑非运算符 ! 的重载。
        /// </summary>
        /// <param name="expr">要取反的表达式。</param>
        /// <returns>逻辑取反后的表达式。</returns>
        public static LogicExpr operator !(LogicExpr expr) => expr?.Not();
    }
}
