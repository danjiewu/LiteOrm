using Autofac;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm 核心初始化器，负责初始化系统和自动同步数据库表结构。
    /// 
    /// 主要职责：
    /// 1. 初始化全局的会话管理器 (SessionManager)
    /// 2. 初始化全局的表信息提供者 (TableInfoProvider)
    /// 3. 自动同步数据库表结构（创建表、添加列、创建索引）
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
    public class LiteOrmCoreInitializer : IStartable
    {
        private readonly SessionManager _sessionManager;
        private readonly TableInfoProvider _tableInfoProvider;
        private readonly ILogger<LiteOrmCoreInitializer> _logger;
        private readonly IDataSourceProvider _dataSourceProvider;
        private readonly DAOContextPoolFactory _daoContextPoolFactory;

        /// <summary>
        /// 初始化 <see cref="LiteOrmCoreInitializer"/> 类的新实例
        /// </summary>
        /// <param name="sessionManager">会话管理器实例</param>
        /// <param name="tableInfoProvider">表信息提供者实例</param>
        /// <param name="dataSourceProvider">数据源提供者</param>
        /// <param name="daoContextPoolFactory">DAO上下文连接池工厂</param>
        /// <param name="logger">日志记录器</param>
        public LiteOrmCoreInitializer(
            SessionManager sessionManager,
            TableInfoProvider tableInfoProvider,
            IDataSourceProvider dataSourceProvider,
            DAOContextPoolFactory daoContextPoolFactory,
            ILogger<LiteOrmCoreInitializer> logger = null)
        {
            _sessionManager = sessionManager;
            _tableInfoProvider = tableInfoProvider;
            _dataSourceProvider = dataSourceProvider;
            _daoContextPoolFactory = daoContextPoolFactory;
            _logger = logger;
        }

        /// <summary>
        /// 启动时执行初始化和表同步逻辑。
        /// </summary>
        public void Start()
        {
            // 步骤 1：初始化全局实例
            InitializeGlobalInstances();

            // 步骤 2：同步数据库表结构
            SyncTables();
        }

        /// <summary>
        /// 初始化全局的会话管理器和表信息提供者
        /// </summary>
        private void InitializeGlobalInstances()
        {
            SessionManager.Current = _sessionManager;
            TableInfoProvider.Default = _tableInfoProvider;
            _logger?.LogInformation("LiteOrm 全局实例初始化完成");
        }

        /// <summary>
        /// 自动同步数据库结构。
        /// </summary>
        private void SyncTables()
        {
            var syncDataSources = _dataSourceProvider.Where(ds => ds.SyncTable).ToList();
            if (!syncDataSources.Any()) return;

            _logger?.LogInformation("开始自动同步数据库结构...");

            // 获取全部已加载程序集中的表实体映射定义
            var assemblies = AssemblyAnalyzer.GetAllReferencedAssemblies();
            var tableTypes = assemblies.SelectMany(a => a.GetTypes())
                                       .Where(t => !t.IsAbstract && t.GetCustomAttribute<TableAttribute>() != null && !typeof(IArged).IsAssignableFrom(t))
                                       .ToList();

            // 按数据源名称对实体类型进行分组
            var tableGroupsByDataSource = tableTypes.GroupBy(t =>
            {
                var attr = t.GetCustomAttribute<TableAttribute>();
                return attr.DataSource ?? _dataSourceProvider.DefaultDataSourceName;
            }).ToList();

            // 循环执行各个数据源的同步任务
            var syncTasks = syncDataSources.Select(async ds =>
            {
                var pool = _daoContextPoolFactory.GetPool(ds.Name);
                if (pool == null) return;

                var currentDsTypes = tableGroupsByDataSource
                    .FirstOrDefault(g => string.Equals(g.Key, ds.Name, StringComparison.OrdinalIgnoreCase))?
                    .ToList() ?? new List<Type>();

                if (!currentDsTypes.Any()) return;

                try
                {
                    _logger?.LogInformation("正在同步数据源 '{DataSource}'，包含 {Count} 个表映射实体", ds.Name, currentDsTypes.Count);

                    var context = pool.PeekContextInternal();
                    try
                    {
                        using (await context.AcquireScopeAsync())
                        {
                            await context.EnsureConnectionOpenAsync();

                            // 直接为每个实体类型的表创建结构
                            foreach (var type in currentDsTypes)
                            {
                                await pool.EnsureTableAsync(type).ConfigureAwait(false);
                            }
                        }
                    }
                    finally
                    {
                        pool.ReturnContext(context);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "同步数据源 '{DataSource}' 时发生异常", ds.Name);
                    throw;
                }
            }).ToArray();

            try
            {
                Task.WhenAll(syncTasks).GetAwaiter().GetResult();
            }
            catch
            {
                throw;
            }

            _logger?.LogInformation("数据库表结构同步完成");
        }

    }
}
