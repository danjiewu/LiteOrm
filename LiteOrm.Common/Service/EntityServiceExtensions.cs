using LiteOrm.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// 提供针对 Lambda 表达式到 Expr 对象的扩展方法，简化实体查询操作。
    /// </summary>
    public static class EntityServiceExtensions
    {
        /// <summary>
        /// 使用 Lambda 表达式删除符合条件的实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityService">实体服务实例。</param>
        /// <param name="expression">定义删除条件的 Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <returns>受影响的行数。</returns>
        public static int Delete<T>(this IEntityService<T> entityService, Expression<Func<T, bool>> expression, params string[] tableArgs)
        {
            return entityService.Delete(Expr.Exp(expression), tableArgs);
        }

        /// <summary>
        /// 使用 Lambda 表达式搜索实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityViewService">实体视图服务实例。</param>
        /// <param name="expression">定义搜索条件的 Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <returns>符合条件的实体对象列表。</returns>
        public static List<T> Search<T>(this IEntityViewService<T> entityViewService, Expression<Func<T, bool>> expression, string[] tableArgs = null)
        {
            return entityViewService.Search(Expr.Exp(expression), tableArgs);
        }

        /// <summary>
        /// 使用 IQueryable 形式的 Lambda 表达式搜索实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityViewService">实体视图服务实例。</param>
        /// <param name="expression">定义查询条件的 IQueryable Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <returns>符合条件的实体对象列表。</returns>
        public static List<T> Search<T>(this IEntityViewService<T> entityViewService, Expression<Func<IQueryable<T>, IQueryable<T>>> expression, string[] tableArgs = null)
        {
            return entityViewService.Search(Expr.Query(expression), tableArgs);
        }

        /// <summary>
        /// 使用 Lambda 表达式搜索单个实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityViewService">实体视图服务实例。</param>
        /// <param name="expression">定义搜索条件的 Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <returns>第一个符合条件的实体对象；如果没有找到则返回 null。</returns>
        public static T SearchOne<T>(this IEntityViewService<T> entityViewService, Expression<Func<T, bool>> expression, string[] tableArgs = null)
        {
            return entityViewService.SearchOne(Expr.Exp(expression), tableArgs);
        }

        /// <summary>
        /// 使用 IQueryable 形式的 Lambda 表达式搜索单个实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityViewService">实体视图服务实例。</param>
        /// <param name="expression">定义查询条件的 IQueryable Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <returns>第一个符合条件的实体对象；如果没有找到则返回 null。</returns>
        public static T SearchOne<T>(this IEntityViewService<T> entityViewService, Expression<Func<IQueryable<T>, IQueryable<T>>> expression, string[] tableArgs = null)
        {
            return entityViewService.SearchOne(Expr.Query(expression), tableArgs);
        }

        /// <summary>
        /// 使用 Lambda 表达式检查是否存在符合条件的实体。
        /// </summary>
        public static bool Exists<T>(this IEntityViewService<T> entityViewService, Expression<Func<T, bool>> expression, params string[] tableArgs)
        {
            return entityViewService.Exists(Expr.Exp(expression), tableArgs);
        }

        /// <summary>
        /// 使用 Lambda 表达式获取符合条件的实体总数。
        /// </summary>
        public static int Count<T>(this IEntityViewService<T> entityViewService, Expression<Func<T, bool>> expression, params string[] tableArgs)
        {
            return entityViewService.Count(Expr.Exp(expression), tableArgs);
        }

        /// <summary>
        /// 使用 Lambda 表达式异步检查是否存在符合条件的实体。
        /// </summary>
        public static Task<bool> ExistsAsync<T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<T, bool>> expression, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return entityViewService.ExistsAsync(Expr.Exp(expression), tableArgs, cancellationToken);
        }

        /// <summary>
        /// 使用 Lambda 表达式异步获取符合条件的实体总数。
        /// </summary>
        public static Task<int> CountAsync<T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<T, bool>> expression, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return entityViewService.CountAsync(Expr.Exp(expression), tableArgs, cancellationToken);
        }

        /// <summary>
        /// 使用 Lambda 表达式异步根据主键删除实体。
        /// </summary>
        public static Task<bool> DeleteIDAsync<T>(this IEntityServiceAsync<T> entityService, object id, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return entityService.DeleteIDAsync(id, tableArgs, cancellationToken);
        }

        /// <summary>
        /// 使用 Lambda 表达式异步删除符合条件的实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityService">支持异步操作的实体服务实例。</param>
        /// <param name="expression">定义删除条件的 Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>表示异步删除操作的任务，结果包含受影响的行数。</returns>
        public static Task<int> DeleteAsync<T>(this IEntityServiceAsync<T> entityService, Expression<Func<T, bool>> expression, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return entityService.DeleteAsync(Expr.Exp(expression), tableArgs, cancellationToken);
        }

        /// <summary>
        /// 使用 Lambda 表达式异步搜索实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityViewService">支持异步操作的实体视图服务实例。</param>
        /// <param name="expression">定义搜索条件的 Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>表示异步搜索操作的任务，结果包含符合条件的实体对象列表。</returns>
        public static Task<List<T>> SearchAsync<T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<T, bool>> expression, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return entityViewService.SearchAsync(Expr.Exp(expression), tableArgs, cancellationToken);
        }

        /// <summary>
        /// 使用 IQueryable 形式的 Lambda 表达式异步搜索实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityViewService">支持异步操作的实体视图服务实例。</param>
        /// <param name="expression">定义查询条件的 IQueryable Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>表示异步搜索操作的任务，结果包含符合条件的实体对象列表。</returns>
        public static Task<List<T>> SearchAsync<T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<IQueryable<T>, IQueryable<T>>> expression, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return entityViewService.SearchAsync(Expr.Query(expression), tableArgs, cancellationToken);
        }

        /// <summary>
        /// 使用 Lambda 表达式异步搜索单个实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityViewService">支持异步操作的实体视图服务实例。</param>
        /// <param name="expression">定义搜索条件的 Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>表示异步搜索操作的任务，结果包含符合条件的单个实体对象，未找到则返回 null。</returns>
        public static Task<T> SearchOneAsync<T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<T, bool>> expression, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return entityViewService.SearchOneAsync(Expr.Exp(expression), tableArgs, cancellationToken);
        }

        /// <summary>
        /// 使用 IQueryable 形式的 Lambda 表达式异步搜索单个实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityViewService">支持异步操作的实体视图服务实例。</param>
        /// <param name="expression">定义查询条件的 IQueryable Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>表示异步搜索操作的任务，结果包含符合条件的单个实体对象，未找到则返回 null。</returns>
        public static Task<T> SearchOneAsync<T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<IQueryable<T>, IQueryable<T>>> expression, string[] tableArgs = null, CancellationToken cancellationToken = default)
        {
            return entityViewService.SearchOneAsync(Expr.Query(expression), tableArgs, cancellationToken);
        }
    }
}
