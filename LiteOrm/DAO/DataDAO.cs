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
    [AutoRegister(ServiceLifetime.Scoped)]
    public class DataDAO<T> : DAOBase
    {
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
        /// <returns>更新的记录数</returns>
        public virtual int UpdateAllValues(IEnumerable<KeyValuePair<string, object>> values, LogicExpr expr)
        {
            List<string> strSets = new List<string>();
            List<KeyValuePair<string, object>> paramValues = new List<KeyValuePair<string, object>>();
            foreach (KeyValuePair<string, object> value in values)
            {
                SqlColumn column = Table.GetColumn(value.Key);
                if (column is null) throw new Exception($"Property \"{value.Key}\" does not exist in type \"{Table.DefinitionType.FullName}\".");
                strSets.Add($"{SqlBuilder.ToSqlName(column.Name)} ={ToSqlParam(paramValues.Count.ToString())}");
                paramValues.Add(paramValues.Count.ToString(), value.Value);
            }
            var context = CreateSqlBuildContext(true);
            string where = expr.ToSql(context, SqlBuilder, paramValues);
            string updateSql = $"UPDATE {ParamTable} SET {String.Join(",", strSets.ToArray())} {ToWhereSql(where)}";
            using var command = MakeNamedParamCommand(updateSql, paramValues);
            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// 根据主键更新数据
        /// </summary>
        /// <param name="values">需要更新的属性及数值，key为属性名，value为数值</param>
        /// <param name="keys">主键</param>
        /// <returns>更新是否成功</returns>
        public virtual bool UpdateValues(IEnumerable<KeyValuePair<string, object>> values, params object[] keys)
        {
            ThrowExceptionIfNoKeys();
            ThrowExceptionIfWrongKeys(keys);
            LogicSet expr = new LogicSet(LogicJoinType.And);
            int i = 0;
            foreach (ColumnDefinition column in Table.Keys)
            {
                expr.Add(Expr.Prop(column.PropertyName, keys[i++]));
            }
            return UpdateAllValues(values, expr) > 0;
        }

        /// <summary>
        /// 异步根据条件更新数据
        /// </summary>
        /// <param name="values">需要更新的属性及数值，key为属性名，value为数值</param>
        /// <param name="expr">更新的条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含更新的记录数</returns>
        public async Task<int> UpdateAllValuesAsync(IEnumerable<KeyValuePair<string, object>> values, LogicExpr expr, CancellationToken cancellationToken = default)
        {
            List<string> strSets = new List<string>();
            List<KeyValuePair<string, object>> paramValues = new List<KeyValuePair<string, object>>();
            foreach (KeyValuePair<string, object> value in values)
            {
                SqlColumn column = Table.GetColumn(value.Key);
                if (column is null) throw new Exception($"Property \"{value.Key}\" does not exist in type \"{Table.DefinitionType.FullName}\".");
                strSets.Add(SqlBuilder.ToSqlName(column.Name) + "=" + ToSqlParam(paramValues.Count.ToString()));
                paramValues.Add(paramValues.Count.ToString(), value.Value);
            }
            var context = CreateSqlBuildContext(true);
            string updateSql = $"UPDATE {ParamTable} SET {String.Join(",", strSets.ToArray())} {ToWhereSql(expr.ToSql(context, SqlBuilder, paramValues))}";

            using var command = MakeNamedParamCommand(updateSql, paramValues);
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步根据主键更新数据
        /// </summary>
        /// <param name="values">需要更新的属性及数值，key为属性名，value为数值</param>
        /// <param name="keys">主键</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含更新是否成功</returns>
        public async Task<bool> UpdateValuesAsync(IEnumerable<KeyValuePair<string, object>> values, object[] keys, CancellationToken cancellationToken = default)
        {
            ThrowExceptionIfNoKeys();
            ThrowExceptionIfWrongKeys(keys);
            LogicSet expr = new LogicSet(LogicJoinType.And);
            int i = 0;
            foreach (ColumnDefinition column in TableDefinition.Keys)
            {
                expr.Add(Expr.Prop(column.PropertyName, keys[i++]));
            }
            return await UpdateAllValuesAsync(values, expr, cancellationToken).ConfigureAwait(false) > 0;
        }
    }
}