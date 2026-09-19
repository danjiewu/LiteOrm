using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表达式 SQL 转换器。
    /// </summary>
    public static class ExprSqlConverter
    {
        private static readonly Dictionary<LogicOperator, string> _logicOperatorSymbols = new()
        {
            { LogicOperator.Equal,"=" },
            { LogicOperator.GreaterThan,">" },
            { LogicOperator.LessThan,"<" },
            { LogicOperator.Like,"LIKE" },
            { LogicOperator.StartsWith,"LIKE" },
            { LogicOperator.EndsWith,"LIKE" },
            { LogicOperator.Contains,"LIKE" },
            { LogicOperator.RegexpLike,"REGEXP_LIKE" },
            { LogicOperator.In,"IN" },
            { LogicOperator.NotEqual,"<>" },
            { LogicOperator.GreaterThanOrEqual,">=" },
            { LogicOperator.LessThanOrEqual,"<=" },
            { LogicOperator.NotIn,"NOT IN" },
            { LogicOperator.NotContains,"NOT LIKE" },
            { LogicOperator.NotLike,"NOT LIKE" },
            { LogicOperator.NotStartsWith,"NOT LIKE" },
            { LogicOperator.NotEndsWith,"NOT LIKE" },
            { LogicOperator.NotRegexpLike,"NOT REGEXP_LIKE" }
        };

        private static readonly Dictionary<ValueOperator, string> _valueOperatorSymbols = new()
        {
            { ValueOperator.Add,"+"  },
            { ValueOperator.Subtract,"-" },
            { ValueOperator.Multiply,"*" },
            { ValueOperator.Divide,"/" },
            { ValueOperator.Modulo,"%" },
            { ValueOperator.Concat,"||" }
        };

        // 数值越大表示绑定越紧。
        /// <summary>
        /// 根优先级，表示最外层的表达式优先级。
        /// </summary>
        private const int RootPriority = 0;
        /// <summary>
        /// 表示集合查询，如 SELECT ... UNION SELECT ... 的优先级，通常最低，因为它们需要整个子查询作为一个整体。
        /// </summary>
        private const int SelectSetPriority = 1;
        /// <summary>
        /// 表示普通子查询（如 SELECT ...）的优先级，略高于集合查询，因为它们通常作为一个单独的表达式出现在其他操作中。
        /// </summary>
        private const int SelectPriority = 2;
        /// <summary>
        /// 表示值列表（如 IN (...)）的优先级，通常较低，因为它们需要整个列表作为一个整体进行处理。
        /// </summary>
        private const int ValueListPriority = 4;
        /// <summary>
        /// 表示逻辑 OR 操作的优先级，较低，因为 OR 通常需要整个表达式作为一个整体进行评估。
        /// </summary>
        private const int OrPriority = 11;
        /// <summary>
        /// 表示逻辑 AND 操作的优先级，略高于 OR。
        /// </summary>
        private const int AndPriority = 12;
        /// <summary>
        /// 表示比较操作（如 =、&lt;&gt;、&gt;、&lt;）的优先级，通常高于 AND 和 OR，因为它们需要先评估比较表达式的结果。
        /// </summary>
        private const int ComparisonPriority = 13;
        /// <summary>
        /// 表示字符串拼接操作的优先级，通常高于比较操作，因为它们需要先计算出字符串结果。
        /// </summary>
        private const int ConcatPriority = 14;
        /// <summary>
        /// 表示加减算术操作的优先级，通常高于比较操作，因为它们需要先计算数值结果。
        /// </summary>
        private const int AddSubtractPriority = 15;
        /// <summary>
        /// 表示乘除取模操作的优先级，高于加减计算。
        /// </summary>
        private const int MultiplyDivideModuloPriority = 16;
        /// <summary>
        /// 表示 NOT 操作的优先级，通常高于比较和算术操作。
        /// </summary>
        private const int NotPriority = 17;
        /// <summary>
        /// 表示一元操作（如取负、位取反）的优先级，通常高于 NOT。
        /// </summary>
        private const int UnaryPriority = 18;
        /// <summary>
        /// 最高优先级。
        /// </summary>
        private const int MaxPriority = 1000;

        /// <summary>
        /// 将当前表达式转换为 SQL 字符串片段。
        /// </summary>
        /// <param name="expr">表达式。</param>
        /// <param name="context">生成 SQL 的上下文环境，包含表信息、别名、SQL 构建器与输出参数集合。</param>
        /// <returns>表示该表达式的 SQL 字符串片段，通常带有参数占位符。</returns>
        public static string ToSql(this Expr expr, SqlBuildContext context)
        {
            if (expr is null) return string.Empty;

            // 预收集 CTE 定义，通过后序遍历表达式树获取所有 CommonTableExpr 节点
            var sb = ValueStringBuilder.Create(256);
            if (context.SqlBuilder.SupportCteExpr)
            {
                var cteList = CollectCteExprs(expr);

                if (cteList.Count > 0)
                {
                    sb.Append(context.SqlBuilder.ExplicitRecursive ? "WITH RECURSIVE " : "WITH ");
                    for (int i = 0; i < cteList.Count; i++)
                    {
                        if (i > 0) sb.Append(",");
                        sb.Append(context.SqlBuilder.ToSqlName(cteList[i].Alias!));
                        sb.Append(" AS ");
                        ToSqlInternal(ref sb, cteList[i].Source, context, MaxPriority);
                        sb.NewLine(0);
                        context.AddTableAlias(cteList[i].Alias, null);
                    }
                    context.DefaultTableAliasName = null;// CTE 定义中的别名不应影响主查询的默认表别名解析
                }
            }
            ToSqlInternal(ref sb, expr, context);
            string res = sb.ToString();
            sb.Dispose();
            return res;
        }

        private static List<CommonTableExpr> CollectCteExprs(Expr expr)
        {
            var cteList = new List<CommonTableExpr>();
            var cteMap = new Dictionary<string, CommonTableExpr>(StringComparer.Ordinal);
            // 记录引用了但尚未找到定义的别名（可能是递归 CTE 的自引用，定义在后序遍历中会稍晚到达）。
            var pendingRefs = new HashSet<string>(StringComparer.Ordinal);

            ExprVisitor.Visit(node =>
            {
                if (node is not CommonTableExpr cte || string.IsNullOrEmpty(cte.Alias))
                {
                    return true;
                }

                if (!cteMap.TryGetValue(cte.Alias!, out var existing))
                {
                    if (cte.Source != null)
                    {
                        // 定义节点：加入集合
                        cteMap.Add(cte.Alias!, cte);
                        cteList.Add(cte);
                        // 定义已找到，从待解析集合中移除
                        pendingRefs.Remove(cte.Alias!);
                    }
                    else
                    {
                        // 引用节点（Source == null）且别名未定义：
                        // 可能是递归 CTE 的自引用（定义节点在后序遍历中会稍晚到达），暂记录，不报错。
                        pendingRefs.Add(cte.Alias!);
                    }
                    return true;
                }

                // 别名已存在
                if (cte.Source == null)
                {
                    // 只有别名的引用节点会复用首个完整定义，不再重复写入 WITH。
                    return true;
                }

                // 定义节点：校验一致性
                if (!Equals(existing.Source, cte.Source))
                {
                    throw new InvalidOperationException($"CTE '{cte.Alias}' has multiple different definitions.");
                }

                return true;
            }, expr, ExprVisitOrder.PostOrder);

            // 遍历结束后，检查是否有引用了但未定义的 CTE（排除递归自引用已找到定义的情况）
            if (pendingRefs.Count > 0)
            {
                throw new InvalidOperationException($"CTE '{pendingRefs.First()}' is referenced but its definition is not available.");
            }

            return cteList;
        }

        /// <summary>
        /// 将 TableJoinExpr 转换为 SQL 片段（JOIN ... ON ...）。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, TableJoinExpr expr, SqlBuildContext context)
        {
            if (expr == null) return;
            if (expr.Source == null) return;


            string joinAlias = expr.Source.Alias ?? $"T{context.Sequence++}";
            TableDefinition? joinTable = null;
            if (expr.Source is TableExpr tbe) joinTable = TableInfoProvider.Instance.GetTableDefinition(tbe.Type!);
            context.AddTableAlias(joinAlias, joinTable);

            LogicExpr? onExpr = expr.On;
            sb.NewLine(context.Indent);
            sb.Append((expr.JoinType).ToString().ToUpper());
            sb.Append(" JOIN ");
            if (joinTable != null)
            {
                onExpr &= GetAliasedConstFilter(joinTable.ConstFilter, joinAlias);
                sb.Append(context.SqlBuilder.ToSqlName(context.FormatTableName(joinTable.Name!)));
                sb.Append(" ");
                sb.Append(context.SqlBuilder.ToSqlName(joinAlias));
            }
            else
            {
                using (context.BeginScope())
                {
                    ToSqlInternal(ref sb, expr.Source, context, MaxPriority);
                }
            }

            if (onExpr != null)
            {
                sb.Append(" ON ");
                int lenBefore = sb.Length;
                ToSqlInternal(ref sb, onExpr, context);
                if (sb.Length == lenBefore) sb.Length = lenBefore;
            }
        }

        /// <summary>
        /// 将 TableExpr 转换为 SQL 片段（表名 别名）。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, TableExpr expr, SqlBuildContext context)
        {
            if (expr.TableArgs != null && expr.TableArgs.Length > 0) context.TableArgs = expr.TableArgs;
            if (context.SingleTable)
            {
                var tableDef = TableInfoProvider.Instance.GetTableDefinition(expr.Type!);
                var tableName = context.SqlBuilder.ToSqlName(context.FormatTableName(tableDef!.Name!));
                sb.Append(tableName);
                context.AddTableAlias(tableName, tableDef);
            }
            else
            {
                var tableView = TableInfoProvider.Instance.GetTableView(expr.Type!);
                var tableName = context.SqlBuilder.ToSqlName(context.FormatTableName(tableView!.Definition.Name!));
                bool isMain = context.Depth == 0 && context.DefaultTableAliasName is null;
                string aliasName = expr.Alias ?? (isMain ? Constants.DefaultTableAlias : $"T{context.Sequence++}");
                sb.Append(tableName);
                sb.Append(" ");
                sb.Append(context.SqlBuilder.ToSqlName(aliasName));
                context.AddTableAlias(aliasName, tableView);
                foreach (var joined in tableView.JoinedTables)
                {
                    if (joined.Used)
                    {
                        ToSql(ref sb, joined, context);
                        context.AddTableAlias(joined.Name, joined.TableDefinition);
                    }
                }
            }
        }

        /// <summary>
        /// 将当前表达式转换为预编译的 SQL 语句。转换产生的参数集合会写入 <see cref="SqlBuildContext.OutputParams"/>。
        /// </summary>
        /// <param name="expr">表达式。</param>
        /// <param name="context">生成 SQL 的上下文环境，包含表信息、别名、SQL 构建器与输出参数集合。</param>
        /// <returns>包含 SQL 语句和参数列表的 <see cref="PreparedSql"/> 实例。</returns>
        public static PreparedSql ToPreparedSql(this Expr expr, SqlBuildContext context)
        {
            string sql = ToSql(expr, context);
            return new PreparedSql(sql, context.OutputParams);
        }

        /// <summary>
        /// 将当前表达式转换为 SQL 字符串片段，结果直接追加到提供的 <see cref="ValueStringBuilder"/> 中。
        /// </summary>
        /// <param name="expr">表达式。</param>
        /// <param name="sb">用于接收 SQL 片段的字符串构建器。</param>
        /// <param name="context">生成 SQL 的上下文环境，包含表信息、别名、SQL 构建器与输出参数集合。</param>
        public static void ToSql(this Expr expr, ref ValueStringBuilder sb, SqlBuildContext context)
        {
            ToSqlInternal(ref sb, expr, context);
        }

        private static void ToSqlInternal(ref ValueStringBuilder sb, Expr? expr, SqlBuildContext context, int priority = RootPriority)
        {
            if (expr is null) return;
            expr = expr.Reduce()!;

            int curPriority = GetPriority(expr!);
            bool needParen = curPriority < priority;
            if (needParen) sb.Append('(');

            switch (expr)
            {
                // 根据 Expr 的具体类型，分发到对应的 SQL 转换逻辑
                case LogicBinaryExpr lb: ToSql(ref sb, lb, context); break;
                case ValueBinaryExpr vb: ToSql(ref sb, vb, context); break;
                case NotExpr lu: ToSql(ref sb, lu, context); break;
                case UnaryExpr vu: ToSql(ref sb, vu, context); break;
                case ValueExpr value: ToSql(ref sb, value, context); break;
                case PropertyExpr prop: ToSql(ref sb, prop, context); break;
                case FunctionExpr func: ToSql(ref sb, func, context); break;
                case LambdaExpr lambda: ToSql(ref sb, lambda, context); break;
                case GenericSqlExpr generic: ToSql(ref sb, generic, context); break;
                case ForeignExpr foreign: ToSql(ref sb, foreign, context); break;
                case AndExpr ae: ToSql(ref sb, ae, context); break;
                case OrExpr oe: ToSql(ref sb, oe, context); break;
                case ValueSet vs: ToSql(ref sb, vs, context); break;
                case OrderByItemExpr obi: ToSql(ref sb, obi, context); break;
                case FromExpr from: ToSql(ref sb, from, context); break;
                case TableExpr table: ToSql(ref sb, table, context); break;
                case SelectExpr select:
                    if (priority > RootPriority)// 只有在当前表达式作为子表达式才启用作用域，以避免不必要的作用域嵌套
                        using (var scope = context.BeginScope())
                        {
                            ToSql(ref sb, select, context);
                        }
                    else
                        ToSql(ref sb, select, context);
                    break;
                case SelectItemExpr selectItem: ToSql(ref sb, selectItem, context); break;
                case DeleteExpr delete: ToSql(ref sb, delete, context); break;
                case UpdateExpr update: ToSql(ref sb, update, context); break;
                case CommonTableExpr cte: ToSql(ref sb, cte, context); break;
                default: throw new NotSupportedException($"Expression type {expr.GetType().FullName} is not supported.");
            }

            if (needParen) sb.Append(')');
        }

        /// <summary>
        /// 计算表达式的优先级（用于决定是否需要在生成 SQL 时添加括号）。
        /// 返回值越大表示优先级越高（更紧密结合），在需要时会根据与外层优先级比较决定是否加括号。
        /// 当前层级从低到高大致为：集合查询、普通子查询、值列表、OR、AND、比较、算术、拼接、NOT、一元运算、原子表达式。
        /// </summary>
        /// <param name="expr">要计算优先级的表达式。</param>
        /// <returns>表示表达式优先级的整数值。</returns>
        private static int GetPriority(Expr expr)
        {
            return expr switch
            {
                SelectExpr select => select.NextSelects?.Count > 0 ? SelectSetPriority : SelectPriority,
                ValueSet vs when vs.JoinType != ValueJoinType.Concat => ValueListPriority,
                ValueSet vs when vs.JoinType == ValueJoinType.Concat => ConcatPriority,
                OrExpr _ => OrPriority,
                AndExpr _ => AndPriority,
                LogicBinaryExpr _ => ComparisonPriority,
                ValueBinaryExpr vb => vb.Operator switch
                {
                    ValueOperator.Add or ValueOperator.Subtract => AddSubtractPriority,
                    ValueOperator.Concat => ConcatPriority,
                    _ => MultiplyDivideModuloPriority
                },
                NotExpr _ => NotPriority,
                UnaryExpr _ => UnaryPriority,
                _ => MaxPriority
            };
        }

        /// <summary>
        /// 将 SQL 片段通过递归方式拆解并填充到 SqlValueResult 结构中。
        /// </summary>
        /// <param name="sql">目标 SQL 结果结构。</param>
        /// <param name="sqlSegment">要处理的 SQL 片段。</param>
        /// <param name="context">SQL 构建上下文。</param>
        private static void AddSqlSegmentInternal(ref SqlValueStringBuilder sql, SqlSegment? sqlSegment, SqlBuildContext context)
        {
            if (sqlSegment is null) throw new ArgumentNullException(nameof(sqlSegment));

            switch (sqlSegment)
            {
                case SelectExpr select:
                    AddSqlSegment(ref sql, select, context);
                    break;
                case WhereExpr where:
                    AddSqlSegment(ref sql, where, context);
                    break;
                case GroupByExpr groupBy:
                    AddSqlSegment(ref sql, groupBy, context);
                    break;
                case HavingExpr having:
                    AddSqlSegment(ref sql, having, context);
                    break;
                case OrderByExpr orderBy:
                    AddSqlSegment(ref sql, orderBy, context);
                    break;
                case SectionExpr section:
                    AddSqlSegment(ref sql, section, context);
                    break;
                case FromExpr from:
                    AddSqlSegment(ref sql, from, context);
                    break;
                case CommonTableExpr commonTable:
                    AddSqlSegment(ref sql, commonTable, context);
                    break;
                case TableExpr table:
                    AddSqlSegment(ref sql, table, context);
                    break;
                default:
                    throw new NotSupportedException($"SQL segment type {sqlSegment.GetType().FullName} is not supported.");
            }
        }

        // SelectExpr handling is performed centrally in ToSqlInternal to ensure NextSelects
        // are rendered with the same outer priority. The specific ToSql overload for
        // SelectExpr has been removed.
        /// <summary>
        /// 将逻辑二元表达式转换为 SQL。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, LogicBinaryExpr expr, SqlBuildContext context)
        {
            string? op = String.Empty;
            bool isOppsite = expr.Operator.IsNot();
            char escapeChar = Constants.LikeEscapeChar;
            _logicOperatorSymbols.TryGetValue(expr.Operator, out op);
            int curPriority = GetPriority(expr!);
            switch (expr.OriginOperator)
            {
                case LogicOperator.In:
                    int begin = sb.Length;
                    ToSqlInternal(ref sb, expr.Left, context, curPriority);
                    sb.Append(" ");
                    sb.Append(op);
                    sb.Append(" ");
                    int valuesBegin = sb.Length;
                    ToSqlInternal(ref sb, expr.Right, context, curPriority);
                    if (valuesBegin == sb.Length)
                    {
                        // IN 后面没有内容，视为空集合
                        sb.Length = begin;
                        if (!isOppsite) sb.Append("0=1");
                    }
                    break;
                case LogicOperator.RegexpLike:
                    // 通过构造 FunctionExpr 委托给 SqlBuilder 中注册的 REGEXP_LIKE 函数处理器生成各方言 SQL
                    if (isOppsite) sb.Append("NOT ");
                    var regexpFunc = Expr.Func("REGEXP_LIKE", expr.Left!, expr.Right!);
                    ToSqlInternal(ref sb, regexpFunc, context);
                    break;
                case LogicOperator.Equal:
                    // 特殊处理 NULL 值的比较：在 SQL 中 a = NULL 始终为假，必须使用 IS NULL
                    if (expr.Right is null || expr.Right is ValueExpr vs && vs.Value is null)
                    {
                        ToSqlInternal(ref sb, expr.Left, context, curPriority);
                        sb.Append(isOppsite ? " IS NOT NULL" : " IS NULL");
                    }
                    else if (expr.Left is null || expr.Left is ValueExpr vsl && vsl.Value is null)
                    {
                        ToSqlInternal(ref sb, expr.Right, context, curPriority);
                        sb.Append(isOppsite ? " IS NOT NULL" : " IS NULL");
                    }
                    else
                    {
                        ToSqlInternal(ref sb, expr.Left, context, curPriority);
                        sb.Append(" ");
                        sb.Append(op);
                        sb.Append(" ");
                        ToSqlInternal(ref sb, expr.Right, context, curPriority);
                    }
                    break;
                case LogicOperator.Contains:
                case LogicOperator.StartsWith:
                case LogicOperator.EndsWith:
                    if (expr.Right is ValueExpr vs2 && vs2.Value is not Expr)
                    {
                        ToSqlInternal(ref sb, expr.Left, context, curPriority);
                        sb.Append(" ");
                        sb.Append(op);
                        sb.Append(" ");
                        string paramName = context.OutputParams.Count.ToString();
                        string rawValue = vs2.Value?.ToString() ?? string.Empty;
                        bool needEscape = vs2.Value is string && context.SqlBuilder.NeedLikeEscape(rawValue);
                        string val = needEscape ? context.SqlBuilder.ToSqlLikeValue(rawValue) : rawValue;
                        val = expr.OriginOperator switch
                        {
                            LogicOperator.StartsWith => $"{val}%",
                            LogicOperator.EndsWith => $"%{val}",
                            LogicOperator.Contains => $"%{val}%",
                            _ => val
                        };
                        context.OutputParams.Add(new Param(context.SqlBuilder.ToParamName(paramName), val));
                        sb.Append(context.SqlBuilder.ToSqlParam(paramName));
                        if (needEscape)
                        {
                            sb.Append($" ESCAPE '{escapeChar}'");
                        }
                    }
                    else
                    {
                        if (expr.OriginOperator == LogicOperator.Contains)
                        {
                            var compExpr = Expr.Func("SubString", expr.Left!, expr.Right!) >= 0;
                            if (isOppsite) compExpr = compExpr.Not();
                            ToSqlInternal(ref sb, compExpr, context, curPriority);
                        }
                        else if (expr.OriginOperator == LogicOperator.StartsWith)
                        {
                            var compExpr = Expr.Func("SubString", expr.Left!, expr.Right!) == 0;
                            if (isOppsite) compExpr = compExpr.Not();
                            ToSqlInternal(ref sb, compExpr, context, curPriority);
                        }
                        else//EndsWith 无法通过单次调用表达式转换实现，需要生成复杂的嵌套 REPLACE 来转义特殊字符再用 LIKE 匹配结尾
                        {
                            ToSqlInternal(ref sb, expr.Left, context, curPriority);
                            sb.Append(" ");
                            sb.Append(op);
                            sb.Append(" ");
                            var nestedRightSb = ValueStringBuilder.Create(64);
                            ToSqlInternal(ref nestedRightSb, expr.Right, context);
                            string nestedRight = nestedRightSb.ToString();
                            nestedRightSb.Dispose();

                            string right = $"REPLACE(REPLACE(REPLACE(REPLACE(REPLACE({nestedRight},'{escapeChar}', '{escapeChar}{escapeChar}'),'_', '{escapeChar}_'),'%', '{escapeChar}%'),'[', '{escapeChar}['),']', '{escapeChar}]')";
                            context.SqlBuilder.BuildConcatSql(ref sb, "'%'", right);
                            sb.Append($" ESCAPE '{escapeChar}'");
                        }
                    }
                    break;
                default:
                    ToSqlInternal(ref sb, expr.Left, context, curPriority);
                    sb.Append(" ");
                    sb.Append(op);
                    sb.Append(" ");
                    ToSqlInternal(ref sb, expr.Right, context, curPriority);
                    break;
            }
        }

        /// <summary>
        /// 将值二元表达式（如加减乘除）转换为 SQL。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, ValueBinaryExpr expr, SqlBuildContext context)
        {
            string? op = String.Empty;
            _valueOperatorSymbols.TryGetValue(expr.Operator, out op);
            int curPriority = GetPriority(expr!);
            if (expr.Operator == ValueOperator.Concat)
            {
                ToSqlInternal(ref sb, new ValueSet(ValueJoinType.Concat, expr.Left, expr.Right), context, curPriority);
            }
            else
            {
                // 对于非交换运算符（减、除、取模），右操作数相同优先级也需要括号，以保证左结合性
                bool isCommutative = expr.Operator is ValueOperator.Add or ValueOperator.Multiply;
                ToSqlInternal(ref sb, expr.Left, context, curPriority);
                sb.Append(" ");
                sb.Append(op);
                sb.Append(" ");
                ToSqlInternal(ref sb, expr.Right, context, isCommutative ? curPriority : curPriority + 1);
            }
        }

        /// <summary>
        /// 处理 NOT 表达式。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, NotExpr expr, SqlBuildContext context)
        {
            int curPriority = GetPriority(expr!);
            sb.Append("NOT ");
            ToSqlInternal(ref sb, expr.Operand, context, curPriority);
        }

        /// <summary>
        /// 处理一元表达式（如取负、位取反）。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, UnaryExpr expr, SqlBuildContext context)
        {
            switch (expr.Operator)
            {
                case UnaryOperator.Nagive:
                    sb.Append("-");
                    break;
                case UnaryOperator.BitwiseNot:
                    sb.Append("~");
                    break;
                case UnaryOperator.Distinct:
                    sb.Append("DISTINCT ");
                    break;
            }
            ToSqlInternal(ref sb, expr.Operand, context);
        }

        /// <summary>
        /// 将值表达式转换为 SQL，并支持参数化。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, ValueExpr expr, SqlBuildContext context)
        {
            object? value = expr.Value;
            if (expr.IsConst && value is Enum enumValue)
            {
                value = EnumUtil.EnumToUnderlying(enumValue);
            }
            if (value == null)
            {
                sb.Append("NULL");
            }
            else if (expr.IsConst && value is bool b)
            {
                sb.Append(b ? "1" : "0");
            }
            else if (expr.IsConst && value.GetType().IsPrimitive)
            {
                // 数值类型常量直接以字面量形式输出，较为高效
                sb.Append(value.ToString());
            }
            else if (expr.IsConst && value is string s)
            {
                // 字符串常量尝试直接输出为字面量，如果不支持则使用参数化
                if (!context.SqlBuilder.TryAppendSqlLiteral(ref sb, s))
                {
                    string paramName = context.OutputParams.Count.ToString();
                    context.OutputParams.Add(new Param(context.SqlBuilder.ToParamName(paramName), s));
                    sb.Append(context.SqlBuilder.ToSqlParam(paramName));
                }
            }
            else if (value is Expr innerExpr)
            {
                ToSqlInternal(ref sb, innerExpr, context);
            }
            else if (value is IEnumerable enumerable && !(value is string))
            {
                // 处理 IN (...) 集合
                bool first = true;
                foreach (var item in enumerable)
                {
                    if (first) sb.Append('(');
                    else sb.Append(',');
                    if (item is Expr e)
                    {
                        ToSqlInternal(ref sb, e, context);
                    }
                    else
                    {
                        // 对集合中的每个元素进行参数化
                        string paramName = context.OutputParams.Count.ToString();
                        context.OutputParams.Add(new Param(context.SqlBuilder.ToParamName(paramName), item));
                        sb.Append(context.SqlBuilder.ToSqlParam(paramName));
                    }
                    first = false;
                }
                if (!first) sb.Append(')');
            }
            else
            {
                // 其他类型（如字符串、日期）通过参数化处理以保证安全
                string paramName = context.OutputParams.Count.ToString();
                context.OutputParams.Add(new Param(context.SqlBuilder.ToParamName(paramName), value));
                sb.Append(context.SqlBuilder.ToSqlParam(paramName));
            }
        }

        /// <summary>
        /// 处理属性名称表达式，映射为数据库列名。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, PropertyExpr expr, SqlBuildContext context, string? aliasName = null)
        {
            var table = context.GetTable(expr.TableAlias);
            var column = table?.GetColumn(expr.PropertyName!);
            var columnName = column?.Name ?? expr.PropertyName;

            // 计算列（非实际列）：按表达式渲染，不输出物理列名
            if (column is ColumnDefinition columnDef && columnDef.IsComputed && columnDef.HasExpression)
            {
                columnDef.RenderComputedExpression(ref sb, context);
                if (aliasName != null)
                {
                    sb.Append(" AS ");
                    sb.Append(context.SqlBuilder.ToSqlName(aliasName));
                }
                return;
            }

            if (context.SingleTable)
            {
                // 单表模式下只需要输出列名
                sb.Append(context.SqlBuilder.ToSqlName(columnName!));
            }
            else if (column is ForeignColumn foreignColumn)
            {
                foreignColumn.TargetColumn!.ToSql(ref sb, context);
            }
            else
            {
                string? tableAlias = expr.TableAlias ?? context.DefaultTableAliasName;
                if (!String.IsNullOrEmpty(tableAlias))
                {
                    // 如果 PropertyExpr 中指定了 TableAlias，则使用该别名来限定列名
                    sb.Append(context.SqlBuilder.ToSqlName(tableAlias!));
                    sb.Append(".");
                }
                sb.Append(context.SqlBuilder.ToSqlName(columnName!));
            }
            if (aliasName != null && !String.Equals(columnName, aliasName, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(" AS ");
                sb.Append(context.SqlBuilder.ToSqlName(aliasName));
            }
        }
        /// <summary>
        /// 处理关联表过滤表达式（EXISTS 查询）。
        /// 完全通过 InnerExpr 控制关联条件。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, ForeignExpr foreignExpr, SqlBuildContext context)
        {
            if (foreignExpr.Foreign == null) throw new ArgumentException("ForeignExpr.Foreign is required");

            var foreignTable = TableInfoProvider.Instance.GetTableView(foreignExpr.Foreign);
            if (foreignTable == null) throw new ArgumentException($"Table info not found for type {foreignExpr.Foreign}");

            string? foreignAlias = string.IsNullOrEmpty(foreignExpr.Alias) ? $"T{context.Sequence++}" : foreignExpr.Alias;
            LogicExpr? joinedExpr = null;
            if (foreignExpr.AutoRelated && context.Table is not null)
            {
                var mainTable = TableInfoProvider.Instance.GetTableView(context.Table.DefinitionType);
                // 首先尝试正向查找当前表与目标表之间的关联关系
                foreach (JoinedTable joinedTable in mainTable!.JoinedTables)
                {
                    if (joinedTable.TableDefinition is null) continue;
                    if (joinedTable.TableDefinition.DefinitionType.IsAssignableFrom(foreignExpr.Foreign))
                    {
                        // 找到当前表与目标表之间的关联关系，自动生成关联条件
                        joinedExpr |= new AndExpr(joinedTable.ForeignPrimeKeys.Zip(joinedTable.ForeignKeys, (pk, fk) =>
                            Expr.Prop(pk.Name!) == Expr.Prop(fk.Table?.Name ?? context.DefaultTableAliasName, fk.Name!)
                        ));
                    }
                }
                // 正向没有找到关联关系，尝试反向查找
                if (joinedExpr is null)
                {
                    foreach (JoinedTable joinedTable in foreignTable.JoinedTables)
                    {
                        if (joinedTable.TableDefinition is null) continue;
                        if (joinedTable.TableDefinition.DefinitionType.IsAssignableFrom(context.Table.DefinitionType))
                        {
                            // 找到当前表与目标表之间的关联关系，自动生成关联条件
                            joinedExpr |= new AndExpr(joinedTable.ForeignPrimeKeys.Zip(joinedTable.ForeignKeys, (pk, fk) =>
                                Expr.Prop(fk.Table?.Name, fk.Name!) == Expr.Prop(context.DefaultTableAliasName, pk.Name!)
                            ));
                        }
                    }
                }
            }

            using (context.BeginScope())
            {
                context.AddTableAlias(foreignAlias, foreignTable);
                context.TableArgs = foreignExpr.TableArgs;

                sb.Append("EXISTS(SELECT 1 FROM ");
                sb.Append(context.SqlBuilder.ToSqlName(context.FormatTableName(foreignTable.Definition.Name!)));
                sb.Append(" ");
                sb.Append(context.SqlBuilder.ToSqlName(foreignAlias!));

                LogicExpr whereExpr = foreignTable.Definition.ConstFilter & joinedExpr & foreignExpr.InnerExpr;
                sb.NewLine(context.Indent);
                sb.Append("WHERE ");
                int lenBefore = sb.Length;
                ToSqlInternal(ref sb, whereExpr, context);
                if (sb.Length == lenBefore) sb.Length = lenBefore - 7;

                sb.Append(")");
            }
        }

        /// <summary>
        /// 处理数据库函数表达式。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, FunctionExpr expr, SqlBuildContext context)
        {
            context.SqlBuilder.BuildFunctionSql(ref sb, expr, context);
        }


        /// <summary>
        /// 处理 Lambda 封装表达式。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, LambdaExpr expr, SqlBuildContext context)
        {
            ToSqlInternal(ref sb, expr.InnerExpr, context);
        }

        /// <summary>
        /// 处理动态生成的 SQL 片段。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, GenericSqlExpr expr, SqlBuildContext context)
        {
            sb.Append(expr.GenerateSql(context));
        }

        /// <summary>
        /// 处理 AND 表达式组合。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, AndExpr expr, SqlBuildContext context)
        {
            int count = expr.Count;
            if (count == 0) return;
            int curPriority = GetPriority(expr!);

            bool first = true;
            for (int i = 0; i < count; i++)
            {
                int lenBefore = sb.Length;
                sb.NewLine(context.Indent, true);
                if (!first) sb.Append(" AND ");
                int lenWithJoin = sb.Length;

                ToSqlInternal(ref sb, expr[i], context, curPriority);

                if (sb.Length == lenWithJoin)
                {
                    sb.Length = lenBefore;
                }
                else
                {
                    first = false;
                }
            }
        }

        /// <summary>
        /// 处理 OR 表达式组合。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, OrExpr expr, SqlBuildContext context)
        {
            int count = expr.Count;
            if (count == 0) return;
            int curPriority = GetPriority(expr!);
            bool first = true;
            for (int i = 0; i < count; i++)
            {
                int lenBefore = sb.Length;
                sb.NewLine(context.Indent, true);
                if (!first) sb.Append(" OR ");
                int lenWithJoin = sb.Length;

                ToSqlInternal(ref sb, expr[i], context, curPriority);

                if (sb.Length == lenWithJoin)
                {
                    sb.Length = lenBefore;
                }
                else
                {
                    first = false;
                }
            }
        }

        /// <summary>
        /// 处理值集合。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, ValueSet expr, SqlBuildContext context)
        {
            int count = expr.Count;
            if (count == 0) return;

            if (expr.JoinType == ValueJoinType.Concat)
            {
                List<string> subExprs = new List<string>();
                var subSb = ValueStringBuilder.Create(64);
                void Flush(ref ValueStringBuilder sb)
                {
                    if (sb.Length > 0)
                    {
                        subExprs.Add(sb.ToString());
                        sb.Length = 0;
                    }
                }
                for (int i = 0; i < count; i++)
                {
                    if (expr[i] is ValueExpr { IsConst: true } valueExpr)
                    {
                        if (valueExpr.Value is string str)
                        {
                            if (context.SqlBuilder.TryAppendSqlLiteral(ref subSb, str))
                                continue;
                        }
                        else
                        {
                            subSb.Append(valueExpr.Value?.ToString());
                            continue;
                        }
                    }
                    Flush(ref subSb);
                    ToSqlInternal(ref subSb, expr[i], context);
                    Flush(ref subSb);
                }
                Flush(ref subSb);
                subSb.Dispose();
                context.SqlBuilder.BuildConcatSql(ref sb, subExprs.ToArray());
                return;
            }

            string joinStr = expr.JoinType switch
            {
                ValueJoinType.List => ",",
                ValueJoinType.Blank => " ",
                _ => ","
            };
            bool first = true;
            for (int i = 0; i < count; i++)
            {
                if (!first) sb.Append(joinStr);
                ToSqlInternal(ref sb, expr[i], context);
                first = false;
            }
        }

        /// <summary>
        /// 处理排序项，渲染为 "field" 或 "field DESC"。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, OrderByItemExpr expr, SqlBuildContext context)
        {
            ToSqlInternal(ref sb, expr.Field, context);
            if (!expr.Ascending) sb.Append(" DESC");
        }

        /// <summary>
        /// 向 SQL 结果结构中添加 Select 相关的子查询片段。
        /// </summary>
        private static void AddSqlSegment(ref SqlValueStringBuilder sql, SelectExpr expr, SqlBuildContext context)
        {
            ToSqlInternal(ref sql.From, expr, context, MaxPriority);
            string aliasName = expr.Alias ?? $"T{context.Sequence++}";
            sql.From.Append($" {aliasName}");
            context.AddTableAlias(aliasName, null);
        }

        /// <summary>
        /// 向 SQL 结果结构中添加 Where 过滤片段。
        /// </summary>
        private static void AddSqlSegment(ref SqlValueStringBuilder sql, WhereExpr expr, SqlBuildContext context)
        {
            AddSqlSegmentInternal(ref sql, expr.Source, context);
            LogicExpr whereExpr = GetContextConstFilter(context).And(expr.Where!);
            if (whereExpr != null)
            {
                if (sql.Where.Length > 0) sql.Where.Append(" AND ");
                ToSqlInternal(ref sql.Where, whereExpr, context);
            }
        }

        private static void AddSqlSegment(ref SqlValueStringBuilder sql, FromExpr expr, SqlBuildContext context)
        {
            ToSqlInternal(ref sql.From, expr, context);
        }

        private static void AddSqlSegment(ref SqlValueStringBuilder sql, CommonTableExpr expr, SqlBuildContext context)
        {
            ToSqlInternal(ref sql.From, expr, context);
        }

        private static void AddSqlSegment(ref SqlValueStringBuilder sql, TableExpr expr, SqlBuildContext context)
        {
            ToSqlInternal(ref sql.From, expr, context);
        }

        /// <summary>
        /// 向 SQL 结果结构中添加 Group By 分组片段。
        /// </summary>
        private static void AddSqlSegment(ref SqlValueStringBuilder sql, GroupByExpr expr, SqlBuildContext context)
        {
            AddSqlSegmentInternal(ref sql, expr.Source, context);
            if (expr.GroupBys != null && expr.GroupBys.Count > 0)
            {
                for (int i = 0; i < expr.GroupBys.Count; i++)
                {
                    if (sql.GroupBy.Length > 0) sql.GroupBy.Append(", ");
                    ToSqlInternal(ref sql.GroupBy, expr.GroupBys[i], context);
                }
            }
        }

        /// <summary>
        /// 向 SQL 结果结构中添加 Order By 排序片段。
        /// </summary>
        private static void AddSqlSegment(ref SqlValueStringBuilder sql, OrderByExpr expr, SqlBuildContext context)
        {
            AddSqlSegmentInternal(ref sql, expr.Source, context);
            if (expr.OrderBys != null && expr.OrderBys.Count > 0)
            {
                for (int i = 0; i < expr.OrderBys.Count; i++)
                {
                    if (sql.OrderBy.Length > 0) sql.OrderBy.Append(", ");
                    ToSqlInternal(ref sql.OrderBy, expr.OrderBys[i].Field, context);
                    if (!expr.OrderBys[i].Ascending) sql.OrderBy.Append(" DESC");
                }
            }
        }

        /// <summary>
        /// 向 SQL 结果结构中添加分页相关参数。
        /// </summary>
        private static void AddSqlSegment(ref SqlValueStringBuilder sql, SectionExpr expr, SqlBuildContext context)
        {
            AddSqlSegmentInternal(ref sql, expr.Source, context);
            sql.Skip = expr.Skip;
            sql.Take = expr.Take;
        }

        /// <summary>
        /// 向 SQL 结果结构中添加 Having 过滤片段。
        /// </summary>
        private static void AddSqlSegment(ref SqlValueStringBuilder sql, HavingExpr expr, SqlBuildContext context)
        {
            AddSqlSegmentInternal(ref sql, expr.Source, context);
            if (expr.Having != null)
            {
                ToSqlInternal(ref sql.Having, expr.Having, context);
            }
        }

        /// <summary>
        /// 处理 From 片段，根据 SingleTable 判断生成单表还是视图的 SQL。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, FromExpr expr, SqlBuildContext context)
        {
            if (expr.Source == null) return;
            ToSqlInternal(ref sb, expr.Source, context);
            if (!context.SingleTable)
            {
                if (expr.Joins != null && expr.Joins.Count > 0)
                {
                    foreach (var j in expr.Joins)
                    {
                        ToSql(ref sb, j, context);
                    }
                }
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, JoinedTable joined, SqlBuildContext context)
        {
            if (joined == null) return;

            sb.NewLine(context.Indent);
            sb.Append(joined.JoinType.ToString().ToUpper());
            sb.Append(" JOIN ");
            sb.Append(context.SqlBuilder.ToSqlName(context.FormatTableName(joined.TableDefinition.Name!)));
            sb.Append(" ");
            sb.Append(context.SqlBuilder.ToSqlName(joined.Name!));
            sb.Append(" ON ");

            context.AddTableAlias(joined.Name, joined.TableDefinition);

            bool isFirst = true;
            int count = joined.ForeignKeys.Count;
            for (int i = 0; i < count; i++)
            {
                if (!isFirst) sb.Append(" AND ");
                joined.ForeignKeys[i].ToSql(ref sb, context);
                sb.Append(" = ");
                joined.ForeignPrimeKeys[i].ToSql(ref sb, context);
                isFirst = false;
            }

            if (joined.ConstFilter != null)
            {
                if (!isFirst) sb.Append(" AND ");
                ToSqlInternal(ref sb, joined.ConstFilter, context, AndPriority);
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, SelectExpr select, SqlBuildContext context)
        {
            SqlValueStringBuilder sql = new SqlValueStringBuilder();
            AddSqlSegmentInternal(ref sql, select.Source, context);

            if (select.Selects == null || select.Selects.Count == 0)
            {
                sql.Select.Append("*");
            }
            else
            {
                for (int i = 0; i < select.Selects.Count; i++)
                {
                    if (i > 0) sql.Select.Append(", ");
                    sql.Select.NewLine(context.Indent, true);
                    ToSqlInternal(ref sql.Select, select.Selects[i], context);
                }
            }
            // 如果 SQL 片段链中缺少 Where 片段，则未触发常量过滤条件的插入，需补常量过滤条件到 Where 片段，以确保常量过滤条件始终生效
            if (sql.Where.Length == 0) ToSqlInternal(ref sql.Where, GetContextConstFilter(context), context);
            context.SqlBuilder.BuildSelectSql(ref sql, ref sb, context.Indent);
            sql.Dispose();
            foreach (var next in select.NextSelects)
            {
                sb.NewLine(context.Indent);
                sb.Append(context.SqlBuilder.ToSelectSetTypeSql(next.SetType));
                sb.Append(" ");
                ToSqlInternal(ref sb, next, context, SelectSetPriority);// 集合查询分支按 SelectSet 优先级渲染，嵌套集合查询时会自动补括号
            }
        }

        /// <summary>
        /// 处理查询列项（带有 Alias）。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, SelectItemExpr expr, SqlBuildContext context)
        {
            if (expr is null) return;
            if (expr.Value is PropertyExpr propertyExpr)
            {
                ToSql(ref sb, propertyExpr, context, expr.Alias ?? propertyExpr.PropertyName);
            }
            else
            {
                ToSqlInternal(ref sb, expr.Value, context);
                if (!string.IsNullOrEmpty(expr.Alias))
                {
                    sb.Append(" AS ");
                    sb.Append(context.SqlBuilder.ToSqlName(expr.Alias!));
                }
            }
        }

        /// <summary>
        /// 生成 DELETE 语句对应的 SQL。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, DeleteExpr expr, SqlBuildContext context)
        {
            using (context.BeginScope())
            {
                context.SingleTable = true;// Delete 语句强制单表，禁止生成多表关联的 Delete 语句
                sb.Append("DELETE FROM ");
                ToSql(ref sb, expr.Table ?? new TableExpr(context.Table!.DefinitionType), context);
                LogicExpr deleteWhere = expr.Where.And(GetContextConstFilter(context)!);
                if (deleteWhere != null)
                {
                    sb.NewLine(context.Indent);
                    sb.Append("WHERE ");
                    ToSqlInternal(ref sb, deleteWhere, context);
                }
            }
        }

        /// <summary>
        /// 处理公共表表达式（CTE）。若 SqlBuilder 支持 CTE 则仅输出别名（CTE 定义已在顶层 WITH 子句中预生成）；
        /// 否则按内联子查询方式展开。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, CommonTableExpr expr, SqlBuildContext context)
        {
            if (context.SqlBuilder.SupportCteExpr)
            {
                if (string.IsNullOrEmpty(expr.Alias))
                {
                    throw new InvalidOperationException("CTE alias cannot be null or empty when rendering a CTE reference.");
                }
                sb.Append(context.SqlBuilder.ToSqlName(expr.Alias!));
            }
            else
            {
                if (expr.Source == null)
                {
                    throw new InvalidOperationException($"CTE '{expr.Alias}' cannot be inlined because its definition is unavailable.");
                }

                sb.Append("(");
                using (context.BeginScope())
                {
                    ToSqlInternal(ref sb, expr.Source, context);
                }
                sb.Append(") ");
                sb.Append(context.SqlBuilder.ToSqlName(expr.Alias!));
            }
        }

        /// <summary>
        /// 生成 UPDATE 语句对应的 SQL。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, UpdateExpr expr, SqlBuildContext context)
        {
            TableExpr tableExpr = expr.Table ?? new TableExpr(context.Table!.DefinitionType);
            if (tableExpr == null)
                throw new ArgumentException("UpdateExpr Source is null and context Table is null, cannot determine update target.");
            var table = TableInfoProvider.Instance.GetTableDefinition(tableExpr.Type!);
            using (context.BeginScope())
            {
                context.SingleTable = true;// Update 语句强制单表，禁止生成多表关联的 Update 语句
                sb.Append("UPDATE ");
                ToSql(ref sb, tableExpr, context);
                sb.NewLine(context.Indent);
                sb.Append("SET ");
                for (int i = 0; i < expr.Sets.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.NewLine(context.Indent, true);
                    var set = expr.Sets[i];
                    if (set.Property is null) throw new Exception($"SetItem.Property is null at index {i} in UpdateExpr.Sets.");
                    SqlColumn? column = table?.GetColumn(set.Property.PropertyName!);
                    if (column == null) throw new Exception($"Property \"{set.Property}\" does not exist in type \"{context.Table!.DefinitionType.FullName}\".");
                    sb.Append(context.SqlBuilder.ToSqlName(column.Name!));

                    sb.Append(" = ");
                    ToSqlInternal(ref sb, set.Value, context, AddSubtractPriority);// 赋值右侧至少按算术表达式优先级渲染，比较等更低优先级表达式会自动补括号
                }
                LogicExpr updateWhere = expr.Where.And(GetContextConstFilter(context)!);
                if (updateWhere != null)
                {
                    sb.NewLine(context.Indent);
                    sb.Append("WHERE ");
                    ToSqlInternal(ref sb, updateWhere, context);
                }
            }
        }


        private static LogicExpr? GetContextConstFilter(SqlBuildContext context)
        {
            return context.Table?.Definition?.ConstFilter;
        }

        private static LogicExpr? GetAliasedConstFilter(LogicExpr? constFilter, string tableAlias)
        {
            if (constFilter == null) return null;
            if (String.IsNullOrEmpty(tableAlias)) return constFilter;

            LogicExpr aliasedFilter = (LogicExpr)constFilter.Clone();
            ExprVisitor.Visit(node =>
            {
                if (node is PropertyExpr propertyExpr && String.IsNullOrEmpty(propertyExpr.TableAlias))
                {
                    propertyExpr.TableAlias = tableAlias;
                }
                return true;
            }, aliasedFilter);
            return aliasedFilter;
        }
    }
}
