using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LiteOrm.Common
{
    /// <summary>
    /// SqlBuilder生成sql时的上下文
    /// </summary>
    public class SqlBuildContext
    {
        /// <summary>
        /// 表信息提供者
        /// </summary>
        public TableInfoProvider TableInfoProvider { get; set; }
        /// <summary>
        /// 表别名
        /// </summary>
        public string TableAliasName { get; set; }
        /// <summary>
        /// 表信息
        /// </summary>
        public SqlTable Table { get; set; }
        /// <summary>
        /// 序列，用来生成表别名
        /// </summary>
        public int Sequence { get; set; }
        /// <summary>
        /// 单表模式，字段名前无需加表名
        /// </summary>
        public bool SingleTable { get; set; } = false;
        /// <summary>
        /// 参数前缀，用于生成SQL参数名
        /// </summary>
        public string ArgPrefix { get; set; }
        
        /// <summary>
        /// 表名参数，用于动态生成表名
        /// </summary>
        public string[] TableNameArgs { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 获取带参数的表名
        /// </summary>
        /// <param name="oraginTableName">原始表名（可能包含格式化占位符）</param>
        /// <returns>格式化后的表名</returns>
        public string GetTableNameWithArgs(string oraginTableName)
        {
            if (TableNameArgs is not null && TableNameArgs.Length > 0)
            {
                return String.Format(oraginTableName, TableNameArgs.Select(s => ArgPrefix + s).ToArray());
            }
            else
            {
                return oraginTableName;
            }
        }
    }
}
