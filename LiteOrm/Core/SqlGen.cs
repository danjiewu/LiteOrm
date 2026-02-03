using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LiteOrm
{
    /// <summary>
    /// SQL 生成器类，负责将抽象表达式树 (Expr) 转换为针对特定数据库方言的 SQL 语句。
    /// </summary>
    /// <remarks>
    /// 该类作为表达式解析的入口，根据目标对象类型获取表元数据，并调用相应的 SqlBuilder 进行翻译。
    /// 支持参数化查询以防止 SQL 注入。
    /// </remarks>
    public class SqlGen
    {
        /// <summary>
        /// 初始化 <see cref="SqlGen"/> 类的新实例。
        /// </summary>
        /// <param name="objectType">关联的实体对象类型，用于解析表名和列名。</param>
        public SqlGen(Type objectType)
        {
            ObjectType = objectType;
        }

        /// <summary>
        /// 获取当前操作的目标实体类型。
        /// </summary>
        public Type ObjectType { get; }

        /// <summary>
        /// 获取或设置表别名（例如在多表连接查询中使用）。
        /// </summary>
        public string AliasName { get; set; }

        /// <summary>
        /// 获取或设置表名模板参数（通常用于分表场景，如 Table_{0}）。
        /// </summary>
        public string[] TableArgs { get; set; }

        /// <summary>
        /// 获取实体的元数据定义。
        /// </summary>
        public SqlTable Table => TableInfoProvider.Default.GetTableView(ObjectType);

        /// <summary>
        /// 将逻辑表达式转换为具体的 SQL 生成结果。
        /// </summary>
        /// <param name="expr">要转换的表达式（如条件、计算等）。</param>
        /// <returns>包含 SQL 语句文本和对应参数集合的 <see cref="SqlGenResult"/>。</returns>
        /// <exception cref="ArgumentNullException">当 expr 为空时抛出。</exception>
        public SqlGenResult ToSql(Expr expr)
        {
            if (expr is null) throw new ArgumentNullException(nameof(expr));

            List<KeyValuePair<string, object>> paramList = new List<KeyValuePair<string, object>>();
            // 获取实体的元数据定义
            SqlTable table = Table;

            // 构造解析上下文
            var context = new SqlBuildContext(table, AliasName, TableArgs);

            // 获取对应的数据库构建器（如 SQLiteBuilder, SqlServerBuilder）
            var sqlBuilder = SqlBuilderFactory.Instance.GetSqlBuilder(table.Definition.DataProviderType);
            // 执行递归解析
            string sql = expr.ToSql(context, sqlBuilder, paramList);

            return new SqlGenResult(sql, paramList);
        }

        /// <summary>
        /// 生成 SELECT 语句结果。
        /// </summary>
        /// <param name="expr">查询表达式，可以是条件 LogicExpr 或结构化 SelectExpr 等。</param>
        /// <returns>SQL 生成结果。</returns>
        public SqlGenResult ToSelectSql(Expr expr = null)
        {
            SelectSourceExpr selectable;
            if (expr is null)
                selectable = new TableExpr(Table);
            else if (expr is LogicExpr logic)
                selectable = new WhereExpr { Source = new TableExpr(Table), Where = logic };
            else if (expr is SelectSourceExpr s)
                selectable = s;
            else
                throw new NotSupportedException($"Expression type {expr.GetType().Name} is not supported for SELECT.");

            SelectExpr selectExpr = selectable as SelectExpr ?? new SelectExpr { Source = selectable };
            return ToSql(selectExpr);
        }

        /// <summary>
        /// 生成 COUNT 统计语句结果。
        /// </summary>
        /// <param name="expr">查询表达式。</param>
        /// <returns>SQL 生成结果。</returns>
        public SqlGenResult ToCountSql(Expr expr = null)
        {
            SelectSourceExpr selectable;
            if (expr is null)
                selectable = new TableExpr(Table);
            else if (expr is LogicExpr logic)
                selectable = new WhereExpr { Source = new TableExpr(Table), Where = logic };
            else if (expr is SelectSourceExpr s)
                selectable = s;
            else
                throw new NotSupportedException($"Expression type {expr.GetType().Name} is not supported for COUNT.");

            SelectExpr selectExpr = new SelectExpr
            {
                Source = selectable,
                Selects = new List<ValueTypeExpr> { new AggregateFunctionExpr("COUNT", new ValueExpr(1) { IsConst = true }) }
            };
            return ToSql(selectExpr);
        }

        /// <summary>
        /// 生成 UPDATE 语句结果。
        /// </summary>
        /// <param name="values">要更新的属性及数值。</param>
        /// <param name="expr">更新条件。</param>
        /// <returns>SQL 生成结果。</returns>
        public SqlGenResult ToUpdateSql(IEnumerable<KeyValuePair<string, object>> values, Expr expr = null)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));

            List<KeyValuePair<string, object>> paramList = new List<KeyValuePair<string, object>>();
            SqlTable table = Table;
            var context = new SqlBuildContext(table, AliasName, TableArgs) { SingleTable = true };
            var sqlBuilder = SqlBuilderFactory.Instance.GetSqlBuilder(table.Definition.DataProviderType);

            ValueStringBuilder sb = ValueStringBuilder.Create(256);
            sb.Append("UPDATE ");
            sb.Append(sqlBuilder.BuildExpression(table.Definition, TableArgs));
            sb.Append(" SET ");

            bool first = true;
            foreach (var kvp in values)
            {
                if (!first) sb.Append(",");
                SqlColumn column = table.GetColumn(kvp.Key);
                if (column == null) throw new Exception($"Property \"{kvp.Key}\" does not exist in type \"{ObjectType.FullName}\".");

                sb.Append(sqlBuilder.ToSqlName(column.Name));
                sb.Append("=");

                string paramName = paramList.Count.ToString();
                paramList.Add(new KeyValuePair<string, object>(sqlBuilder.ToParamName(paramName), kvp.Value));
                sb.Append(sqlBuilder.ToSqlParam(paramName));

                first = false;
            }

            if (expr != null)
            {
                sb.Append(" WHERE ");
                if (expr is LogicExpr)
                    sb.Append(expr.ToSql(context, sqlBuilder, paramList));
                else if (expr is WhereExpr where)
                    sb.Append(where.Where.ToSql(context, sqlBuilder, paramList));
                else
                    sb.Append(expr.ToSql(context, sqlBuilder, paramList));
            }

            string sql = sb.ToString();
            sb.Dispose();
            return new SqlGenResult(sql, paramList);
        }

        /// <summary>
        /// 生成 DELETE 语句结果。
        /// </summary>
        /// <param name="expr">删除条件。</param>
        /// <returns>SQL 生成结果。</returns>
        public SqlGenResult ToDeleteSql(Expr expr = null)
        {
            List<KeyValuePair<string, object>> paramList = new List<KeyValuePair<string, object>>();
            SqlTable table = Table;
            var context = new SqlBuildContext(table, AliasName, TableArgs) { SingleTable = true };
            var sqlBuilder = SqlBuilderFactory.Instance.GetSqlBuilder(table.Definition.DataProviderType);

            ValueStringBuilder sb = ValueStringBuilder.Create(128);
            sb.Append("DELETE FROM ");
            sb.Append(sqlBuilder.BuildExpression(table, TableArgs));

            if (expr != null)
            {
                sb.Append(" WHERE ");
                if (expr is LogicExpr)
                    sb.Append(expr.ToSql(context, sqlBuilder, paramList));
                else if (expr is WhereExpr where)
                    sb.Append(where.Where.ToSql(context, sqlBuilder, paramList));
                else
                    sb.Append(expr.ToSql(context, sqlBuilder, paramList));
            }

            string sql = sb.ToString();
            sb.Dispose();
            return new SqlGenResult(sql, paramList);
        }

        /// <summary>
        /// 生成 INSERT 语句结果。
        /// </summary>
        /// <param name="values">要插入的属性及数值。</param>
        /// <returns>SQL 生成结果。</returns>
        public SqlGenResult ToInsertSql(IEnumerable<KeyValuePair<string, object>> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            List<KeyValuePair<string, object>> paramList = new List<KeyValuePair<string, object>>();
            SqlTable table = Table;
            var sqlBuilder = SqlBuilderFactory.Instance.GetSqlBuilder(table.Definition.DataProviderType);

            ValueStringBuilder sbColumns = ValueStringBuilder.Create(128);
            ValueStringBuilder sbValues = ValueStringBuilder.Create(128);

            bool first = true;
            foreach (var kvp in values)
            {
                SqlColumn column = table.GetColumn(kvp.Key);
                if (column == null) continue;

                if (!first)
                {
                    sbColumns.Append(",");
                    sbValues.Append(",");
                }

                sbColumns.Append(sqlBuilder.ToSqlName(column.Name));

                string paramName = paramList.Count.ToString();
                paramList.Add(new KeyValuePair<string, object>(sqlBuilder.ToParamName(paramName), kvp.Value));
                sbValues.Append(sqlBuilder.ToSqlParam(paramName));
                first = false;
            }

            string sql = $"INSERT INTO {sqlBuilder.BuildExpression(table, TableArgs)} ({sbColumns.ToString()}) VALUES ({sbValues.ToString()})";
            sbColumns.Dispose();
            sbValues.Dispose();
            return new SqlGenResult(sql, paramList);
        }

        /// <summary>
        /// 表示 SQL 生成后的最终产物。
        /// </summary>
        public class SqlGenResult
        {
            /// <summary>
            /// 初始化生成的 SQL 结果。
            /// </summary>
            /// <param name="sql">SQL 文本片段。</param>
            /// <param name="paramsList">参数化查询所需的键值对列表。</param>
            public SqlGenResult(string sql, List<KeyValuePair<string, object>> paramsList)
            {
                Sql = sql;
                Params = paramsList;
            }

            /// <summary>
            /// 获取生成的 SQL 语句。
            /// </summary>
            public string Sql { get; }

            /// <summary>
            /// 获取 SQL 语句中引用的参数列表（按 0, 1, 2... 命名，或按数据库方言命名）。
            /// </summary>
            public List<KeyValuePair<string, object>> Params { get; }

            /// <summary>
            /// 返回调试友好的生成的 SQL 及其参数列表。
            /// </summary>
            public override string ToString()
            {
                return $"SQL: {Sql} \nParams : {String.Join("\n", Params)}";
            }
        }
    }
}
