using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    public class StatementDisplayTextBuilder
    {
        public Type Type { get; init; }
        public StatementDisplayTextBuilder(Type entityType)
        {
            Type = entityType;
        }

        public string ToDisplayText(UnaryStatement condition)
        {
            return $"{ToDisplayText(condition.Operator)} {ToDisplayText(condition.Operand)}";
        }
        public string ToDisplayText(PropertyStatement property)
        {
            var prop = Util.GetProperty(Type, property.PropertyName);
            if (prop == null) throw new ArgumentException($"属性'{property.PropertyName}'在类型'{Type.FullName}'中不存在或不可读", property.PropertyName);
            return prop.DisplayName;
        }

        public string ToDisplayText(RawSqlStatement rawSqlStatement)
        {
            return $"{{SQL:{rawSqlStatement.Sql}}}";
        }

        public string ToDisplayText(ValueStatement valueStatement)
        {
            return Util.ToDisplayText(valueStatement.Value);
        }
        public string ToDisplayText(FunctionStatement functionStatement)
        {
            return $"{functionStatement.FunctionName}({String.Join(", ", functionStatement.Parameters.Select(arg => ToDisplayText(arg)))})";
        }
        public string ToDisplayText(StatementSet set)
        {
            string joiner = set.JoinType switch { StatementJoinType.And => " 且 ", StatementJoinType.Or => " 或 ", StatementJoinType.Comma => "," };
            return $"({String.Join(joiner, set.Items.Select(s => ToDisplayText(s)))})";
        }

        public string ToDisplayText(Statement condition)
        {
            return condition switch
            {
                PropertyStatement property => ToDisplayText(property),
                ValueStatement value => ToDisplayText(value),
                BinaryStatement binary => ToDisplayText(binary),
                UnaryStatement unary => ToDisplayText(unary),
                StatementSet set => ToDisplayText(set),
                RawSqlStatement rawSql => ToDisplayText(rawSql),
                FunctionStatement function => ToDisplayText(function),
                _ => condition.ToString()
            };
        }
        public string ToDisplayText(UnaryOperator op)
        {
            return op switch
            {
                UnaryOperator.Not => "不",
                UnaryOperator.BitwiseNot => "反",
                UnaryOperator.Nagive => "负",
                _ => op.ToString(),
            };
        }
        public string ToDisplayFormat(BinaryOperator op)
        {
            StringBuilder sb = new StringBuilder();
            if (op.IsNot()) sb.Append("不");
            switch (op.Origin())
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
                    sb.Append(op.Origin() + "{0}"); break;
            }
            return sb.ToString();
        }

        public string ToDisplayText(BinaryStatement condtion)
        {
            return $"{ToDisplayText(condtion.Left)} {String.Format(ToDisplayFormat(condtion.Operator), condtion.Right)}";
        }
    }
}
