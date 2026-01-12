using MyOrm.Service;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// Expression 到 Expr 的扩展方法
    /// </summary>
    public static class LamdaExpressionExtensions
    {
        /// <summary>
        /// 使用Lambda表达式搜索实体
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entityViewService">实体视图服务</param>
        /// <param name="expression">搜索条件的Lambda表达式</param>
        /// <returns>符合条件的实体列表</returns>
        public static List<T> Search<T>(this IEntityViewService<T> entityViewService, Expression<Func<T, bool>> expression)
        {
            return entityViewService.Search(Expr.Exp(expression).InnerExpr);
        }

        /// <summary>
        /// 使用Lambda表达式搜索单个实体
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entityViewService">实体视图服务</param>
        /// <param name="expression">搜索条件的Lambda表达式</param>
        /// <returns>符合条件的单个实体，如果没有找到则返回null</returns>
        public static T SearchOne<T>(this IEntityViewService<T> entityViewService, Expression<Func<T, bool>> expression)
        {
            return entityViewService.SearchOne(Expr.Exp(expression).InnerExpr);
        }

        /// <summary>
        /// 使用Lambda表达式分页搜索实体
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entityViewService">实体视图服务</param>
        /// <param name="expression">搜索条件的Lambda表达式</param>
        /// <param name="sectionSet">分页设置</param>
        /// <param name="tableArgs">表参数</param>
        /// <returns>符合条件的实体列表</returns>
        public static List<T> SearchSection<T>(this IEntityViewService<T> entityViewService, Expression<Func<T, bool>> expression, SectionSet sectionSet, params string[] tableArgs)
        {
            return entityViewService.SearchSection(Expr.Exp(expression).InnerExpr, sectionSet, tableArgs);
        }

        /// <summary>
        /// 使用Lambda表达式异步搜索实体
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entityViewService">异步实体视图服务</param>
        /// <param name="expression">搜索条件的Lambda表达式</param>
        /// <returns>符合条件的实体列表的任务</returns>
        public static Task<List<T>> SearchAsync<T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default, params string[] tableArgs)
        {
            return entityViewService.SearchAsync(Expr.Exp(expression).InnerExpr, cancellationToken, tableArgs);
        }

        /// <summary>
        /// 使用Lambda表达式异步搜索单个实体
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entityViewService">异步实体视图服务</param>
        /// <param name="expression">搜索条件的Lambda表达式</param>
        /// <returns>符合条件的单个实体的任务，如果没有找到则返回null</returns>
        public static Task<T> SearchOneAsync<T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default, params string[] tableArgs)
        {
            return entityViewService.SearchOneAsync(Expr.Exp(expression).InnerExpr, cancellationToken, tableArgs);
        }

        /// <summary>
        /// 使用Lambda表达式异步分页搜索实体
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="entityViewService">异步实体视图服务</param>
        /// <param name="expression">搜索条件的Lambda表达式</param>
        /// <param name="sectionSet">分页设置</param>
        /// <param name="tableArgs">表参数</param>
        /// <returns>符合条件的实体列表的任务</returns>
        public static Task<List<T>> SearchSectionAsync<T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<T, bool>> expression, SectionSet sectionSet, CancellationToken cancellationToken = default, params string[] tableArgs)
        {
            return entityViewService.SearchSectionAsync(Expr.Exp(expression).InnerExpr, sectionSet, cancellationToken, tableArgs);
        }
    }
}
