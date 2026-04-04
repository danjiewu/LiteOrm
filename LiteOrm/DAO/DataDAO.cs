using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// 提供针对数据的基本更新操作实现
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    [AutoRegister(Lifetime.Scoped)]
    public class DataDAO<T> : DAOBase
    {
        public DataDAO(TableInfoProvider tableInfoProvider, BulkProviderFactory bulkFactory)
            : base(tableInfoProvider, bulkFactory)
        {
        }

        /// <summary>
        /// 实体对象类型
        /// </summary>
        public override Type ObjectType => typeof(T);

        /// <summary>
        /// 获取实体对应的数据库表元数据。
        /// </summary>
        public override SqlTable Table => TableInfoProvider.GetTableDefinition(ObjectType);

        /// <summary>
        /// 获取或设置用于生成 SQL 的上下文。
        /// </summary>
        public override SqlBuildContext CreateSqlBuildContext(bool initTable = false)
        {
            var context = base.CreateSqlBuildContext(initTable);
            context.SingleTable = true;
            return context;
        }

        /// <summary>
        /// 根据条件更新数据
        /// </summary>
        /// <param name="values">需要更新的属性及数值，key为属性名，value为数值</param>
        /// <param name="expr">更新的条件</param>
        /// <returns>空结果对象，可通过Execute()和ExecuteAsync()执行操作。</returns>
        public virtual NonQueryResult UpdateAllValues(IEnumerable<KeyValuePair<string, object>> values, LogicExpr expr)
        {
            List<string> strSets = new List<string>();
            List<KeyValuePair<string, object>> paramValues = new List<KeyValuePair<string, object>>();
            foreach (KeyValuePair<string, object> value in values)
            {
                SqlColumn column = Table.GetColumn(value.Key);
                if (column is null) throw new Exception($"Property \"{value.Key}\" does not exist in type \"{Table.DefinitionType.FullName}\".");
                strSets.Add($"{SqlBuilder.ToSqlName(column.Name)} ={ToSqlParam(paramValues.Count.ToString())}");
                paramValues.Add(new(paramValues.Count.ToString(), value.Value));
            }
            var context = CreateSqlBuildContext(true);
            string where = expr.ToSql(context, SqlBuilder, paramValues);
            string updateSql = $"UPDATE {ParamTable} SET {String.Join(",", strSets.ToArray())} {ToWhereSql(where)}";
            var command = MakeNamedParamCommand(updateSql, paramValues);
            return new NonQueryResult(command);
        }

        /// <summary>
        /// 根据主键更新数据
        /// </summary>
        /// <param name="values">需要更新的属性及数值，key为属性名，value为数值</param>
        /// <param name="keys">主键</param>
        /// <returns>空结果对象，可通过Execute()和ExecuteAsync()执行操作。</returns>
        public virtual NonQueryResult UpdateValues(IEnumerable<KeyValuePair<string, object>> values, params object[] keys)
        {
            ThrowExceptionIfNoKeys();
            ThrowExceptionIfWrongKeys(keys);
            AndExpr expr = new AndExpr();
            int i = 0;
            foreach (ColumnDefinition column in Table.Keys)
            {
                expr.Add(Expr.PropEqual(column.PropertyName, keys[i++]));
            }
            return UpdateAllValues(values, expr);
        }


    }
}
