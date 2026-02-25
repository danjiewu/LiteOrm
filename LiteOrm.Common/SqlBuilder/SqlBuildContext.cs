using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// SQL构建上下文，用于管理SQL生成过程中的表别名、作用域等信息
    /// </summary>
    public class SqlBuildContext
    {
        /// <summary>
        /// 序列，用来生成表别名
        /// </summary>
        public int Sequence { get; set; }

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
                CurrentScope.AddTableAlias(aliasName ?? "T0", table);
            }
        }

        /// <summary>
        /// 默认表别名名称
        /// </summary>
        public string DefaultTableAliasName { get => CurrentScope.DefaultTableAliasName; set => CurrentScope.DefaultTableAliasName = value; }

        /// <summary>
        /// 动态表名参数数组
        /// </summary>
        public string[] TableArgs { get => CurrentScope.TableArgs; set => CurrentScope.TableArgs = value; }
        
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
            CurrentScope = new SqlScopeContext() { Parent = CurrentScope, TableArgs = TableArgs };
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

            public void Dispose()
            {
                _context.CurrentScope = _context.CurrentScope.Parent;
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

            private Dictionary<string, SqlTable> _aliasTableMap = new Dictionary<string, SqlTable>(StringComparer.OrdinalIgnoreCase);
            
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
                            if (!Const.ValidNameRegex.IsMatch(item)) throw new ArgumentException($"Invalid table argument name: {item}");
                        }
                    }
                    _tableArgs = value ?? Array.Empty<string>();
                }
            }

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
                if (string.IsNullOrEmpty(aliasName)) throw new ArgumentException("No default table alias defined.");
                if (!_aliasTableMap.ContainsKey(aliasName)) return Parent?.GetTable(aliasName);
                return _aliasTableMap[aliasName];
            }

            /// <summary>
            /// 链式结构中的上级上下文节点
            /// </summary>
            public SqlScopeContext Parent { get; set; }
        }
    }
}
