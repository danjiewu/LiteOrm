using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Text.RegularExpressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// SQL构建上下文，用于管理SQL生成过程中的表别名、作用域等信息
    /// </summary>
    public class SqlBuildContext
    {
        /// <summary>
        /// 初始化 <see cref="SqlBuildContext"/> 类的新实例
        /// </summary>
        public SqlBuildContext()
        {
            CurrentScope = new SqlScopeContext();
        }

        /// <summary>
        /// 使用指定的表、别名和表名参数初始化 <see cref="SqlBuildContext"/> 类的新实例
        /// </summary>
        /// <param name="table">SQL表定义</param>
        /// <param name="aliasName">表别名</param>
        /// <param name="tableArgs">动态表名参数</param>
        public SqlBuildContext(SqlTable table, string aliasName, string[] tableArgs)
        {
            CurrentScope = new SqlScopeContext
            {
                DefaultTableAliasName = aliasName,
                TableArgs = tableArgs
            };
            if (table != null)
            {
                CurrentScope.AddTableAlias(aliasName ?? Constants.DefaultTableAlias, table);
            }
        }

        /// <summary>
        /// 序列，用来生成表别名
        /// </summary>
        public int Sequence { get; set; } = 1;        

        /// <summary>
        /// 默认表别名名称
        /// </summary>
        public string DefaultTableAliasName { get => CurrentScope.DefaultTableAliasName; set => CurrentScope.DefaultTableAliasName = value; }

        /// <summary>
        /// 动态表名参数数组
        /// </summary>
        public string[] TableArgs { get => CurrentScope.TableArgs; set => CurrentScope.TableArgs = value; }
        /// <summary>
        /// 当前作用域深度，根作用域为0，每进入一个新的作用域深度加1
        /// </summary>
        public int Depth => CurrentScope.Depth;

        /// <summary>
        /// 获取作用域对应的缩进字符串，根作用域无缩进，每增加一级作用域增加两个空格，最多八个空格
        /// </summary>
        public int Indent => AutoIndent ? Depth switch
        {
            0 => 0,
            1 => 0,
            2 => 2,
            3 => 4,
            4 => 6,
            _ => 8
        } : 0;
        /// <summary>
        /// 获取或设置是否自动添加缩进，默认为 true，设置为 false 后生成的 SQL 将不包含任何缩进
        /// </summary>
        public bool AutoIndent { get; set; } = true;

        /// <summary>
        /// 获取当前表定义
        /// </summary>
        public SqlTable Table => CurrentScope.Table;

        /// <summary>
        /// 是否单表模式，单表模式下字段名前无需加表名
        /// </summary>
        public bool SingleTable { get => CurrentScope.SingleTable; set => CurrentScope.SingleTable = value; }

        /// <summary>
        /// 根据别名获取表定义
        /// </summary>
        /// <param name="aliasName">表别名，为空时使用默认别名</param>
        /// <returns>表定义</returns>
        public SqlTable GetTable(string aliasName = null)
        {
            return CurrentScope.GetTable(aliasName);
        }

        /// <summary>
        /// 格式化表名，如果设置了 TableArgs 则使用 string.Format 进行格式化，否则直接返回原始表名
        /// </summary>
        /// <param name="name">原始表名</param>
        /// <returns>格式化后的表名。</returns>
        public string FormatTableName(string name)
        {
            if (TableArgs?.Length > 0)
                return string.Format(name, TableArgs);
            else
                return name;
        }

        /// <summary>
        /// 添加表别名映射
        /// </summary>
        /// <param name="aliasName">表别名</param>
        /// <param name="table">表定义</param>
        /// <returns>是否添加成功</returns>
        public bool AddTableAlias(string aliasName, SqlTable table)
        {
            return CurrentScope.AddTableAlias(aliasName, table);
        }

        /// <summary>
        /// 压入新的作用域，并返回 IDisposable 对象用于 using 模式
        /// </summary>
        /// <returns>作用域释放器</returns>
        public IDisposable BeginScope()
        {
            CurrentScope = new SqlScopeContext() { Parent = CurrentScope, TableArgs = TableArgs, Depth = CurrentScope.Depth + 1 };
            return new SqlScope(this);
        }

        /// <summary>
        /// 作用域释放器，用于 using 语句自动释放作用域
        /// </summary>
        private class SqlScope : IDisposable
        {
            private readonly SqlBuildContext _context;

            public SqlScope(SqlBuildContext context)
            {
                _context = context;
            }

            /// <summary>
            /// 释放作用域，恢复到父级作用域。
            /// </summary>
            public void Dispose()
            {
                _context.CurrentScope = _context.CurrentScope?.Parent;
            }
        }

        /// <summary>
        /// 获取当前作用域上下文
        /// </summary>
        public SqlScopeContext CurrentScope { get; private set; }

        /// <summary>
        /// SQL作用域上下文，用于管理当前作用域内的表别名映射和参数
        /// </summary>
        public class SqlScopeContext
        {
            internal SqlScopeContext() { }

            private readonly Dictionary<string, SqlTable> _aliasTableMap = new Dictionary<string, SqlTable>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// 表别名
            /// </summary>
            public string DefaultTableAliasName { get; set; }

            /// <summary>
            /// 表信息
            /// </summary>
            public SqlTable Table { get => DefaultTableAliasName != null && _aliasTableMap.ContainsKey(DefaultTableAliasName) ? _aliasTableMap[DefaultTableAliasName] : null; }

            /// <summary>
            /// 单表模式，字段名前无需加表名
            /// </summary>
            public bool SingleTable { get; set; } = false;

            private string[] _tableArgs = Array.Empty<string>();

            /// <summary>
            /// 表名参数，用于动态生成表名
            /// </summary>
            public string[] TableArgs
            {
                get => _tableArgs;
                set
                {
                    if (value != null)
                    {
                        foreach (var item in value)
                        {
                            Expr.ThrowIfInvalidSqlName(nameof(TableArgs), item);
                        }
                    }
                    _tableArgs = value ?? Array.Empty<string>();
                }
            }

            /// <summary>
            /// 当前运算符优先级，用于表达式生成过程中处理括号等情况
            /// </summary>
            public int Depth { get; set; }

            /// <summary>
            /// 添加表别名映射
            /// </summary>
            /// <param name="aliasName">表别名</param>
            /// <param name="table">表定义</param>
            /// <returns>是否添加成功</returns>
            public bool AddTableAlias(string aliasName, SqlTable table)
            {
                if (string.IsNullOrEmpty(aliasName)) throw new ArgumentException("Alias name cannot be null or empty.");
                if (_aliasTableMap.ContainsKey(aliasName)) return false;
                if (_aliasTableMap.Count == 0 && String.IsNullOrEmpty(DefaultTableAliasName)) DefaultTableAliasName = aliasName;
                _aliasTableMap.Add(aliasName, table);
                return true;
            }

            /// <summary>
            /// 根据别名获取表定义
            /// </summary>
            /// <param name="aliasName">表别名，为空时使用默认别名</param>
            /// <returns>表定义</returns>
            public SqlTable GetTable(string aliasName = null)
            {
                if (string.IsNullOrEmpty(aliasName)) aliasName = DefaultTableAliasName;
                if (string.IsNullOrEmpty(aliasName)) return null;
                if (!_aliasTableMap.TryGetValue(aliasName, out SqlTable value)) return Parent?.GetTable(aliasName);
                return value;
            }

            /// <summary>
            /// 链式结构中的上级上下文节点
            /// </summary>
            public SqlScopeContext Parent { get; set; }
        }
    }
}
