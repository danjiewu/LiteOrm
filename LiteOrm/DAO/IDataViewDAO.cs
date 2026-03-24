using System;
using System.Data;
using System.Linq.Expressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// 提供视图查询功能的接口，返回 DataTable 格式结果
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public interface IDataViewDAO<T>
    {
        /// <summary>
        /// 根据条件查询数据
        /// </summary>
        /// <param name="expr">查询条件，可以是SelectExpr指定字段，或WhereExpr指定条件</param>
        /// <returns>查询结果数据表</returns>
        DataTableResult Search(Expr expr);

        /// <summary>
        /// 指定字段查询数据
        /// </summary>
        /// <param name="propertyNames">要查询的字段名称列表</param>
        /// <param name="expr">查询条件</param>
        /// <returns>查询结果数据表</returns>
        DataTableResult Search(string[] propertyNames, Expr expr);
    }
}
