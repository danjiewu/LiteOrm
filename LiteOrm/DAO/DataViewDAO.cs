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
    [AutoRegister(Lifetime.Scoped)]
    public class DataViewDAO<T> : DAOBase, IDataViewDAO<T>
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
            var command = MakeExprCommand(expr, true);
            return new DataTableResult(command, ReadDataRow, false);
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
            return new DataTableResult(command, ReadDataRow, false);
        }

        /// <summary>
        /// 使用插值字符串查询数据
        /// </summary>
        /// <param name="sqlBody">查询SQL，使用插值字符串格式，可插入普通变量或 Expr。<see cref="LiteOrm.Common.ExprString"/></param>
        /// <param name="isFull">传入的是否是完整sql，默认为 false</param>
        /// <returns>查询结果数据表</returns>
        public virtual DataTableResult Search([InterpolatedStringHandlerArgument("")] ref ExprString sqlBody, bool isFull = false)
        {
            string sql = isFull ? sqlBody.GetSqlResult() : $"SELECT {AllFields} \nFROM {From} \n{sqlBody.GetSqlResult()}";
            var command = MakeNamedParamCommand(sql, sqlBody.GetParams());
            return new DataTableResult(command, ReadDataRow, false);
        }

        /// <summary>
        /// 根据属性名列表和条件构建SelectExpr
        /// </summary>
        private SelectExpr BuildSelectExpr(string[] propertyNames, Expr expr)
        {
            List<SelectItemExpr> selects;
            if (propertyNames != null && propertyNames.Length > 0)
            {
                selects = Array.ConvertAll(propertyNames, p => new SelectItemExpr(Expr.Prop(p), p)).ToList();
            }
            else
            {
                selects = Array.ConvertAll(SelectColumns, p => new SelectItemExpr(Expr.Prop(p.Name), p.Name)).ToList();
            }

            SelectExpr selectExpr;
            if (expr is SelectExpr selectExpr1)
            {
                selectExpr = selectExpr1;
                selectExpr.Selects = selects;
            }
            else
            {
                selectExpr = new SelectExpr
                {
                    Source = expr.ToSource(ObjectType),
                    Selects = selects
                };
            }
            return selectExpr;
        }
    }
}
