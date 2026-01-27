using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace LiteOrm
{
    /// <summary>
    /// 提供查询方法，结果以 DataTable 形式返回
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    [AutoRegister(ServiceLifetime.Scoped)]
    public class DataViewDAO<T> : DAOBase
    {
        public override Type ObjectType => typeof(T);

        public override SqlTable Table => TableInfoProvider.GetTableView(ObjectType);

        protected override string ReplaceParam(string sqlWithParam)
        {
            return base.ReplaceParam(sqlWithParam).Replace(ParamAllFields, AllFieldsSql);
        }

        /// <summary>
        /// 根据条件查询数据
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <returns>符合条件的数据表</returns>
        public virtual DataTable Search(Expr expr)
        {
            using var command = MakeConditionCommand($"SELECT {ParamAllFields} \nFROM {ParamFromTable} {ParamWhere}", expr);
            return GetDataTable(command);
        }

        /// <summary>
        /// 根据属性查询列表数据
        /// </summary>
        /// <param name="propertyNames">要查询的属性名数组</param>
        /// <param name="expr">查询条件</param>
        /// <returns>查询结果数据表</returns>
        public virtual DataTable Search(string[] propertyNames,Expr expr)
        {
            string fieldsSql = ParamAllFields;
            if (propertyNames != null && propertyNames.Length > 0)
            {
                var columns = propertyNames.Select(p => Table.GetColumn(p)).Where(c => c != null);
                fieldsSql = GetSelectFieldsSql(columns);
            }
            using var command = MakeConditionCommand($"SELECT {fieldsSql} \nFROM {ParamFromTable} {ParamWhere}", expr);
            return GetDataTable(command);
        }

        /// <summary>
        /// 根据条件查询列表数据并进行排序
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="orderBy">排序项集合</param>
        /// <returns>查询结果数据表</returns>
        public virtual DataTable Search(Expr expr, params Sorting[] orderBy)
        {
            if (orderBy == null || orderBy.Length == 0) return Search(expr);
            using var command = MakeConditionCommand($"SELECT {ParamAllFields} \nFROM {ParamFromTable} {ParamWhere} ORDER BY " + GetOrderBySql(orderBy), expr);
            return GetDataTable(command);
        }

        /// <summary>
        /// 根据条件查询指定列的数据并进行排序
        /// </summary>
        /// <param name="propertyNames">要查询的属性名数组</param>
        /// <param name="expr">查询条件</param>
        /// <param name="orderBy">排序项集合</param>
        /// <returns>查询结果数据表</returns>
        public virtual DataTable Search(string[] propertyNames, Expr expr, params Sorting[] orderBy)
        {
            var columns = propertyNames.Select(p => Table.GetColumn(p)).Where(c => c != null);
            string fieldsSql = GetSelectFieldsSql(columns);
            string orderBySql = (orderBy == null || orderBy.Length == 0) ? string.Empty : " ORDER BY " + GetOrderBySql(orderBy);
            using var command = MakeConditionCommand($"SELECT {fieldsSql} \nFROM {ParamFromTable} {ParamWhere}{orderBySql}", expr);
            return GetDataTable(command);
        }

        /// <summary>
        /// 分页查询数据，并指定输出列
        /// </summary>
        /// <param name="propertyNames">需要输出的属性名集合</param>
        /// <param name="expr">查询条件</param>
        /// <param name="section">分页设定</param>
        /// <returns>分页后的数据表</returns>
        public virtual DataTable SearchSection(string[] propertyNames,Expr expr, PageSection section)
        {
            string fieldsSql = (propertyNames == null || propertyNames.Length == 0) ? AllFieldsSql : GetSelectFieldsSql(propertyNames.Select(p => Table.GetColumn(p)).Where(c => c != null));
            string sql = SqlBuilder.GetSelectSectionSql(fieldsSql, From, ParamWhere, GetOrderBySql(section.Orders), section.StartIndex, section.SectionSize);
            using var command = MakeConditionCommand(sql, expr);
            return GetDataTable(command);
        }


        /// <summary>
        /// 执行命令并将结果填充到 DataTable
        /// </summary>
        protected DataTable GetDataTable(DbCommandProxy command)
        {
            using (var reader = command.ExecuteReader())
            {
                DataTable dt = new DataTable();
                dt.Load(reader);
                return dt;
            }
        }
    }
}
