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
                ToSql(ref sb, expr, context, sqlBuilder, outputParams);
                string res = sb.ToString();
                sb.Dispose();
                return res;
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, Expr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (expr is null) return;

            // 根据 Expr 的具体类型，分发到对应的 SQL 转换逻辑
            if (expr is LogicBinaryExpr lb) ToSql(ref sb, lb, context, sqlBuilder, outputParams);
            else if (expr is ValueBinaryExpr vb) ToSql(ref sb, vb, context, sqlBuilder, outputParams);
            else if (expr is NotExpr lu) ToSql(ref sb, lu, context, sqlBuilder, outputParams);
            else if (expr is UnaryExpr vu) ToSql(ref sb, vu, context, sqlBuilder, outputParams);
            else if (expr is ValueExpr value) ToSql(ref sb, value, context, sqlBuilder, outputParams);
            else if (expr is PropertyExpr prop) ToSql(ref sb, prop, context, sqlBuilder, outputParams);
            else if (expr is FunctionExpr func) ToSql(ref sb, func, context, sqlBuilder, outputParams);
            else if (expr is LambdaExpr lambda) ToSql(ref sb, lambda, context, sqlBuilder, outputParams);
            else if (expr is GenericSqlExpr generic) ToSql(ref sb, generic, context, sqlBuilder, outputParams);
            else if (expr is ForeignExpr foreign) ToSql(ref sb, foreign, context, sqlBuilder, outputParams);
            else if (expr is LogicSet ls) ToSql(ref sb, ls, context, sqlBuilder, outputParams);
            else if (expr is ValueSet vs) ToSql(ref sb, vs, context, sqlBuilder, outputParams);
            else if (expr is AggregateFunctionExpr agg) ToSql(ref sb, agg, context, sqlBuilder, outputParams);
            else if (expr is FromExpr from) ToSql(ref sb, from, context, sqlBuilder, outputParams);
            else if (expr is SelectExpr select) ToSql(ref sb, select, context, sqlBuilder, outputParams);
            else if (expr is DeleteExpr delete) ToSql(ref sb, delete, context, sqlBuilder, outputParams);
            else if (expr is UpdateExpr update) ToSql(ref sb, update, context, sqlBuilder, outputParams);            
            else throw new NotSupportedException($"Expression type {expr.GetType().FullName} is not supported.");
        }

        /// <summary>
        /// 将 SQL 片段通过递归方式拆解并填充到 SqlValueResult 结构中。
        /// </summary>
        /// <param name="sql">目标 SQL 结果结构。</param>
        /// <param name="sqlSegment">要处理的 SQL 片段。</param>
        /// <param name="context">SQL 构建上下文。</param>
        /// <param name="sqlBuilder">具体数据库的构建器。</param>
        /// <param name="outputParams">参数集合。</param>
        private static void AddSql(ref SqlValueStringBuilder sql, ISqlSegment sqlSegment, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (sqlSegment is null) throw new ArgumentNullException(nameof(sqlSegment));

            switch (sqlSegment)
            {
                case SelectExpr select:
                    AddSql(ref sql, select, ref context, sqlBuilder, outputParams);
                    break;
                case WhereExpr where:
                    AddSql(ref sql, where, ref context, sqlBuilder, outputParams);
                    break;
                case GroupByExpr groupBy:
                    AddSql(ref sql, groupBy, ref context, sqlBuilder, outputParams);
                    break;
                case HavingExpr having:
                    AddSql(ref sql, having, ref context, sqlBuilder, outputParams);
                    break;
                case OrderByExpr orderBy:
                    AddSql(ref sql, orderBy, ref context, sqlBuilder, outputParams);
                    break;
                case SectionExpr section:
                    AddSql(ref sql, section, ref context, sqlBuilder, outputParams);
                    break;
                case FromExpr from:
                    AddSql(ref sql, from, ref context, sqlBuilder, outputParams);
                    break;
                default:
                    throw new NotSupportedException($"SQL segment type {sqlSegment.GetType().FullName} is not supported.");
            }
        }

        /// <summary>
        /// 将 SelectExpr 转换为 SQL 字符串片段。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, SelectExpr select, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            bool isMain = sb.Length == 0;
            if (!isMain) context.PushScope();
            SqlValueStringBuilder sql = new SqlValueStringBuilder();
            AddSql(ref sql, select.Source, ref context, sqlBuilder, outputParams);

            if (select.Selects == null || select.Selects.Count == 0)
            {
                sql.Select.Append("*");
            }
            else
            {
                for (int i = 0; i < select.Selects.Count; i++)
                {
                    if (i > 0) sql.Select.Append(", ");
                    ToSql(ref sql.Select, select.Selects[i], context, sqlBuilder, outputParams);
                }
            }
            sqlBuilder.BuildSelectSql(ref sql, ref sb);
            sql.Dispose();
            if (!isMain) context.PopScope();
        }
        /// <summary>
        /// 将逻辑二元表达式转换为 SQL。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, LogicBinaryExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            string op = String.Empty;
            _logicOperatorSymbols.TryGetValue(expr.Operator, out op);
            switch (expr.OriginOperator)
            {
                case LogicOperator.In:
                    int begin = sb.Length;
                    ToSql(ref sb, expr.Left, context, sqlBuilder, outputParams);
                    sb.Append(" ");
                    sb.Append(op);
                    sb.Append(" ");
                    int valuesBegin = sb.Length;
                    ToSql(ref sb, expr.Right, context, sqlBuilder, outputParams);
                    if (valuesBegin == sb.Length)
                    {
                        // IN 后面没有内容，视为空集合
                        sb.Length = begin;
                        if (!expr.Operator.IsNot()) sb.Append("0=1");
                    }
                    break;
                case LogicOperator.RegexpLike:
                    // 正则表达式匹配通常使用特定的函数调用语法
                    sb.Append(op);
                    sb.Append("(");
                    ToSql(ref sb, expr.Left, context, sqlBuilder, outputParams);
                    sb.Append(",");
                    ToSql(ref sb, expr.Right, context, sqlBuilder, outputParams);
                    sb.Append(")");
                    break;
                case LogicOperator.Equal:
                    // 特殊处理 NULL 值的比较：在 SQL 中 a = NULL 始终为假，必须使用 IS NULL
                    if (expr.Right is null || expr.Right is ValueExpr vs && vs.Value is null)
                    {
                        ToSql(ref sb, expr.Left, context, sqlBuilder, outputParams);
                        sb.Append(expr.Operator == LogicOperator.Equal ? " IS NULL" : " IS NOT NULL");
                    }
                    else if (expr.Left is null || expr.Left is ValueExpr vsl && vsl.Value is null)
                    {
                        ToSql(ref sb, expr.Right, context, sqlBuilder, outputParams);
                        sb.Append(expr.Operator == LogicOperator.Equal ? " IS NULL" : " IS NOT NULL");
                    }
                    else
                    {
                        ToSql(ref sb, expr.Left, context, sqlBuilder, outputParams);
                        sb.Append(" ");
                        sb.Append(op);
                        sb.Append(" ");
                        ToSql(ref sb, expr.Right, context, sqlBuilder, outputParams);
                    }
                    break;
                case LogicOperator.Contains:
                case LogicOperator.EndsWith:
                case LogicOperator.StartsWith:
                    // 处理 LIKE 相关 的模糊查询
                    if (expr.Right is ValueExpr vs2)
                    {

                        // 使用 escape 子句转义用户输入中的特殊字符
                        ToSql(ref sb, expr.Left, context, sqlBuilder, outputParams);
                        sb.Append(" ");
                        sb.Append(op);
                        sb.Append(" ");
                        // 参数化处理：将包含通配符的字符串作为参数传入，避免 SQL 注入
                        string paramName = outputParams.Count.ToString();
                        string val = sqlBuilder.ToSqlLikeValue(vs2.Value?.ToString());
                        switch (expr.OriginOperator)
                        {
                            case LogicOperator.StartsWith:
                                val = $"{val}%"; break;
                            case LogicOperator.EndsWith:
                                val = $"%{val}"; break;
                            case LogicOperator.Contains:
                                val = $"%{val}%"; break;
                        }
                        outputParams.Add(new KeyValuePair<string, object>(sqlBuilder.ToParamName(paramName), val));
                        sb.Append(sqlBuilder.ToSqlParam(paramName));
                        sb.Append(" ESCAPE '");
                        sb.Append(Const.LikeEscapeChar);
                        sb.Append("'");
                    }
                    else
                    {
                        ToSql(ref sb, expr.Left, context, sqlBuilder, outputParams);
                        sb.Append(" ");
                        sb.Append(op);
                        sb.Append(" ");
                        // 若右侧不是常量而是表达式，则需要生成复杂的嵌套 REPLACE 来转义特殊字符
                        var nestedRightSb = ValueStringBuilder.Create(64);
                        ToSql(ref nestedRightSb, expr.Right, context, sqlBuilder, outputParams);
                        string nestedRight = nestedRightSb.ToString();
                        nestedRightSb.Dispose();
                        string right = $"REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE({nestedRight},'{Const.LikeEscapeChar}', '{Const.LikeEscapeChar}{Const.LikeEscapeChar}'),'_', '{Const.LikeEscapeChar}_'),'%', '{Const.LikeEscapeChar}%'),'/', '{Const.LikeEscapeChar}/'),'[', '{Const.LikeEscapeChar}['),']', '{Const.LikeEscapeChar}]')";
                        switch (expr.OriginOperator)
                        {
                            case LogicOperator.StartsWith:
                                right = sqlBuilder.BuildConcatSql(right, "%"); break;
                            case LogicOperator.EndsWith:
                                right = sqlBuilder.BuildConcatSql("%", right); break;
                            case LogicOperator.Contains:
                                right = sqlBuilder.BuildConcatSql("%", right, "%"); break;
                        }
                        sb.Append(right);
                        sb.Append(" ESCAPE '");
                        sb.Append(Const.LikeEscapeChar);
                        sb.Append("'");
                    }
                    break;
                default:
                    ToSql(ref sb, expr.Left, context, sqlBuilder, outputParams);
                    sb.Append(" ");
                    sb.Append(op);
                    sb.Append(" ");
                    ToSql(ref sb, expr.Right, context, sqlBuilder, outputParams);
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
            if (expr.Operator == ValueOperator.Concat)
            {
                // 字符串拼接逻辑委托给具体的 sqlBuilder，因为不同数据库的语法差异很大（如 || vs CONCAT）
                var leftSb = ValueStringBuilder.Create(64);
                ToSql(ref leftSb, expr.Left, context, sqlBuilder, outputParams);
                string left = leftSb.ToString();
                leftSb.Dispose();

                var rightSb = ValueStringBuilder.Create(64);
                ToSql(ref rightSb, expr.Right, context, sqlBuilder, outputParams);
                string right = rightSb.ToString();
                rightSb.Dispose();

                sb.Append(sqlBuilder.BuildConcatSql(left, right));
            }
            else
            {
                ToSql(ref sb, expr.Left, context, sqlBuilder, outputParams);
                sb.Append(" ");
                sb.Append(op);
                sb.Append(" ");
                ToSql(ref sb, expr.Right, context, sqlBuilder, outputParams);
            }
        }

        /// <summary>
        /// 处理 NOT 表达式。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, NotExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (expr.Operand is LogicBinaryExpr be)
            {
                // 优化：将 NOT (a = b) 转换为 a <> b，避免冗余的 NOT 关键字
                var opposite = be.Operator.Opposite();
                ToSql(ref sb, new LogicBinaryExpr(be.Left, opposite, be.Right), context, sqlBuilder, outputParams);
            }
            else if (expr.Operand is NotExpr inner)
            {
                // 优化：双重否定 NOT (NOT a) 转换为 a
                ToSql(ref sb, inner.Operand, context, sqlBuilder, outputParams);
            }
            else
            {
                sb.Append("NOT (");
                ToSql(ref sb, expr.Operand, context, sqlBuilder, outputParams);
                sb.Append(")");
            }
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
            }
            ToSql(ref sb, expr.Operand, context, sqlBuilder, outputParams);
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
                        ToSql(ref sb, e, context, sqlBuilder, outputParams);
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
        private static void ToSql(ref ValueStringBuilder sb, PropertyExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
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
                // 如果 PropertyExpr 中指定了 TableAlias，则使用该别名来限定列名
                sb.Append(sqlBuilder.ToSqlName(expr.TableAlias ?? context.DefaultTableAliasName));
                sb.Append(".");
                sb.Append(sqlBuilder.ToSqlName(column?.Name ?? expr.PropertyName));
            }
        }
        /// <summary>
        /// 处理关联表过滤表达式（EXISTS 查询）。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, ForeignExpr foreginExpr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            TableView tableView = TableInfoProvider.Default.GetTableView(context.GetTable()?.DefinitionType);
            if (tableView == null) throw new ArgumentException($"Current context is not a TableView, cannot use ForeignExpr");
            var joinedTable = tableView.JoinedTables.FirstOrDefault(joined => joined.Name == foreginExpr.Foreign);
            if (joinedTable == null) throw new ArgumentException($"Foregin table {foreginExpr.Foreign} not exists in {context.GetTable().DefinitionType}");
            string defaultBaseTable = context.DefaultTableAliasName;
            context.PushScope();
            string foreignAlias = $"T{context.Sequence++}";
            context.AddTableAlias(foreignAlias, joinedTable.TableDefinition);
            context.TableArgs = foreginExpr.TableArgs;

            sb.Append("EXISTS(SELECT 1 FROM ");
            sb.Append(sqlBuilder.ToSqlName(String.Format(joinedTable.TableDefinition.Name, context.TableArgs)));
            sb.Append(" ");
            sb.Append(sqlBuilder.ToSqlName(foreignAlias));
            sb.Append(" \nWHERE ");
            for (int i = 0; i < joinedTable.ForeignKeys.Count; i++)
            {
                if (i > 0) sb.Append(" AND ");
                sb.Append(sqlBuilder.ToSqlName(joinedTable.ForeignKeys[i].Table?.Name ?? defaultBaseTable));
                sb.Append('.');
                sb.Append(sqlBuilder.ToSqlName(joinedTable.ForeignKeys[i].Name));
                sb.Append(" = ");
                sb.Append(sqlBuilder.ToSqlName(foreignAlias));
                sb.Append('.');
                sb.Append(sqlBuilder.ToSqlName(joinedTable.ForeignPrimeKeys[i].Name));
            }
            if (joinedTable.FilterExpression != null)
            {
                sb.Append("\n AND "); sb.Append(joinedTable.FilterExpression);
            }
            sb.Append(" AND ");
            int lenBefore = sb.Length;
            ToSql(ref sb, foreginExpr.InnerExpr, context, sqlBuilder, outputParams);
            if (sb.Length == lenBefore) sb.Length = lenBefore - 5; // 内部表达式没有生成任何 SQL 片段，视为永真条件，去掉多余的 AND            
            sb.Append(")");
            context.PopScope();
        }

        /// <summary>
        /// 处理数据库函数表达式。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            // 分发给具体的 sqlBuilder 生成数据库对应的函数 SQL
            var parameters = expr.Parameters;
            int count = parameters.Count;
            var args = new List<KeyValuePair<string, Expr>>(count);
            for (int i = 0; i < count; i++)
            {
                Expr p = parameters[i];
                var pSb = ValueStringBuilder.Create(64);
                ToSql(ref pSb, p, context, sqlBuilder, outputParams);
                string pSql = pSb.ToString();
                pSb.Dispose();
                args.Add(new KeyValuePair<string, Expr>(pSql, p));
            }
            sb.Append(sqlBuilder.BuildFunctionSql(expr.FunctionName, args));
        }


        /// <summary>
        /// 处理 Lambda 封装表达式。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, LambdaExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            ToSql(ref sb, expr.InnerExpr, context, sqlBuilder, outputParams);
        }

        /// <summary>
        /// 处理动态生成的 SQL 片段。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, GenericSqlExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            sb.Append(expr.GenerateSql(context, sqlBuilder, outputParams));
        }

        /// <summary>
        /// 处理逻辑集合（AND/OR）。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, LogicSet expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            int count = expr.Count;
            if (count == 0) return;

            string joinStr = expr.JoinType switch
            {
                LogicJoinType.And => " AND ",
                LogicJoinType.Or => " OR ",
                _ => ","
            };

            if (count > 1) sb.Append("(");

            bool first = true;
            for (int i = 0; i < count; i++)
            {
                int lenBefore = sb.Length;
                if (!first) sb.Append(joinStr);
                int lenWithJoin = sb.Length;

                ToSql(ref sb, expr[i], context, sqlBuilder, outputParams);

                if (sb.Length == lenWithJoin)
                {
                    sb.Length = lenBefore;
                }
                else
                {
                    first = false;
                }
            }
            if (count > 1) sb.Append(")");
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
                    ToSql(ref subSb, expr[i], context, sqlBuilder, outputParams);
                    subExprs[i] = subSb.ToString();
                    subSb.Dispose();
                }
                sb.Append(sqlBuilder.BuildConcatSql(subExprs));
                return;
            }

            sb.Append("(");
            bool first = true;
            for (int i = 0; i < count; i++)
            {
                if (!first) sb.Append(",");
                ToSql(ref sb, expr[i], context, sqlBuilder, outputParams);
                first = false;
            }
            sb.Append(")");
        }

        /// <summary>
        /// 向 SQL 结果结构中添加 Select 相关的子查询片段。
        /// </summary>
        private static void AddSql(ref SqlValueStringBuilder sql, SelectExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            context.PushScope();
            SqlValueStringBuilder innerSql = new SqlValueStringBuilder();
            AddSql(ref innerSql, expr.Source, ref context, sqlBuilder, outputParams);

            if (expr.Selects == null || expr.Selects.Count == 0)
            {
                innerSql.Select.Append("*");
            }
            else
            {
                for (int i = 0; i < expr.Selects.Count; i++)
                {
                    if (i > 0) innerSql.Select.Append(", ");
                    ToSql(ref innerSql.Select, expr.Selects[i], context, sqlBuilder, outputParams);
                }
            }
            sql.From.Append("(");
            sqlBuilder.BuildSelectSql(ref innerSql, ref sql.From);
            sql.From.Append(") ");
            context.PopScope();
            context.DefaultTableAliasName = $"T{context.Sequence++}";
            sql.From.Append(context.DefaultTableAliasName);
            context.AddTableAlias(context.DefaultTableAliasName, null);
            innerSql.Dispose();
        }

        /// <summary>
        /// 向 SQL 结果结构中添加 Where 过滤片段。
        /// </summary>
        private static void AddSql(ref SqlValueStringBuilder sql, WhereExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            AddSql(ref sql, expr.Source, ref context, sqlBuilder, outputParams);
            if (expr.Where != null)
            {
                if (sql.Where.Length > 0) sql.Where.Append(" AND ");
                ToSql(ref sql.Where, expr.Where, context, sqlBuilder, outputParams);
            }
        }

        private static void AddSql(ref SqlValueStringBuilder sql, FromExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            ToSql(ref sql.From, expr, context, sqlBuilder, outputParams);
        }


        /// <summary>
        /// 向 SQL 结果结构中添加 Group By 分组片段。
        /// </summary>
        private static void AddSql(ref SqlValueStringBuilder sql, GroupByExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            AddSql(ref sql, expr.Source, ref context, sqlBuilder, outputParams);
            if (expr.GroupBys != null && expr.GroupBys.Count > 0)
            {
                for (int i = 0; i < expr.GroupBys.Count; i++)
                {
                    if (sql.GroupBy.Length > 0) sql.GroupBy.Append(", ");
                    ToSql(ref sql.GroupBy, expr.GroupBys[i], context, sqlBuilder, outputParams);
                }
            }
        }

        /// <summary>
        /// 向 SQL 结果结构中添加 Order By 排序片段。
        /// </summary>
        private static void AddSql(ref SqlValueStringBuilder sql, OrderByExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            AddSql(ref sql, expr.Source, ref context, sqlBuilder, outputParams);
            if (expr.OrderBys != null && expr.OrderBys.Count > 0)
            {
                for (int i = 0; i < expr.OrderBys.Count; i++)
                {
                    if (sql.OrderBy.Length > 0) sql.OrderBy.Append(", ");
                    ToSql(ref sql.OrderBy, expr.OrderBys[i].Item1, context, sqlBuilder, outputParams);
                    if (!expr.OrderBys[i].Item2) sql.OrderBy.Append(" DESC");
                }
            }
        }

        /// <summary>
        /// 向 SQL 结果结构中添加分页相关参数。
        /// </summary>
        private static void AddSql(ref SqlValueStringBuilder sql, SectionExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            AddSql(ref sql, expr.Source, ref context, sqlBuilder, outputParams);
            sql.Skip = expr.Skip;
            sql.Take = expr.Take;
        }

        /// <summary>
        /// 向 SQL 结果结构中添加 Having 过滤片段。
        /// </summary>
        private static void AddSql(ref SqlValueStringBuilder sql, HavingExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            AddSql(ref sql, expr.Source, ref context, sqlBuilder, outputParams);
            if (expr.Having != null)
            {
                ToSql(ref sql.Having, expr.Having, context, sqlBuilder, outputParams);
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
                var tableDef = TableInfoProvider.Default.GetTableDefinition(expr.ObjectType);
                string tableName = string.Format(tableDef.Name, tableArgs);
                sb.Append(sqlBuilder.ToSqlName(tableName));
                sb.Append(" ");
                if (expr.Alias != null)
                {
                    sb.Append(sqlBuilder.ToSqlName(expr.Alias));
                }
                context.AddTableAlias(expr.Alias ?? tableDef.Name, tableDef);
            }
            else
            {
                var tableView = TableInfoProvider.Default.GetTableView(expr.ObjectType);
                string tableName = string.Format(tableView.Definition.Name, tableArgs);
                sb.Append(sqlBuilder.ToSqlName(tableName));
                sb.Append(" ");
                string aliasName = expr.Alias ?? tableView.Name;
                sb.Append(sqlBuilder.ToSqlName(aliasName));
                context.AddTableAlias(aliasName, tableView);
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

        /// <summary>
        /// 处理查询列项（带有 Alias）。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, SelectItemExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            ToSql(ref sb, expr.Value, context, sqlBuilder, outputParams);
            if (!string.IsNullOrEmpty(expr.Name))
            {
                sb.Append(" AS ");
                sb.Append(sqlBuilder.ToSqlName(expr.Name));
            }
        }

        /// <summary>
        /// 处理聚合函数 SQL。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, AggregateFunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            sb.Append(expr.FunctionName);
            sb.Append("(");
            if (expr.IsDistinct) sb.Append("DISTINCT ");
            ToSql(ref sb, expr.Expression, context, sqlBuilder, outputParams);
            sb.Append(")");
        }


        /// <summary>
        /// 生成 DELETE 语句对应的 SQL。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, DeleteExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            sb.Append("DELETE FROM ");
            ToSql(ref sb, expr.Source as FromExpr ?? new FromExpr(context.Table.Definition.ObjectType), context, sqlBuilder, outputParams);
            if (expr.Where != null)
            {
                sb.Append(" WHERE ");
                ToSql(ref sb, expr.Where, context, sqlBuilder, outputParams);
            }
        }

        /// <summary>
        /// 生成 UPDATE 语句对应的 SQL。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, UpdateExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (expr.Source is not FromExpr && context.Table is null) throw new ArgumentException("UpdateExpr Source is null and context Table is null, cannot determine update target.");
            FromExpr fromExpr = expr.Source as FromExpr ?? Expr.From(context.Table.Definition.ObjectType);
            var source = expr.Source is FromExpr from ? TableInfoProvider.Default.GetTableDefinition(from.ObjectType) : context.Table.Definition;
            sb.Append("UPDATE ");
            ToSql(ref sb, fromExpr, context, sqlBuilder, outputParams);
            sb.Append(" SET ");
            for (int i = 0; i < expr.Sets.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var set = expr.Sets[i];

                SqlColumn column = source.GetColumn(set.Item1);
                if (column == null) throw new Exception($"Property \"{set.Item1}\" does not exist in type \"{context.Table.DefinitionType.FullName}\".");
                sb.Append(sqlBuilder.ToSqlName(column.Name));

                sb.Append("=");
                ToSql(ref sb, set.Item2, context, sqlBuilder, outputParams);
            }
            if (expr.Where != null)
            {
                sb.Append(" WHERE ");
                ToSql(ref sb, expr.Where, context, sqlBuilder, outputParams);
            }
        }
    }
}
