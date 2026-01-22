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

            // 根据 Expr 的具体类型，分发到对应的 SQL 转换逻辑
            if (expr is BinaryExpr binary) return ToSql(binary, context, sqlBuilder, outputParams);
            if (expr is UnaryExpr unary) return ToSql(unary, context, sqlBuilder, outputParams);
            if (expr is ValueExpr value) return ToSql(value, context, sqlBuilder, outputParams);
            if (expr is PropertyExpr prop) return ToSql(prop, context, sqlBuilder, outputParams);
            if (expr is FunctionExpr func) return ToSql(func, context, sqlBuilder, outputParams);
            if (expr is LambdaExpr lambda) return ToSql(lambda, context, sqlBuilder, outputParams);
            if (expr is GenericSqlExpr generic) return ToSql(generic, context, sqlBuilder, outputParams);
            if (expr is ForeignExpr foreign) return ToSql(foreign, context, sqlBuilder, outputParams);
            if (expr is ExprSet set) return ToSql(set, context, sqlBuilder, outputParams);

            throw new NotSupportedException($"Expression type {expr.GetType().FullName} is not supported.");
        }

        private static string ToSql(BinaryExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            string op = String.Empty;
            _operatorSymbols.TryGetValue(expr.Operator, out op);
            switch (expr.OriginOperator)
            {
                case BinaryOperator.In:
                    string inleft = expr.Left.ToSql(context, sqlBuilder, outputParams);
                    string inright = expr.Right.ToSql(context, sqlBuilder, outputParams);
                    // 处理 IN () 空集合的特殊情况
                    if (string.IsNullOrWhiteSpace(inright) || inright.Trim() == "()") return expr.Operator.IsNot() ? string.Empty : "0=1";
                    else return $"{inleft} {op} {inright}";
                case BinaryOperator.RegexpLike:
                    // 正则表达式匹配通常使用特定的函数调用语法
                    return $"{op}({expr.Left.ToSql(context, sqlBuilder, outputParams)},{expr.Right.ToSql(context, sqlBuilder, outputParams)})";
                case BinaryOperator.Equal:
                    // 特殊处理 NULL 值的比较：在 SQL 中 a = NULL 始终为假，必须使用 IS NULL
                    if (expr.Right is null || expr.Right is ValueExpr vs && vs.Value is null)
                    {
                        if (expr.Operator == BinaryOperator.Equal)
                            return $"{expr.Left.ToSql(context, sqlBuilder, outputParams)} is null";
                        else
                            return $"{expr.Left.ToSql(context, sqlBuilder, outputParams)} is not null";
                    }
                    else if (expr.Left is null || expr.Left is ValueExpr vsl && vsl.Value is null)
                    {
                        if (expr.Operator == BinaryOperator.Equal)
                            return $"{expr.Right.ToSql(context, sqlBuilder, outputParams)} is null";
                        else
                            return $"{expr.Right.ToSql(context, sqlBuilder, outputParams)} is not null";
                    }
                    else
                        return $"{expr.Left.ToSql(context, sqlBuilder, outputParams)} {op} {expr.Right.ToSql(context, sqlBuilder, outputParams)}";
                case BinaryOperator.Concat:
                    // 字符串拼接逻辑委托给具体的 sqlBuilder，因为不同数据库的语法差异很大（如 || vs CONCAT）
                    return sqlBuilder.BuildConcatSql(expr.Left.ToSql(context, sqlBuilder, outputParams), expr.Right.ToSql(context, sqlBuilder, outputParams));
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
                        return $@"{expr.Left.ToSql(context, sqlBuilder, outputParams)} {op} {sqlBuilder.ToSqlParam(paramName)} escape '{Const.LikeEscapeChar}'";
                    }
                    else
                    {
                        // 若右侧不是常量而是表达式，则需要生成复杂的嵌套 REPLACE 来转义特殊字符
                        string left = expr.Left.ToSql(context, sqlBuilder, outputParams);
                        string right = $"REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE({expr.Right.ToSql(context, sqlBuilder, outputParams)},'{Const.LikeEscapeChar}', '{Const.LikeEscapeChar}{Const.LikeEscapeChar}'),'_', '{Const.LikeEscapeChar}_'),'%', '{Const.LikeEscapeChar}%'),'/', '{Const.LikeEscapeChar}/'),'[', '{Const.LikeEscapeChar}['),']', '{Const.LikeEscapeChar}]')";
                        switch (expr.OriginOperator)
                        {
                            case BinaryOperator.StartsWith:
                                right = sqlBuilder.BuildConcatSql(right, "%"); break;
                            case BinaryOperator.EndsWith:
                                right = sqlBuilder.BuildConcatSql("%", right); break;
                            case BinaryOperator.Contains:
                                right = sqlBuilder.BuildConcatSql("%", right, "%"); break;
                        }
                        return $@"{left} {op} {right} escape '{Const.LikeEscapeChar}'";
                    }
                default:
                    return $"{expr.Left.ToSql(context, sqlBuilder, outputParams)} {op} {expr.Right.ToSql(context, sqlBuilder, outputParams)}";
            }
        }

        private static string ToSql(UnaryExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            // 处理一元操作符
            switch (expr.Operator)
            {
                case UnaryOperator.Not:
                    return $"NOT {expr.Operand.ToSql(context, sqlBuilder, outputParams)}";
                case UnaryOperator.Nagive:
                    return $"-{expr.Operand.ToSql(context, sqlBuilder, outputParams)}";
                case UnaryOperator.BitwiseNot:
                    return $"~{expr.Operand.ToSql(context, sqlBuilder, outputParams)}";
                default:
                    return expr.Operand.ToSql(context, sqlBuilder, outputParams);
            }
        }

        private static string ToSql(ValueExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (expr.Value == null)
            {
                return "NULL";
            }
            else if (expr.IsConst && expr.Value is bool b)
            {
                return b ? "1" : "0";
            }
            else if (expr.IsConst && expr.Value.GetType().IsPrimitive)
            {
                // 数值类型通常直接以字面量形式输出，较为高效
                return expr.Value.ToString();
            }
            else if (expr.Value is IEnumerable enumerable && !(expr.Value is string))
            {
                // 处理 IN (...) 集合
                List<string> strs = new List<string>();
                foreach (var item in enumerable)
                {
                    if (item is Expr e)
                    {
                        string s = e.ToSql(context, sqlBuilder, outputParams);
                        if (!string.IsNullOrWhiteSpace(s)) strs.Add(s);
                    }
                    else
                    {
                        // 对集合中的每个元素进行参数化
                        string paramName = outputParams.Count.ToString();
                        outputParams.Add(new(sqlBuilder.ToParamName(paramName), item));
                        strs.Add(sqlBuilder.ToSqlParam(paramName));
                    }
                }
                return strs.Count == 0 ? string.Empty : $"({string.Join(",", strs)})";
            }
            else
            {
                // 其他类型（如字符串、日期）通过参数化处理以保证安全
                string paramName = outputParams.Count.ToString();
                outputParams.Add(new(sqlBuilder.ToParamName(paramName), expr.Value));
                return sqlBuilder.ToSqlParam(paramName);
            }
        }

        private static string ToSql(PropertyExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            // 将属性名映射为带限定符的列名，如 [User].[Name] 或 [Name]
            SqlColumn column = context.Table.GetColumn(expr.PropertyName);
            if (column is null) throw new Exception($"Property \"{expr.PropertyName}\" does not exist in type \"{context.Table.DefinitionType.FullName}\". ");
            string tableAlias = context.TableAliasName;
            return tableAlias is null ? (context.SingleTable ? column.FormattedName(sqlBuilder) : column.FormattedExpression(sqlBuilder)) : $"[{tableAlias}].[{column.Name}]";
        }

        private static string ToSql(ForeignExpr foreginExpr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            // 生成外键表达式的 SQL
            SqlColumn column = context.Table.GetColumn(foreginExpr.Foreign);
            if (column is null) throw new InvalidOperationException($"Foreign key \"{foreginExpr.Foreign}\" not found.");
            ForeignTable foreignTable = column.ForeignTable;
            if (foreignTable == null) throw new Exception($"Foreign key \"{foreginExpr.Foreign}\" does not reference a valid foreign type.");
            TableDefinition foreignTableDef = TableInfoProvider.Default.GetTableDefinition(foreignTable.ForeignType);
            if (foreignTableDef is null) throw new Exception($"Foreign table \"{foreginExpr.Foreign}\" not found.");
            SqlBuildContext foreignContext = new SqlBuildContext()
            {
                Table = foreignTableDef,
                Sequence = context.Sequence + 1,
                TableAliasName = $"T{context.Sequence}",
            };
            string innerSql = foreginExpr.InnerExpr.ToSql(foreignContext, sqlBuilder, outputParams);
            string columnSql = context.TableAliasName == null ? column.FormattedExpression(sqlBuilder) : sqlBuilder.ToSqlName($"{context.TableAliasName}.{column.Name}");
            string keySql = foreignTableDef.Keys.Count == 1 ?
                sqlBuilder.ToSqlName($"{foreignContext.TableAliasName}.{foreignTableDef.Keys[0].Name}") :
                throw new InvalidOperationException("Foreign table has multiple keys.");
            context.Sequence = foreignContext.Sequence;
            return $"EXISTS(SELECT 1 FROM {foreignTableDef.FormattedName(sqlBuilder)} {foreignContext.TableAliasName} " +
                $"\nWHERE {columnSql} = {keySql} AND {innerSql})";
        }

        private static string ToSql(FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            // 分发给具体的 sqlBuilder 生成数据库对应的函数 SQL
            return sqlBuilder.BuildFunctionSql(expr.FunctionName, expr.Parameters.Select(p => new KeyValuePair<string, Expr>(p.ToSql(context, sqlBuilder, outputParams), p)).ToList());
        }

        private static string ToSql(LambdaExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            return expr.InnerExpr.ToSql(context, sqlBuilder, outputParams);
        }

        private static string ToSql(GenericSqlExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            return expr.GenerateSql(context, sqlBuilder, outputParams);
        }

        private static string ToSql(ExprSet expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            var subExprs = expr.Items.Select(s => s.ToSql(context, sqlBuilder, outputParams)).Where(s => !String.IsNullOrEmpty(s)).ToList();
            if (subExprs.Count == 0) return string.Empty;
            switch (expr.JoinType)
            {
                case ExprJoinType.And:
                case ExprJoinType.Or:
                    if (subExprs.Count == 1) return subExprs[0];
                    else return $"({String.Join($" {expr.JoinType} ", subExprs)})";
                case ExprJoinType.Concat:
                    // 字符串拼接逻辑委托给具体的 sqlBuilder，因为不同数据库的语法差异很大（如 || vs CONCAT）
                    return sqlBuilder.BuildConcatSql(subExprs.ToArray());
                default:
                    return $"({String.Join($",", subExprs)})";
            }
        }
    }
}
