using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace LiteOrm.CodeGen
{
    /// <summary>
    /// SQL 生成器类，负责将抽象表达式树 (Expr) 转换为针对特定数据库方言的 SQL 语句。
    /// </summary>
    /// <remarks>
    /// 该类作为表达式解析的入口，根据目标对象类型获取表元数据，并调用相应的 SqlBuilder 进行翻译。
    /// 支持参数化查询以防止 SQL 注入。
    /// </remarks>
    public class SqlGen : IExprStringBuildContext
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
        /// 获取与当前实体类型关联的 SQL 构建器实例，用于生成特定数据库方言的 SQL 语句。
        /// </summary>
        public ISqlBuilder SqlBuilder
        {
            get
            {
                var table = TableInfoProvider.Default.GetTableDefinition(ObjectType);
                if (table == null) return null;
                return SqlBuilderFactory.Instance.GetSqlBuilder(table.DataProviderType, table.DataSource);
            }
        }

        /// <summary>
        /// 创建一个新的 <see cref="SqlBuildContext"/> 实例，用于在 SQL 生成过程中维护上下文信息。
        /// </summary>
        /// <param name="initTable">指示是否初始化表信息。</param>
        /// <returns>返回一个新的 <see cref="SqlBuildContext"/> 实例。</returns>
        public SqlBuildContext CreateSqlBuildContext(bool initTable = false)
        {
            if (initTable)
                return new SqlBuildContext(TableInfoProvider.Default.GetTableView(ObjectType), Constants.DefaultTableAlias, TableArgs);
            else
                return new SqlBuildContext() { TableArgs = TableArgs };
        }

        /// <summary>
        /// 将逻辑表达式转换为具体的 SQL 生成结果。
        /// </summary>
        /// <param name="expr">要转换的表达式（如条件、计算等）。</param>
        /// <returns>包含 SQL 语句文本和对应参数集合的 <see cref="SqlGenResult"/>。</returns>
        public SqlGenResult ToSql(Expr expr)
        {
            List<KeyValuePair<string, object>> paramList = new List<KeyValuePair<string, object>>();      
            bool isFull = expr is UpdateExpr || expr is DeleteExpr || expr is SelectExpr;
            var context = CreateSqlBuildContext(!isFull);
            context.SingleTable = !(expr is UpdateExpr || expr is DeleteExpr);
            // 执行递归解析
            string sql = expr.ToSql(context, SqlBuilder, paramList);
            return new SqlGenResult(sql, paramList);
        }

        /// <summary>
        /// ExprString 方式生成 SQL 语句，直接接受一个格式化的字符串表达式<seealso cref="ExprString"/>。
        /// </summary>
        /// <param name="sqlBody">格式化的字符串表达式。</param>
        /// <returns>包含 SQL 语句文本和对应参数集合的 <see cref="SqlGenResult"/>。</returns>
        public SqlGenResult ToSql([InterpolatedStringHandlerArgument("")] ExprString sqlBody)
        {
            return new SqlGenResult(sqlBody.GetSqlResult(), sqlBody.GetParams());
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
