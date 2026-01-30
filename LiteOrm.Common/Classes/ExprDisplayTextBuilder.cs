using System;
using System.Linq;

namespace LiteOrm.Common
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
        /// 将逻辑一元表达式转换为显示文本
        /// </summary>
        /// <param name="condition">逻辑一元表达式</param>
        /// <returns>显示文本</returns>
        public string ToDisplayText(NotExpr condition)
        {
            return $"不 {ToDisplayText(condition.Operand)}";
        }

        /// <summary>
        /// 将值一元表达式转换为显示文本
        /// </summary>
        /// <param name="condition">值一元表达式</param>
        /// <returns>显示文本</returns>
        public string ToDisplayText(ValueUnaryExpr condition)
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
            if (prop is null) throw new ArgumentException($"Property '{property.PropertyName}' does not exist or is not readable in type '{Type.FullName}'", property.PropertyName);
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
        /// 将逻辑表达式集合转换为显示文本
        /// </summary>
        public string ToDisplayText(LogicExprSet set)
        {
            string joiner = set.JoinType switch { LogicJoinType.And => " 且 ", LogicJoinType.Or => " 或 ", _ => "," };
            return $"({String.Join(joiner, set.Select(s => ToDisplayText(s)))})";
        }

        /// <summary>
        /// 将值表达式集合转换为显示文本
        /// </summary>
        public string ToDisplayText(ValueExprSet set)
        {
            string joiner = set.JoinType switch { ValueJoinType.List => ",", ValueJoinType.Concat => "", _ => "," };
            return $"({String.Join(joiner, set.Select(s => ToDisplayText(s)))})";
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
            if (condition is LogicBinaryExpr logicBinary)
                return ToDisplayText(logicBinary);
            if (condition is ValueBinaryExpr valueBinary)
                return ToDisplayText(valueBinary);
            if (condition is NotExpr lu)
                return ToDisplayText(lu);
            if (condition is ValueUnaryExpr vu)
                return ToDisplayText(vu);
            if (condition is LogicExprSet logicSet)
                return ToDisplayText(logicSet);
            if (condition is ValueExprSet valueSet)
                return ToDisplayText(valueSet);
            if (condition is FunctionExpr function)
                return ToDisplayText(function);
            if (condition is LambdaExpr lambdaExpr)
                return ToDisplayText(lambdaExpr.InnerExpr);
            return condition.ToString();
        }

        /// <summary>
        /// 将值一元操作符转换为显示文本
        /// </summary>
        /// <param name="op">值一元操作符</param>
        /// <returns>显示文本</returns>
        public string ToDisplayText(ValueUnaryOperator op)
        {
            switch (op)
            {
                case ValueUnaryOperator.BitwiseNot: return "反";
                case ValueUnaryOperator.Nagive: return "负";
                default: return op.ToString();
            }
        }

        /// <summary>
        /// 将二元操作符转换为显示格式字符串
        /// </summary>
        /// <param name="op">二元操作符</param>
        /// <returns>显示格式字符串，使用{0}作为占位符</returns>
        public string ToDisplayFormat(LogicBinaryOperator op)
        {
            Span<char> initialBuffer = stackalloc char[128];
            var sb = new ValueStringBuilder(initialBuffer);
            if (op.IsNot()) sb.Append("不");
            switch (op.Positive())
            {
                case LogicBinaryOperator.In:
                    sb.Append("在{0}中"); break;
                case LogicBinaryOperator.GreaterThan:
                    sb.Append("大于{0}"); break;
                case LogicBinaryOperator.LessThan:
                    sb.Append("小于{0}"); break;
                case LogicBinaryOperator.Contains:
                    sb.Append("包含{0}"); break;
                case LogicBinaryOperator.Like:
                    sb.Append("匹配{0}"); break;
                case LogicBinaryOperator.RegexpLike:
                    sb.Append("正则匹配{0}"); break;
                case LogicBinaryOperator.Equal:
                    sb.Append("等于{0}"); break;
                case LogicBinaryOperator.EndsWith:
                    sb.Append("以{0}结尾"); break;
                case LogicBinaryOperator.StartsWith:
                    sb.Append("以{0}开头"); break;
                default:
                    sb.Append(op.Positive().ToString());
                    sb.Append("{0}"); break;
            }
            string result = sb.ToString();
            sb.Dispose();
            return result;
        }

        /// <summary>
        /// 将值二元操作符转换为显示格式字符串
        /// </summary>
        public string ToDisplayFormat(ValueBinaryOperator op)
        {
            return op switch
            {
                ValueBinaryOperator.Add => "{0} + {1}",
                ValueBinaryOperator.Subtract => "{0} - {1}",
                ValueBinaryOperator.Multiply => "{0} * {1}",
                ValueBinaryOperator.Divide => "{0} / {1}",
                ValueBinaryOperator.Concat => "{0}{1}",
                _ => op.ToString()
            };
        }

        /// <summary>
        /// 将逻辑二元表达式转换为显示文本
        /// </summary>
        public string ToDisplayText(LogicBinaryExpr condition)
        {
            return $"{ToDisplayText(condition.Left)} {String.Format(ToDisplayFormat(condition.Operator), ToDisplayText(condition.Right))}";
        }

        /// <summary>
        /// 将值二元表达式转换为显示文本
        /// </summary>
        public string ToDisplayText(ValueBinaryExpr condition)
        {
            if (condition.Operator == ValueBinaryOperator.Concat)
                return ToDisplayText(condition.Left) + ToDisplayText(condition.Right);
            return $"({String.Format(ToDisplayFormat(condition.Operator), ToDisplayText(condition.Left), ToDisplayText(condition.Right))})";
        }
    }
}
