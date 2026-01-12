using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 表达式显示文本构建器，用于将Expr对象转换为可读的显示文本
    /// </summary>
    public class ExprDisplayTextBuilder
    {
        /// <summary>
        /// 获取实体类型
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// 使用指定的实体类型初始化ExprDisplayTextBuilder
        /// </summary>
        /// <param name="entityType">实体类型</param>
        public ExprDisplayTextBuilder(Type entityType)
        {
            Type = entityType;
        }

        /// <summary>
        /// 将一元表达式转换为显示文本
        /// </summary>
        /// <param name="condition">一元表达式</param>
        /// <returns>显示文本</returns>
        public string ToDisplayText(UnaryExpr condition)
        {
            return $"{ToDisplayText(condition.Operator)} {ToDisplayText(condition.Operand)}";
        }

        /// <summary>
        /// 将属性表达式转换为显示文本
        /// </summary>
        /// <param name="property">属性表达式</param>
        /// <returns>显示文本</returns>
        public string ToDisplayText(PropertyExpr property)
        {
            var prop = Util.GetProperty(Type, property.PropertyName);
            if (prop is null) throw new ArgumentException($"属性'{property.PropertyName}'在类型'{Type.FullName}'中不存在或不可读", property.PropertyName);
            return prop.DisplayName;
        }


        /// <summary>
        /// 将值表达式转换为显示文本
        /// </summary>
        /// <param name="valueExpr">值表达式</param>
        /// <returns>显示文本</returns>
        public string ToDisplayText(ValueExpr valueExpr)
        {
            return Util.ToDisplayText(valueExpr.Value);
        }

        /// <summary>
        /// 将函数表达式转换为显示文本
        /// </summary>
        /// <param name="functionExpr">函数表达式</param>
        /// <returns>显示文本</returns>
        public string ToDisplayText(FunctionExpr functionExpr)
        {
            return $"{functionExpr.FunctionName}({String.Join(", ", functionExpr.Parameters.Select(arg => ToDisplayText(arg)))})";
        }

        /// <summary>
        /// 将表达式集合转换为显示文本
        /// </summary>
        /// <param name="set">表达式集合</param>
        /// <returns>显示文本</returns>
        public string ToDisplayText(ExprSet set)
        {
            string joiner = set.JoinType switch { ExprJoinType.And => " 且 ", ExprJoinType.Or => " 或 ", ExprJoinType.Default => ",", ExprJoinType.Concat => "", _ => "," };
            return $"({String.Join(joiner, set.Items.Select(s => ToDisplayText(s)))})";
        }

        /// <summary>
        /// 将任意表达式转换为显示文本
        /// </summary>
        /// <param name="condition">要转换的表达式</param>
        /// <returns>显示文本</returns>
        public string ToDisplayText(Expr condition)
        {
            if (condition is PropertyExpr property)
                return ToDisplayText(property);
            if (condition is ValueExpr value)
                return ToDisplayText(value);
            if (condition is BinaryExpr binary)
                return ToDisplayText(binary);
            if (condition is UnaryExpr unary)
                return ToDisplayText(unary);
            if (condition is ExprSet set)
                return ToDisplayText(set);
            if (condition is FunctionExpr function)
                return ToDisplayText(function);
            if (condition is LambdaExpr lambdaExpr)
                return ToDisplayText(lambdaExpr.InnerExpr);
            return condition.ToString();
        }

        /// <summary>
        /// 将一元操作符转换为显示文本
        /// </summary>
        /// <param name="op">一元操作符</param>
        /// <returns>显示文本</returns>
        public string ToDisplayText(UnaryOperator op)
        {
            switch (op)
            {
                case UnaryOperator.Not: return "不";
                case UnaryOperator.BitwiseNot: return "反";
                case UnaryOperator.Nagive: return "负";
                default: return op.ToString();
            }
        }

        /// <summary>
        /// 将二元操作符转换为显示格式字符串
        /// </summary>
        /// <param name="op">二元操作符</param>
        /// <returns>显示格式字符串，使用{0}作为占位符</returns>
        public string ToDisplayFormat(BinaryOperator op)
        {
            StringBuilder sb = new StringBuilder();
            if (op.IsNot()) sb.Append("不");
            switch (op.Positive())
            {
                case BinaryOperator.In:
                    sb.Append("在{0}中"); break;
                case BinaryOperator.GreaterThan:
                    sb.Append("大于{0}"); break;
                case BinaryOperator.LessThan:
                    sb.Append("小于{0}"); break;
                case BinaryOperator.Contains:
                    sb.Append("包含{0}"); break;
                case BinaryOperator.Like:
                    sb.Append("匹配{0}"); break;
                case BinaryOperator.RegexpLike:
                    sb.Append("正则匹配{0}"); break;
                case BinaryOperator.Equal:
                    sb.Append("等于{0}"); break;
                case BinaryOperator.EndsWith:
                    sb.Append("以{0}结尾"); break;
                case BinaryOperator.StartsWith:
                    sb.Append("以{0}开头"); break;
                default:
                    sb.Append(op.Positive() + "{0}"); break;
            }
            return sb.ToString();
        }

        /// <summary>
        /// 将二元表达式转换为显示文本
        /// </summary>
        /// <param name="condtion">二元表达式</param>
        /// <returns>显示文本</returns>
        public string ToDisplayText(BinaryExpr condtion)
        {
            return $"{ToDisplayText(condtion.Left)} {String.Format(ToDisplayFormat(condtion.Operator), condtion.Right)}";
        }
    }
}
