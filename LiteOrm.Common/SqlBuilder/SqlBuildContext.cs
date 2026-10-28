using System;
using System.Collections.Generic;

namespace LiteOrm.Common
{
    /// <summary>
    /// SQL构建上下文，用于管理SQL生成过程中的表别名、作用域等信息
    /// </summary>
    public class SqlBuildContext
    {
        private readonly SqlTable? _table;
        private readonly string? _aliasName;
        private readonly string[]? _tableArgs;

        /// <summary>
        /// 使用指定的 SQL 构建器初始化 <see cref="SqlBuildContext"/> 类的新实例
        /// </summary>
        /// <param name="sqlBuilder">SQL 构建器，提供当前数据库方言的名称、参数与函数处理，不能为空</param>
        public SqlBuildContext(ISqlBuilder sqlBuilder)
        {
            SqlBuilder = sqlBuilder ?? throw new ArgumentNullException(nameof(sqlBuilder));
            Init();
        }

        /// <summary>
        /// 使用指定的 SQL 构建器、表、别名和表名参数初始化 <see cref="SqlBuildContext"/> 类的新实例
        /// </summary>
        /// <param name="sqlBuilder">SQL 构建器，提供当前数据库方言的名称、参数与函数处理，不能为空</param>
        /// <param name="table">SQL表定义</param>
        /// <param name="aliasName">表别名</param>
        /// <param name="tableArgs">动态表名参数</param>
        public SqlBuildContext(ISqlBuilder sqlBuilder, SqlTable? table, string aliasName, string[]? tableArgs)
        {
            SqlBuilder = sqlBuilder ?? throw new ArgumentNullException(nameof(sqlBuilder));
            _table = table;
            _aliasName = aliasName;
            _tableArgs = tableArgs;
            Init();
        }

        /// <summary>
        /// 重置上下文的渲染状态：丢弃渲染过程中累积的作用域、表别名映射、表别名序列与已收集的 CTE，
        /// 回到构造时的根作用域配置。<see cref="TableArgs"/>、<see cref="SingleTable"/> 等调用方配置保持不变，
        /// <see cref="OutputParams"/> 由调用方管理，不做清空。顶层调用 <see cref="ExprSqlConverter.ToSql(Expr, SqlBuildContext, bool)"/>（<c>isRoot</c> 为 <see langword="true"/>）时会自动调用该方法。
        /// </summary>
        public void Init()
        {
            string[]? tableArgs = CurrentScope?.TableArgs is { Length: > 0 } args ? args : _tableArgs;
            bool singleTable = CurrentScope?.SingleTable ?? false;

            CurrentScope = new SqlScopeContext
            {
                DefaultTableAliasName = _aliasName,
                TableArgs = tableArgs,
                SingleTable = singleTable
            };
            if (_table != null)
            {
                CurrentScope.AddTableAlias(_aliasName ?? Constants.DefaultTableAlias, _table);
            }
            Sequence = 1;
            _commonTableExprs.Clear();
        }

        private ICollection<Param>? _outputParams;

        /// <summary>
        /// SQL 构建器，提供当前数据库方言的名称、参数与函数处理。在构造时注入，保证非空。
        /// </summary>
        public ISqlBuilder SqlBuilder { get; }

        /// <summary>
        /// 构建 SQL 过程中产生的参数集合，渲染表达式时生成的参数会依次追加到该集合。
        /// 未显式设置时自动创建一个空集合。
        /// </summary>
        public ICollection<Param> OutputParams
        {
            get => _outputParams ??= new List<Param>();
            set => _outputParams = value;
        }

        private readonly List<CommonTableExpr> _commonTableExprs = new List<CommonTableExpr>();

        /// <summary>
        /// 渲染过程中收集到的公共表表达式（CTE）节点，按出现顺序排列，包含完整定义与仅别名引用的节点。
        /// 主查询渲染完成后，由 <see cref="ExprSqlConverter"/> 据此拼出 WITH 子句。
        /// </summary>
        public IReadOnlyList<CommonTableExpr> CommonTableExprs => _commonTableExprs;

        /// <summary>
        /// 收集一个 CTE 节点：先后序遍历定义主体，一次性收集其中引用的 CTE（依赖在先），再收集自身。
        /// </summary>
        /// <param name="cte">要收集的 CTE 节点</param>
        internal void AddCommonTableExpr(CommonTableExpr cte)
        {
            // 后序遍历定义主体，一次性收集其中引用的 CTE，避免逐节点递归重复遍历
            if (cte.Source != null)
            {
                ExprVisitor.Visit(node =>
                {
                    if (node is CommonTableExpr nested) AddCteNode(nested);
                }, cte.Source, ExprVisitOrder.PostOrder);
            }

            AddCteNode(cte);
        }

