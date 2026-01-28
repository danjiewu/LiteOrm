using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表达式 SQL 转换器。
    /// </summary>
    public static class ExprSqlConverter
    {
        private static readonly Dictionary<BinaryOperator, string> _operatorSymbols = new Dictionary<BinaryOperator, string>()
        {
            { BinaryOperator.Equal,"=" },
            { BinaryOperator.GreaterThan,">" },
            { BinaryOperator.LessThan,"<" },
            { BinaryOperator.Like,"LIKE" },
            { BinaryOperator.StartsWith,"LIKE" },
            { BinaryOperator.EndsWith,"LIKE" },
            { BinaryOperator.Contains,"LIKE" },
            { BinaryOperator.RegexpLike,"REGEXP_LIKE" },
            { BinaryOperator.In,"IN" },
            { BinaryOperator.NotEqual,"<>" },
            { BinaryOperator.GreaterThanOrEqual,">=" },
            { BinaryOperator.LessThanOrEqual,"<=" },
            { BinaryOperator.NotIn,"NOT IN" },
            { BinaryOperator.NotContains,"NOT LIKE" },
            { BinaryOperator.NotLike,"NOT LIKE" },
            { BinaryOperator.NotStartsWith,"NOT LIKE" },
            { BinaryOperator.NotEndsWith,"NOT LIKE" },
            { BinaryOperator.NotRegexpLike,"NOT REGEXP_LIKE" },
            { BinaryOperator.Add,"+"  },
            { BinaryOperator.Subtract,"-" },
            { BinaryOperator.Multiply,"*" },
            { BinaryOperator.Divide,"/" },
            { BinaryOperator.Concat,"||" }
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
            ToSql(ref sb, expr, context, sqlBuilder, outputParams);
            string res = sb.ToString();
            sb.Dispose();
            return res;
        }

        private static void ToSql(ref ValueStringBuilder sb, Expr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (expr is null) return;

            // 根据 Expr 的具体类型，分发到对应的 SQL 转换逻辑
            if (expr is BinaryExpr binary) ToSql(ref sb, binary, context, sqlBuilder, outputParams);
            else if (expr is UnaryExpr unary) ToSql(ref sb, unary, context, sqlBuilder, outputParams);
            else if (expr is ValueExpr value) ToSql(ref sb, value, context, sqlBuilder, outputParams);
            else if (expr is PropertyExpr prop) ToSql(ref sb, prop, context, sqlBuilder, outputParams);
            else if (expr is FunctionExpr func) ToSql(ref sb, func, context, sqlBuilder, outputParams);
            else if (expr is LambdaExpr lambda) ToSql(ref sb, lambda, context, sqlBuilder, outputParams);
            else if (expr is GenericSqlExpr generic) ToSql(ref sb, generic, context, sqlBuilder, outputParams);
            else if (expr is ForeignExpr foreign) ToSql(ref sb, foreign, context, sqlBuilder, outputParams);
            else if (expr is ExprSet set) ToSql(ref sb, set, context, sqlBuilder, outputParams);
            else
                throw new NotSupportedException($"Expression type {expr.GetType().FullName} is not supported.");
        }


        private static void ToSql(ref ValueStringBuilder sb, BinaryExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            string op = String.Empty;
            _operatorSymbols.TryGetValue(expr.Operator, out op);
            switch (expr.OriginOperator)
            {
                case BinaryOperator.In:
                    var inrightSb = ValueStringBuilder.Create(64);
                    ToSql(ref inrightSb, expr.Right, context, sqlBuilder, outputParams);
                    ReadOnlySpan<char> inright = inrightSb.AsSpan();
                    if (inright.Length == 0)
                    {
                        // IN 后面没有内容，视为空集合
                        if (!expr.Operator.IsNot()) sb.Append("0=1");
                    }
                    else
                    {
                        ToSql(ref sb, expr.Left, context, sqlBuilder, outputParams);
                        sb.Append(" ");
                        sb.Append(op);
                        sb.Append(" ");
                        sb.Append(inright);
                    }
                    inrightSb.Dispose();
                    break;
                case BinaryOperator.RegexpLike:
                    // 正则表达式匹配通常使用特定的函数调用语法
                    sb.Append(op);
                    sb.Append("(");
                    ToSql(ref sb, expr.Left, context, sqlBuilder, outputParams);
                    sb.Append(",");
                    ToSql(ref sb, expr.Right, context, sqlBuilder, outputParams);
                    sb.Append(")");
                    break;
                case BinaryOperator.Equal:
                    // 特殊处理 NULL 值的比较：在 SQL 中 a = NULL 始终为假，必须使用 IS NULL
                    if (expr.Right is null || expr.Right is ValueExpr vs && vs.Value is null)
                    {
                        ToSql(ref sb, expr.Left, context, sqlBuilder, outputParams);
                        sb.Append(expr.Operator == BinaryOperator.Equal ? " IS NULL" : " IS NOT NULL");
                    }
                    else if (expr.Left is null || expr.Left is ValueExpr vsl && vsl.Value is null)
                    {
                        ToSql(ref sb, expr.Right, context, sqlBuilder, outputParams);
                        sb.Append(expr.Operator == BinaryOperator.Equal ? " IS NULL" : " IS NOT NULL");
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
                case BinaryOperator.Concat:
                    // 字符串拼接逻辑委托给具体的 sqlBuilder，因为不同数据库的语法差异很大（如 || vs CONCAT）
                    sb.Append(sqlBuilder.BuildConcatSql(expr.Left.ToSql(context, sqlBuilder, outputParams), expr.Right.ToSql(context, sqlBuilder, outputParams)));
                    break;
                case BinaryOperator.Contains:
                case BinaryOperator.EndsWith:
                case BinaryOperator.StartsWith:
                    // 处理 LIKE 相关的模糊查询
                    if (expr.Right is ValueExpr vs2)
                    {
                        // 参数化处理：将包含通配符的字符串作为参数传入，避免 SQL 注入
                        string paramName = outputParams.Count.ToString();
                        string val = sqlBuilder.ToSqlLikeValue(vs2.Value?.ToString());
                        switch (expr.OriginOperator)
                        {
                            case BinaryOperator.StartsWith:
                                val = $"{val}%"; break;
                            case BinaryOperator.EndsWith:
                                val = $"%{val}"; break;
                            case BinaryOperator.Contains:
                                val = $"%{val}%"; break;
                        }
                        outputParams.Add(new KeyValuePair<string, object>(sqlBuilder.ToParamName(paramName), val));
                        // 使用 escape 子句转义用户输入中的特殊字符
                        ToSql(ref sb, expr.Left, context, sqlBuilder, outputParams);
                        sb.Append(" ");
                        sb.Append(op);
                        sb.Append(" ");
                        sb.Append(sqlBuilder.ToSqlParam(paramName));
                        sb.Append(" ESCAPE '");
                        sb.Append(Const.LikeEscapeChar);
                        sb.Append("'");
                    }
                    else
                    {
                        // 若右侧不是常量而是表达式，则需要生成复杂的嵌套 REPLACE 来转义特殊字符
                        string nestedRight = expr.Right.ToSql(context, sqlBuilder, outputParams);
                        string right = $"REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE({nestedRight},'{Const.LikeEscapeChar}', '{Const.LikeEscapeChar}{Const.LikeEscapeChar}'),'_', '{Const.LikeEscapeChar}_'),'%', '{Const.LikeEscapeChar}%'),'/', '{Const.LikeEscapeChar}/'),'[', '{Const.LikeEscapeChar}['),']', '{Const.LikeEscapeChar}]')";
                        switch (expr.OriginOperator)
                        {
                            case BinaryOperator.StartsWith:
                                right = sqlBuilder.BuildConcatSql(right, "%"); break;
                            case BinaryOperator.EndsWith:
                                right = sqlBuilder.BuildConcatSql("%", right); break;
                            case BinaryOperator.Contains:
                                right = sqlBuilder.BuildConcatSql("%", right, "%"); break;
                        }
                        ToSql(ref sb, expr.Left, context, sqlBuilder, outputParams);
                        sb.Append(" ");
                        sb.Append(op);
                        sb.Append(" ");
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

        private static void ToSql(ref ValueStringBuilder sb, UnaryExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            // 处理一元操作符
            switch (expr.Operator)
            {
                case UnaryOperator.Not:
                    sb.Append("NOT ");
                    break;
                case UnaryOperator.Nagive:
                    sb.Append("-");
                    break;
                case UnaryOperator.BitwiseNot:
                    sb.Append("~");
                    break;
            }
            ToSql(ref sb, expr.Operand, context, sqlBuilder, outputParams);
        }

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
                // 数值类型通常直接以字面量形式输出，较为高效
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

        private static void ToSql(ref ValueStringBuilder sb, PropertyExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            // 将属性名映射为带限定符的列名，如 [User].[Name] 或 [Name]
            SqlColumn column = context.Table.GetColumn(expr.PropertyName);
            if (column is null) throw new Exception($"Property \"{expr.PropertyName}\" does not exist in type \"{context.Table.DefinitionType.FullName}\". ");
            string tableAlias = context.TableAliasName;
            sb.Append(tableAlias is null ? (context.SingleTable ? sqlBuilder.ToSqlName(column.Name) : sqlBuilder.BuildExpression(column)) : $"[{tableAlias}].[{column.Name}]");
        }


        private static void ToSql(ref ValueStringBuilder sb, ForeignExpr foreginExpr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            TableView tableView = TableInfoProvider.Default.GetTableView(context.Table.DefinitionType);
            var joinedTable = tableView.JoinedTables.FirstOrDefault(joined => joined.Name == foreginExpr.Foreign);
            if (joinedTable == null) throw new ArgumentException($"Foregin table {foreginExpr.Foreign} not exists in {context.Table.DefinitionType}");
            SqlBuildContext foreignContext = new SqlBuildContext(joinedTable.TableDefinition, $"T{context.Sequence}", context.TableNameArgs)
            {
                Sequence = context.Sequence + 1
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
            ToSql(ref sb, foreginExpr.InnerExpr, foreignContext, sqlBuilder, outputParams);
            sb.Append(")");
            context.Sequence = foreignContext.Sequence;
        }

        private static void ToSql(ref ValueStringBuilder sb, FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            // 分发给具体的 sqlBuilder 生成数据库对应的函数 SQL
            var parameters = expr.Parameters;
            int count = parameters.Count;
            var args = new List<KeyValuePair<string, Expr>>(count);
            for (int i = 0; i < count; i++)
            {
                Expr p = parameters[i];
                args.Add(new KeyValuePair<string, Expr>(p.ToSql(context, sqlBuilder, outputParams), p));
            }
            sb.Append(sqlBuilder.BuildFunctionSql(expr.FunctionName, args));
        }


        private static void ToSql(ref ValueStringBuilder sb, LambdaExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            ToSql(ref sb, expr.InnerExpr, context, sqlBuilder, outputParams);
        }

        private static void ToSql(ref ValueStringBuilder sb, GenericSqlExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            sb.Append(expr.GenerateSql(context, sqlBuilder, outputParams));
        }

        private static void ToSql(ref ValueStringBuilder sb, ExprSet expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            int count = expr.Count;
            if (count == 0) return;

            if (expr.JoinType == ExprJoinType.Concat)
            {
                string[] subExprs = new string[count];
                for (int i = 0; i < count; i++) subExprs[i] = expr[i].ToSql(context, sqlBuilder, outputParams);
                sb.Append(sqlBuilder.BuildConcatSql(subExprs));
                return;
            }

            bool isLogical = expr.JoinType == ExprJoinType.And || expr.JoinType == ExprJoinType.Or;
            string joinStr = expr.JoinType switch
            {
                ExprJoinType.And => " AND ",
                ExprJoinType.Or => " OR ",
                _ => ","
            };

            if (count > 1 || !isLogical) sb.Append("(");

            bool first = true;
            for (int i = 0; i < count; i++)
            {
                int lenBefore = sb.Length;
                if (!first) sb.Append(joinStr);
                int lenWithJoin = sb.Length;

                ToSql(ref sb, expr[i], context, sqlBuilder, outputParams);

                if (sb.Length == lenWithJoin)
                {
                    // Nothing was appended, revert joinStr
                    if (!first) sb.Length = lenBefore;
                }
                else
                {
                    first = false;
                }
            }
            if (count > 1 || !isLogical) sb.Append(")");
        }
    }
}
