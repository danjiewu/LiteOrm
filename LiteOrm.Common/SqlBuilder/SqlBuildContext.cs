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
        /// 表名参数，用于动态生成表名
        /// </summary>
        public string[] TableNameArgs { get; set; } = Array.Empty<string>();

        private Dictionary<string, TableDefinition>  nameTableDefMap = new Dictionary<string, TableDefinition>(StringComparer.OrdinalIgnoreCase);
    }
}
