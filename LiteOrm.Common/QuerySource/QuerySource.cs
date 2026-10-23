using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// <see cref="Expr.From{T}(string[])"/> 的返回类型，同时实现 <see cref="IQueryable{T}"/>。
    /// 未提供执行器时只作为 Lambda 内的表达式占位，用于拼装查询并在之后转成 <see cref="Expr"/>；
    /// 提供执行器（如 <c>new QuerySource&lt;User&gt;(dao)</c>）时可以直接取数。
    /// </summary>
    /// <remarks>
    /// <typeparamref name="T1"/> 允许是元组类型（<c>(A, B)</c>、<c>((A, B), C)</c>），
    /// 连接结果仍是 <see cref="QuerySource{T}"/>，因此可以继续调用 <c>Join&lt;T3&gt;</c> 追加多级连接。
    /// 终端的 <c>ToList</c> / <c>Count</c> / <c>FirstOrDefault</c> 在 Lambda 内由预注册的
    /// method handler 接管解析，不会真正执行。
    /// </remarks>
    /// <remarks>
    /// 同时实现 <see cref="IOrderedQueryable{T}"/>：<see cref="Queryable.OrderBy{TSource, TKey}(IQueryable{TSource}, Expression{Func{TSource, TKey}})"/>
    /// 会把 <see cref="IQueryProvider.CreateQuery{TElement}(Expression)"/> 的返回值直接转成 <see cref="IOrderedQueryable{T}"/>，
    /// 不实现该接口时排序会抛 <see cref="InvalidCastException"/>。
    /// </remarks>
    /// <typeparam name="T1">实体类型，或多级连接时的左元组类型</typeparam>
