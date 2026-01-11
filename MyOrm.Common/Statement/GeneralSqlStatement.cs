using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 通过委托生成的 SQL 片段。
    /// </summary>
    public sealed class GeneralSqlStatement : Statement
    {
        /// <summary>
        /// 使用委托构造，可以在生成 SQL 时依据上下文动态生成字符串。
        /// </summary>
        /// <param name="func">处理上下文并返回 SQL 字符串的委托</param>
        public GeneralSqlStatement(Expression<Func<SqlBuildContext, ISqlBuilder, ICollection<KeyValuePair<string, object>>, string>> func)
        {
            sqlHandler = func ?? throw new ArgumentNullException(nameof(func));
        }

        private Expression<Func<SqlBuildContext, ISqlBuilder, ICollection<KeyValuePair<string, object>>, string>> sqlHandler;
        /// <inheritdoc/>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            return sqlHandler?.Compile()?.Invoke(context, sqlBuilder, outputParams);
        }

        public override string ToString()
        {
            return sqlHandler?.ToString();
        }

        public override bool Equals(object obj)
        {
            return obj is GeneralSqlStatement g && g.sqlHandler.ToString() == sqlHandler.ToString();
        }

        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), sqlHandler.ToString().GetHashCode());
        }
    }
}
