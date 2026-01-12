using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 表示一个基于Lambda表达式的SQL条件表达式对象
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public sealed class LamdaExpr<T> : Expr
    {
        private Expr expr;
        
        /// <summary>
        /// 无参构造函数
        /// </summary>
        public LamdaExpr()
        {
        }
        
        /// <summary>
        /// 使用Lambda表达式初始化表达式
        /// </summary>
        /// <param name="expression">Lambda表达式，例如：x => x.Name == "John"</param>
        public LamdaExpr(Expression<Func<T, bool>> expression)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression)); ;
        }
        
        /// <summary>
        /// 获取Lambda表达式
        /// </summary>
        public Expression<Func<T, bool>> Expression
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
                if (expr == null)
                {
                    var converter = new ExpressionExprConverter(Expression.Parameters[0]);
                    expr = converter.Convert(Expression.Body);
                }

                return expr;
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

        /// <summary>
        /// 从Lambda表达式隐式转换为ExpressionExpr
        /// </summary>
        /// <param name="expression">Lambda表达式</param>
        public static implicit operator LamdaExpr<T>(Expression<Func<T, bool>> expression)
        {
            return new LamdaExpr<T>(expression);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is LamdaExpr<T> es && es.InnerExpr.Equals(InnerExpr);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return InnerExpr.GetHashCode();
        }
    }
}
