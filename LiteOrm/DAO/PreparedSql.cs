using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LiteOrm
{
    /// <summary>
    /// 包含命名参数的 SQL 语句。
    /// </summary>
    public class PreparedSql
    {
        /// <summary>
        /// 初始化的 SQL 和参数列表。
        /// </summary>
        /// <param name="sql">SQL 文本片段。</param>
        /// <param name="paramsList">参数化查询所需的键值对集合。</param>
        public PreparedSql(string sql, IEnumerable<KeyValuePair<string, object>> paramsList)
        {
            Sql = sql;
            Params = paramsList.ToList();
        }


        /// <summary>
        /// 获取生成的 SQL 语句。
        /// </summary>
        public string Sql { get; }

        /// <summary>
        /// 获取 SQL 语句中引用的参数列表（按 0, 1, 2... 命名，或按数据库方言命名）。
        /// </summary>
        public List<KeyValuePair<string, object>> Params { get; }

        /// <summary>
        /// 返回调试友好的生成的 SQL 及其参数列表。
        /// </summary>
        public override string ToString()
        {
            return $"SQL: {Sql} \nParams : {String.Join("\n", Params)}";
        }
    }
}
