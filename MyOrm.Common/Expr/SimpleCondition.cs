using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MyOrm.Common
{
    /// <summary>
    /// 简单查询条件
    /// <see cref="Expr"/>    /// 
    /// <code>
    /// SimpleCondition condition = new SimpleCondition("Age", ConditionOperator.LargerThan, 18);
    /// 替换为
    /// Expr.Property("Age", ConditionOperator.LargerThan, 18)
    /// 或通过condition.ToExpr()方法转换
    /// </code>
    /// </summary>
    [Serializable]
    [Obsolete("使用Expr替代")]
    public sealed class SimpleCondition
    {
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public SimpleCondition() { }

        /// <summary>
        /// 以默认操作符ConditionOperator.Equals生成简单查询条件
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="value">条件值</param>
        public SimpleCondition(string propertyName, object value)
        {
            Property = propertyName;
            Operator = ConditionOperator.Equals;
            Value = value;
        }

        /// <summary>
        /// 生成简单查询条件
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="op">条件比较符</param>
        /// <param name="value">条件值</param>
        public SimpleCondition(string propertyName, ConditionOperator op, object value)
        {
            Property = propertyName;
            Operator = op;
            Value = value;
        }

        /// <summary>
        /// 生成简单查询条件
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="op">条件比较符</param>
        /// <param name="value">条件值</param>
        /// <param name="opposite">是否为非</param>
        public SimpleCondition(string propertyName, ConditionOperator op, object value, bool opposite)
        {
            Property = propertyName;
            Operator = op;
            Value = value;
            Opposite = opposite;
        }

        /// <summary>
        /// 逻辑求反
        /// </summary>
        [DefaultValue(false)]
        [XmlAttribute]
        public bool Opposite { get; set; }

        /// <summary>
        /// 属性名
        /// </summary>
        [XmlAttribute]
        public string Property { get; set; }

        /// <summary>
        /// 条件值
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// 条件比较符
        /// </summary>
        [DefaultValue(ConditionOperator.Equals)]
        [XmlAttribute]
        public ConditionOperator Operator { get; set; }

        /// <summary>
        /// 将当前简单条件转换为二元语句。
        /// </summary>
        /// <returns>转换后的二元语句。</returns>
        public BinaryExpr ToExpr()
        {
            BinaryOperator op = Operator switch
            {
                ConditionOperator.Equals => BinaryOperator.Equal,
                ConditionOperator.LargerThan => BinaryOperator.GreaterThan,
                ConditionOperator.SmallerThan => BinaryOperator.LessThan,
                ConditionOperator.StartsWith => BinaryOperator.StartsWith,
                ConditionOperator.EndsWith => BinaryOperator.EndsWith,
                ConditionOperator.Contains => BinaryOperator.Contains,
                ConditionOperator.Like => BinaryOperator.Like,
                ConditionOperator.In => BinaryOperator.In,
                ConditionOperator.RegexpLike => BinaryOperator.RegexpLike,
                _ => throw new NotSupportedException($"不支持的条件操作符：{Operator}"),
            };
            if (Opposite) op |= BinaryOperator.Not;
            return Expr.Property(Property, op, Value);
        }

        /// <summary>
        /// 重写ToString方法
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string str;
            if (Operator == ConditionOperator.In)
            {
                List<string> values = new List<string>();
                foreach (object o in Value as IEnumerable)
                {
                    values.Add(Convert.ToString(o));
                }
                str = String.Join(",", values.ToArray());
            }
            else
                str = Convert.ToString(Value);
            string oper;
            switch (Operator)
            {
                case ConditionOperator.Equals:
                    oper = Opposite ? "!=" : "=";
                    break;
                case ConditionOperator.LargerThan:
                    oper = Opposite ? "<=" : ">";
                    break;
                case ConditionOperator.SmallerThan:
                    oper = Opposite ? ">=" : "<";
                    break;
                default:
                    oper = (Opposite ? "Not " : "") + Operator;
                    break;
            }
            return String.Format("{0} {1} {2}", Property, oper, str);
        }

        /// <summary>
        /// 重写Equals方法
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != typeof(SimpleCondition)) return false;
            SimpleCondition condition = (SimpleCondition)obj;
            return condition.Property == Property && condition.Operator == Operator && Equals(condition.Value, Value);
        }

        /// <summary>
        /// 重写GetHashCode方法
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            int hash = (int)Operator;
            if (Property != null) hash += Property.GetHashCode();
            if (Value != null) hash += Value.GetHashCode();
            return hash;
        }
    }

    /// <summary>
    /// 条件判断操作符
    /// </summary>
    public enum ConditionOperator
    {
        /// <summary>
        /// 相等
        /// </summary>
        Equals,
        /// <summary>
        /// 大于
        /// </summary>
        LargerThan,
        /// <summary>
        /// 小于
        /// </summary>
        SmallerThan,
        /// <summary>
        /// 以指定字符串为开始（作为字符串比较）
        /// </summary>
        StartsWith,
        /// <summary>
        /// 以指定字符串为结尾（作为字符串比较）
        /// </summary>
        EndsWith,
        /// <summary>
        /// 包含制定字符串（作为字符串比较）
        /// </summary>
        Contains,
        /// <summary>
        /// 匹配字符串格式（作为字符串比较）
        /// </summary>
        Like,
        /// <summary>
        /// 包含
        /// </summary>
        In,
        /// <summary>
        /// 正则表达式匹配
        /// </summary>
        RegexpLike
    }

}
