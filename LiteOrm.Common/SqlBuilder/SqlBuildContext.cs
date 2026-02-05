using System;
using System.Text.RegularExpressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// SqlBuilder生成sql时的上下文
    /// </summary>
    public class SqlBuildContext
    {
        /// <summary>
        /// 使用指定的表元数据初始化 <see cref="SqlBuildContext"/> 的新实例。
        /// </summary>
        /// <param name="table">表元数据。</param>
        public SqlBuildContext(SqlTable table)
        {
            Table = table;
        }

        /// <summary>
        /// 使用指定的表元数据和别名初始化 <see cref="SqlBuildContext"/> 的新实例。
        /// </summary>
        /// <param name="table">表元数据。</param>
        /// <param name="tableAliasName">表别名。</param>
        public SqlBuildContext(SqlTable table, string tableAliasName)
        {
            Table = table;
            TableAliasName = tableAliasName;
        }

        /// <summary>
        /// 使用指定的表元数据、别名和表名参数初始化 <see cref="SqlBuildContext"/> 的新实例。
        /// </summary>
        /// <param name="table">表元数据。</param>
        /// <param name="tableAliasName">表别名。</param>
        /// <param name="tableNameArgs">用于动态生成表名的参数集合。</param>
        public SqlBuildContext(SqlTable table, string tableAliasName, string[] tableNameArgs)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (tableNameArgs == null) tableNameArgs = Array.Empty<string>();
            foreach (var arg in tableNameArgs)
            {
                if (!tableNameRegex.IsMatch(arg))
                {
                    throw new ArgumentException("Table name parameter contains illegal characters.", nameof(tableNameArgs));
                }
            }
            Table = table;
            TableAliasName = tableAliasName;
            TableNameArgs = tableNameArgs;
        }
        /// <summary>
        /// 表别名
        /// </summary>
        public string TableAliasName { get; }
        /// <summary>
        /// 表信息
        /// </summary>
        public SqlTable Table { get; }
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
        public string[] TableNameArgs { get; }

        /// <summary>
        /// 链式结构中的上级上下文节点
        /// </summary>
        public SqlBuildContext Parent { get; set; }

        private SqlBuildContext _root;
        /// <summary>
        /// 链式结构的根上下文节点
        /// </summary>
        public SqlBuildContext Root
        {
            get
            {
                if (_root == null)
                {
                    _root = Parent?.Root ?? this;
                }
                else if(_root==this){
                    return _root;
                }
                else if (_root != _root.Root)
                {
                    _root = _root.Root;
                }
                return _root;
            }
        }


        private const string tableNamePattern = @"^[a-zA-Z0-9_]*$";
        private static readonly Regex tableNameRegex = new Regex(tableNamePattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// 获取带参数的表名。
        /// </summary>
        /// <returns>格式化后的表名。</returns>
        public string FactTableName
        {
            get
            {
                if (TableNameArgs != null && TableNameArgs.Length > 0)
                {
                    return String.Format(Table.Name, TableNameArgs);
                }
                return Table.Name;
            }
        }
    }
}
