using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
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
        /// 指示当前 DAO 是视图查询 DAO。
        /// </summary>
        protected override bool IsView => true;

        /// <summary>
        /// 获取需要替换的关键字和内容的字典
        /// </summary>
        /// <returns></returns>
        protected override Dictionary<string, string> GetReplacements()
        {
            return new Dictionary<string, string>
            {
                { ParamTable, FactTableName },
                { ParamFrom, From },
                { ParamAllFields, AllFields }
            };
        }

        /// <summary>
        /// 从Reader读取数据并创建DataRow
        /// </summary>
        /// <param name="reader">数据读取器</param>
        /// <param name="dt">目标DataTable</param>
        /// <returns>创建的DataRow</returns>
        protected virtual DataRow ReadDataRow(IDataReader reader, DataTable dt)
        {
            DataRow row = dt.NewRow();
            int fieldCount = reader.FieldCount;
            SqlColumn[] columns = new SqlColumn[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                string name = reader.GetName(i);
                SqlColumn column = Table.GetColumn(name);
                columns[i] = column;
                Type propertyType = column?.PropertyType ?? reader.GetFieldType(i);
                object value = reader.GetValue(i);
                row[i] = ConvertFromDbValue(value, propertyType) ?? DBNull.Value;
            }
            return row;
        }

        /// <summary>
        /// 根据条件查询数据
        /// </summary>
        /// <param name="expr">查询条件，可以是SelectExpr指定字段，或WhereExpr指定条件</param>
        /// <returns>查询结果数据表</returns>
        public virtual DataTableResult Search(Expr expr)
        {
            var command = MakeSelectExprCommand(expr);
            return new DataTableResult(command, () => new DataTable(), ReadDataRow, false);
        }

        /// <summary>
        /// 指定字段查询数据
        /// </summary>
        /// <param name="propertyNames">要查询的字段名称列表</param>
        /// <param name="expr">查询条件</param>
        /// <returns>查询结果数据表</returns>
        public virtual DataTableResult Search(string[] propertyNames, Expr expr)
        {
            SelectExpr selectExpr = BuildSelectExpr(propertyNames, expr);
            var command = MakeExprCommand(selectExpr);
            return new DataTableResult(command, () => new DataTable(), ReadDataRow, false);
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// 使用插值字符串查询数据
        /// </summary>
        /// <param name="fullSql">完整的SQL语句，包括Select ... From ... 部分，使用插值字符串格式</param>
        /// <returns>查询结果数据表</returns>
        public virtual DataTableResult Search([InterpolatedStringHandlerArgument("")] ref ExprString fullSql    )
        {
            var sql = ReplaceParam(fullSql.GetSqlResult());
            var command = MakeNamedParamCommand(sql, fullSql.GetParams());
            return new DataTableResult(command, () => new DataTable(), ReadDataRow, false);
        }
#endif

        /// <summary>
        /// 根据属性名列表和条件构建SelectExpr
        /// </summary>
        private SelectExpr BuildSelectExpr(string[] propertyNames, Expr expr)
        {
            ISqlSegment selectSource;
            if (expr is null)
            {
                selectSource = new FromExpr(ObjectType);
            }
            else if (expr is LogicExpr logicExpr)
            {
                selectSource = new WhereExpr() { Source = new FromExpr(ObjectType), Where = logicExpr };
            }                
            else if (expr is ISqlSegment sourceExpr)
            {
                selectSource = sourceExpr;
            }
            else
            {
                throw new ArgumentException("expr 参数类型不支持");
            }

            SelectItemExpr[] selects;
            if (propertyNames != null && propertyNames.Length > 0)
            {
                selects = Array.ConvertAll(propertyNames, p => new SelectItemExpr(Expr.Prop(p)));
            }
            else
            {
                selects = Array.ConvertAll(SelectColumns, p => new SelectItemExpr(Expr.Prop(p.Name)));
            }

            return new SelectExpr(selectSource, selects);
        }
    }
}
