using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 表示静态原始 SQL 片段表达式。
    /// </summary>
    public sealed class RawSqlExpr : Expr
    {
        /// <summary>
        /// 无参构造。
        /// </summary>
        public RawSqlExpr()
        {
        }

        /// <summary>
        /// 使用指定 SQL 字符串构造。
        /// </summary>
        /// <param name="sql">原始 SQL 片段</param>
        public RawSqlExpr(string sql)
        {
            Sql = sql;
        }

        /// <summary>
        /// 指定的静态 SQL 字符串。
        /// </summary>
        public string Sql { get; set; }
        /// <inheritdoc/>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            return Sql;
        }

        /// <summary>
        /// 返回表示当前表达式的字符串。
        /// </summary>
        /// <returns>表示当前表达式的字符串。</returns>
        public override string ToString()
        {
            return Sql;
        }

        /// <summary>
        /// 确定指定的对象是否等于当前对象。
        /// </summary>
        /// <param name="obj">要与当前对象进行比较的对象。</param>
        /// <returns>如果指定的对象等于当前对象，则为 true；否则为 false。</returns>
        public override bool Equals(object obj)
        {
            return obj is RawSqlExpr r && r.Sql == Sql;
        }

        /// <summary>
        /// 作为默认哈希函数。
        /// </summary>
        /// <returns>当前对象的哈希代码。</returns>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), Sql?.GetHashCode() ?? 0);
        }
    }
}
