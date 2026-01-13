using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示一个基于Lambda表达式的SQL条件表达式对象
    /// </summary>
    public sealed class LambdaExpr : Expr
    {
        private Expr expr;

        /// <summary>
        /// 无参构造函数
        /// </summary>
        public LambdaExpr()
        {
        }

        /// <summary>
        /// 使用Lambda表达式初始化表达式
        /// </summary>
        /// <param name="expression">Lambda表达式，例如：x => x.Name == "John"</param>
        public LambdaExpr(LambdaExpression expression)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression)); ;
        }

        /// <summary>
        /// 获取Lambda表达式
        /// </summary>
        public LambdaExpression Expression
        {
            get;
        }

        /// <summary>
        /// 获取转换后的表达式对象
        /// </summary>
        public Expr InnerExpr
        {
            get
            {
                return expr ?? (expr = new LambdaExprConverter(Expression).ToExpr());
            }
        }

        /// <inheritdoc/>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            return InnerExpr.ToSql(context, sqlBuilder, outputParams);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Expression.ToString();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is LambdaExpr es && es.InnerExpr.Equals(InnerExpr);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), InnerExpr.GetHashCode());
        }
    }
}
