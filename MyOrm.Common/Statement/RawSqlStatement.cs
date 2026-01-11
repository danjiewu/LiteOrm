using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 表示静态原始 SQL 片段。
    /// </summary>
    public sealed class RawSqlStatement : Statement
    {
        /// <summary>
        /// 无参构造。
        /// </summary>
        public RawSqlStatement()
        {
        }

        /// <summary>
        /// 使用指定 SQL 字符串构造。
        /// </summary>
        /// <param name="sql">原始 SQL 片段</param>
        public RawSqlStatement(string sql)
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

        public override string ToString()
        {
            return Sql;
        }

        public override bool Equals(object obj)
        {
            return obj is RawSqlStatement r && r.Sql == Sql;
        }

        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), Sql?.GetHashCode() ?? 0);
        }
    }
}
