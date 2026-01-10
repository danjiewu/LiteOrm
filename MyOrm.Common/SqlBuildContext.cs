using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MyOrm.Common
{
    /// <summary>
    /// SqlBuilder生成sql时的上下文
    /// </summary>
    public class SqlBuildContext
    {
        /// <summary>
        /// 表别名
        /// </summary>
        public string TableAliasName { get; set; }
        /// <summary>
        /// 表信息
        /// </summary>
        public Table Table { get; set; }
        /// <summary>
        /// 序列，用来生成表别名
        /// </summary>
        public int Sequence { get; set; }
        /// <summary>
        /// 单表模式，字段名前无需加表名
        /// </summary>
        public bool SingleTable { get; set; } = false;
        public string ArgPrefix { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string[] TableNameArgs { get; set; } = Array.Empty<string>();

        public string GetTableNameWithArgs(string oraginTableName)
        {
            if (TableNameArgs != null && TableNameArgs.Length > 0)
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
