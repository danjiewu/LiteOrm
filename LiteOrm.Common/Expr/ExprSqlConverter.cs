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

            if (expr is BinaryExpr binary) return ToSql(binary, context, sqlBuilder, outputParams);
            if (expr is UnaryExpr unary) return ToSql(unary, context, sqlBuilder, outputParams);
            if (expr is ValueExpr value) return ToSql(value, context, sqlBuilder, outputParams);
            if (expr is PropertyExpr prop) return ToSql(prop, context, sqlBuilder, outputParams);
            if (expr is FunctionExpr func) return ToSql(func, context, sqlBuilder, outputParams);
            if (expr is LambdaExpr lambda) return ToSql(lambda, context, sqlBuilder, outputParams);
            if (expr is GenericSqlExpr generic) return ToSql(generic, context, sqlBuilder, outputParams);
            if (expr is ExprSet set) return ToSql(set, context, sqlBuilder, outputParams);

            throw new NotSupportedException($"Expression type {expr.GetType().FullName} is not supported.");
        }

        private static string ToSql(BinaryExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            string op = String.Empty;
            _operatorSymbols.TryGetValue(expr.Operator, out op);
            switch (expr.OriginOperator)
            {
                case BinaryOperator.RegexpLike:
                    return $"{op}({expr.Left.ToSql(context, sqlBuilder, outputParams)},{expr.Right.ToSql(context, sqlBuilder, outputParams)})";
                case BinaryOperator.Equal:
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
                    return sqlBuilder.BuildConcatSql(expr.Left.ToSql(context, sqlBuilder, outputParams), expr.Right.ToSql(context, sqlBuilder, outputParams));
                case BinaryOperator.Contains:
                case BinaryOperator.EndsWith:
                case BinaryOperator.StartsWith:
                    if (expr.Right is ValueExpr vs2)
                    {
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
                        return $@"{expr.Left.ToSql(context, sqlBuilder, outputParams)} {op} {sqlBuilder.ToSqlParam(paramName)} escape '{Const.LikeEscapeChar}'";
                    }
                    else
                    {
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
            if (expr.Value is IEnumerable enumerable && !(expr.Value is string))
            {
                StringBuilder sb = new StringBuilder();
                foreach (var item in enumerable)
                {
                    if (sb.Length > 0) sb.Append(",");
                    if (item is Expr s)
                    {
                        sb.Append(s.ToSql(context, sqlBuilder, outputParams));
                    }
                    else
                    {
                        string paramName = outputParams.Count.ToString();
                        outputParams.Add(new(sqlBuilder.ToParamName(paramName), item));
                        sb.Append(sqlBuilder.ToSqlParam(paramName));
                    }
                }
                return $"({sb})";
            }
            else
            {
                string paramName = outputParams.Count.ToString();
                outputParams.Add(new(sqlBuilder.ToParamName(paramName), expr.Value));
                return sqlBuilder.ToSqlParam(paramName);
            }
        }

        private static string ToSql(PropertyExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            SqlColumn column = context.Table.GetColumn(expr.PropertyName);
            if (column is null) throw new Exception($"Property \"{expr.PropertyName}\" does not exist in type \"{context.Table.DefinitionType.FullName}\". ");
            string tableAlias = context.TableAliasName;
            return tableAlias is null ? (context.SingleTable ? column.FormattedName(sqlBuilder) : column.FormattedExpression(sqlBuilder)) : $"[{tableAlias}].[{column.Name}]";
        }

        private static string ToSql(FunctionExpr expr, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
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
            if (expr.JoinType == ExprJoinType.Concat)
                return sqlBuilder.BuildConcatSql(expr.Items.Select(s => s.ToSql(context, sqlBuilder, outputParams)).ToArray());
            string joinStr;
            switch (expr.JoinType)
            {
                case ExprJoinType.And: joinStr = " AND "; break;
                case ExprJoinType.Or: joinStr = " OR "; break;
                case ExprJoinType.Concat: joinStr = " || "; break;
                default: joinStr = ","; break;
            }
            if (expr.IsValue)
                return $"({String.Join(joinStr, expr.Items.Select(s => s.ToSql(context, sqlBuilder, outputParams)))})";
            else
                return $"({String.Join(joinStr, expr.Items.Select(s => s.ToSql(context, sqlBuilder, outputParams)).Where(s => !String.IsNullOrEmpty(s)))})";
        }
    }
}
