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
    /// 纯手动创建的 LiteOrm 客户端。把数据源配置、连接池工厂与会话收敛成一条链，
    /// 不经任何依赖注入容器即可创建 <see cref="ObjectDAO{T}"/> / <see cref="ObjectViewDAO{T}"/>
    /// 并执行增删改查。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 这是与 DI 集成（<c>AddLiteOrm()</c> / <c>RegisterLiteOrm()</c>）<b>完全分开</b>的一条线路：
    /// 本类型不引用 <c>IServiceCollection</c>，不注册 <c>IConfiguration</c>，也不接管
    /// <see cref="SessionManager.Current"/>，一切生命周期都由创建方自己持有。
    /// </para>
    ///
    /// <code>
    /// using var client = new LiteOrmClient()
    ///     .AddDataSource&lt;SqliteConnection&gt;("main", "Data Source=main.db", @default: true, poolSize: 8, maxPoolSize: 32, paramCountLimit: 500);
    ///
    /// using var session = client.CreateSession();
    /// var dao = new ObjectDAO&lt;User&gt;(session);
    /// var users = dao.Search(Expr.Prop("Age") &gt; 18);
    /// </code>
    ///
    /// <para>
    /// 数据源在 <c>AddDataSource</c> 调用时即完成类型解析与类型预注册，连接池参数与建表同步
    /// 也在此刻一次设定；连接池工厂则在首次 <see cref="CreateSession"/> 时按需创建。
    /// 配置阶段不需要数据源依赖的其他包（例如 <c>SqlBuilder</c> 所在包）已经可用，
    /// 只要在真正取连接之前补齐即可。
    /// </para>
    /// <para>
    /// 若要使用 AOT 或裁剪，请调用泛型重载 <c>AddDataSource&lt;TConnection&gt;</c>，
    /// 它会把连接类型同时登记到 <see cref="DAOContextPoolFactory.RegisterDbConnectionType{T}"/>，
    /// 使运行期按名称反查在 AOT 下仍可命中。
    /// </para>
    /// </remarks>
    public sealed class LiteOrmClient : IDisposable
    {
        private readonly DataSourceProvider _dataSourceProvider = new DataSourceProvider();
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger<SessionManager>? _sessionLogger;
        private readonly ILogger<DAOContextPoolFactory>? _poolFactoryLogger;
        private DAOContextPoolFactory? _poolFactory;
        private bool _disposed;

        /// <summary>
        /// 初始化一个新的客户端。
        /// </summary>
        /// <param name="loggerFactory">
        /// 日志工厂；为 null 时连接池与会话都不记录日志。
        /// </param>
        public LiteOrmClient(ILoggerFactory? loggerFactory = null)
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
        /// 默认数据源名称；未显式设置且仅配置了一个数据源时，指向该数据源。
        /// </summary>
        public string? DefaultDataSourceName
        {
            get
            {
                var name = _dataSourceProvider.DefaultDataSourceName;
                if (!string.IsNullOrWhiteSpace(name)) return name;
                return _dataSourceProvider.DataSources.Count == 1
                    ? _dataSourceProvider.DataSources.First().Name
                    : null;
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
        /// 调用方负责释放；客户端被释放时会一并释放其创建的全部会话。
        /// </summary>
        /// <returns>新建的会话管理器。</returns>
        /// <exception cref="ObjectDisposedException">客户端已释放时抛出。</exception>
        /// <remarks>
        /// 本方法不会修改 <see cref="SessionManager.Current"/>，会话通过构造参数显式传给
        /// <see cref="ObjectDAO{T}"/> 等对象，与 DI 线路互不干扰。首次调用会按当前数据源配置
        /// 建好全部连接池，此后不再接受新的数据源。
        /// </remarks>
        public SessionManager CreateSession()
        {
            ThrowIfDisposed();
            return new SessionManager(PoolFactory, _sessionLogger);
        }

        /// <summary>
        /// 添加或更新一个数据源，并返回客户端本身以便继续链式调用。
        /// </summary>
        /// <param name="config">数据源配置。</param>
        /// <returns>当前客户端。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="config"/> 为 null 时抛出。</exception>
        /// <exception cref="InvalidOperationException">连接池工厂已创建时抛出。</exception>
        /// <remarks>
        /// 数据源的连接池参数、建表同步等全部设置都在本次调用中一次性确定，
        /// 客户端不提供后续的补充设置方法。连接池工厂一旦创建，便不再接受新的数据源。
        /// </remarks>
        public LiteOrmClient AddDataSource(DataSourceConfig config)
        {
            ThrowIfDisposed();
            if (config is null) throw new ArgumentNullException(nameof(config));
            ThrowIfPoolsCreated(nameof(AddDataSource));

            if (string.IsNullOrWhiteSpace(config.Name))
                throw new ArgumentException("DataSource name cannot be empty", nameof(config));

            _dataSourceProvider.AddDataSource(config);

            if (_dataSourceProvider.DataSources.Count == 1 && string.IsNullOrWhiteSpace(_dataSourceProvider.DefaultDataSourceName))
            {
                _dataSourceProvider.SetDefaultDataSource(config.Name!);
            }
            return this;
        }

        /// <summary>
        /// 添加或更新一个数据源，并指定数据库连接类型。
        /// </summary>
        /// <typeparam name="TConnection">数据库连接类型，例如 <c>SqliteConnection</c>。</typeparam>
        /// <param name="name">数据源名称。</param>
        /// <param name="connectionString">连接字符串。</param>
        /// <param name="default">是否设为默认数据源；为 true 时同时覆盖当前的默认数据源。</param>
        /// <param name="sqlBuilder">SQL 构建器类型全名（可选，不指定时按 Provider 自动匹配）。</param>
        /// <param name="syncTable">是否自动同步建表结构。</param>
        /// <param name="provider">数据库提供程序类型全名；为 null 时由 <typeparamref name="TConnection"/> 推导。</param>
        /// <param name="poolSize">连接池缓存数量；为 null 时沿用配置默认值。</param>
        /// <param name="maxPoolSize">最大并发连接数；为 null 时沿用配置默认值。</param>
        /// <param name="paramCountLimit">单条 SQL 参数数量上限；为 null 时沿用配置默认值。</param>
        /// <param name="keepAliveDuration">连接保活时长；为 null 时沿用配置默认值。</param>
        /// <returns>当前客户端。</returns>
        /// <exception cref="ArgumentException">当 <paramref name="name"/> 为空时抛出。</exception>
        /// <exception cref="InvalidOperationException">连接池工厂已创建时抛出。</exception>
        /// <remarks>
        /// <paramref name="provider"/> 缺省时使用 <c>AssemblyQualifiedName</c>，在任意程序集版本下都能解析；
        /// 显式传入时可填配置文件里的短名形式。无论走哪条路径，类型都会在添加时立即解析并登记到
        /// <see cref="TypeResolverHelper"/>，因此运行期仍能按名称创建连接。
        /// <para>
        /// 无参的 <c>AddDataSource&lt;MySqlConnection&gt;()</c> 表示"注册类型但暂不配置连接"，
        /// 请在真正取连接之前补上连接字符串。
        /// </para>
        /// </remarks>
        public LiteOrmClient AddDataSource<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TConnection>(
            string name = DefaultDataSourceKey,
            string? connectionString = null,
            bool @default = false,
            string? sqlBuilder = null,
            bool syncTable = false,
            string? provider = null,
            int? poolSize = null,
            int? maxPoolSize = null,
            int? paramCountLimit = null,
            TimeSpan? keepAliveDuration = null)
            where TConnection : DbConnection, new()
        {
            var type = typeof(TConnection);
            var resolvedProvider = provider;

            if (string.IsNullOrWhiteSpace(resolvedProvider))
            {
                resolvedProvider = type.AssemblyQualifiedName;
            }
            else
            {
                // 先在当前环境解析一次；解析成功则改用它，解析不到（例如 AOT 下未预注册）则退回类型全名。
                var resolvedType = TypeResolverHelper.FindType(resolvedProvider!);
                resolvedProvider = resolvedType?.AssemblyQualifiedName ?? resolvedProvider;
            }

            // 预注册，使运行期按名称反查在 AOT 下也能命中。
            DAOContextPoolFactory.RegisterDbConnectionType<TConnection>();
            RegisterBuilderType(sqlBuilder);

            AddDataSource(new DataSourceConfig
            {
                Name = name,
                ConnectionString = connectionString,
                Provider = resolvedProvider,
                SqlBuilder = sqlBuilder,
                SyncTable = syncTable,
                PoolSize = poolSize ?? DefaultPoolSize,
                MaxPoolSize = maxPoolSize ?? DefaultMaxPoolSize,
                ParamCountLimit = paramCountLimit ?? DefaultParamCountLimit,
                KeepAliveDuration = keepAliveDuration ?? DefaultKeepAliveDuration
            });

            if (@default)
            {
                _dataSourceProvider.SetDefaultDataSource(name);
            }
            return this;
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
        /// 释放客户端持有的连接池工厂。
        /// </summary>
        /// <remarks>
        /// 只销毁连接池工厂，不修改 <see cref="SessionManager.Current"/>，因此不会影响同一进程内的 DI 线路。
        /// 由调用方自己创建的会话需要自行释放，请在释放客户端之前完成。
        /// </remarks>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _poolFactory?.Dispose();
        }

        private static void RegisterBuilderType(string? sqlBuilder)
        {
            if (string.IsNullOrWhiteSpace(sqlBuilder)) return;

            var type = TypeResolverHelper.FindType(sqlBuilder!);
            if (type is null || !typeof(SqlBuilder).IsAssignableFrom(type)) return;

            // 通过已注册的实例或静态 Instance 属性，把类型登记进 TypeResolverHelper，
            // 使 AOT 下按名称反查 SqlBuilder 也能命中。
            var instanceProperty = type.GetProperty(nameof(SqlBuilder.Instance), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (instanceProperty?.GetValue(null) is SqlBuilder instance)
            {
                SqlBuilderFactory.Instance.RegisterSqlBuilder(type, instance);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LiteOrmClient));
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
