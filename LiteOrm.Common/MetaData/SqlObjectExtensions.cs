using System;
using System.Collections.Generic;

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
            if (sqlObject == null) return null;
            var sb = ValueStringBuilder.Create(128);
            ToSql(sqlObject, ref sb, context, sqlBuilder);
            string result = sb.ToString();
            sb.Dispose();
            return result;
        }

        /// <summary>
        /// 将 SqlObject 转换为 SQL 字符串片段。
        /// </summary>
        public static void ToSql(this SqlObject sqlObject, ref ValueStringBuilder sb, SqlBuildContext context, ISqlBuilder sqlBuilder)
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
                ToSql(ref sb, tableView, context, sqlBuilder);
                return;
            }

            if (sqlObject is JoinedTable joinedTable)
            {
                ToSql(ref sb, joinedTable, context, sqlBuilder);
                return;
            }
            if (sqlObject is SqlTable sqlTable)
            {
                sb.Append(sqlBuilder.ToSqlName(context.FormatTableName(sqlTable.Name)));
                return;
            }

            sb.Append(sqlBuilder.ToSqlName(sqlObject.Name));
        }

        /// <summary>
        /// 处理 SqlColumn 列引用。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, SqlColumn column, SqlBuildContext context, ISqlBuilder sqlBuilder)
        {
            if (column.Table != null)
            {
                sb.Append(sqlBuilder.ToSqlName(context.FormatTableName(column.Table.Name)));
                sb.Append('.');
            }
            else if (context?.Table != null)
            {
                sb.Append(sqlBuilder.ToSqlName(context.FormatTableName(context.Table.Name)));
                sb.Append('.');
            }
            sb.Append(sqlBuilder.ToSqlName(column.Name));
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
            var tableName = columnRef.Table?.Name ?? context.DefaultTableAliasName;
            sb.Append(sqlBuilder.ToSqlName(tableName));
            sb.Append('.');
            if (columnRef.Column != null)
            {
                sb.Append(sqlBuilder.ToSqlName(columnRef.Column.Name));
            }
        }

        /// <summary>
        /// 处理视图表（TableView）转换为SQL片段。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, TableView tableView, SqlBuildContext context, ISqlBuilder sqlBuilder)
        {
            if (tableView == null) return;

            sb.Append(sqlBuilder.ToSqlName(context.FormatTableName(tableView.Definition.Name)));
            sb.Append(" ");
            sb.Append(sqlBuilder.ToSqlName(tableView.Name));
            foreach (var joined in tableView.JoinedTables)
            {
                if (joined.Used)
                {
                    joined.ToSql(ref sb, context, sqlBuilder);
                }
            }
        }

        /// <summary>
        /// 处理联合表的 SQL 生成。
        /// </summary>
        private static void ToSql(ref ValueStringBuilder sb, JoinedTable joined, SqlBuildContext context, ISqlBuilder sqlBuilder)
        {
            if (joined == null) return;

            sb.Append("\n");
            sb.Append(joined.JoinType.ToString().ToUpper());
            sb.Append(" JOIN ");
            sb.Append(sqlBuilder.ToSqlName(context.FormatTableName(joined.TableDefinition.Name)));
            sb.Append(" ");
            sb.Append(sqlBuilder.ToSqlName(joined.Name));
            sb.Append(" ON ");

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
            if (!string.IsNullOrEmpty(joined.FilterExpression))
            {
                if (!isFirst) sb.Append(" AND ");
                sb.Append(joined.FilterExpression);
            }
        }
    }
}
