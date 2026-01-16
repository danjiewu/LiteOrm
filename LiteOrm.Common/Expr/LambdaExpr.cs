using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示一个包含 Lambda 表达式的 SQL 生成表达式容器。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class LambdaExpr : Expr
    {
        private Expr _expr;

        /// <summary>
        /// 无参构造函数。
        /// </summary>
        public LambdaExpr()
        {
        }

        /// <summary>
        /// 使用 Lambda 表达式初始化表达式。
        /// </summary>
        /// <param name="expression">Lambda 表达式，例如：x => x.Name == "John"</param>
        public LambdaExpr(LambdaExpression expression)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression)); ;
        }

        /// <summary>
        /// 获取 Lambda 表达式。
        /// </summary>
        public LambdaExpression Expression
        {
            get;
        }

        /// <summary>
        /// 获取转换后的表达式对象。
        /// </summary>
        public Expr InnerExpr
        {
            get
            {
                return _expr ?? (_expr = new LambdaExprConverter(Expression).ToExpr());
            }
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
