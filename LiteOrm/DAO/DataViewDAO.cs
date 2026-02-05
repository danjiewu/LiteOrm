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
    /// 提供视图查询功能，返回 DataTable 格式结果
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    [AutoRegister(ServiceLifetime.Scoped)]
    public class DataViewDAO<T> : DAOBase
    {
        /// <summary>
        /// 获取实体类型信息。
        /// </summary>
        public override Type ObjectType => typeof(T);

        /// <summary>
        /// 获取实体对应的数据库表或视图元数据。
        /// </summary>
        public override SqlTable Table => TableInfoProvider.GetTableView(ObjectType);

        /// <summary>
        /// 替换 SQL 语句中的占位符为实际值。
        /// </summary>
        /// <param name="sqlWithParam">包含占位符的 SQL 语句。</param>
        /// <returns>替换后的完整 SQL 语句。</returns>
        protected override string ReplaceParam(string sqlWithParam)
        {
            return base.ReplaceParam(sqlWithParam).Replace(ParamAllFields, AllFieldsSql);
        }

        /// <summary>
        /// 根据条件查询数据
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <returns>查询结果数据表</returns>
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
        /// <returns>查询结果数据表</returns>
        public virtual async Task<DataTable> SearchAsync(Expr expr, CancellationToken cancellationToken = default)
        {
            using var command = MakeConditionCommand($"SELECT {ParamAllFields} \nFROM {ParamFromTable} {ParamWhere}", expr);
            return await GetDataTableAsync(command, cancellationToken);
        }

        /// <summary>
        /// 指定字段查询数据
        /// </summary>
        /// <param name="propertyNames">要查询的字段名称列表</param>
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
        /// 指定字段查询数据 (异步)
        /// </summary>
        /// <param name="propertyNames">要查询的字段名称列表</param>
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
        /// 执行命令并将结果填充到 DataTable，使用 SqlBuilder 进行参数转换
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
        /// <param name="command">数据库查询命令</param>
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