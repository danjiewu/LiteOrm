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
    /// LiteOrm 表同步初始化器，负责自动同步数据库表结构（创建表、添加列、创建索引）。
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
    public class LiteOrmTableSyncInitializer : IStartable
    {
        private readonly ILogger<LiteOrmTableSyncInitializer> _logger;
        private readonly IDataSourceProvider _dataSourceProvider;
        private readonly TableInfoProvider _tableInfoProvider;
        private readonly DAOContextPoolFactory _daoContextPoolFactory;

        /// <summary>
        /// 初始化 <see cref="LiteOrmTableSyncInitializer"/> 类的新实例
        /// </summary>
        public LiteOrmTableSyncInitializer(
            IDataSourceProvider dataSourceProvider,
            TableInfoProvider tableInfoProvider,
            DAOContextPoolFactory daoContextPoolFactory,
            ILogger<LiteOrmTableSyncInitializer> logger = null)
        {
            _dataSourceProvider = dataSourceProvider;
            _tableInfoProvider = tableInfoProvider;
            _daoContextPoolFactory = daoContextPoolFactory;
            _logger = logger;
        }

        /// <summary>
        /// 启动时执行表同步逻辑。
        /// </summary>
        public void Start()
        {
            SyncTables();
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
                                       .Where(t => !t.IsAbstract && t.GetCustomAttribute<TableAttribute>() != null)
                                       .ToList();

            // 按数据源名称和表名对实体类型进行分组
            var tableGroups = tableTypes.GroupBy(t =>
            {
                var attr = t.GetCustomAttribute<TableAttribute>();
                return (DataSource: attr.DataSource ?? _dataSourceProvider.DefaultDataSourceName, TableName: attr.TableName ?? t.Name);
            }).ToList();

            // 循环执行各个数据源的同步任务
            var syncTasks = syncDataSources.Select(async ds =>
            {
                var pool = _daoContextPoolFactory.GetPool(ds.Name);
                if (pool == null) return;

                var currentDsGroups = tableGroups.Where(g => string.Equals(g.Key.DataSource, ds.Name, StringComparison.OrdinalIgnoreCase)).ToList();

                try
                {
                    if (currentDsGroups.Any())
                    {
                        _logger?.LogInformation("正在同步数据源 '{DataSource}'，包含 {Count} 个表和映射实体", ds.Name, currentDsGroups.Count);

                        var context = pool.PeekContextInternal();
                        try
                        {
                            using (await context.AcquireScopeAsync())
                            {
                                await context.EnsureConnectionOpenAsync();
                                foreach (var group in currentDsGroups)
                                {
                                    string tableName = group.Key.TableName;
                                    // 合并在此表名上映射的实体定义（支持多实体映射到同一个表）
                                    var allColumns = new Dictionary<string, ColumnDefinition>(StringComparer.OrdinalIgnoreCase);
                                    TableDefinition firstTableDef = null;

                                    foreach (var type in group)
                                    {
                                        var tableDef = _tableInfoProvider.GetTableDefinition(type);
                                        if (tableDef == null) continue;
                                        if (firstTableDef == null) firstTableDef = tableDef;

                                        foreach (var col in tableDef.Columns)
                                        {
                                            if (!allColumns.ContainsKey(col.Name))
                                                allColumns[col.Name] = col;
                                        }
                                    }

                                    if (firstTableDef == null || allColumns.Count == 0) continue;

                                    await pool.EnsureTableAsync(tableName, allColumns.Values).ConfigureAwait(false);
                                }
                            }
                        }
                        finally
                        {
                            pool.ReturnContext(context);
                        }
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
