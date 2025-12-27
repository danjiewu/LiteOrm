using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Collections;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace MyOrm.Common
{
    /// <summary>
    /// 查询条件
    /// <seealso cref="MyOrm.Common.SimpleCondition"/>
    /// <seealso cref="MyOrm.Common.ConditionSet"/>  
    /// <seealso cref="MyOrm.Common.ForeignCondition"/>    
    /// </summary>
    [Serializable]
    [JsonDerivedType(typeof(SimpleCondition))]
    [JsonDerivedType(typeof(ForeignCondition), typeDiscriminator: "foreign")]    
    [JsonDerivedType(typeof(ConditionSet), typeDiscriminator: "set")]
    [JsonDerivedType(typeof(ExpressionCondition), typeDiscriminator: "express")]
    public abstract class Condition
    {
        /// <summary>
        /// 逻辑求反
        /// </summary>
        [DefaultValue(false)]
        [XmlAttribute]
        public bool Opposite { get; set; }

        public abstract EnsureResult Ensure(object target);

        public static Condition None
        {
            get { return null; }
        }

        public static Condition Equals(string propertyName, object value)
        {
            return new SimpleCondition(propertyName, value);
        }
        public static Condition Contains(string propertyName, object value)
        {
            return new SimpleCondition(propertyName, ConditionOperator.Contains, value);
        }
        public static Condition LargerThan(string propertyName, object value)
        {
            return new SimpleCondition(propertyName, ConditionOperator.LargerThan, value);
        }
        public static Condition SmallerThan(string propertyName, object value)
        {
            return new SimpleCondition(propertyName, ConditionOperator.SmallerThan, value);
        }
        public static Condition Like(string propertyName, object value)
        {
            return new SimpleCondition(propertyName, ConditionOperator.Like, value);
        }

        public static Condition RegexpLike(string propertyName, object value)
        {
            return new SimpleCondition(propertyName, ConditionOperator.RegexpLike, value);
        }
        public static Condition StartsWith(string propertyName, object value)
        {
            return new SimpleCondition(propertyName, ConditionOperator.StartsWith, value);
        }
        public static Condition EndsWith(string propertyName, object value)
        {
            return new SimpleCondition(propertyName, ConditionOperator.EndsWith, value);
        }

        public static Condition In(string propertyName, IEnumerable values)
        {
            return new SimpleCondition(propertyName, ConditionOperator.In, values);
        }
        public static Condition In(string propertyName, params object[] values)
        {
            return new SimpleCondition(propertyName, ConditionOperator.In, values);
        }

        public static Condition operator &(Condition a, Condition b)
        {
            if (a is null) return b;
            else if (b is null) return a;
            else
                return a.And(b);
        }

        public static Condition operator |(Condition a, Condition b)
        {
            if (a is null || b is null) return null;
            return a.Or(b);
        }

        public static bool operator true(Condition a)
            => a == null;

        public static bool operator false(Condition a)
            => false;

        public static Condition Exp(Expression expression)
        {
            return new ExpressionCondition(expression);
        }
        /// <summary>
        /// Lambda表达式转换Condition
        /// </summary>
        /// <param name="expression">Lambda表达式</param>
        public static implicit operator Condition(LambdaExpression expression)
        {
            return new ExpressionCondition(expression);
        }

    }

    public static class ConditionExtention
    {
        public static Condition And(this Condition condition1, Condition condition2)
        {
            ConditionSet conditionSet1 = condition1 as ConditionSet;
            ConditionSet conditionSet2 = condition2 as ConditionSet;
            if (conditionSet1 != null && conditionSet1.JoinType == ConditionJoinType.And)
            {
                conditionSet1.Add(condition2);
                return conditionSet1;
            }
            else if (conditionSet2 != null && conditionSet2.JoinType == ConditionJoinType.And)
            {
                conditionSet2.Add(condition1);
                return conditionSet2;
            }
            else
                return new ConditionSet(condition1, condition2);
        }

        public static Condition Or(this Condition condition1, Condition condition2)
        {
            ConditionSet conditionSet1 = condition1 as ConditionSet;
            ConditionSet conditionSet2 = condition2 as ConditionSet;
            if (conditionSet1 != null && conditionSet1.JoinType == ConditionJoinType.Or)
            {
                conditionSet1.Add(condition2);
                return conditionSet1;
            }
            else if (conditionSet2 != null && conditionSet2.JoinType == ConditionJoinType.Or)
            {
                conditionSet2.Add(condition1);
                return conditionSet2;
            }
            else
                return new ConditionSet(ConditionJoinType.Or, condition1, condition2);
        }

        public static Condition Opposite(this Condition condition)
        {
            condition.Opposite = !condition.Opposite;
            return condition;
        }

        public static Condition Foreign(this Condition condition, Type foreignType)
        {
            return new ForeignCondition(foreignType, condition);
        }
    }

    /// <summary>
    /// 简单查询条件
    /// </summary>
    [Serializable]
    public sealed class SimpleCondition : Condition
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
        /// 
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public override EnsureResult Ensure(object target)
        {
            object targetValue = target is IIndexedProperty ? ((IIndexedProperty)target)[Property] : target.GetType().GetProperty(Property).GetValue(target, null);
            bool result = false;
            switch (Operator)
            {
                case ConditionOperator.Equals: result = String.Compare(Convert.ToString(targetValue), Convert.ToString(Value), true) == 0; break;
                case ConditionOperator.Contains: result = Convert.ToString(targetValue ?? String.Empty).Contains(Convert.ToString(Value ?? String.Empty)); break;
                case ConditionOperator.EndsWith: result = Convert.ToString(targetValue ?? String.Empty).EndsWith(Convert.ToString(Value ?? String.Empty), StringComparison.OrdinalIgnoreCase); break;
                case ConditionOperator.StartsWith: result = Convert.ToString(targetValue ?? String.Empty).StartsWith(Convert.ToString(Value ?? String.Empty), StringComparison.OrdinalIgnoreCase); break;
                case ConditionOperator.SmallerThan: result = Comparer.Default.Compare(targetValue, Value) < 0; break;
                case ConditionOperator.LargerThan: result = Comparer.Default.Compare(targetValue, Value) > 0; break;
                case ConditionOperator.In:
                    foreach (object o in Value as IEnumerable)
                    {
                        if (Equals(targetValue, o))
                        {
                            result = true;
                            break;
                        }
                    }
                    break;
                case ConditionOperator.Like:
                    result = new Regex(Convert.ToString(Value ?? String.Empty).ToString().Replace(".", "\\.").Replace("_", ".").Replace("%", ".*"), RegexOptions.IgnoreCase).IsMatch(Convert.ToString(targetValue ?? String.Empty));///TODO
                    break;
                case ConditionOperator.RegexpLike:
                    result = new Regex(Convert.ToString(Value ?? String.Empty), RegexOptions.IgnoreCase).IsMatch(Convert.ToString(targetValue ?? String.Empty));
                    break;
                default: return EnsureResult.Undetermined;
            }
            if (Opposite) result = !result;
            return result ? EnsureResult.True : EnsureResult.False;
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
    /// 查询条件集合
    /// </summary>
    [Serializable]
    public sealed class ConditionSet : Condition, IEnumerable<Condition>
    {
        private List<Condition> subConditions = new List<Condition>();
        private ConditionJoinType joinType = ConditionJoinType.And;
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ConditionSet() { }

        /// <summary>
        /// 指定连接类型创建查查询条件集合
        /// </summary>
        /// <param name="joinType">连接类型</param>
        public ConditionSet(ConditionJoinType joinType)
        {
            this.joinType = joinType;
        }

        /// <summary>
        /// 以子查询条件集合初始化
        /// </summary>
        /// <param name="subConditions">子查询条件集合</param>
        public ConditionSet(IEnumerable<Condition> subConditions)
        {
            this.subConditions.AddRange(subConditions);
        }


        /// <summary>
        /// 以子查询条件集合初始化
        /// </summary>
        /// <param name="subConditions">子查询条件集合</param>
        public ConditionSet(params Condition[] subConditions)
        {
            this.subConditions.AddRange(subConditions);
        }

        /// <summary>
        /// 指定连接类型，以子查询条件集合初始化
        /// </summary>
        /// <param name="joinType">连接类型</param>
        /// <param name="subConditions">子查询条件集合</param>
        public ConditionSet(ConditionJoinType joinType, IEnumerable<Condition> subConditions)
        {
            this.joinType = joinType;
            this.subConditions.AddRange(subConditions);
        }

        /// <summary>
        /// 指定连接类型，以子查询条件集合初始化
        /// </summary>
        /// <param name="joinType">连接类型</param>
        /// <param name="subConditions">子查询条件集合</param>
        public ConditionSet(ConditionJoinType joinType, params Condition[] subConditions)
        {
            this.joinType = joinType;
            this.subConditions.AddRange(subConditions);
        }

        /// <summary>
        /// 连接类型，默认为ConditionJoinType.And
        /// </summary>
        [DefaultValue(ConditionJoinType.And)]
        [XmlAttribute]
        public ConditionJoinType JoinType
        {
            get { return joinType; }
            set { joinType = value; }
        }

        /// <summary>
        /// 添加单个查询条件
        /// </summary>
        /// <param name="condition">子查询条件</param>
        public void Add(Condition condition)
        {
            subConditions.Add(condition);
        }        
        /// <summary>
        /// 子查询条件集合
        /// </summary>
        [XmlArray]
        public List<Condition> SubConditions
        {
            get { return subConditions; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public override EnsureResult Ensure(object target)
        {
            bool undetermined = false;
            bool opposite = Opposite;
            ConditionJoinType joinType = JoinType;
            foreach (Condition subCondition in SubConditions)
            {
                EnsureResult subResult = subCondition == null ? EnsureResult.True : subCondition.Ensure(target);
                if (subResult == EnsureResult.False && joinType == ConditionJoinType.And) return opposite ? EnsureResult.True : EnsureResult.False;
                else if (subResult == EnsureResult.True && joinType == ConditionJoinType.Or) return opposite ? EnsureResult.True : EnsureResult.False;
                else if (subResult == EnsureResult.Undetermined) undetermined = true;
            }
            if (undetermined)
                return EnsureResult.Undetermined;
            else if (joinType == ConditionJoinType.Or && SubConditions.Count > 0)
                return opposite ? EnsureResult.True : EnsureResult.False;
            else
                return opposite ? EnsureResult.False : EnsureResult.True;
        }


        /// <summary>
        /// 重写ToString方法
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Condition condition in subConditions)
            {
                if (sb.Length != 0) sb.Append(" " + joinType.ToString() + " ");
                sb.Append("{");
                if (condition != null) sb.Append(condition.ToString());
                sb.Append("}");
            }
            return sb.ToString();
        }

        #region IEnumerable<Condition> 成员

        IEnumerator<Condition> IEnumerable<Condition>.GetEnumerator()
        {
            return (IEnumerator<Condition>)SubConditions;
        }

        #endregion

        #region IEnumerable 成员

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return (System.Collections.IEnumerator)SubConditions;
        }

        #endregion
    }

    /// <summary>
    /// 查询条件的连接关系枚举
    /// </summary>
    public enum ConditionJoinType
    {
        /// <summary>
        /// 通过逻辑与（And）连接
        /// </summary>
        And,
        /// <summary>
        /// 通过逻辑与（Or）连接
        /// </summary>
        Or
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

    public class ExpressionCondition : Condition
    {
        public Expression Expression { get; set; }
        public ExpressionCondition(Expression expression)
        {
            Expression = expression;
        }
        public override EnsureResult Ensure(object target)
        {
            LambdaExpression lambda = Expression.Lambda(Expression);
            Delegate func = lambda.Compile();
            bool result = (bool)func.DynamicInvoke(target);
            return result ? EnsureResult.True : EnsureResult.False;
        }

        public override string ToString()
        {
            return Expression.ToString();
        }
    }

    /// <summary>
    /// 关联外部对象的条件  
    /// </summary>
    [Serializable]
    public class ForeignCondition : Condition
    {
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ForeignCondition() { }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="foreignKey">外键</param>
        /// <param name="condition">外部对象的条件</param>
        public ForeignCondition(string foreignKey, Condition condition)
        {
            JoinedProperty = foreignKey;
            Condition = condition;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="foreignType">外部对象的类型</param>
        /// <param name="condition">外部对象的条件</param>
        public ForeignCondition(Type foreignType, Condition condition)
        {
            ForeignType = foreignType;
            Condition = condition;
        }

        /// <summary>
        /// 关联对象类型
        /// </summary>
        [XmlAttribute(typeof(string))]
        public Type ForeignType { get; set; }

        /// <summary>
        /// 关联外部对象的属性
        /// </summary>
        [XmlAttribute]
        public string ForeignProperty { get; set; }

        /// <summary>
        /// 关联属性，与ForeignProperties对应
        /// </summary>
        [XmlAttribute]
        public string JoinedProperty { get; set; }
        /// <summary>
        /// 外部对象的条件
        /// </summary>
        public Condition Condition { get; set; }

        public override string ToString()
        {
            return String.Format("{0}{1}{2}{3}:{{{4}}}", Opposite ? "Not " : null, JoinedProperty == null ? null : JoinedProperty + "=", ForeignType.Name, ForeignProperty == null ? null : "." + ForeignProperty, Condition);
        }

        public override EnsureResult Ensure(object target)
        {
            return EnsureResult.Undetermined;
        }
    }
}
