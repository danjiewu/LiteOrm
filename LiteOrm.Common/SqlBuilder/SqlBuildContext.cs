using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LiteOrm.Common
{
    public class SqlBuildContext
    {
        /// <summary>
        /// 序列，用来生成表别名
        /// </summary>
        public int Sequence { get; set; }

        public SqlBuildContext()
        {
            CurrentScope = new SqlScopeContext();
        }

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

        public string DefaultTableAliasName { get => CurrentScope.DefaultTableAliasName; set => CurrentScope.DefaultTableAliasName = value; }

        public string[] TableArgs { get => CurrentScope.TableArgs; set => CurrentScope.TableArgs = value; }
        public SqlTable Table => CurrentScope.Table;

        public bool SingleTable { get => CurrentScope.SingleTable; set => CurrentScope.SingleTable = value; }

        public SqlTable GetTable(string aliasName = null)
        {
            return CurrentScope.GetTable(aliasName);
        }

        public bool AddTableAlias(string aliasName, SqlTable table)
        {
            return CurrentScope.AddTableAlias(aliasName, table);
        }

        public SqlScopeContext PushScope()
        {
            return CurrentScope = new SqlScopeContext() { Parent = CurrentScope, TableArgs = TableArgs };
        }

        public SqlScopeContext PopScope()
        {
            if (CurrentScope.Parent == null) throw new InvalidOperationException("Cannot exit root scope.");
            return CurrentScope = CurrentScope.Parent;
        }

        public SqlScopeContext CurrentScope { get; private set; }

        /// <summary>
        /// SqlBuilder生成sql时的上下文
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

            public bool AddTableAlias(string aliasName, SqlTable table)
            {
                if (string.IsNullOrEmpty(aliasName)) throw new ArgumentException("Alias name cannot be null or empty.");
                if (_aliasTableMap.ContainsKey(aliasName)) return false;
                if (_aliasTableMap.Count == 0 && String.IsNullOrEmpty(DefaultTableAliasName)) DefaultTableAliasName = aliasName;
                _aliasTableMap.Add(aliasName, table);
                return true;
            }

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

