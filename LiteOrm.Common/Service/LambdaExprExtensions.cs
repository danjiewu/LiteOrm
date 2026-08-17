using LiteOrm.Service;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// 提供针对 Lambda 表达式到 Expr 对象的扩展方法，简化实体查询操作。
    /// </summary>
    public static class LambdaExprExtensions
    {
        /// <summary>
        /// 使用 Lambda 表达式删除符合条件的实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityService">实体服务实例。</param>
        /// <param name="expression">定义删除条件的 Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <returns>受影响的行数。</returns>
        public static int Delete<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityService<T> entityService, Expression<Func<T, bool>> expression, params string[] tableArgs)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return entityService.DeleteAll(lambdaConvert.ToLogicExpr(), tableArgs ?? lambdaConvert.Table?.TableArgs);
        }

        /// <summary>
        /// 使用 Lambda 表达式搜索实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityViewService">实体视图服务实例。</param>
        /// <param name="expression">定义搜索条件的 Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <returns>符合条件的实体对象列表。</returns>
        public static List<T> Search<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityViewService<T> entityViewService, Expression<Func<T, bool>> expression, string[]? tableArgs = null)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return entityViewService.Search(lambdaConvert.ToLogicExpr(), tableArgs ?? lambdaConvert.Table?.TableArgs);
        }

        /// <summary>
        /// 使用 IQueryable 形式的 Lambda 表达式搜索实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityViewService">实体视图服务实例。</param>
        /// <param name="expression">定义查询条件的 IQueryable Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <returns>符合条件的实体对象列表。</returns>
        public static List<T> Search<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityViewService<T> entityViewService, Expression<Func<IQueryable<T>, IQueryable<T>>> expression, string[]? tableArgs = null)
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
        public static T SearchOne<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityViewService<T> entityViewService, Expression<Func<T, bool>> expression, string[]? tableArgs = null)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return entityViewService.SearchOne(lambdaConvert.ToLogicExpr(), tableArgs ?? lambdaConvert.Table?.TableArgs);
        }

        /// <summary>
        /// 使用 IQueryable 形式的 Lambda 表达式搜索单个实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityViewService">实体视图服务实例。</param>
        /// <param name="expression">定义查询条件的 IQueryable Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <returns>第一个符合条件的实体对象；如果没有找到则返回 null。</returns>
        public static T SearchOne<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityViewService<T> entityViewService, Expression<Func<IQueryable<T>, IQueryable<T>>> expression, string[]? tableArgs = null)
        {
            return entityViewService.SearchOne(Expr.Query(expression), tableArgs);
        }

        /// <summary>
        /// 使用 Lambda 表达式检查是否存在符合条件的实体。
        /// </summary>
        public static bool Exists<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityViewService<T> entityViewService, Expression<Func<T, bool>> expression, params string[] tableArgs)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return entityViewService.Exists(lambdaConvert.ToLogicExpr(), tableArgs ?? lambdaConvert.Table?.TableArgs);
        }

        /// <summary>
        /// 使用 Lambda 表达式获取符合条件的实体总数。
        /// </summary>
        public static int Count<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityViewService<T> entityViewService, Expression<Func<T, bool>> expression, params string[] tableArgs)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return entityViewService.Count(lambdaConvert.ToLogicExpr(), tableArgs ?? lambdaConvert.Table?.TableArgs);
        }

        /// <summary>
        /// 使用 Lambda 表达式更新符合条件的实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityService">实体服务实例。</param>
        /// <param name="updateExpression">定义更新操作的 Lambda 表达式。</param>
        /// <param name="expression">定义更新条件的 Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <returns>受影响的行数。</returns>
        public static int UpdateAll<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityService<T> entityService, Expression<Func<T, T>> updateExpression, Expression<Func<T, bool>> expression, params string[] tableArgs)
        {
            return entityService.UpdateAll(Expr.Update(updateExpression, expression), tableArgs);
        }

        /// <summary>
        /// 使用 Lambda 表达式异步检查是否存在符合条件的实体。
        /// </summary>
        public static Task<bool> ExistsAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<T, bool>> expression, string[]? tableArgs = null, CancellationToken cancellationToken = default)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return entityViewService.ExistsAsync(lambdaConvert.ToLogicExpr(), tableArgs ?? lambdaConvert.Table?.TableArgs, cancellationToken);
        }

        /// <summary>
        /// 使用 Lambda 表达式异步获取符合条件的实体总数。
        /// </summary>
        public static Task<int> CountAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<T, bool>> expression, string[]? tableArgs = null, CancellationToken cancellationToken = default)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return entityViewService.CountAsync(lambdaConvert.ToLogicExpr(), tableArgs ?? lambdaConvert.Table?.TableArgs, cancellationToken);
        }

        /// <summary>
        /// 使用 Lambda 表达式异步根据主键删除实体。
        /// </summary>
        public static Task<bool> DeleteIDAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityServiceAsync<T> entityService, object id, string[]? tableArgs = null, CancellationToken cancellationToken = default)
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
        public static Task<int> DeleteAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityServiceAsync<T> entityService, Expression<Func<T, bool>> expression, string[]? tableArgs = null, CancellationToken cancellationToken = default)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return entityService.DeleteAllAsync(lambdaConvert.ToLogicExpr(), tableArgs ?? lambdaConvert.Table?.TableArgs, cancellationToken);
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
        public static Task<List<T>> SearchAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<T, bool>> expression, string[]? tableArgs = null, CancellationToken cancellationToken = default)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return entityViewService.SearchAsync(lambdaConvert.ToLogicExpr(), tableArgs ?? lambdaConvert.Table?.TableArgs, cancellationToken);
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
        public static Task<List<T>> SearchAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<IQueryable<T>, IQueryable<T>>> expression, string[]? tableArgs = null, CancellationToken cancellationToken = default)
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
        public static Task<T> SearchOneAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<T, bool>> expression, string[]? tableArgs = null, CancellationToken cancellationToken = default)
        {
            var lambdaConvert = new LambdaExprConverter(expression);
            return entityViewService.SearchOneAsync(lambdaConvert.ToLogicExpr(), tableArgs ?? lambdaConvert.Table?.TableArgs, cancellationToken);
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
        public static Task<T> SearchOneAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityViewServiceAsync<T> entityViewService, Expression<Func<IQueryable<T>, IQueryable<T>>> expression, string[]? tableArgs = null, CancellationToken cancellationToken = default)
        {
            return entityViewService.SearchOneAsync(Expr.Query(expression), tableArgs, cancellationToken);
        }

        /// <summary>
        /// 使用 Lambda 表达式异步更新符合条件的实体。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <param name="entityService">支持异步操作的实体服务实例。</param>
        /// <param name="updateExpression">定义更新字段和值的 Lambda 表达式。</param>
        /// <param name="expression">定义 WHERE 条件的 Lambda 表达式（可选）。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <param name="cancellationToken">取消操作的令牌。</param>
        /// <returns>表示异步更新操作的任务，结果包含受影响的行数。</returns>
        public static Task<int> UpdateAllAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(this IEntityServiceAsync<T> entityService, Expression<Func<T, T>> updateExpression, Expression<Func<T, bool>> expression, string[]? tableArgs = null, CancellationToken cancellationToken = default)
        {
            return entityService.UpdateAllAsync(Expr.Update(updateExpression, expression), tableArgs, cancellationToken);
        }

        /// <summary>
        /// 使用 IQueryable 形式的 Lambda 表达式查询实体，并将结果投影为指定类型的列表。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <typeparam name="TResult">结果类型。</typeparam>
        /// <param name="entityViewService">实体视图服务实例。</param>
        /// <param name="expression">定义查询与投影的 IQueryable Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <returns>投影后的结果列表。</returns>
        public static List<TResult> SearchAs<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(
            this IEntityViewService<T> entityViewService,
            Expression<Func<IQueryable<T>, IQueryable<TResult>>> expression,
            string[]? tableArgs = null)
        {
            var selectExpr = LambdaExprConverter.ToSqlSegment(expression);
            return entityViewService.SearchAs<TResult>(ToSelectExpr<T>(selectExpr), tableArgs!);
        }

        /// <summary>
        /// 使用 IQueryable 形式的 Lambda 表达式查询单个实体，并将结果投影为指定类型。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <typeparam name="TResult">结果类型。</typeparam>
        /// <param name="entityViewService">实体视图服务实例。</param>
        /// <param name="expression">定义查询与投影的 IQueryable Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <returns>第一个符合条件的投影结果；未找到时返回默认值。</returns>
        public static TResult SearchOneAs<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(
            this IEntityViewService<T> entityViewService,
            Expression<Func<IQueryable<T>, IQueryable<TResult>>> expression,
            string[]? tableArgs = null)
        {
            var selectExpr = LambdaExprConverter.ToSqlSegment(expression);
            return entityViewService.SearchOneAs<TResult>(ToSelectExpr<T>(selectExpr), tableArgs!);
        }

        /// <summary>
        /// 使用 IQueryable 形式的 Lambda 表达式异步查询实体，并将结果投影为指定类型的列表。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <typeparam name="TResult">结果类型。</typeparam>
        /// <param name="entityViewService">实体视图服务实例。</param>
        /// <param name="expression">定义查询与投影的 IQueryable Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <returns>表示异步查询的任务，结果包含投影后的列表。</returns>
        public static Task<List<TResult>> SearchAsAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(
            this IEntityViewServiceAsync<T> entityViewService,
            Expression<Func<IQueryable<T>, IQueryable<TResult>>> expression,
            string[]? tableArgs = null)
        {
            var selectExpr = LambdaExprConverter.ToSqlSegment(expression);
            return entityViewService.SearchAsAsync<TResult>(ToSelectExpr<T>(selectExpr), tableArgs!);
        }

        /// <summary>
        /// 使用 IQueryable 形式的 Lambda 表达式异步查询单个实体，并将结果投影为指定类型。
        /// </summary>
        /// <typeparam name="T">实体类型。</typeparam>
        /// <typeparam name="TResult">结果类型。</typeparam>
        /// <param name="entityViewService">实体视图服务实例。</param>
        /// <param name="expression">定义查询与投影的 IQueryable Lambda 表达式。</param>
        /// <param name="tableArgs">动态表名参数（可选）。</param>
        /// <returns>表示异步查询的任务，结果包含第一个符合条件的投影结果；未找到时返回默认值。</returns>
        public static Task<TResult> SearchOneAsAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] TResult>(
            this IEntityViewServiceAsync<T> entityViewService,
            Expression<Func<IQueryable<T>, IQueryable<TResult>>> expression,
            string[]? tableArgs = null)
        {
            var selectExpr = LambdaExprConverter.ToSqlSegment(expression);
            return entityViewService.SearchOneAsAsync<TResult>(ToSelectExpr<T>(selectExpr), tableArgs!);
        }

        /// <summary>
        /// 将 Lambda 转换得到的片段包装为 <see cref="SelectExpr"/>（若本身已是 <see cref="SelectExpr"/> 则直接返回）。
        /// </summary>
        private static SelectExpr ToSelectExpr<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(Expr expr)
        {
            if (expr is SelectExpr selectExpr) return selectExpr;
            var selects = new List<SelectItemExpr>();
            TableView? view = TableInfoProvider.Instance.GetTableView(typeof(T));
            if (view != null)
            {
                foreach (SqlColumn col in view.SelectColumns)
                {
                    selects.Add(new SelectItemExpr(Expr.Prop(col.PropertyName), col.PropertyName));
                }
            }
            return new SelectExpr { Source = expr.ToSource(typeof(T)), Selects = selects };
        }
    }
}
