using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace LiteOrm
{
    /// <summary>
    /// DAO 扩展方法 - 提供基于 Lambda 表达式的查询扩展
    /// </summary>
    public static class DAOExtensions
    {
        #region IDataViewDAO Lambda 扩展

        /// <summary>
        /// 使用 Lambda 表达式查询数据。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="dataViewDao">IDataViewDAO 实例。</param>
        /// <param name="expression">定义查询条件的 Lambda 表达式。</param>
        /// <returns>查询结果数据表。</returns>
        public static DataTableResult Search<T>(this IDataViewDAO<T> dataViewDao, Expression<Func<T, bool>> expression)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return dataViewDao.Search(lambdaConvert.ToLogicExpr());
        }

        /// <summary>
        /// 使用 IQueryable 形式的 Lambda 表达式查询数据。
        /// </summary>
        /// <typeparam name="TInput">实体类型。</typeparam>
        /// <typeparam name="TResult">查询结果类型。</typeparam>
        /// <param name="dataViewDao">IDataViewDAO 实例。</param>
        /// <param name="expression">定义查询条件的 IQueryable Lambda 表达式。</param>
        /// <returns>查询结果数据表。</returns>
        public static DataTableResult Search<TInput, TResult>(this IDataViewDAO<TInput> dataViewDao, Expression<Func<IQueryable<TInput>, IQueryable<TResult>>> expression)
        {
            return dataViewDao.Search(Expr.Query(expression));
        }

        /// <summary>
        /// 指定字段并使用 Lambda 表达式查询数据。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="dataViewDao">IDataViewDAO 实例。</param>
        /// <param name="propertyNames">要查询的字段名称列表。</param>
        /// <param name="expression">定义查询条件的 Lambda 表达式。</param>
        /// <returns>查询结果数据表。</returns>
        public static DataTableResult Search<T>(this IDataViewDAO<T> dataViewDao, string[] propertyNames, Expression<Func<T, bool>> expression)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return dataViewDao.Search(propertyNames, lambdaConvert.ToLogicExpr());
        }

        /// <summary>
        /// 指定字段并使用 IQueryable 形式的 Lambda 表达式查询数据。
        /// </summary>
        /// <typeparam name="TInput">实体类型。</typeparam>
        /// <typeparam name="TResult">查询结果类型。</typeparam>
        /// <param name="dataViewDao">IDataViewDAO 实例。</param>
        /// <param name="propertyNames">要查询的字段名称列表。</param>
        /// <param name="expression">定义查询条件的 IQueryable Lambda 表达式。</param>
        /// <returns>查询结果数据表。</returns>
        public static DataTableResult Search<TInput, TResult>(this IDataViewDAO<TInput> dataViewDao, string[] propertyNames, Expression<Func<IQueryable<TInput>, IQueryable<TResult>>> expression)
        {
            return dataViewDao.Search(propertyNames, Expr.Query(expression));
        }

        #endregion

        #region IObjectViewDAO Lambda 扩展

        /// <summary>
        /// 使用 Lambda 表达式查询数据。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="objectViewDao">IObjectViewDAO 实例。</param>
        /// <param name="expression">定义查询条件的 Lambda 表达式。</param>
        /// <returns>符合条件的对象枚举。</returns>
        public static EnumerableResult<T> Search<T>(this IObjectViewDAO<T> objectViewDao, Expression<Func<T, bool>> expression)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return objectViewDao.Search(lambdaConvert.ToLogicExpr());
        }

        /// <summary>
        /// 使用 IQueryable 形式的 Lambda 表达式查询数据。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="objectViewDao">IObjectViewDAO 实例。</param>
        /// <param name="expression">定义查询条件的 IQueryable Lambda 表达式。</param>
        /// <returns>符合条件的对象枚举。</returns>
        public static EnumerableResult<T> Search<T>(this IObjectViewDAO<T> objectViewDao, Expression<Func<IQueryable<T>, IQueryable<T>>> expression)
        {
            return objectViewDao.Search(Expr.Query(expression));
        }

        /// <summary>
        /// 使用 Lambda 表达式获取符合条件的对象个数。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="objectViewDao">IObjectViewDAO 实例。</param>
        /// <param name="expression">定义查询条件的 Lambda 表达式。</param>
        /// <returns>符合条件的对象个数。</returns>
        public static ValueResult<int> Count<T>(this IObjectViewDAO<T> objectViewDao, Expression<Func<T, bool>> expression)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return objectViewDao.Count(lambdaConvert.ToLogicExpr());
        }

        /// <summary>
        /// 使用 Lambda 表达式判断符合条件的对象是否存在。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="objectViewDao">IObjectViewDAO 实例。</param>
        /// <param name="expression">定义查询条件的 Lambda 表达式。</param>
        /// <returns>符合条件的对象是否存在。</returns>
        public static ValueResult<bool> Exists<T>(this IObjectViewDAO<T> objectViewDao, Expression<Func<T, bool>> expression)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return objectViewDao.Exists(lambdaConvert.ToLogicExpr());
        }

        #endregion
    }
}
