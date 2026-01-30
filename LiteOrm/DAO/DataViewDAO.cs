using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// 提供查询方法，结果以 DataTable 形式返回
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    [AutoRegister(ServiceLifetime.Scoped)]
    public class DataViewDAO<T> : DAOBase
    {
        /// <summary>
        /// 获取实体对象类型。
        /// </summary>
        public override Type ObjectType => typeof(T);

        /// <summary>
        /// 获取实体对应的数据库表或视图元数据。
        /// </summary>
        public override SqlTable Table => TableInfoProvider.GetTableView(ObjectType);

        /// <summary>
        /// 替换 SQL 语句中的占位符参数。
        /// </summary>
        /// <param name="sqlWithParam">包含占位符的 SQL 字符串。</param>
        /// <returns>替换参数后的 SQL 字符串。</returns>
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
        /// 根据条件查询数据 (异步)
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>符合条件的数据表</returns>
        public virtual async Task<DataTable> SearchAsync(Expr expr, CancellationToken cancellationToken = default)
        {
            using var command = MakeConditionCommand($"SELECT {ParamAllFields} \nFROM {ParamFromTable} {ParamWhere}", expr);
            return await GetDataTableAsync(command, cancellationToken);
        }

        /// <summary>
        /// 根据属性查询列表数据
        /// </summary>
        /// <param name="propertyNames">要查询的属性名数组</param>
        /// <param name="expr">查询条件</param>
        /// <returns>查询结果数据表</returns>
        public virtual DataTable Search(string[] propertyNames, Expr expr)
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
        /// 根据属性查询列表数据 (异步)
        /// </summary>
        /// <param name="propertyNames">要查询的属性名数组</param>
        /// <param name="expr">查询条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>查询结果数据表</returns>
        public virtual async Task<DataTable> SearchAsync(string[] propertyNames, Expr expr, CancellationToken cancellationToken = default)
        {
            string fieldsSql = ParamAllFields;
            if (propertyNames != null && propertyNames.Length > 0)
            {
                var columns = propertyNames.Select(p => Table.GetColumn(p)).Where(c => c != null);
                fieldsSql = GetSelectFieldsSql(columns);
            }
            using var command = MakeConditionCommand($"SELECT {fieldsSql} \nFROM {ParamFromTable} {ParamWhere}", expr);
            return await GetDataTableAsync(command, cancellationToken);
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
        /// 根据条件查询列表数据并进行排序 (异步)
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="orderBy">排序项集合</param>
        /// <returns>查询结果数据表</returns>
        public virtual async Task<DataTable> SearchAsync(Expr expr, CancellationToken cancellationToken = default, params Sorting[] orderBy)
        {
            if (orderBy == null || orderBy.Length == 0) return await SearchAsync(expr, cancellationToken);
            using var command = MakeConditionCommand($"SELECT {ParamAllFields} \nFROM {ParamFromTable} {ParamWhere} ORDER BY " + GetOrderBySql(orderBy), expr);
            return await GetDataTableAsync(command, cancellationToken);
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
        /// 根据条件查询指定列的数据并进行排序 (异步)
        /// </summary>
        /// <param name="propertyNames">要查询的属性名数组</param>
        /// <param name="expr">查询条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="orderBy">排序项集合</param>
        /// <returns>查询结果数据表</returns>
        public virtual async Task<DataTable> SearchAsync(string[] propertyNames, Expr expr, CancellationToken cancellationToken = default, params Sorting[] orderBy)
        {
            var columns = propertyNames.Select(p => Table.GetColumn(p)).Where(c => c != null);
            string fieldsSql = GetSelectFieldsSql(columns);
            string orderBySql = (orderBy == null || orderBy.Length == 0) ? string.Empty : " ORDER BY " + GetOrderBySql(orderBy);
            using var command = MakeConditionCommand($"SELECT {fieldsSql} \nFROM {ParamFromTable} {ParamWhere}{orderBySql}", expr);
            return await GetDataTableAsync(command, cancellationToken);
        }

        /// <summary>
        /// 分页查询数据，并指定输出列
        /// </summary>
        /// <param name="propertyNames">需要输出的属性名集合</param>
        /// <param name="expr">查询条件</param>
        /// <param name="section">分页设定</param>
        /// <returns>分页后的数据表</returns>
        public virtual DataTable SearchSection(string[] propertyNames, Expr expr, PageSection section)
        {
            string fieldsSql = (propertyNames == null || propertyNames.Length == 0) ? AllFieldsSql : GetSelectFieldsSql(propertyNames.Select(p => Table.GetColumn(p)).Where(c => c != null));
            string sql = SqlBuilder.GetSelectSectionSql(fieldsSql, From, ParamWhere, GetOrderBySql(section.Orders), section.StartIndex, section.SectionSize);
            using var command = MakeConditionCommand(sql, expr);
            return GetDataTable(command);
        }

        /// <summary>
        /// 分页查询数据，并指定输出列 (异步)
        /// </summary>
        /// <param name="propertyNames">需要输出的属性名集合</param>
        /// <param name="expr">查询条件</param>
        /// <param name="section">分页设定</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>分页后的数据表</returns>
        public virtual async Task<DataTable> SearchSectionAsync(string[] propertyNames, Expr expr, PageSection section, CancellationToken cancellationToken = default)
        {
            string fieldsSql = (propertyNames == null || propertyNames.Length == 0) ? AllFieldsSql : GetSelectFieldsSql(propertyNames.Select(p => Table.GetColumn(p)).Where(c => c != null));
            string sql = SqlBuilder.GetSelectSectionSql(fieldsSql, From, ParamWhere, GetOrderBySql(section.Orders), section.StartIndex, section.SectionSize);
            using var command = MakeConditionCommand(sql, expr);
            return await GetDataTableAsync(command, cancellationToken);
        }


        /// <summary>
        /// 执行命令并将结果填充到 DataTable，使用 SqlBuilder 进行数据转换
        /// </summary>
        protected DataTable GetDataTable(DbCommandProxy command)
        {
            using (var reader = command.ExecuteReader())
            {
                DataTable dt = new DataTable();
                int fieldCount = reader.FieldCount;
                SqlColumn[] columns = new SqlColumn[fieldCount];
                for (int i = 0; i < fieldCount; i++)
                {
                    string name = reader.GetName(i);
                    SqlColumn column = Table.GetColumn(name);
                    columns[i] = column;
                    Type propertyType = column?.PropertyType ?? reader.GetFieldType(i);
                    dt.Columns.Add(name, propertyType.GetUnderlyingType());
                }

                dt.BeginLoadData();
                while (reader.Read())
                {
                    DataRow dr = dt.NewRow();
                    for (int i = 0; i < fieldCount; i++)
                    {
                        object value = reader.GetValue(i);
                        dr[i] = ConvertFromDbValue(value, columns[i]?.PropertyType) ?? DBNull.Value;
                    }
                    dt.Rows.Add(dr);
                }
                dt.EndLoadData();
                return dt;
            }
        }

        /// <summary>
        /// 执行命令并将结果填充到 DataTable (异步)
        /// </summary>
        /// <param name="command">数据库命令代理</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>查询结果数据表</returns>
        protected async Task<DataTable> GetDataTableAsync(DbCommandProxy command, CancellationToken cancellationToken = default)
        {
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                DataTable dt = new DataTable();
                int fieldCount = reader.FieldCount;
                SqlColumn[] columns = new SqlColumn[fieldCount];
                for (int i = 0; i < fieldCount; i++)
                {
                    string name = reader.GetName(i);
                    SqlColumn column = Table.GetColumn(name);
                    columns[i] = column;
                    Type propertyType = column?.PropertyType ?? reader.GetFieldType(i);
                    dt.Columns.Add(name, propertyType.GetUnderlyingType());
                }

                dt.BeginLoadData();
                while (await reader.ReadAsync(cancellationToken))
                {
                    DataRow dr = dt.NewRow();
                    for (int i = 0; i < fieldCount; i++)
                    {
                        object value = reader.GetValue(i);
                        dr[i] = ConvertFromDbValue(value, columns[i]?.PropertyType) ?? DBNull.Value;
                    }
                    dt.Rows.Add(dr);
                }
                dt.EndLoadData();
                return dt;
            }
        }
    }
}
