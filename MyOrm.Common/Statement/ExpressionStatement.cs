using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    public sealed class ExpressionStatement<T> : Statement
    {
        private Statement statement;
        public ExpressionStatement()
        {
        }
        public ExpressionStatement(Expression<Func<T, bool>> expression)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression)); ;
        }
        public Expression<Func<T, bool>> Expression
        {
            get;
        }

        public Statement Statement
        {
            get
            {
                if (statement == null)
                {
                    var converter = new ExpressionStatementConverter(Expression.Parameters[0]);
                    statement = converter.Convert(Expression.Body);
                }

                return statement;
            }
        }
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            return Statement.ToSql(context, sqlBuilder, outputParams);
        }
        public override string ToString()
        {
            return Expression.ToString();
        }

        public static implicit operator ExpressionStatement<T>(Expression<Func<T, bool>> expression)
        {
            return new ExpressionStatement<T>(expression);
        }

        public override bool Equals(object obj)
        {
            return obj is ExpressionStatement<T> es && es.Statement.Equals(Statement);
        }

        public override int GetHashCode()
        {
            return Statement.GetHashCode();
        }
    }
}
