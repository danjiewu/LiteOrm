using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Common
{


    #region IObjectViewDAO<T>
    /// <summary>
    /// 实体类的查询数据访问对象的泛型接口
    /// </summary>
    /// <typeparam name="T">实体类类型</typeparam>
    public interface IObjectViewDAO<T> : IObjectViewDAO
    {
        /// <summary>
        /// 根据主键获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>可枚举结果对象，可通过FirstOrDefault()和FirstOrDefaultAsync()获取结果</returns>
        new EnumerableResult<T> GetObject(params object[] keys);
        /// <summary>
        /// 根据条件查询
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <returns>符合条件的对象枚举，同时支持同步和异步操作</returns>
        new EnumerableResult<T> Search(Expr expr = null);
        /// <summary>
        /// 根据 Lambda 表达式查询对象集合。
        /// </summary>
        /// <param name="expr">Lambda 表达式，用于生成 SQL 查询。</param>
        /// <returns>符合条件的对象集合。</returns>
        EnumerableResult<T> Search(Expression<Func<IQueryable<T>, IQueryable<T>>> expr);
        /// <summary>
        /// 使用带参数的SQL查询
        /// </summary>
        /// <param name="sqlBody">查询SQL，使用插值字符串格式，可插入普通变量或 Expr。<see cref="LiteOrm.Common.ExprString"/></param>
        /// <param name="isFull">传入的是否是完整SQL，默认为 false</param>
        /// <returns>符合条件的对象枚举，同时支持同步和异步操作</returns>
        /// <example>
        /// <code>
        /// var users = objectViewDAO.Search($"WHERE {Expr.Prop("Age") > 20 }");
        /// </code>
        /// </example>
        new EnumerableResult<T> Search([InterpolatedStringHandlerArgument("")] ref ExprString sqlBody, bool isFull = false);
        /// <summary>
        /// 执行 Lambda 表达式，并返回自定义类型集合。
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="expr">Lambda 表达式，用于生成 SQL 查询</param>
        /// <param name="readerFunc">用于从 IDataReader 读取结果的函数，为空时默认使用 <see cref="DataReaderConverter.GetConverter{TResult}()"/></param>
        /// <returns></returns>

        EnumerableResult<TResult> SearchAs<TResult>(Expression<Func<IQueryable<T>, IQueryable<TResult>>> expr, Func<DbDataReader, TResult> readerFunc = null);
    }
    #endregion

    #region IObjectViewDAO
    /// <summary>
    /// 实体类的查询数据访问对象的非泛型接口
    /// </summary>
    [AutoRegister(false)]
    public interface IObjectViewDAO : IExprStringBuildContext
    {
        /// <summary>
        /// 根据主键获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>可枚举结果对象，可通过FirstOrDefault()和FirstOrDefaultAsync()获取结果</returns>
        IEnumerableResult GetObject(params object[] keys);

        /// <summary>
        /// 判断主键对应的对象是否存在
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>值结果对象，可通过GetValue()和GetValueAsync()获取结果</returns>
        ValueResult<bool> ExistsKey(params object[] keys);

        /// <summary>
        /// 判断对象是否存在
        /// </summary>
        /// <param name="o">对象</param>
        /// <returns>值结果对象，可通过GetValue()和GetValueAsync()获取结果</returns>
        ValueResult<bool> Exists(object o);

        /// <summary>
        /// 判断符合条件的对象是否存在
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <returns>值结果对象，可通过GetValue()和GetValueAsync()获取结果</returns>
        ValueResult<bool> Exists(Expr expr);

        /// <summary>
        /// 得到符合条件的对象个数
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <returns>值结果对象，可通过GetValue()和GetValueAsync()获取结果</returns>
        ValueResult<int> Count(Expr expr);

        /// <summary>
        /// 根据条件查询
        /// </summary>
        /// <param name="expr">查询条件，若为null则表示没有条件</param>
        /// <returns>符合条件的对象列表</returns>
        IEnumerableResult Search(Expr expr);
        /// <summary>
        /// 使用带参数的SQL查询
        /// </summary>
        /// <param name="sqlBody">查询SQL，使用插值字符串格式，可插入普通变量或 Expr。<see cref="LiteOrm.Common.ExprString"/></param>
        /// <param name="isFull">传入的是否是完整SQL，默认为 false</param>
        /// <returns>符合条件的对象枚举，同时支持同步和异步操作</returns>
        /// <example>
        /// <code>
        /// var users = objectViewDAO.Search($"WHERE {Expr.Prop("Age") > 20 }");
        /// </code>
        /// </example>
        IEnumerableResult Search([InterpolatedStringHandlerArgument("")] ref ExprString sqlBody, bool isFull = false);
        /// <summary>
        /// 根据自定义 SELECT 表达式查询并转换为指定类型集合。
        /// </summary>
        /// <typeparam name="TResult">结果类型。</typeparam>
        /// <param name="selectExpr">SELECT 表达式。</param>
        /// <param name="readerFunc">读取器转换函数，为 null 时使用默认转换。</param>
        /// <returns>自定义类型的集合。</returns>
        EnumerableResult<TResult> SearchAs<TResult>(SelectExpr selectExpr, Func<DbDataReader, TResult> readerFunc = null);
        /// <summary>
        /// 使用带参数的SQL查询
        /// </summary>
        /// <param name="sqlBody">完整的查询SQL，使用插值字符串格式，可插入普通变量或 Expr。<see cref="LiteOrm.Common.ExprString"/></param>
        /// <returns>符合条件的对象枚举，同时支持同步和异步操作</returns>
        /// <example>
        /// <code>
        /// var users = objectViewDAO.SearchAs&lt;int&gt;($"SELECT ID FROM {{FROM}} WHERE {Expr.Prop("Age") > 20 }");
        /// </code>
        /// </example>
        EnumerableResult<TResult> SearchAs<TResult>([InterpolatedStringHandlerArgument("")] ref ExprString sqlBody);
    }
    #endregion
}
