using LiteOrm.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm 核心初始化器，负责在启动时初始化服务和自动同步数据库表结构。
    /// 
    /// 主要职责：
    /// 1. 将 DI 解析的 TableInfoProvider 回写为全局静态单例
    /// 2. 自动同步数据库表结构（创建表、添加列、创建索引）
    /// </summary>
    public class LiteOrmCoreInitializer : IHostedService
    {
        private readonly ILogger<LiteOrmCoreInitializer>? _logger;
        private readonly IDataSourceProvider _dataSourceProvider;
        private readonly DAOContextPoolFactory _daoContextPoolFactory;

        /// <summary>
        /// 初始化 <see cref="LiteOrmCoreInitializer"/> 类的新实例
        /// </summary>
        /// <param name="dataSourceProvider">数据源提供者</param>
        /// <param name="daoContextPoolFactory">DAO上下文连接池工厂</param>
        /// <param name="logger">日志记录器</param>
        /// <param name="waitForInit">
        /// 是否在 <see cref="StartAsync"/> 中阻塞等待初始化完成。
        /// 为 <c>false</c> 时初始化在后台执行（fire-and-forget）。
        /// </param>
        public LiteOrmCoreInitializer(
            IDataSourceProvider dataSourceProvider,
            DAOContextPoolFactory daoContextPoolFactory,
            ILogger<LiteOrmCoreInitializer>? logger = null,
            bool waitForInit = true)
        {
            _dataSourceProvider = dataSourceProvider;
            _daoContextPoolFactory = daoContextPoolFactory;
            _logger = logger;
            WaitForInit = waitForInit;
        }

        /// <summary>
        /// 是否在宿主启动时阻塞等待初始化完成。
        /// </summary>
        public bool WaitForInit { get; }

        /// <summary>
        /// 启动时执行全局单例回写和表结构同步逻辑。
        /// <para>
        /// <see cref="WaitForInit"/> 为 <c>true</c> 时等待全部初始化工作（读取器预热、表结构同步）完成后返回，
        /// 避免后台任务与随后启动的定时任务/HTTP 请求并发访问尚未建好的元数据；
        /// 为 <c>false</c> 时初始化转到后台执行（fire-and-forget），启动不被阻塞，异常只记录日志。
        /// </para>
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (WaitForInit)
            {
                return SyncTablesAsync(cancellationToken);
            }

            _ = RunInitializationInBackgroundAsync(cancellationToken);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 后台执行初始化；异常记录日志而不影响宿主启动。
        /// </summary>
        private async Task RunInitializationInBackgroundAsync(CancellationToken cancellationToken)
        {
            try
            {
                await SyncTablesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "LiteOrm background initialization failed.");
            }
        }

        /// <summary>
        /// 停止时清理。
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 自动同步数据库结构。
        /// </summary>
        private async Task SyncTablesAsync(CancellationToken cancellationToken)
        {
            var syncDataSources = _dataSourceProvider.Where(ds => ds.SyncTable).ToList();
            if (!syncDataSources.Any()) return;

            _logger?.LogInformation("Starting automatic database schema synchronization...");

            // 获取全部已加载程序集中的表实体映射定义
            var assemblies = AssemblyAnalyzer.GetAllReferencedAssemblies();
            var tableTypes = assemblies.SelectMany(a =>
            {
                try
                {
                    return (IEnumerable<Type>)a.GetTypes().OfType<Type>();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    _logger?.LogWarning(ex, "Failed to load types from assembly '{Assembly}', some types will be skipped", a.FullName);
                    return ex.Types.OfType<Type>();
                }
            })
            // 标记为永不同步（TableAttribute.SyncTable = Never）的类型直接排除：
            .Where(t =>
            {
                var tableAttribute = t.GetCustomAttribute<TableAttribute>();
                return !t.IsAbstract && tableAttribute != null && tableAttribute.SyncTable != SyncTableMode.Never;
            })
            .ToList();

            tableTypes = tableTypes.Where(t => !typeof(IArged).IsAssignableFrom(t)).ToList();


            // 按数据源名称对实体类型进行分组  
            var tableGroupsByDataSource = tableTypes.GroupBy(t =>
            {
                var attr = t.GetCustomAttribute<TableAttribute>();
                return attr!.DataSource ?? _dataSourceProvider.DefaultDataSourceName;
            }).ToList();

            // 预注册读取器：必须等待完成。原先的 fire-and-forget 会越过 StartAsync 继续运行，
            // 与数据库结构同步、定时任务和首个 HTTP 请求并发访问元数据缓存。
            await Task.Run(() =>
            {
                int successCount = 0;
                int failedCount = 0;
                foreach (var tableGroup in tableGroupsByDataSource)
                {
                    var pool = _daoContextPoolFactory.GetPool(tableGroup.Key);
                    if(pool is null)
                    {
                        _logger?.LogWarning("No connection pool found for data source '{DataSource}', skipping pre-registration of data reader converters", tableGroup.Key);
                        continue;
                    }
                    foreach (var type in tableGroup)
                    {
                        try
                        {
                            DataReaderConverter.GetConverterByType(type, pool.SqlBuilder);
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            {
                                _logger?.LogWarning(ex, "Failed to pre-register data reader converter for type '{Type}' (data source: '{DataSource}')", type.FullName, tableGroup.Key);
                                failedCount++;
                            }
                        }
                    }
                }
                _logger?.LogInformation("Pre-registered {SucessCount} data reader converters, {FailedCount} failed", successCount, failedCount);
            }, cancellationToken).ConfigureAwait(false);

            // 循环执行各个数据源的同步任务
            var syncTasks = syncDataSources.Select(async ds =>
            {
                var pool = _daoContextPoolFactory.GetPool(ds.Name);
                if (pool == null)
                {
                    _logger?.LogWarning("No connection pool found for data source '{DataSource}', skipping sync", ds.Name);
                    return;
                }

                var currentDsTypes = tableGroupsByDataSource
                    .FirstOrDefault(g => string.Equals(g.Key, ds.Name, StringComparison.OrdinalIgnoreCase))?
                    .ToList() ?? new List<Type>();

                if (!currentDsTypes.Any()) return;

                {
                    _logger?.LogInformation("Syncing data source '{DataSource}' with {Count} entity type(s)", ds.Name, currentDsTypes.Count);

                    var context = pool.PeekContext();
                    if (context is null)
                    {
                        _logger?.LogWarning("Failed to obtain a connection context for data source '{DataSource}', skipping sync", ds.Name);
                        return;
                    }

                    try
                    {

                        foreach (var type in currentDsTypes)
                        {
                            try
                            {
                                // 直接为每个实体类型的表创建结构
                                await context.EnsureTableAsync(type).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "An error occurred while syncing table '{Type}' (data source: '{DataSource}')", type.FullName, ds.Name);
                                throw;
                            }
                        }
                    }
                    finally
                    {
                        pool.ReturnContext(context);
                    }
                }
            }).ToArray();

            await Task.WhenAll(syncTasks).ConfigureAwait(false);
            _logger?.LogInformation("Database schema synchronization complete");
        }

    }
}
