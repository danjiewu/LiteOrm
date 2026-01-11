using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Quic;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 抽象语句基类。子类应实现 <see cref="ToSql"/> 将语句转换为 SQL 片段并把所需参数写入。
    /// </summary>
    public abstract class Statement
    {
        protected const int HashSeed = 31;

        protected static int OrderedHashCodes(params int[] hashcodes)
        {
            unchecked
            {
                int hashcode = 0;
                foreach (int hc in hashcodes)
                {
                    hashcode = (hashcode * HashSeed) + hc;
                }
                return hashcode;
            }
        }
        /// <summary>
        /// 将当前语句转换为 SQL 字符串片段。
        /// </summary>
        /// <param name="context">生成 SQL 所需的上下文（表定义、别名等）。</param>
        /// <param name="sqlBuilder">提供数据库特定 SQL 生成辅助的方法。</param>
        /// <param name="outputParams">输出参数集合，方法应在此集合中添加本语句所需的参数（键为参数名，值为参数值）。</param>
        /// <returns>表示本语句的 SQL 字符串片段（不包含外层分号）。</returns>
        public abstract string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams);
        public static PropertyStatement Property(string propertyName)
        {
            return new PropertyStatement(propertyName);
        }
        public static BinaryStatement Property(string propertyName, object value)
        {
            return new BinaryStatement()
            {
                Left = new PropertyStatement(propertyName),
                Right = new ValueStatement(value)
            };
        }

        public static BinaryStatement Property(string propertyName, BinaryOperator oper, object value)
        {
            return new BinaryStatement()
            {
                Left = new PropertyStatement(propertyName),
                Operator = oper,
                Right = new ValueStatement(value)
            };
        }

        // 添加这个方法以支持从 Expression 创建
        public static ExpressionStatement<T> Exp<T>(Expression<Func<T, bool>> expression)
        {
            return new ExpressionStatement<T>(expression);
        }

        public static readonly ValueStatement Null = new ValueStatement();

        // 重载true运算符
        public static bool operator true(Statement a) => a == null;

        // 重载false运算符
        public static bool operator false(Statement a) => false;
        // 重载等于运算符==
        public static bool operator ==(Statement left, Statement right)
        {
            if (ReferenceEquals(left, right)) return true;
            else if (!ReferenceEquals(left, null)) return left.Equals(right);
            else if (!ReferenceEquals(right, null)) return right.Equals(left);
            return Equals(left, right);
        }
        // 重载不等于运算符!=
        public static bool operator !=(Statement left, Statement right)
        {
            return !Equals(left, right);
        }

        // 重载与运算符&
        public static Statement operator &(Statement left, Statement right)
        {
            if (left == null) return right;
            else if (right == null) return left;
            else
                return left.And(right);
        }

        // 重载或运算符|
        public static Statement operator |(Statement left, Statement right)
        {
            if (left == null || right == null) return Null;
            return left.Or(right);
        }

        public static implicit operator Statement(ValueType value)
        {
            return new ValueStatement(value);
        }

        // 重载加法运算符|
        public static Statement operator +(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.Add, right);
        }
        // 重载减法运算符-
        public static Statement operator -(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.Subtract, right);
        }
        // 重载乘法运算符*
        public static Statement operator *(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.Multiply, right);
        }
        // 重载除法运算符/
        public static Statement operator /(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.Divide, right);
        }
        // 重载大于运算符>
        public static Statement operator >(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.GreaterThan, right);
        }
        // 重载小于运算符<
        public static Statement operator <(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.LessThan, right);
        }
        // 重载大于等于运算符>=
        public static Statement operator >=(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.GreaterThanOrEqual, right);
        }
        // 重载小于等于运算符<=
        public static Statement operator <=(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.LessThanOrEqual, right);
        }
    }
    public static class StatementExt
    {
        public static StatementSet And(this Statement left, Statement right)
        {
            return Join(left, right, StatementJoinType.And);
        }

        public static StatementSet Or(this Statement left, Statement right)
        {
            return Join(left, right, StatementJoinType.Or);
        }

        public static StatementSet Concat(this Statement left, Statement right)
        {
            return Join(left, right, StatementJoinType.Concat);
        }

        public static StatementSet Join(this Statement left, Statement right, StatementJoinType joinType = StatementJoinType.Default)
        {
            return new StatementSet(joinType, left, right);
        }
    }
}
