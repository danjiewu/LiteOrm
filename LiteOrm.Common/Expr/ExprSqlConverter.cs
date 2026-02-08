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
            var sb = ValueStringBuilder.Create(128);
            ToSql(ref sb, expr, ref context, sqlBuilder, outputParams);
            string res = sb.ToString();
            sb.Dispose();
            return res;
        }

        private static void ToSql(ref ValueStringBuilder sb, Expr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (expr is null) return;

            // 根据 Expr 的具体类型，分发到对应的 SQL 转换逻辑
            if (expr is LogicBinaryExpr lb) ToSql(ref sb, lb, ref context, sqlBuilder, outputParams);
            else if (expr is ValueBinaryExpr vb) ToSql(ref sb, vb, ref context, sqlBuilder, outputParams);
            else if (expr is NotExpr lu) ToSql(ref sb, lu, ref context, sqlBuilder, outputParams);
            else if (expr is UnaryExpr vu) ToSql(ref sb, vu, ref context, sqlBuilder, outputParams);
            else if (expr is ValueExpr value) ToSql(ref sb, value, ref context, sqlBuilder, outputParams);
            else if (expr is PropertyExpr prop) ToSql(ref sb, prop, ref context, sqlBuilder, outputParams);
            else if (expr is FunctionExpr func) ToSql(ref sb, func, ref context, sqlBuilder, outputParams);
            else if (expr is LambdaExpr lambda) ToSql(ref sb, lambda, ref context, sqlBuilder, outputParams);
            else if (expr is GenericSqlExpr generic) ToSql(ref sb, generic, ref context, sqlBuilder, outputParams);
            else if (expr is ForeignExpr foreign) ToSql(ref sb, foreign, ref context, sqlBuilder, outputParams);
            else if (expr is LogicSet ls) ToSql(ref sb, ls, ref context, sqlBuilder, outputParams);
            else if (expr is ValueSet vs) ToSql(ref sb, vs, ref context, sqlBuilder, outputParams);
            else if (expr is SelectExpr select) ToSql(ref sb, select, ref context, sqlBuilder, outputParams);
            else if (expr is WhereExpr where) ToSql(ref sb, where, ref context, sqlBuilder, outputParams);
            else if (expr is TableExpr table) ToSql(ref sb, table, ref context, sqlBuilder, outputParams);
            else if (expr is GroupByExpr groupBy) ToSql(ref sb, groupBy, ref context, sqlBuilder, outputParams);
            else if (expr is HavingExpr having) ToSql(ref sb, having, ref context, sqlBuilder, outputParams);
            else if (expr is AggregateFunctionExpr agg) ToSql(ref sb, agg, ref context, sqlBuilder, outputParams);
            else if (expr is OrderByExpr order) ToSql(ref sb, order, ref context, sqlBuilder, outputParams);
            else if (expr is SectionExpr section) ToSql(ref sb, section, ref context, sqlBuilder, outputParams);
            else if (expr is DeleteExpr delete) ToSql(ref sb, delete, ref context, sqlBuilder, outputParams);
            else if (expr is UpdateExpr update) ToSql(ref sb, update, ref context, sqlBuilder, outputParams);
            else
                throw new NotSupportedException($"Expression type {expr.GetType().FullName} is not supported.");
        }


        private static void ToSql(ref ValueStringBuilder sb, LogicBinaryExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            string op = String.Empty;
            _logicOperatorSymbols.TryGetValue(expr.Operator, out op);
            switch (expr.OriginOperator)
            {
                case LogicOperator.In:
                    var inrightSb = ValueStringBuilder.Create(64);
                    ToSql(ref inrightSb, expr.Right, ref context, sqlBuilder, outputParams);
                    ReadOnlySpan<char> inright = inrightSb.AsSpan();
                    if (inright.Length == 0)
                    {
                        // IN 后面没有内容，视为空集合
                        if (!expr.Operator.IsNot()) sb.Append("0=1");
                    }
                    else
                    {
                        ToSql(ref sb, expr.Left, ref context, sqlBuilder, outputParams);
                        sb.Append(" ");
                        sb.Append(op);
                        sb.Append(" ");
                        sb.Append(inright);
                    }
                    inrightSb.Dispose();
                    break;
                case LogicOperator.RegexpLike:
                    // 正则表达式匹配通常使用特定的函数调用语法
                    sb.Append(op);
                    sb.Append("(");
                    ToSql(ref sb, expr.Left, ref context, sqlBuilder, outputParams);
                    sb.Append(",");
                    ToSql(ref sb, expr.Right, ref context, sqlBuilder, outputParams);
                    sb.Append(")");
                    break;
                case LogicOperator.Equal:
                    // 特殊处理 NULL 值的比较：在 SQL 中 a = NULL 始终为假，必须使用 IS NULL
                    if (expr.Right is null || expr.Right is ValueExpr vs && vs.Value is null)
                    {
                        ToSql(ref sb, expr.Left, ref context, sqlBuilder, outputParams);
                        sb.Append(expr.Operator == LogicOperator.Equal ? " IS NULL" : " IS NOT NULL");
                    }
                    else if (expr.Left is null || expr.Left is ValueExpr vsl && vsl.Value is null)
                    {
                        ToSql(ref sb, expr.Right, ref context, sqlBuilder, outputParams);
                        sb.Append(expr.Operator == LogicOperator.Equal ? " IS NULL" : " IS NOT NULL");
                    }
                    else
                    {
                        ToSql(ref sb, expr.Left, ref context, sqlBuilder, outputParams);
                        sb.Append(" ");
                        sb.Append(op);
                        sb.Append(" ");
                        ToSql(ref sb, expr.Right, ref context, sqlBuilder, outputParams);
                    }
                    break;
                case LogicOperator.Contains:
                case LogicOperator.EndsWith:
                case LogicOperator.StartsWith:
                    // 处理 LIKE 相关 的模糊查询
                    if (expr.Right is ValueExpr vs2)
                    {

                        // 使用 escape 子句转义用户输入中的特殊字符
                        ToSql(ref sb, expr.Left, ref context, sqlBuilder, outputParams);
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
                        ToSql(ref sb, expr.Left, ref context, sqlBuilder, outputParams);
                        sb.Append(" ");
                        sb.Append(op);
                        sb.Append(" ");
                        // 若右侧不是常量而是表达式，则需要生成复杂的嵌套 REPLACE 来转义特殊字符
                        var nestedRightSb = ValueStringBuilder.Create(64);
                        ToSql(ref nestedRightSb, expr.Right, ref context, sqlBuilder, outputParams);
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
                    ToSql(ref sb, expr.Left, ref context, sqlBuilder, outputParams);
                    sb.Append(" ");
                    sb.Append(op);
                    sb.Append(" ");
                    ToSql(ref sb, expr.Right, ref context, sqlBuilder, outputParams);
                    break;
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, ValueBinaryExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            string op = String.Empty;
            _valueOperatorSymbols.TryGetValue(expr.Operator, out op);
            if (expr.Operator == ValueOperator.Concat)
            {
                // 字符串拼接逻辑委托给具体的 sqlBuilder，因为不同数据库的语法差异很大（如 || vs CONCAT）
                var leftSb = ValueStringBuilder.Create(64);
                ToSql(ref leftSb, expr.Left, ref context, sqlBuilder, outputParams);
                string left = leftSb.ToString();
                leftSb.Dispose();

                var rightSb = ValueStringBuilder.Create(64);
                ToSql(ref rightSb, expr.Right, ref context, sqlBuilder, outputParams);
                string right = rightSb.ToString();
                rightSb.Dispose();

                sb.Append(sqlBuilder.BuildConcatSql(left, right));
            }
            else
            {
                ToSql(ref sb, expr.Left, ref context, sqlBuilder, outputParams);
                sb.Append(" ");
                sb.Append(op);
                sb.Append(" ");
                ToSql(ref sb, expr.Right, ref context, sqlBuilder, outputParams);
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, NotExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (expr.Operand is LogicBinaryExpr be)
            {
                // 优化：将 NOT (a = b) 转换为 a <> b，避免冗余的 NOT 关键字
                var opposite = be.Operator.Opposite();
                ToSql(ref sb, new LogicBinaryExpr(be.Left, opposite, be.Right), ref context, sqlBuilder, outputParams);
            }
            else if (expr.Operand is NotExpr inner)
            {
                // 优化：双重否定 NOT (NOT a) 转换为 a
                ToSql(ref sb, inner.Operand, ref context, sqlBuilder, outputParams);
            }
            else
            {
                sb.Append("NOT (");
                ToSql(ref sb, expr.Operand, ref context, sqlBuilder, outputParams);
                sb.Append(")");
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, UnaryExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
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
            ToSql(ref sb, expr.Operand, ref context, sqlBuilder, outputParams);
        }

        private static void ToSql(ref ValueStringBuilder sb, ValueExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
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
                        ToSql(ref sb, e, ref context, sqlBuilder, outputParams);
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

        private static void ToSql(ref ValueStringBuilder sb, PropertyExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {   
            string tableAlias = context.TableAliasName;
            if (tableAlias is null)
            {   
                // 将属性名映射为带限定符的列名，如 [User].[Name] 或 [Name]
                SqlColumn column = context.Table.GetColumn(expr.PropertyName); 
                if (column is null) throw new Exception($"Property \"{expr.PropertyName}\" does not exist in type \"{context.Table.DefinitionType.FullName}\". ");
                if (context.SingleTable)
                {
                    sb.Append(sqlBuilder.ToSqlName(column.Name));
                }
                else
                {
                    sb.Append(sqlBuilder.BuildExpression(column));
                }
            }
            else
            {
                sb.Append(sqlBuilder.ToSqlName(tableAlias));
                sb.Append(".");
                sb.Append(sqlBuilder.ToSqlName(expr.PropertyName));
            }
        }


        private static void ToSql(ref ValueStringBuilder sb, ForeignExpr foreginExpr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            TableView tableView = TableInfoProvider.Default.GetTableView(context.Table.DefinitionType);
            var joinedTable = tableView.JoinedTables.FirstOrDefault(joined => joined.Name == foreginExpr.Foreign);
            if (joinedTable == null) throw new ArgumentException($"Foregin table {foreginExpr.Foreign} not exists in {context.Table.DefinitionType}");
            SqlBuildContext foreignContext = new SqlBuildContext(joinedTable.TableDefinition, $"T{context.Sequence}", foreginExpr.TableArgs ?? context.TableArgs)
            {
                Sequence = context.Sequence + 1,
                Parent = context
            };

            string foreginTableName = sqlBuilder.ToSqlName(foreignContext.FactTableName);
            string baseTableName = sqlBuilder.ToSqlName(context.TableAliasName == null ? context.FactTableName : context.TableAliasName);

            sb.Append("EXISTS(SELECT 1 FROM ");
            sb.Append(foreginTableName);
            sb.Append(" ");
            sb.Append(sqlBuilder.ToSqlName(foreignContext.TableAliasName));
            sb.Append(" \nWHERE ");
            for (int i = 0; i < joinedTable.ForeignKeys.Count; i++)
            {
                if (i > 0) sb.Append(" AND ");
                sb.Append(sqlBuilder.ToSqlName(foreignContext.TableAliasName));
                sb.Append('.');
                sb.Append(joinedTable.ForeignPrimeKeys[i].Name);
                sb.Append(" = ");
                sb.Append(baseTableName);
                sb.Append('.');
                sb.Append(joinedTable.ForeignKeys[i].Name);
            }
            sb.Append(" AND ");
            ToSql(ref sb, foreginExpr.InnerExpr, ref foreignContext, sqlBuilder, outputParams);
            sb.Append(")");
            context.Sequence = foreignContext.Sequence;
        }

        private static void ToSql(ref ValueStringBuilder sb, FunctionExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            // 分发给具体的 sqlBuilder 生成数据库对应的函数 SQL
            var parameters = expr.Parameters;
            int count = parameters.Count;
            var args = new List<KeyValuePair<string, Expr>>(count);
            for (int i = 0; i < count; i++)
            {
                Expr p = parameters[i];
                var pSb = ValueStringBuilder.Create(64);
                ToSql(ref pSb, p, ref context, sqlBuilder, outputParams);
                string pSql = pSb.ToString();
                pSb.Dispose();
                args.Add(new KeyValuePair<string, Expr>(pSql, p));
            }
            sb.Append(sqlBuilder.BuildFunctionSql(expr.FunctionName, args));
        }


        private static void ToSql(ref ValueStringBuilder sb, LambdaExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            ToSql(ref sb, expr.InnerExpr, ref context, sqlBuilder, outputParams);
        }

        private static void ToSql(ref ValueStringBuilder sb, GenericSqlExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            sb.Append(expr.GenerateSql(ref context, sqlBuilder, outputParams));
        }

        private static void ToSql(ref ValueStringBuilder sb, LogicSet expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
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

                ToSql(ref sb, expr[i], ref context, sqlBuilder, outputParams);

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

        private static void ToSql(ref ValueStringBuilder sb, ValueSet expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            int count = expr.Count;
            if (count == 0) return;

            if (expr.JoinType == ValueJoinType.Concat)
            {
                string[] subExprs = new string[count];
                for (int i = 0; i < count; i++)
                {
                    var subSb = ValueStringBuilder.Create(64);
                    ToSql(ref subSb, expr[i], ref context, sqlBuilder, outputParams);
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
                ToSql(ref sb, expr[i], ref context, sqlBuilder, outputParams);
                first = false;
            }
            sb.Append(")");
        }

        private static void ToSql(ref ValueStringBuilder sb, SelectExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            bool isMain = sb.Length == 0;
            if (!isMain) sb.Append("(");

            //首先解析 FROM 子句中的源表或子查询，生成对应的 SQL 片段并存储在 sourceSb 中，因为 SELECT 子句的生成可能需要引用 FROM 中定义的表别名等信息，所以必须先处理 FROM 子句以正确设置上下文环境
            ValueStringBuilder sourceSb = ValueStringBuilder.Create(64);
            if (expr.Source != null)
            {
                sourceSb.Append(" FROM ");
                ToSql(ref sourceSb, expr.Source, ref context, sqlBuilder, outputParams);
            }

            sb.Append("SELECT ");
            if (expr.Selects == null || expr.Selects.Count == 0)
            {
                sb.Append("*");
            }
            else
            {
                for (int i = 0; i < expr.Selects.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    ToSql(ref sb, expr.Selects[i], ref context, sqlBuilder, outputParams);
                }
            }
            if (sourceSb.Length>0)
            {
                sb.Append(sourceSb.ToString());
            }
            if (!isMain)
            {
                sb.Append($") as T{context.Sequence}");
                context = new SqlBuildContext(context.Table, $"T{context.Sequence}", context.TableArgs)
                {
                    Sequence = ++context.Sequence,
                    Parent = context
                };
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, SelectItemExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            ToSql(ref sb, expr.Value, ref context, sqlBuilder, outputParams);
            if (!string.IsNullOrEmpty(expr.Name))
            {
                sb.Append(" AS ");
                sb.Append(sqlBuilder.ToSqlName(expr.Name));
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, WhereExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            ToSql(ref sb, expr.Source ?? Expr.Table(context.Table), ref context, sqlBuilder, outputParams);
            if (expr.Where != null)
            {
                sb.Append(" WHERE ");
                ToSql(ref sb, expr.Where, ref context, sqlBuilder, outputParams);
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, GroupByExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            ToSql(ref sb, expr.Source ?? Expr.Table(context.Table), ref context, sqlBuilder, outputParams);
            if (expr.GroupBys != null && expr.GroupBys.Count > 0)
            {
                sb.Append(" GROUP BY ");
                for (int i = 0; i < expr.GroupBys.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    ToSql(ref sb, expr.GroupBys[i], ref context, sqlBuilder, outputParams);
                }
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, HavingExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            ToSql(ref sb, expr.Source ?? Expr.Table(context.Table), ref context, sqlBuilder, outputParams);
            if (expr.Having != null)
            {
                sb.Append(" HAVING ");
                ToSql(ref sb, expr.Having, ref context, sqlBuilder, outputParams);
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, TableExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            sb.Append(sqlBuilder.BuildExpression(expr.Table, context.TableArgs));
        }

        private static void ToSql(ref ValueStringBuilder sb, AggregateFunctionExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            sb.Append(expr.FunctionName);
            sb.Append("(");
            if (expr.IsDistinct) sb.Append("DISTINCT ");
            ToSql(ref sb, expr.Expression, ref context, sqlBuilder, outputParams);
            sb.Append(")");
        }

        private static void ToSql(ref ValueStringBuilder sb, OrderByExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            ToSql(ref sb, expr.Source ?? Expr.Table(context.Table), ref context, sqlBuilder, outputParams);
            if (expr.OrderBys != null && expr.OrderBys.Count > 0)
            {
                sb.Append(" ORDER BY ");
                for (int i = 0; i < expr.OrderBys.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    ToSql(ref sb, expr.OrderBys[i].Item1, ref context, sqlBuilder, outputParams);
                    if (!expr.OrderBys[i].Item2) sb.Append(" DESC");
                }
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, SectionExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            ToSql(ref sb, expr.Source ?? Expr.Table(context.Table), ref context, sqlBuilder, outputParams);
            sb.Append(" LIMIT ");
            sb.Append(expr.Take.ToString());
            if (expr.Skip > 0)
            {
                sb.Append(" OFFSET ");
                sb.Append(expr.Skip.ToString());
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, DeleteExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            sb.Append("DELETE FROM ");
            ToSql(ref sb, expr.Source ?? Expr.Table(context.Table), ref context, sqlBuilder, outputParams);
            if (expr.Where != null)
            {
                sb.Append(" WHERE ");
                ToSql(ref sb, expr.Where, ref context, sqlBuilder, outputParams);
            }
        }

        private static void ToSql(ref ValueStringBuilder sb, UpdateExpr expr, ref SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            sb.Append("UPDATE ");
            ToSql(ref sb, expr.Source ?? Expr.Table(context.Table), ref context, sqlBuilder, outputParams);
            sb.Append(" SET ");
            for (int i = 0; i < expr.Sets.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var set = expr.Sets[i];

                SqlColumn column = context.Table.GetColumn(set.Item1);
                if (column == null) throw new Exception($"Property \"{set.Item1}\" does not exist in type \"{context.Table.DefinitionType.FullName}\".");
                sb.Append(sqlBuilder.ToSqlName(column.Name));

                sb.Append("=");
                ToSql(ref sb, set.Item2, ref context, sqlBuilder, outputParams);
            }
            if (expr.Where != null)
            {
                sb.Append(" WHERE ");
                ToSql(ref sb, expr.Where, ref context, sqlBuilder, outputParams);
            }
        }
    }
}
