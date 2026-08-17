using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// SqlObject 类型的扩展方法，用于将 SqlObject 转换为 SQL 片段。
    /// </summary>
    public static class SqlObjectExtensions
    {
        /// <summary>
        /// 将 SqlObject 转换为 SQL 字符串片段。
        /// </summary>
        public static string ToSql(this SqlObject sqlObject, SqlBuildContext context, ISqlBuilder sqlBuilder)
        {
            if (sqlObject == null) return null!;
            var sb = ValueStringBuilder.Create(128);
            ToSql(sqlObject, ref sb, context, sqlBuilder);
            string result = sb.ToString();
            sb.Dispose();
            return result;
        }

        /// <summary>
        /// 将 SqlObject 转换为 SQL 字符串片段。
        /// </summary>
        public static void ToSql(this SqlObject sqlObject, ref ValueStringBuilder sb, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>>? outputParams = null)
        {
            if (sqlObject == null) return;

            if (sqlObject is ColumnRef columnRef)
            {
                ToSql(ref sb, columnRef, context, sqlBuilder);
                return;
            }
            if (sqlObject is ForeignColumn foreignColumn)
            {
                ToSql(ref sb, foreignColumn, context, sqlBuilder);
                return;
            }
            if (sqlObject is SqlColumn sqlColumn)
            {
                ToSql(ref sb, sqlColumn, context, sqlBuilder);
                return;
            }
            if (sqlObject is TableView tableView)
            {
                ToSql(ref sb, tableView, context, sqlBuilder, outputParams);
                return;
            }

            if (sqlObject is JoinedTable joinedTable)
            {
                ToSql(ref sb, joinedTable, context, sqlBuilder, outputParams);
                return;
            }
            if (sqlObject is SqlTable sqlTable)
            {
                sb.Append(sqlBuilder.ToSqlName(context.FormatTableName(sqlTable.Name ?? string.Empty)));
                return;
            }

            sb.Append(sqlBuilder.ToSqlName(sqlObject.Name ?? string.Empty));
        }

        /// <summary>
        /// 处理 SqlColumn 列引用。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, SqlColumn column, SqlBuildContext context, ISqlBuilder sqlBuilder)
        {
            // 计算列（非实际列）：按表达式渲染，不输出物理列名
            if (column is ColumnDefinition columnDef && columnDef.IsComputed && columnDef.HasExpression)
            {
                columnDef.RenderComputedExpression(ref sb, context, sqlBuilder);
                return;
            }
            if (!context.SingleTable)
            {
                if (column.Table == null || column.Table == context.Table)
                {
                    if (context.DefaultTableAliasName != null)
                    {
                        sb.Append(sqlBuilder.ToSqlName(context.DefaultTableAliasName));
                        sb.Append('.');
                    }
                }
                else if (column.Table != null)
                {
                    sb.Append(sqlBuilder.ToSqlName(context.FormatTableName(column.Table.Name ?? string.Empty)));
                    sb.Append('.');
                }
            }
            sb.Append(sqlBuilder.ToSqlName(column.Name ?? string.Empty));
        }


        /// <summary>
        /// 渲染计算列表达式，直接写入 <see cref="ValueStringBuilder"/>。优先使用 <see cref="ColumnDefinition.ExpressionExpr"/>（<see cref="ValueTypeExpr"/> 形式），
        /// 其次使用 <see cref="ColumnDefinition.Expression"/>（字符串形式），整体以括号包裹。
        /// <para>
        /// <see cref="ValueTypeExpr"/> 形式仅允许不生成参数的固定 SQL 表达式（如属性引用、算术运算、函数调用）；
        /// 若渲染过程中产生了参数化值，将抛出 <see cref="NotSupportedException"/>。
        /// </para>
        /// </summary>
        /// <param name="column">计算列定义。</param>
        /// <param name="sb">目标字符串构建器。</param>
        /// <param name="context">SQL 构建上下文。</param>
        /// <param name="sqlBuilder">SQL 构建器。</param>
        public static void RenderComputedExpression(this ColumnDefinition column, ref ValueStringBuilder sb, SqlBuildContext context, ISqlBuilder sqlBuilder)
        {
            // 优先使用 ValueTypeExpr 形式
            if (column.ExpressionExpr is not null)
            {
                var paramList = new List<Param>();
                sb.Append('(');
                column.ExpressionExpr.ToSql(ref sb, context, sqlBuilder, paramList);
                sb.Append(')');
                if (paramList.Count > 0)
                    throw new NotSupportedException(
                        $"ColumnDefinition.ExpressionExpr for column '{column.Name}' produced {paramList.Count} parameter(s); " +
                        $"only fixed SQL expressions (property references, constants, functions, arithmetic) are allowed for computed columns.");
                return;
            }

            // 字符串形式
            string expression = column.Expression ?? string.Empty;
            if (expression.Length == 0) return;
            sb.Append('(');
            if (expression.IndexOf('{') >= 0)
            {
                var rendered = Regex.Replace(expression, @"\{([^{}]+)\}", match =>
                {
                    string propertyName = match.Groups[1].Value;
                    SqlColumn? refColumn = column.Table?.GetColumn(propertyName);
                    if (refColumn != null) return refColumn.ToSql(context, sqlBuilder);
                    return sqlBuilder.ToSqlName(propertyName);
                });
                sb.Append(rendered);
            }
            else
            {
                sb.Append(expression);
            }
            sb.Append(')');
        }

        /// <summary>
        /// 处理外键列引用。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, ForeignColumn foreignColumn, SqlBuildContext context, ISqlBuilder sqlBuilder)
        {
            if (foreignColumn.TargetColumn != null)
            {
                ToSql(ref sb, foreignColumn.TargetColumn, context, sqlBuilder);
            }
        }

        /// <summary>
        /// 处理列引用。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, ColumnRef columnRef, SqlBuildContext context, ISqlBuilder sqlBuilder)
        {
            var tableName = columnRef.Table?.Name ?? context.DefaultTableAliasName ?? string.Empty;
            sb.Append(sqlBuilder.ToSqlName(tableName));
            sb.Append('.');
            if (columnRef.Column != null)
            {
                sb.Append(sqlBuilder.ToSqlName(columnRef.Column.Name ?? string.Empty));
            }
        }

        /// <summary>
        /// 处理视图表（TableView）转换为SQL片段。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, TableView tableView, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>>? outputParams)
        {
            if (tableView == null) return;

            sb.Append(sqlBuilder.ToSqlName(context.FormatTableName(tableView.Definition.Name ?? string.Empty)));
            sb.Append(" ");
            if (tableView == context.Table)
                sb.Append(sqlBuilder.ToSqlName(context.DefaultTableAliasName ?? string.Empty));
            else
                sb.Append(sqlBuilder.ToSqlName(tableView.Name ?? string.Empty));
            foreach (var joined in tableView.JoinedTables)
            {
                if (joined.Used)
                {
                    joined.ToSql(ref sb, context, sqlBuilder, outputParams);
                }
            }
        }

        /// <summary>
        /// 处理联合表的 SQL 生成。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, JoinedTable joined, SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>>? outputParams)
        {
            if (joined == null) return;

            sb.Append("\n");
            sb.Append(joined.JoinType.ToString().ToUpper());
            sb.Append(" JOIN ");
            sb.Append(sqlBuilder.ToSqlName(context.FormatTableName(joined.TableDefinition.Name ?? string.Empty)));
            sb.Append(" ");
            sb.Append(sqlBuilder.ToSqlName(joined.Name ?? string.Empty));
            sb.Append(" ON ");
            context.AddTableAlias(joined.Name, joined.TableDefinition);

            bool isFirst = true;
            int count = joined.ForeignKeys.Count;
            for (int i = 0; i < count; i++)
            {
                if (!isFirst) sb.Append(" AND ");
                var foreignKey = joined.ForeignKeys[i];
                foreignKey.ToSql(ref sb, context, sqlBuilder);
                sb.Append(" = ");
                joined.ForeignPrimeKeys[i].ToSql(ref sb, context, sqlBuilder);
                isFirst = false;
            }
        }
    }
}
