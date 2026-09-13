using LiteOrm.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace LiteOrm
{
    /// <summary>
    /// 纯手动创建的 LiteOrm 上下文，不经任何依赖注入容器即可创建 <see cref="ObjectDAO{T}"/> / <see cref="ObjectViewDAO{T}"/>
    /// 并执行增删改查。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这是与 DI 集成（<c>AddLiteOrm()</c> / <c>RegisterLiteOrm()</c>）<b>完全分开</b>的一条线路：
    /// 本类型不引用 <c>IServiceCollection</c>，不注册 <c>IConfiguration</c>；
    /// <see cref="CreateSession"/> 会把新建会话绑定为 <see cref="SessionManager.Current"/>，
    /// 其余生命周期都由创建方自己持有。
    /// </para>
    ///
    /// <code>
    /// using var context = new LiteOrmContext()
    ///     .AddDataSource&lt;SqliteConnection&gt;("main", "Data Source=main.db");
    ///
    /// using var session = context.CreateSession();
    /// var dao = new ObjectDAO&lt;User&gt;(session);
    /// var users = dao.Search(Expr.Prop("Age") &gt; 18);
    /// </code>
    ///
    /// <para>
    /// 若要使用 AOT 或裁剪，请用泛型重载 <c>AddDataSource&lt;TConnection&gt;</c> 在代码里写出连接类型；
    /// 开启 AOT 构建（或显式声明 <c>[assembly: LiteOrmCodeGen]</c>）时，源生成器会在编译期为编译单元中
    /// 出现的 <c>DbConnection</c> / <c>SqlBuilder</c> 派生类型生成 <c>RegisterDbConnectionType</c> /
    /// <c>RegisterSqlBuilderType</c> 注册代码，使运行期按名称反查仍可命中。
    /// </para>
    /// </remarks>
    public sealed class LiteOrmContext : IDisposable
    {
        private readonly DataSourceProvider _dataSourceProvider = new DataSourceProvider();
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger<SessionManager>? _sessionLogger;
        private readonly ILogger<DAOContextPoolFactory>? _poolFactoryLogger;
        private DAOContextPoolFactory? _poolFactory;
        private bool _disposed;

        /// <summary>
        /// 初始化一个新的上下文。
        /// </summary>
        /// <param name="loggerFactory">
        /// 日志工厂；为 null 时连接池与会话都不记录日志。
        /// </param>
        public LiteOrmContext(ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory;
            _sessionLogger = loggerFactory?.CreateLogger<SessionManager>();
            _poolFactoryLogger = loggerFactory?.CreateLogger<DAOContextPoolFactory>();
        }

        /// <summary>
        /// 已配置的数据源配置集合。
        /// </summary>
        public ICollection<DataSourceConfig> DataSources => _dataSourceProvider.DataSources;

        /// <summary>
        /// 默认数据源名称；未显式设置时返回首个DataSource名称。
        /// </summary>
        public string? DefaultDataSourceName
        {
            get
            {
                var name = _dataSourceProvider.DefaultDataSourceName;
                if (!string.IsNullOrWhiteSpace(name)) return name;
                return _dataSourceProvider.DataSources.FirstOrDefault()?.Name;
            }
        }

        /// <summary>
        /// 按名称获取已配置的数据源。
        /// </summary>
        /// <param name="name">数据源名称；为 null 时返回默认数据源。</param>
        /// <returns>数据源配置；不存在时返回 null。</returns>
        public DataSourceConfig? GetDataSource(string? name = null) => _dataSourceProvider.GetDataSource(name!);

        /// <summary>
        /// 创建一个会话。每次调用都返回新的 <see cref="SessionManager"/> 实例，
        /// 调用方负责释放；上下文被释放时会一并释放其创建的全部会话。
        /// </summary>
        /// <returns>新建的会话管理器。</returns>
        /// <exception cref="ObjectDisposedException">上下文已释放时抛出。</exception>
        /// <remarks>
        /// 本方法同时设置 <see cref="SessionManager.Current"/> 为新建的会话，会话通过构造参数显式传给
        /// <see cref="ObjectDAO{T}"/> 等对象，与 DI 线路互不干扰。首次调用会按当前数据源配置
        /// 建好全部连接池，此后不再接受新的数据源。
        /// </remarks>
        public SessionManager CreateSession()
        {
            ThrowIfDisposed();
            var session = new SessionManager(PoolFactory, _sessionLogger);
            SessionManager.SetCurrent(() => session);
            return session;
        }

        /// <summary>
        /// 添加或更新一个数据源，并返回上下文本身以便继续链式调用。
        /// </summary>
        /// <param name="config">数据源配置。</param>
        /// <param name="default">是否设为默认数据源；为 true 时同时覆盖当前的默认数据源。</param>
        /// <returns>当前上下文。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="config"/> 为 null 时抛出。</exception>
        /// <exception cref="InvalidOperationException">连接池工厂已创建时抛出。</exception>
        /// <remarks>
        /// 数据源的连接池参数、建表同步等全部设置都在本次调用中一次性确定，
        /// 上下文不提供后续的补充设置方法。连接池工厂一旦创建，便不再接受新的数据源。
        /// </remarks>
        public LiteOrmContext AddDataSource(DataSourceConfig config, bool @default = false)
        {
            ThrowIfDisposed();
            if (config is null) throw new ArgumentNullException(nameof(config));
            ThrowIfPoolsCreated(nameof(AddDataSource));

            if (string.IsNullOrWhiteSpace(config.Name))
                throw new ArgumentException("DataSource name cannot be empty", nameof(config));

            _dataSourceProvider.AddDataSource(config);

            if (@default || (_dataSourceProvider.DataSources.Count == 1 && string.IsNullOrWhiteSpace(_dataSourceProvider.DefaultDataSourceName)))
            {
                _dataSourceProvider.SetDefaultDataSource(config.Name!);
            }
            return this;
        }

        /// <summary>
        /// 添加或更新一个数据源。数据库提供程序直接取泛型参数 <typeparamref name="TConnection"/>。
        /// </summary>
        /// <typeparam name="TConnection">数据库连接类型，例如 <c>SqliteConnection</c>。</typeparam>
        /// <param name="name">数据源名称。</param>
        /// <param name="connectionString">连接字符串。</param>
        /// <param name="default">是否设为默认数据源；为 true 时同时覆盖当前的默认数据源。</param>
        /// <param name="syncTable">是否自动同步建表结构。</param>
        /// <param name="sqlBuilder">SQL 构建器类型（可选，不指定时按连接类型自动匹配）。</param>
        /// <param name="poolSize">连接池缓存数量；为 null 时沿用配置默认值。</param>
        /// <param name="maxPoolSize">最大并发连接数；为 null 时沿用配置默认值。</param>
        /// <param name="paramCountLimit">单条 SQL 参数数量上限；为 null 时沿用配置默认值。</param>
        /// <param name="keepAliveDuration">连接保活时长；为 null 时沿用配置默认值。</param>
        /// <returns>当前上下文。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="name"/> 为空时抛出。</exception>
        /// <exception cref="InvalidOperationException">连接池工厂已创建时抛出。</exception>
        public LiteOrmContext AddDataSource<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TConnection>(
            string name = DefaultDataSourceKey,
            string? connectionString = null,
            bool @default = false,
            bool syncTable = false,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type? sqlBuilder = null,
            int? poolSize = null,
            int? maxPoolSize = null,
            int? paramCountLimit = null,
            TimeSpan? keepAliveDuration = null)
            where TConnection : DbConnection, new()
        {
            return AddDataSource(new DataSourceConfig(typeof(TConnection), connectionString)
            {
                Name = name,
                SqlBuilderType = sqlBuilder,
                SyncTable = syncTable,
                PoolSize = poolSize ?? DefaultPoolSize,
                MaxPoolSize = maxPoolSize ?? DefaultMaxPoolSize,
                ParamCountLimit = paramCountLimit ?? DefaultParamCountLimit,
                KeepAliveDuration = keepAliveDuration ?? DefaultKeepAliveDuration
            }, @default);
        }

        /// <summary>
        /// 默认数据源名称的约定值，与 DI 线路保持一致。
        /// </summary>
        public const string DefaultDataSourceKey = "DefaultConnection";

        /// <summary>连接池缓存数量默认值，与 <see cref="DataSourceConfig.PoolSize"/> 一致。</summary>
        private const int DefaultPoolSize = 16;

        /// <summary>最大并发连接数默认值，与 <see cref="DataSourceConfig.MaxPoolSize"/> 一致。</summary>
        private const int DefaultMaxPoolSize = 100;

        /// <summary>单条 SQL 参数上限默认值，与 <see cref="DataSourceConfig.ParamCountLimit"/> 一致。</summary>
        private const int DefaultParamCountLimit = 1000;

        /// <summary>连接保活时长默认值，与 <see cref="DataSourceConfig.KeepAliveDuration"/> 一致。</summary>
        private static readonly TimeSpan DefaultKeepAliveDuration = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 连接池工厂。首次访问时按需创建并一次性建好全部已配置数据源的连接池。
        /// </summary>
        private DAOContextPoolFactory PoolFactory
        {
            get
            {
                ThrowIfDisposed();
                return _poolFactory ??= new DAOContextPoolFactory(_dataSourceProvider, _poolFactoryLogger);
            }
        }

        /// <summary>
        /// 释放上下文持有的连接池工厂。
        /// </summary>
        /// <remarks>
        /// 只销毁连接池工厂，不修改 <see cref="SessionManager.Current"/>，因此不会影响同一进程内的 DI 线路。
        /// 由调用方自己创建的会话需要自行释放，请在释放上下文之前完成。
        /// </remarks>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _poolFactory?.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LiteOrmContext));
        }

        private void ThrowIfPoolsCreated(string operation)
        {
            if (_poolFactory is not null)
            {
                throw new InvalidOperationException(
                    $"The connection pool factory has already been created; call {operation} before calling CreateSession.");
            }
        }
    }
}