#if NET8_0_OR_GREATER
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2091", Justification = "T1 flows from Expr.From<T>, whose generic parameter already carries the required annotations; execution is delegated to the executor at runtime.")]
#endif
    public class QuerySource<T1> : IOrderedQueryable<T1>, IQuerySource
    {
        /// <summary>
        /// 未提供执行器时的异常消息。
        /// </summary>
        internal const string NoExecutorMessage =
            "This QuerySource has no executor and cannot run queries. " +
            "Construct it with an executor (new QuerySource<T>(dao)) or use it only inside a lambda " +
            "parsed by Expr.Lambda / ObjectViewDAO.Search(...).";

        private readonly IQuerySourceExecutor? _executor;
        private readonly Expression _expression;
        private readonly IQueryProvider _provider;

        /// <summary>
        /// 动态表名参数，由 <see cref="Expr.From{T}(string[])"/> 写入，解析时回填到根 <see cref="TableExpr"/>。
        /// </summary>
        internal string[]? TableArgs { get; set; }

        /// <summary>
        /// 无执行器构造：仅用于拼装表达式，枚举与异步方法都会抛异常。
        /// </summary>
        public QuerySource() : this((IQuerySourceExecutor?)null, null) { }

        /// <summary>
        /// 带执行器构造：可以直接枚举、调用 ToList / Count 等取数方法。
        /// </summary>
        /// <param name="executor">查询执行器，通常传入 <c>ObjectViewDAO&lt;T&gt;</c>，也可直接从依赖注入容器获取。</param>
        public QuerySource(IQuerySourceExecutor<T1> executor) : this((IQuerySourceExecutor)executor, null) { }

        /// <summary>
        /// 由 <see cref="QuerySourceProvider.CreateQuery{TElement}"/> 延续查询链时使用。
        /// </summary>
        /// <param name="executor">查询执行器。</param>
        /// <param name="expression">已累积的查询表达式，为 null 时以自身为根。</param>
        internal QuerySource(IQuerySourceExecutor? executor, Expression? expression)
        {
            _executor = executor;
            // 静态类型固定为 IQueryable<T1>，保证 Queryable 算子的参数校验与解析器的 UnwrapSourceType 都能识别
            _expression = expression ?? Expression.Constant(this, typeof(IQueryable<T1>));
            _provider = new QuerySourceProvider(executor);
        }

        #region IQueryable<T>

        /// <inheritdoc/>
        public Type ElementType => typeof(T1);

        /// <inheritdoc/>
        public Expression Expression => _expression;

        /// <inheritdoc/>
        public IQueryProvider Provider => _provider;

        /// <summary>
        /// 同步执行查询并逐行返回。未提供执行器时抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        /// <returns>结果枚举器。</returns>
        public IEnumerator<T1> GetEnumerator()
            => GetExecutor().AsEnumerable<T1>(GetQueryExpr()).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region 取数方法

        /// <summary>
        /// 异步流式读取结果。未提供执行器时抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        /// <remarks>
        /// 不直接实现 <see cref="IAsyncEnumerable{T}"/>：那样会让 <c>Where</c> / <c>Select</c> 在
        /// <see cref="Queryable"/> 与 System.Linq.Async 的同名扩展之间产生重载二义。
        /// 需要 <c>await foreach</c> 时调用本方法取得异步序列。
        /// </remarks>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步序列。</returns>
        public IAsyncEnumerable<T1> ToAsyncEnumerable(CancellationToken cancellationToken = default)
            => GetExecutor().AsAsyncEnumerable<T1>(GetQueryExpr(), cancellationToken);

        /// <summary>
        /// 同步取全部结果。Lambda 内由预注册 handler 接管，不真正执行。
        /// </summary>
        /// <returns>结果列表。</returns>
        public List<T1> ToList() => GetExecutor().AsEnumerable<T1>(GetQueryExpr()).ToList();

        /// <summary>
        /// 异步取全部结果。Lambda 内由预注册 handler 接管，不真正执行。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>结果列表。</returns>
        public Task<List<T1>> ToListAsync(CancellationToken cancellationToken = default)
            => GetExecutor().ToListAsync<T1>(GetQueryExpr(), cancellationToken);

        /// <summary>
        /// 同步计数。Lambda 内由预注册 handler 接管，不真正执行。
        /// </summary>
        /// <returns>符合条件的行数。</returns>
        public int Count() => GetExecutor().FirstOrDefault<int>(BuildCountExpr());

        /// <summary>
        /// 异步计数。Lambda 内由预注册 handler 接管，不真正执行。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>符合条件的行数。</returns>
        public async Task<int> CountAsync(CancellationToken cancellationToken = default)
            => await GetExecutor().FirstOrDefaultAsync<int>(BuildCountExpr(), cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// 同步取第一行，无数据时返回默认值。Lambda 内由预注册 handler 接管，不真正执行。
        /// </summary>
        /// <returns>第一行结果。</returns>
        public T1 FirstOrDefault() => GetExecutor().FirstOrDefault<T1>(GetQueryExpr());

        /// <summary>
        /// 异步取第一行，无数据时返回默认值。Lambda 内由预注册 handler 接管，不真正执行。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>第一行结果。</returns>
        public Task<T1> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
            => GetExecutor().FirstOrDefaultAsync<T1>(GetQueryExpr(), cancellationToken);

        #endregion

        #region 查询表达式

        /// <summary>
        /// 把当前累积的查询表达式转换为 <see cref="SqlSegment"/>，可直接传给 <c>ObjectViewDAO.Search</c> / <c>SearchAs</c>。
        /// </summary>
        /// <remarks>
        /// 不需要执行器也可调用，未连接数据库时同样可用，适合把查询拼装与执行拆成两处。
        /// 与 <c>Expr.Query</c> 对应的写法是 <c>Expr.From&lt;T&gt;().Where(...).GetQueryExpr()</c>。
        /// <see cref="Queryable"/> 算子之后的静态类型是 <see cref="IQueryable{T}"/>，链尾请改用
        /// <see cref="QuerySourceExtensions.GetQueryExpr{T}(IQueryable{T})"/>，行为一致。
        /// </remarks>
        /// <returns>已解析的查询段。</returns>
        public SqlSegment GetQueryExpr() => QuerySourceExprConverter.ToExpr(_expression);

        /// <summary>
        /// 构造 COUNT(1) 查询，源本身是 SelectExpr 时取其 Source 避免嵌套 Select。
        /// </summary>
        private SqlSegment BuildCountExpr()
        {
            Expr source = GetQueryExpr();
            if (source is SelectExpr select && select.Source is not null) source = select.Source;
            return new SelectExpr(source as SqlSegment, new SelectItemExpr(Expr.Aggregate("COUNT", Expr.Const(1))));
        }

        #endregion

        #region 内部支持

        /// <summary>
        /// 取得执行器，未设置时抛异常。
        /// </summary>
        internal IQuerySourceExecutor GetExecutor()
            => _executor ?? throw new InvalidOperationException(NoExecutorMessage);

        /// <summary>
        /// 关联的执行器，供多值泛型查询源复用。
        /// </summary>
        internal IQuerySourceExecutor? Executor => _executor;

        /// <summary>
        /// 已累积的查询表达式，供多值泛型查询源在其上追加连接。
        /// </summary>
        internal Expression QueryExpression => _expression;

        string[]? IQuerySource.TableArgs => TableArgs;

        #endregion

        #region Join / JoinRelated

        /// <summary>
        /// 连接另一个实体表，并使用显式的连接条件。返回 <see cref="QuerySource{T1,T2}"/>，可继续调用 <c>Join&lt;T3&gt;</c> 追加连接。
        /// </summary>
        /// <typeparam name="T2">右表实体类型。</typeparam>
        /// <param name="on">连接条件表达式。</param>
        /// <param name="joinType">连接类型，为 null 时使用左连接。</param>
        /// <returns>元素为左右两表元组的查询源。</returns>
        public QuerySource<T1, T2> Join<T2>(Expression<Func<T1, T2, bool>> on, TableJoinType? joinType = null)
        {
            if (on is null) throw new ArgumentNullException(nameof(on));
            Func<Expression<Func<T1, T2, bool>>, TableJoinType?, QuerySource<T1, T2>> target = Join;
            return new QuerySource<T1, T2>(_executor,
                QuerySourceExpressionBuilder.BuildJoinCall(_expression, target.Method, Expression.Quote(on), QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }

        /// <summary>
        /// 连接另一个实体表，并按外键元数据自动推断连接条件。
        /// 推断时按关联顺序从后向前匹配：优先使用最近一次连接的表，找不到关系时再逐级向前回退，最后才使用主表。
        /// </summary>
        /// <typeparam name="T2">右表实体类型。</typeparam>
        /// <param name="joinType">连接类型，为 null 时沿用元数据中声明的连接方式。</param>
        /// <returns>元素为左右两表元组的查询源。</returns>
        public QuerySource<T1, T2> JoinRelated<T2>(TableJoinType? joinType = null)
        {
            Func<TableJoinType?, QuerySource<T1, T2>> target =
                new Func<TableJoinType?, QuerySource<T1, T2>>(JoinRelated<T2>);
            return new QuerySource<T1, T2>(_executor,
                QuerySourceExpressionBuilder.BuildJoinCall(_expression, target.Method, QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }

        /// <summary>
        /// 连接另一个实体表，自动推断的连接条件与显式条件按 AND 合并。
        /// </summary>
        /// <typeparam name="T2">右表实体类型。</typeparam>
        /// <param name="on">附加的连接条件，与自动推断的关联条件取 AND。</param>
        /// <param name="joinType">连接类型，为 null 时沿用元数据中声明的连接方式。</param>
        /// <returns>元素为左右两表元组的查询源。</returns>
        public QuerySource<T1, T2> JoinRelated<T2>(Expression<Func<T1, T2, bool>> on, TableJoinType? joinType = null)
        {
            if (on is null) throw new ArgumentNullException(nameof(on));
            Func<Expression<Func<T1, T2, bool>>, TableJoinType?, QuerySource<T1, T2>> target = JoinRelated<T2>;
            return new QuerySource<T1, T2>(_executor,
                QuerySourceExpressionBuilder.BuildJoinCall(_expression, target.Method, Expression.Quote(on), QuerySourceExpressionBuilder.JoinTypeArgument(joinType)));
        }

        #endregion
    }

    /// <summary>
    /// 拼接连接查询表达式，供各元数（arity）的查询源共用。
    /// </summary>
    internal static class QuerySourceExpressionBuilder
    {
        /// <summary>
        /// 把实例方法调用拼成表达式节点；源表达式静态类型不是方法声明类型时插入一次引用转换。
        /// </summary>
        /// <param name="source">左源表达式。</param>
        /// <param name="method">连接方法。</param>
        /// <param name="arguments">除接收者以外的方法参数。</param>
        /// <returns>方法调用表达式。</returns>
        internal static Expression BuildJoinCall(Expression source, MethodInfo method, params Expression[] arguments)
        {
            Type declaringType = method.DeclaringType!;
            Expression instance = declaringType.IsAssignableFrom(source.Type)
                ? source
                : Expression.Convert(source, declaringType);
            return Expression.Call(instance, method, arguments);
        }

        /// <summary>
        /// 把连接类型包成可放进表达式树的常量；null 表示沿用默认规则。
        /// </summary>
        /// <param name="joinType">连接类型。</param>
        /// <returns>连接类型常量。</returns>
        internal static ConstantExpression JoinTypeArgument(TableJoinType? joinType)
            => Expression.Constant(joinType, typeof(TableJoinType?));
    }


    /// <summary>
    /// 非泛型视图：让解析器无需反射即可拿到查询根的元数据。
    /// </summary>
    internal interface IQuerySource
    {
        /// <summary>动态表名参数。</summary>
        string[]? TableArgs { get; }
    }
}