        /// <summary>
        /// 将单个 CTE 节点合并到收集列表。同别名只保留一份：完整定义相等时去重，不同定义时抛出异常。
        /// 仅别名引用的节点在遇到完整定义时被替换，保证定义能够写入 WITH 子句；
        /// 替换后的定义移动到列表末尾，使其依赖的定义（先被收集）排在它前面。
        /// </summary>
        /// <param name="cte">要合并的 CTE 节点</param>
        private void AddCteNode(CommonTableExpr cte)
        {
            if (string.IsNullOrEmpty(cte.Alias)) return;

            for (int i = 0; i < _commonTableExprs.Count; i++)
            {
                CommonTableExpr existing = _commonTableExprs[i];
                if (!string.Equals(existing.Alias, cte.Alias, StringComparison.OrdinalIgnoreCase)) continue;

                // 仅别名引用不携带定义，遇到已有定义时忽略
                if (cte.Source == null) return;

                if (existing.Source == null)
                {
                    // 之前只收集到别名引用（如递归自引用），补上完整定义并排序到末尾
                    _commonTableExprs.RemoveAt(i);
                    _commonTableExprs.Add(cte);
                    return;
                }

                if (!Equals(existing.Source, cte.Source))
                {
                    throw new InvalidOperationException($"CTE '{cte.Alias}' has multiple different definitions.");
                }
                return;
            }

            _commonTableExprs.Add(cte);
        }

        /// <summary>
        /// 序列，用来生成表别名
        /// </summary>
        public int Sequence { get; set; } = 1;        

        /// <summary>
        /// 默认表别名名称
        /// </summary>
        public string? DefaultTableAliasName { get => CurrentScope!.DefaultTableAliasName; set => CurrentScope!.DefaultTableAliasName = value; }

        /// <summary>
        /// 动态表名参数数组
        /// </summary>
        public string[]? TableArgs { get => CurrentScope!.TableArgs; set => CurrentScope!.TableArgs = value; }
        /// <summary>
        /// 当前作用域深度，根作用域为0，每进入一个新的作用域深度加1
        /// </summary>
        public int Depth => CurrentScope!.Depth;

        /// <summary>
        /// 获取作用域对应的缩进字符串，根作用域无缩进，每增加一级作用域增加两个空格，最多八个空格
        /// </summary>
        public int Indent => AutoIndent ? Depth switch
        {
            0 => 0,
            1 => 2,
            2 => 4,
            3 => 6,
            _ => 8
        } : 0;
        /// <summary>
        /// 获取或设置是否自动添加缩进，默认为 true，设置为 false 后生成的 SQL 将不包含任何缩进
        /// </summary>
        public bool AutoIndent { get; set; } = true;

        /// <summary>
        /// 获取当前表定义
        /// </summary>
        public SqlTable? Table => CurrentScope!.Table;

        /// <summary>
        /// 是否单表模式，单表模式下字段名前无需加表名
        /// </summary>
        public bool SingleTable { get => CurrentScope!.SingleTable; set => CurrentScope!.SingleTable = value; }

        /// <summary>
        /// 根据别名获取表定义
        /// </summary>
        /// <param name="aliasName">表别名，为空时使用默认别名</param>
        /// <returns>表定义</returns>
        public SqlTable? GetTable(string? aliasName = null)
        {
            return CurrentScope!.GetTable(aliasName);
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
        public bool AddTableAlias(string aliasName, SqlTable? table)
        {
            return CurrentScope!.AddTableAlias(aliasName, table);
        }

        /// <summary>
        /// 压入新的作用域，并返回 IDisposable 对象用于 using 模式
        /// </summary>
        /// <returns>作用域释放器</returns>
        public IDisposable BeginScope()
        {
            CurrentScope = new SqlScopeContext() { Parent = CurrentScope, TableArgs = TableArgs, Depth = CurrentScope!.Depth + 1 };
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
        public SqlScopeContext? CurrentScope { get; private set; }

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
            public string? DefaultTableAliasName { get; set; }

            /// <summary>
            /// 表信息
            /// </summary>
            public SqlTable? Table { get => DefaultTableAliasName != null && _aliasTableMap.ContainsKey(DefaultTableAliasName) ? _aliasTableMap[DefaultTableAliasName] : null; }

            /// <summary>
            /// 单表模式，字段名前无需加表名
            /// </summary>
            public bool SingleTable { get; set; } = false;

            private string[] _tableArgs = Array.Empty<string>();

            /// <summary>
            /// 表名参数，用于动态生成表名
            /// </summary>
            public string[]? TableArgs
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
            public bool AddTableAlias(string aliasName, SqlTable? table)
            {
                if (_aliasTableMap.ContainsKey(aliasName)) return false;
                if (String.IsNullOrEmpty(DefaultTableAliasName)) DefaultTableAliasName = aliasName;
                _aliasTableMap.Add(aliasName, table!);
                return true;
            }

            /// <summary>
            /// 根据别名获取表定义
            /// </summary>
            /// <param name="aliasName">表别名，为空时使用默认别名</param>
            /// <returns>表定义</returns>
            public SqlTable? GetTable(string? aliasName = null)
            {
                if (string.IsNullOrEmpty(aliasName)) aliasName = DefaultTableAliasName;
                if (string.IsNullOrEmpty(aliasName)) return null;
                if (!_aliasTableMap.TryGetValue(aliasName!, out SqlTable? value)) return Parent?.GetTable(aliasName);
                return value;
            }

            /// <summary>
            /// 链式结构中的上级上下文节点
            /// </summary>
            public SqlScopeContext? Parent { get; set; }
        }
    }
}
