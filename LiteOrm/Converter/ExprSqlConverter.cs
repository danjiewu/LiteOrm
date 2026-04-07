using System;
using System.Collections;
using System.Collections.Generic;
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

        /// <summary>
        /// 将当前表达式转换为 SQL 字符串片段。
        /// </summary>
        /// <param name="expr">表达式。</param>
        /// <param name="context">生成 SQL 的上下文环境，包含表信息、别名等。</param>
        /// <param name="sqlBuilder">提供数据库特定的 SQL 构建功能的工作类。</param>
        /// <param name="outputParams">输出参数集合，对应于此构建过程中产生的表达式参数与预定义的实际值（用于参数化查询）。</param>
        /// <returns>表示该表达式的 SQL 字符串片段，通常带有参数占位符。</returns>
        public static string ToSql(this Expr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (expr is null) return string.Empty;

            else
            {
                var sb = ValueStringBuilder.Create(128);
                ToSqlInternal(ref sb, expr, context, sqlBuilder, outputParams);
                string res = sb.ToString();
                sb.Dispose();
                return res;
            }
        }

        /// <summary>
        /// 将 TableJoinExpr 转换为 SQL 片段（JOIN ... ON ...）。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, TableJoinExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (expr == null) return;
            if (expr.Table == null) return;

            var joinTable = TableInfoProvider.Default.GetTableDefinition(expr.Table.Type);
            string joinAlias = expr.Table.Alias ?? $"T{context.Sequence++}";

            context.AddTableAlias(joinAlias, joinTable);

            sb.Append($" \n{context.Indent}");
            sb.Append((expr.JoinType).ToString().ToUpper());
            sb.Append(" JOIN ");
            sb.Append(sqlBuilder.ToSqlName(context.FormatTableName(joinTable.Name)));
            sb.Append(" ");
            sb.Append(sqlBuilder.ToSqlName(joinAlias));

            if (expr.On != null)
            {
                sb.Append(" ON ");
                int lenBefore = sb.Length;
                ToSqlInternal(ref sb, expr.On, context, sqlBuilder, outputParams);
                if (sb.Length == lenBefore) sb.Length = lenBefore;
            }
        }

        /// <summary>
        /// 将 TableExpr 转换为 SQL 片段（表名 别名）。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, TableExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (expr == null) return;
            var tableDef = TableInfoProvider.Default.GetTableDefinition(expr.Type);
            var tableName = sqlBuilder.ToSqlName(context.FormatTableName(tableDef.Name));
            if (context.SingleTable)
            {
                sb.Append(tableName);
                context.AddTableAlias(tableName, tableDef);
            }
            else
            {
                string aliasName = expr.Alias ?? $"T{context.Sequence++}";
                sb.Append(tableName);
                sb.Append(" ");
                sb.Append(sqlBuilder.ToSqlName(aliasName));
                context.AddTableAlias(aliasName, tableDef);
            }
        }

        /// <summary>
        /// 将当前表达式转换为预编译的 SQL 语句。
        /// </summary>
        /// <param name="expr">表达式。</param>
        /// <param name="context">生成 SQL 的上下文环境，包含表信息、别名等。</param>
        /// <param name="sqlBuilder">提供数据库特定的 SQL 构建功能的工作类。</param>
        /// <returns>包含 SQL 语句和参数列表的 <see cref="PreparedSql"/> 实例。</returns>
        public static PreparedSql ToPreparedSql(this Expr expr, SqlBuildContext context, ISqlBuilder sqlBuilder)
        {
            List<KeyValuePair<string, object>> outputParams = new List<KeyValuePair<string, object>>();
            string sql = ToSql(expr, context, sqlBuilder, outputParams);
            return new PreparedSql(sql, outputParams);
        }

        /// <summary>
        /// 将当前表达式转换为 SQL 字符串片段，结果直接追加到提供的 <see cref="ValueStringBuilder"/> 中。
        /// </summary>
        /// <param name="expr">表达式。</param>
        /// <param name="sb">用于接收 SQL 片段的字符串构建器。</param>
        /// <param name="context">生成 SQL 的上下文环境，包含表信息、别名等。</param>
        /// <param name="sqlBuilder">提供数据库特定的 SQL 构建功能的工作类。</param>
        /// <param name="outputParams">输出参数集合，对应于此构建过程中产生的参数化查询参数。</param>
        public static void ToSql(this Expr expr, ref ValueStringBuilder sb, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (expr is null) return;
            ToSqlInternal(ref sb, expr, context, sqlBuilder, outputParams);
        }

        private static void ToSqlInternal(ref ValueStringBuilder sb, Expr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams, int priority = 0)
        {
            if (expr is null) return;
            expr = Reduce(expr);

            int curPriority = GetPriority(expr);
            bool needParen = curPriority < priority;
            if (needParen) sb.Append('(');

            switch (expr)
            {
                // 根据 Expr 的具体类型，分发到对应的 SQL 转换逻辑
                case LogicBinaryExpr lb: ToSql(ref sb, lb, context, sqlBuilder, outputParams); break;
                case ValueBinaryExpr vb: ToSql(ref sb, vb, context, sqlBuilder, outputParams); break;
                case NotExpr lu: ToSql(ref sb, lu, context, sqlBuilder, outputParams); break;
                case UnaryExpr vu: ToSql(ref sb, vu, context, sqlBuilder, outputParams); break;
                case ValueExpr value: ToSql(ref sb, value, context, sqlBuilder, outputParams); break;
                case PropertyExpr prop: ToSql(ref sb, prop, context, sqlBuilder, outputParams); break;
                case FunctionExpr func: ToSql(ref sb, func, context, sqlBuilder, outputParams); break;
                case LambdaExpr lambda: ToSql(ref sb, lambda, context, sqlBuilder, outputParams); break;
                case GenericSqlExpr generic: ToSql(ref sb, generic, context, sqlBuilder, outputParams); break;
                case ForeignExpr foreign: ToSql(ref sb, foreign, context, sqlBuilder, outputParams); break;
                case AndExpr ae: ToSql(ref sb, ae, context, sqlBuilder, outputParams); break;
                case OrExpr oe: ToSql(ref sb, oe, context, sqlBuilder, outputParams); break;
                case ValueSet vs: ToSql(ref sb, vs, context, sqlBuilder, outputParams); break;
                case OrderByItemExpr obi: ToSql(ref sb, obi, context, sqlBuilder, outputParams); break;
                case FromExpr from: ToSql(ref sb, from, context, sqlBuilder, outputParams); break;
                case TableExpr table: ToSql(ref sb, table, context, sqlBuilder, outputParams); break;
                case SelectExpr select: ToSql(ref sb, select, context, sqlBuilder, outputParams); break;
                case DeleteExpr delete: ToSql(ref sb, delete, context, sqlBuilder, outputParams); break;
                case UpdateExpr update: ToSql(ref sb, update, context, sqlBuilder, outputParams); break;
                default: throw new NotSupportedException($"Expression type {expr.GetType().FullName} is not supported.");
            }

            if (needParen) sb.Append(')');
        }

        private static Expr Reduce(Expr expr)
        {
            if (expr is NotExpr not)
            {
                if (not.Operand is LogicBinaryExpr be)
                {
                    // 将 NOT (a = b) 转换为 a <> b，减少冗余的 NOT 关键字
                    var opposite = be.Operator.Opposite();
                    return new LogicBinaryExpr(be.Left, opposite, be.Right);
                }
                else if (not.Operand is NotExpr inner)
                {
                    // 将双重否定 NOT (NOT a) 转换为 a
                    return Reduce(inner.Operand);
                }
            }
            else if (expr is AndExpr and && and.Count == 1)
            {
                return and[0];
            }
            else if (expr is OrExpr or && or.Count == 1)
            {
                return or[0];
            }
            else if (expr is ValueExpr)
            {
                while (expr is ValueExpr ve && ve.Value is Expr innerExpr)
                {
                    expr = innerExpr;
                }
                return expr;
            }
            return expr;
        }

        private static int MaxPriority = 1000;
        /// <summary>
        /// 计算表达式的优先级（用于决定是否需要在生成 SQL 时添加括号）。
        /// 返回值越大表示优先级越高（更紧密结合），在需要时会根据与外层优先级比较决定是否加括号。
        /// 特别约定：<see cref="SelectExpr"/> 的优先级为 1，以便在合适的上下文中生成括号包裹子查询。
        /// </summary>
        /// <param name="expr">要计算优先级的表达式。</param>
        /// <returns>表示表达式优先级的整数值。</returns>
        private static int GetPriority(Expr expr)
        {
            return expr switch
            {
                SelectExpr select => select.NextSelects?.Count > 0 ? 0 : 1,
                ValueSet vs when vs.JoinType == ValueJoinType.List => 2,
                OrExpr _ => 11,
                AndExpr _ => 12,                
                LogicBinaryExpr _ => 13,                
                ValueBinaryExpr vb => vb.Operator switch
                {
                    ValueOperator.Add or ValueOperator.Subtract => 14,
                    ValueOperator.Concat => 16,
                    _ => 15
                },
                NotExpr _ => 17,
                UnaryExpr _ => 18, 
                _ => MaxPriority
            };
        }

        /// <summary>
        /// 将 SQL 片段通过递归方式拆解并填充到 SqlValueResult 结构中。
        /// </summary>
        /// <param name="sql">目标 SQL 结果结构。</param>
        /// <param name="sqlSegment">要处理的 SQL 片段。</param>
        /// <param name="context">SQL 构建上下文。</param>
        /// <param name="sqlBuilder">具体数据库的构建器。</param>
        /// <param name="outputParams">参数集合。</param>
        private static void AddSqlSegment(ref SqlValueStringBuilder sql, SqlSegment sqlSegment, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (sqlSegment is null) throw new ArgumentNullException(nameof(sqlSegment));

            switch (sqlSegment)
            {
                case SelectExpr select:
                    AddSqlSegment(ref sql, select, context, sqlBuilder, outputParams);
                    break;
                case WhereExpr where:
                    AddSqlSegment(ref sql, where, context, sqlBuilder, outputParams);
                    break;
                case GroupByExpr groupBy:
                    AddSqlSegment(ref sql, groupBy, context, sqlBuilder, outputParams);
                    break;
                case HavingExpr having:
                    AddSqlSegment(ref sql, having, context, sqlBuilder, outputParams);
                    break;
                case OrderByExpr orderBy:
                    AddSqlSegment(ref sql, orderBy, context, sqlBuilder, outputParams);
                    break;
                case SectionExpr section:
                    AddSqlSegment(ref sql, section, context, sqlBuilder, outputParams);
                    break;
                case FromExpr from:
                    AddSqlSegment(ref sql, from, context, sqlBuilder, outputParams);
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
        private static void ToSql(ref ValueStringBuilder sb, LogicBinaryExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            string op = String.Empty;
            bool isOppsite = expr.Operator.IsNot();
            char escapeChar = Constants.LikeEscapeChar;
            _logicOperatorSymbols.TryGetValue(expr.Operator, out op);
            int curPriority = GetPriority(expr);
            switch (expr.OriginOperator)
            {
                case LogicOperator.In:
                    int begin = sb.Length;
                    ToSqlInternal(ref sb, expr.Left, context, sqlBuilder, outputParams, curPriority);
                    sb.Append(" ");
                    sb.Append(op);
                    sb.Append(" ");
                    int valuesBegin = sb.Length;
                    ToSqlInternal(ref sb, expr.Right, context, sqlBuilder, outputParams, curPriority);
                    if (valuesBegin == sb.Length)
                    {
                        // IN 后面没有内容，视为空集合
                        sb.Length = begin;
                        if (!isOppsite) sb.Append("0=1");
                    }
                    break;
                case LogicOperator.RegexpLike:
                    // 正则表达式匹配通常使用特定的函数调用语法
                    sb.Append(op);
                    sb.Append("(");
                    ToSqlInternal(ref sb, expr.Left, context, sqlBuilder, outputParams, curPriority);
                    sb.Append(",");
                    ToSqlInternal(ref sb, expr.Right, context, sqlBuilder, outputParams, curPriority);
                    sb.Append(")");
                    break;
                case LogicOperator.Equal:
                    // 特殊处理 NULL 值的比较：在 SQL 中 a = NULL 始终为假，必须使用 IS NULL
                    if (expr.Right is null || expr.Right is ValueExpr vs && vs.Value is null)
                    {
                        ToSqlInternal(ref sb, expr.Left, context, sqlBuilder, outputParams, curPriority);
                        sb.Append(isOppsite ? " IS NOT NULL" : " IS NULL");
                    }
                    else if (expr.Left is null || expr.Left is ValueExpr vsl && vsl.Value is null)
                    {
                        ToSqlInternal(ref sb, expr.Right, context, sqlBuilder, outputParams, curPriority);
                        sb.Append(isOppsite ? " IS NOT NULL" : " IS NULL");
                    }
                    else
                    {
                        ToSqlInternal(ref sb, expr.Left, context, sqlBuilder, outputParams, curPriority);
                        sb.Append(" ");
                        sb.Append(op);
                        sb.Append(" ");
                        ToSqlInternal(ref sb, expr.Right, context, sqlBuilder, outputParams, curPriority);
                    }
                    break;
                case LogicOperator.Contains:
                case LogicOperator.StartsWith:
                case LogicOperator.EndsWith:
                    // 处理 LIKE 相关 的模糊查询
                    if (expr.Right is ValueExpr vs2 && vs2.Value is not Expr)
                    {
                        // 使用 escape 子句转义用户输入中的特殊字符
                        ToSqlInternal(ref sb, expr.Left, context, sqlBuilder, outputParams, curPriority);
                        sb.Append(" ");
                        sb.Append(op);
                        sb.Append(" ");
                        // 参数化处理：将包含通配符的字符串作为参数传入，避免 SQL 注入
                        string paramName = outputParams.Count.ToString();
                        string val = sqlBuilder.ToSqlLikeValue(vs2.Value?.ToString());
                        val = expr.OriginOperator switch
                        {
                            LogicOperator.StartsWith => $"{val}%",
                            LogicOperator.EndsWith => $"%{val}",
                            LogicOperator.Contains => $"%{val}%",
                            _ => val
                        };
                        outputParams.Add(new(sqlBuilder.ToParamName(paramName), val));
                        sb.Append(sqlBuilder.ToSqlParam(paramName));
                        sb.Append($" ESCAPE '{escapeChar}'");
                    }
                    else
                    {
                        if (expr.OriginOperator == LogicOperator.Contains)
                        {
                            var compExpr = Expr.Func("SubString", expr.Left, expr.Right) >= 0;
                            if (isOppsite) compExpr = compExpr.Not();
                            ToSqlInternal(ref sb, compExpr, context, sqlBuilder, outputParams, curPriority);
                        }
                        else if (expr.OriginOperator == LogicOperator.StartsWith)
                        {
                            var compExpr = Expr.Func("SubString", expr.Left, expr.Right) == 0;
                            if (isOppsite) compExpr = compExpr.Not();
                            ToSqlInternal(ref sb, compExpr, context, sqlBuilder, outputParams, curPriority);
                        }
                        else//EndsWith 无法通过单次调用表达式转换实现，需要生成复杂的嵌套 REPLACE 来转义特殊字符再用 LIKE 匹配结尾
                        {
                            ToSqlInternal(ref sb, expr.Left, context, sqlBuilder, outputParams, curPriority);
                            sb.Append(" ");
                            sb.Append(op);
                            sb.Append(" ");
                            var nestedRightSb = ValueStringBuilder.Create(64);
                            ToSqlInternal(ref nestedRightSb, expr.Right, context, sqlBuilder, outputParams);
                            string nestedRight = nestedRightSb.ToString();
                            nestedRightSb.Dispose();

                            string right = $"REPLACE(REPLACE(REPLACE(REPLACE(REPLACE({nestedRight},'{escapeChar}', '{escapeChar}{escapeChar}'),'_', '{escapeChar}_'),'%', '{escapeChar}%'),'[', '{escapeChar}['),']', '{escapeChar}]')";
                            right = sqlBuilder.BuildConcatSql("'%'", right);
                            sb.Append(right);
                            sb.Append($" ESCAPE '{escapeChar}'");
                        }
                    }
                    break;
                default:
                    ToSqlInternal(ref sb, expr.Left, context, sqlBuilder, outputParams, curPriority);
                    sb.Append(" ");
                    sb.Append(op);
                    sb.Append(" ");
                    ToSqlInternal(ref sb, expr.Right, context, sqlBuilder, outputParams, curPriority);
                    break;
            }
        }

        /// <summary>
        /// 将值二元表达式（如加减乘除）转换为 SQL。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, ValueBinaryExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            string op = String.Empty;
            _valueOperatorSymbols.TryGetValue(expr.Operator, out op);
            int curPriority = GetPriority(expr);
            if (expr.Operator == ValueOperator.Concat)
            {
                // 字符串拼接逻辑委托给具体的 sqlBuilder，因为不同数据库的语法差异很大（如 || vs CONCAT）
                var leftSb = ValueStringBuilder.Create(64);
                ToSqlInternal(ref leftSb, expr.Left, context, sqlBuilder, outputParams);
                string left = leftSb.ToString();
                leftSb.Dispose();

                var rightSb = ValueStringBuilder.Create(64);
                ToSqlInternal(ref rightSb, expr.Right, context, sqlBuilder, outputParams);
                string right = rightSb.ToString();
                rightSb.Dispose();

                sb.Append(sqlBuilder.BuildConcatSql(left, right));
            }
            else
            {
                // 对于非交换运算符（减、除、取模），右操作数相同优先级也需要括号，以保证左结合性
                bool isCommutative = expr.Operator is ValueOperator.Add or ValueOperator.Multiply;
                ToSqlInternal(ref sb, expr.Left, context, sqlBuilder, outputParams, curPriority);
                sb.Append(" ");
                sb.Append(op);
                sb.Append(" ");
                ToSqlInternal(ref sb, expr.Right, context, sqlBuilder, outputParams, isCommutative ? curPriority : curPriority + 1);
            }
            // Parenthesis are managed by ToSqlInternal.
        }

        /// <summary>
        /// 处理 NOT 表达式。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, NotExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            // Reduce has already simplified NOT (a = b) and double negation cases.
            int curPriority = GetPriority(expr);
            sb.Append("NOT ");
            ToSqlInternal(ref sb, expr.Operand, context, sqlBuilder, outputParams, curPriority);
        }

        /// <summary>
        /// 处理一元表达式（如取负、位取反）。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, UnaryExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
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
            ToSqlInternal(ref sb, expr.Operand, context, sqlBuilder, outputParams);
        }

        /// <summary>
        /// 将值表达式转换为 SQL，并支持参数化。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, ValueExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (expr.Value == null)
            {
                sb.Append("NULL");
            }
            else if (expr.IsConst && expr.Value is bool b)
            {
                sb.Append(b ? "1" : "0");
            }
            else if (expr.IsConst && expr.Value.GetType().IsPrimitive)
            {
                // 数值类型常量直接以字面量形式输出，较为高效
                sb.Append(expr.Value.ToString());
            }
            else if (expr.Value is Expr innerExpr)
            {
                ToSqlInternal(ref sb, innerExpr, context, sqlBuilder, outputParams);
            }
            else if (expr.Value is IEnumerable enumerable && !(expr.Value is string))
            {
                // 处理 IN (...) 集合
                bool first = true;
                foreach (var item in enumerable)
                {
                    if (first) sb.Append('(');
                    else sb.Append(',');
                    if (item is Expr e)
                    {
                        ToSqlInternal(ref sb, e, context, sqlBuilder, outputParams);
                    }
                    else
                    {
                        // 对集合中的每个元素进行参数化
                        string paramName = outputParams.Count.ToString();
                        outputParams.Add(new(sqlBuilder.ToParamName(paramName), item));
                        sb.Append(sqlBuilder.ToSqlParam(paramName));
                    }
                    first = false;
                }
                if (!first) sb.Append(')');
            }
            else
            {
                // 其他类型（如字符串、日期）通过参数化处理以保证安全
                string paramName = outputParams.Count.ToString();
                outputParams.Add(new(sqlBuilder.ToParamName(paramName), expr.Value));
                sb.Append(sqlBuilder.ToSqlParam(paramName));
            }
        }

        /// <summary>
        /// 处理属性名称表达式，映射为数据库列名。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, PropertyExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams, string aliasName = null)
        {
            var table = context.GetTable(expr.TableAlias);
            var column = table?.GetColumn(expr.PropertyName);
            if (context.SingleTable)
            {
                // 单表模式下只需要输出列名
                sb.Append(sqlBuilder.ToSqlName(column?.Name ?? expr.PropertyName));
            }
            else if (column is ForeignColumn foreignColumn)
            {
                foreignColumn.TargetColumn.ToSql(ref sb, context, sqlBuilder);
            }
            else
            {
                string tableAlias = expr.TableAlias ?? context.DefaultTableAliasName;
                if (!String.IsNullOrEmpty(tableAlias))
                {
                    // 如果 PropertyExpr 中指定了 TableAlias，则使用该别名来限定列名
                    sb.Append(sqlBuilder.ToSqlName(tableAlias));
                    sb.Append(".");
                }
                sb.Append(sqlBuilder.ToSqlName(column?.Name ?? expr.PropertyName));
            }
            if (aliasName != null && !String.Equals(column?.Name, aliasName, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(" AS ");
                sb.Append(sqlBuilder.ToSqlName(aliasName));
            }
        }
        /// <summary>
        /// 处理关联表过滤表达式（EXISTS 查询）。
        /// 完全通过 InnerExpr 控制关联条件。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, ForeignExpr foreignExpr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (foreignExpr.Foreign == null) throw new ArgumentException("ForeignExpr.Foreign is required");

            var foreignTable = TableInfoProvider.Default.GetTableView(foreignExpr.Foreign);
            if (foreignTable == null) throw new ArgumentException($"Table info not found for type {foreignExpr.Foreign}");

            string foreignAlias = string.IsNullOrEmpty(foreignExpr.Alias) ? $"T{context.Sequence++}" : foreignExpr.Alias;
            LogicExpr joinedExpr = null;
            if (foreignExpr.AutoRelated && context.Table is not null)
            {
                foreach (JoinedTable joinedTable in TableInfoProvider.Default.GetTableView(context.Table.DefinitionType).JoinedTables)
                {
                    if (foreignExpr.Foreign.IsAssignableFrom(joinedTable.TableDefinition.DefinitionType))
                    {
                        // 找到当前表与目标表之间的关联关系，自动生成关联条件
                        joinedExpr |= new AndExpr(joinedTable.ForeignPrimeKeys.Zip(joinedTable.ForeignKeys, (pk, fk) =>
                            Expr.Prop(foreignAlias, pk.Name) == Expr.Prop(context.DefaultTableAliasName, fk.Name)
                        ));
                    }
                }
                // 正向没有找到关联关系，尝试反向查找
                if (joinedExpr is null)
                {
                    foreach (JoinedTable joinedTable in foreignTable.JoinedTables)
                    {
                        if (context.Table.DefinitionType.IsAssignableFrom(joinedTable.TableDefinition.DefinitionType))
                        {
                            // 找到当前表与目标表之间的关联关系，自动生成关联条件
                            joinedExpr |= new AndExpr(joinedTable.ForeignPrimeKeys.Zip(joinedTable.ForeignKeys, (pk, fk) =>
                                Expr.Prop(foreignAlias, fk.Name) == Expr.Prop(context.DefaultTableAliasName, pk.Name)
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
                sb.Append(sqlBuilder.ToSqlName(context.FormatTableName(foreignTable.Definition.Name)));
                sb.Append(" ");
                sb.Append(sqlBuilder.ToSqlName(foreignAlias));

                LogicExpr whereExpr = joinedExpr.And(foreignExpr.InnerExpr);
                sb.Append($" \n{context.Indent}WHERE ");
                int lenBefore = sb.Length;
                ToSqlInternal(ref sb, whereExpr, context, sqlBuilder, outputParams);
                if (sb.Length == lenBefore) sb.Length = lenBefore - 7;

                sb.Append(")");
            }
            sb.Append($" \n{context.Indent}");
        }

        /// <summary>
        /// 处理数据库函数表达式。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            sqlBuilder.BuildFunctionSql(ref sb, expr, context, outputParams);
        }


        /// <summary>
        /// 处理 Lambda 封装表达式。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, LambdaExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            ToSqlInternal(ref sb, expr.InnerExpr, context, sqlBuilder, outputParams);
        }

        /// <summary>
        /// 处理动态生成的 SQL 片段。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, GenericSqlExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            sb.Append(expr.GenerateSql(context, sqlBuilder, outputParams));
        }

        /// <summary>
        /// 处理 AND 表达式组合。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, AndExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            int count = expr.Count;
            if (count == 0) return;
            int curPriority = GetPriority(expr);

            bool first = true;
            for (int i = 0; i < count; i++)
            {
                int lenBefore = sb.Length;
                if (!first) sb.Append(" AND ");
                int lenWithJoin = sb.Length;

                ToSqlInternal(ref sb, expr[i], context, sqlBuilder, outputParams, curPriority);

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
        private static void ToSql(ref ValueStringBuilder sb, OrExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            int count = expr.Count;
            if (count == 0) return;
            int curPriority = GetPriority(expr);
            bool first = true;
            for (int i = 0; i < count; i++)
            {
                int lenBefore = sb.Length;
                if (!first) sb.Append(" OR ");
                int lenWithJoin = sb.Length;

                ToSqlInternal(ref sb, expr[i], context, sqlBuilder, outputParams, curPriority);

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
        private static void ToSql(ref ValueStringBuilder sb, ValueSet expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            int count = expr.Count;
            if (count == 0) return;

            if (expr.JoinType == ValueJoinType.Concat)
            {
                string[] subExprs = new string[count];
                for (int i = 0; i < count; i++)
                {
                    var subSb = ValueStringBuilder.Create(64);
                    ToSqlInternal(ref subSb, expr[i], context, sqlBuilder, outputParams);
                    subExprs[i] = subSb.ToString();
                    subSb.Dispose();
                }
                sb.Append(sqlBuilder.BuildConcatSql(subExprs));
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
                ToSqlInternal(ref sb, expr[i], context, sqlBuilder, outputParams);
                first = false;
            }
        }

        /// <summary>
        /// 处理排序项，渲染为 "field" 或 "field DESC"。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, OrderByItemExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            ToSqlInternal(ref sb, expr.Field, context, sqlBuilder, outputParams);
            if (!expr.Ascending) sb.Append(" DESC");
        }

        /// <summary>
        /// 向 SQL 结果结构中添加 Select 相关的子查询片段。
        /// </summary>
        private static void AddSqlSegment(ref SqlValueStringBuilder sql, SelectExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            using (context.BeginScope())
            {
                ToSqlInternal(ref sql.From, expr, context, sqlBuilder, outputParams, MaxPriority);
            }
            string aliasMain = expr.Alias ?? $"T{context.Sequence++}";
            context.DefaultTableAliasName = aliasMain;
            sql.From.Append($" {aliasMain}\n{context.Indent}");
            context.AddTableAlias(aliasMain, null);
        }

        /// <summary>
        /// 向 SQL 结果结构中添加 Where 过滤片段。
        /// </summary>
        private static void AddSqlSegment(ref SqlValueStringBuilder sql, WhereExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            AddSqlSegment(ref sql, expr.Source, context, sqlBuilder, outputParams);
            if (expr.Where != null)
            {
                if (sql.Where.Length > 0) sql.Where.Append(" AND ");
                ToSqlInternal(ref sql.Where, expr.Where, context, sqlBuilder, outputParams);
            }
        }

        private static void AddSqlSegment(ref SqlValueStringBuilder sql, FromExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            ToSql(ref sql.From, expr, context, sqlBuilder, outputParams);
        }

        /// <summary>
        /// 向 SQL 结果结构中添加 Group By 分组片段。
        /// </summary>
        private static void AddSqlSegment(ref SqlValueStringBuilder sql, GroupByExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            AddSqlSegment(ref sql, expr.Source, context, sqlBuilder, outputParams);
            if (expr.GroupBys != null && expr.GroupBys.Count > 0)
            {
                for (int i = 0; i < expr.GroupBys.Count; i++)
                {
                    if (sql.GroupBy.Length > 0) sql.GroupBy.Append(", ");
                    ToSqlInternal(ref sql.GroupBy, expr.GroupBys[i], context, sqlBuilder, outputParams);
                }
            }
        }

        /// <summary>
        /// 向 SQL 结果结构中添加 Order By 排序片段。
        /// </summary>
        private static void AddSqlSegment(ref SqlValueStringBuilder sql, OrderByExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            AddSqlSegment(ref sql, expr.Source, context, sqlBuilder, outputParams);
            if (expr.OrderBys != null && expr.OrderBys.Count > 0)
            {
                for (int i = 0; i < expr.OrderBys.Count; i++)
                {
                    if (sql.OrderBy.Length > 0) sql.OrderBy.Append(", ");
                    ToSqlInternal(ref sql.OrderBy, expr.OrderBys[i].Field, context, sqlBuilder, outputParams);
                    if (!expr.OrderBys[i].Ascending) sql.OrderBy.Append(" DESC");
                }
            }
        }

        /// <summary>
        /// 向 SQL 结果结构中添加分页相关参数。
        /// </summary>
        private static void AddSqlSegment(ref SqlValueStringBuilder sql, SectionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            AddSqlSegment(ref sql, expr.Source, context, sqlBuilder, outputParams);
            sql.Skip = expr.Skip;
            sql.Take = expr.Take;
        }

        /// <summary>
        /// 向 SQL 结果结构中添加 Having 过滤片段。
        /// </summary>
        private static void AddSqlSegment(ref SqlValueStringBuilder sql, HavingExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            AddSqlSegment(ref sql, expr.Source, context, sqlBuilder, outputParams);
            if (expr.Having != null)
            {
                ToSqlInternal(ref sql.Having, expr.Having, context, sqlBuilder, outputParams);
            }
        }

        /// <summary>
        /// 处理 From 片段，根据 SingleTable 判断生成单表还是视图的 SQL。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, FromExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (expr == null) return;
            var tableArgs = expr.TableArgs ?? context.TableArgs ?? Array.Empty<string>();
            if (context.SingleTable)
            {
                var tableDef = TableInfoProvider.Default.GetTableDefinition(expr.Type);
                context.TableArgs = tableArgs;
                sb.Append(sqlBuilder.ToSqlName(context.FormatTableName(tableDef.Name)));
                context.AddTableAlias(tableDef.Name, tableDef);
            }
            else
            {
                bool isMain = context.Depth <= 1;
                var mainTable = expr.Table;
                var tableType = mainTable?.Type ?? expr.Type;
                var tableView = TableInfoProvider.Default.GetTableView(tableType);
                context.TableArgs = tableArgs;
                string aliasName = (mainTable?.Alias) ?? expr.Alias ?? (isMain ? Constants.DefaultTableAlias : $"T{context.Sequence++}");

                sb.Append(sqlBuilder.ToSqlName(context.FormatTableName(tableView.Definition.Name)));
                sb.Append(" ");
                sb.Append(sqlBuilder.ToSqlName(aliasName));
                context.AddTableAlias(aliasName, tableView);

                if (expr.Joins != null && expr.Joins.Count > 0)
                {
                    foreach (var j in expr.Joins)
                    {
                        ToSql(ref sb, j, context, sqlBuilder, outputParams);
                    }
                }
                else
                {
                    foreach (var joined in tableView.JoinedTables)
                    {
                        if (joined.Used)
                        {
                            joined.ToSql(ref sb, context, sqlBuilder);
                            context.AddTableAlias(joined.Name, joined.TableDefinition);
                        }
                    }
                }
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, SelectExpr select, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            using (context.BeginScope())
            {
                SqlValueStringBuilder sql = new SqlValueStringBuilder() { Indent = context.Indent };
                AddSqlSegment(ref sql, select.Source, context, sqlBuilder, outputParams);

                if (select.Selects == null || select.Selects.Count == 0)
                {
                    sql.Select.Append("*");
                }
                else
                {
                    for (int i = 0; i < select.Selects.Count; i++)
                    {
                        if (i > 0) sql.Select.Append(",");
                        ToSql(ref sql.Select, select.Selects[i], context, sqlBuilder, outputParams);
                    }
                }

                sqlBuilder.BuildSelectSql(ref sql, ref sb);
                sql.Dispose();
            }
            foreach (var next in select.NextSelects)
            {
                sb.Append($" \n{context.Indent}");
                sb.Append(sqlBuilder.ToSqlSelectSetType(next.SetType));
                sb.Append($" \n{context.Indent}");
                ToSqlInternal(ref sb, next, context, sqlBuilder, outputParams, 1);// Select默认优先级为1，NextSelects 中的 Select 如果有嵌套 NextSelects 则优先级为0，需要括号包裹
            }
        }

        /// <summary>
        /// 处理查询列项（带有 Alias）。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, SelectItemExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (expr is null) return;
            if (expr.Value is PropertyExpr propertyExpr)
            {
                ToSql(ref sb, propertyExpr, context, sqlBuilder, outputParams, expr.Alias);
            }
            else
            {
                ToSqlInternal(ref sb, expr.Value, context, sqlBuilder, outputParams);
                if (!string.IsNullOrEmpty(expr.Alias))
                {
                    sb.Append(" AS ");
                    sb.Append(sqlBuilder.ToSqlName(expr.Alias));
                }
            }
        }

        /// <summary>
        /// 生成 DELETE 语句对应的 SQL。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, DeleteExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {

            sb.Append("DELETE FROM ");
            ToSql(ref sb, expr.Table ?? new TableExpr(context.Table.DefinitionType), context, sqlBuilder, outputParams);
            if (expr.Where != null)
            {
                using (context.BeginScope())
                {
                    sb.Append($" \n{context.Indent}WHERE ");
                    ToSqlInternal(ref sb, expr.Where, context, sqlBuilder, outputParams);
                }
            }
        }

        /// <summary>
        /// 生成 UPDATE 语句对应的 SQL。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, UpdateExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            TableExpr tableExpr = expr.Table ?? new TableExpr(context.Table.DefinitionType);
            if (tableExpr == null)
                throw new ArgumentException("UpdateExpr Source is null and context Table is null, cannot determine update target.");
            var table = TableInfoProvider.Default.GetTableDefinition(tableExpr.Type);
            sb.Append("UPDATE ");
            ToSql(ref sb, tableExpr, context, sqlBuilder, outputParams);
            sb.Append($" \n{context.Indent}SET ");
            for (int i = 0; i < expr.Sets.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var set = expr.Sets[i];
                int priority = GetPriority(expr);
                SqlColumn column = table?.GetColumn(set.Item1.PropertyName);
                if (column == null) throw new Exception($"Property \"{set.Item1}\" does not exist in type \"{context.Table.DefinitionType.FullName}\".");
                sb.Append(sqlBuilder.ToSqlName(column.Name));

                sb.Append("=");
                ToSqlInternal(ref sb, set.Item2, context, sqlBuilder, outputParams, priority);
            }
            if (expr.Where != null)
            {
                using (context.BeginScope())
                {
                    sb.Append($" \n{context.Indent}WHERE ");
                    ToSqlInternal(ref sb, expr.Where, context, sqlBuilder, outputParams);
                }
            }
        }
    }
}
