using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 通过委托生成的 SQL 片段表达式。
    /// </summary>
    public sealed class GeneralSqlExpr : Expr
    {
        /// <summary>
        /// 使用委托构造，可以在生成 SQL 时依据上下文动态生成字符串。
        /// </summary>
        /// <param name="func">处理上下文并返回 SQL 字符串的委托</param>
        public GeneralSqlExpr(Expression<Func<SqlBuildContext, ISqlBuilder, ICollection<KeyValuePair<string, object>>, string>> func)
        {
            sqlHandler = func ?? throw new ArgumentNullException(nameof(func));
        }

        private Expression<Func<SqlBuildContext, ISqlBuilder, ICollection<KeyValuePair<string, object>>, string>> sqlHandler;
        /// <inheritdoc/>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            return sqlHandler?.Compile()?.Invoke(context, sqlBuilder, outputParams);
        }

        /// <summary>
        /// 返回表示当前表达式的字符串。
        /// </summary>
        /// <returns>表示当前表达式的字符串。</returns>
        public override string ToString()
        {
            return sqlHandler?.ToString();
        }

        /// <summary>
        /// 确定指定的对象是否等于当前对象。
        /// </summary>
        /// <param name="obj">要与当前对象进行比较的对象。</param>
        /// <returns>如果指定的对象等于当前对象，则为 true；否则为 false。</returns>
        public override bool Equals(object obj)
        {
            return obj is GeneralSqlExpr g && g.sqlHandler.ToString() == sqlHandler.ToString();
        }

        /// <summary>
        /// 作为默认哈希函数。
        /// </summary>
        /// <returns>当前对象的哈希代码。</returns>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), sqlHandler.ToString().GetHashCode());
        }
    }
}
