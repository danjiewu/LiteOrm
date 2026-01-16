using LiteOrm.Common;
using System;
using System.Collections.Generic;

namespace LiteOrm
{
    /// <summary>
    /// SQL 生成器类，负责将表达式转换为特定数据库的 SQL 语句。
    /// </summary>
    public class SqlGen
    {
        /// <summary>
        /// 初始化 <see cref="SqlGen"/> 类的新实例。
        /// </summary>
        /// <param name="objectType">对象类型。</param>
        public SqlGen(Type objectType)
        {
            ObjectType = objectType;
        }

        /// <summary>
        /// 获取当前操作的对象类型。
        /// </summary>
        public Type ObjectType { get; }

        /// <summary>
        /// 获取或设置表别名。
        /// </summary>
        public string AliasName { get; set; }

        /// <summary>
        /// 获取或设置表名参数（通常用于分表逻辑）。
        /// </summary>
        public string[] TableArgs { get; set; }

        /// <summary>
        /// 将给定的表达式转换为 SQL 生成结果。
        /// </summary>
        /// <param name="expr">要转换的表达式。</param>
        /// <returns>包含 SQL 语句和参数列表的 <see cref="SqlGenResult"/>。</returns>
        public SqlGenResult ToSql(Expr expr)
        {
            List<KeyValuePair<string, object>> paramList = new List<KeyValuePair<string, object>>();
            SqlTable table = TableInfoProvider.Default.GetTableView(ObjectType);

            var context = new SqlBuildContext()
            {
                Table = table,
                TableAliasName = AliasName,
                TableNameArgs = TableArgs
            };

            var sqlBuilder = SqlBuilderFactory.Instance.GetSqlBuilder(table.Definition.DataProviderType);
            string sql = expr.ToSql(context, sqlBuilder, paramList);

            return new SqlGenResult(sql, paramList);
        }

        /// <summary>
        /// 表示 SQL 生成的结果。
        /// </summary>
        public class SqlGenResult
        {
            /// <summary>
            /// 初始化 <see cref="SqlGenResult"/> 类的新实例。
            /// </summary>
            /// <param name="sql">生成的 SQL 语句。</param>
            /// <param name="paramsList">SQL 对应的参数列表。</param>
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
            /// 获取 SQL 语句对应的参数列表。
            /// </summary>
            public List<KeyValuePair<string, object>> Params { get; }

            public override string ToString()
            {
                return $"SQL: {Sql} \nParams : {String.Join("\n", Params)}";
            }
        }
    }
}
