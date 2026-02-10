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
        private readonly SqlBuilderFactory _sqlBuilderFactory;
        private readonly TableInfoProvider _tableInfoProvider;
        private readonly DAOContextPoolFactory _daoContextPoolFactory;

        /// <summary>
        /// 初始化 <see cref="LiteOrmTableSyncInitializer"/> 类的新实例
        /// </summary>
        public LiteOrmTableSyncInitializer(
            IDataSourceProvider dataSourceProvider,
            SqlBuilderFactory sqlBuilderFactory,
            TableInfoProvider tableInfoProvider,
            DAOContextPoolFactory daoContextPoolFactory,
            ILogger<LiteOrmTableSyncInitializer> logger = null)
        {
            _dataSourceProvider = dataSourceProvider;
            _sqlBuilderFactory = sqlBuilderFactory;
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
                var sqlBuilder = _sqlBuilderFactory.GetSqlBuilder(ds.ProviderType);
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

                                    // 检查表是否存在
                                    if (!await TableExistsAsync(context, sqlBuilder, tableName))
                                    {
                                        _logger?.LogInformation("正在数据源 '{DataSource}' 中创建表 '{TableName}'", ds.Name, tableName);
                                        string createSql = sqlBuilder.BuildCreateTableSql(tableName, allColumns.Values);
                                        await ExecuteSqlAsync(context, createSql);

                                        // 创建索引 (包含索引列和唯一列)
                                        foreach (var col in allColumns.Values.Where(c => c.IsIndex || c.IsUnique))
                                        {
                                            try
                                            {
                                                string indexSql = sqlBuilder.BuildCreateIndexSql(tableName, col);
                                                await ExecuteSqlAsync(context, indexSql);
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger?.LogWarning("为表 '{TableName}' 的列 '{ColumnName}' 创建索引失败: {Message}", tableName, col.Name, ex.Message);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // 表已存在，检查并补全缺失列
                                        var existingColumns = await GetTableColumnsAsync(context, sqlBuilder.ToSqlName(tableName));
                                        var missingColumns = allColumns.Values.Where(col => !existingColumns.Contains(col.Name)).ToList();
                                        if (missingColumns.Any())
                                        {
                                            _logger?.LogInformation("正在向数据源 '{DataSource}' 的表 '{TableName}' 添加 {Count} 个新列", ds.Name, tableName, missingColumns.Count);
                                            string addColsSql = sqlBuilder.BuildAddColumnsSql(tableName, missingColumns);
                                            await ExecuteSqlAsync(context, addColsSql);

                                            // 为新添加的列创建索引 (已有列不用新建索引)
                                            foreach (var col in missingColumns.Where(c => c.IsIndex || c.IsUnique))
                                            {
                                                try
                                                {
                                                    string indexSql = sqlBuilder.BuildCreateIndexSql(tableName, col);
                                                    await ExecuteSqlAsync(context, indexSql);
                                                }
                                                catch (Exception ex)
                                                {
                                                    _logger?.LogWarning("为表 '{TableName}' 的新列 '{ColumnName}' 创建索引失败: {Message}", tableName, col.Name, ex.Message);
                                                }
                                            }
                                        }
                                    }
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
                finally
                {
                    // 标记该数据源初始化完成
                    pool.MarkInitialized();
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

        /// <summary>
        /// 异步执行同步 SQL 语句。
        /// </summary>
        /// <param name="context">数据库上下文。</param>
        /// <param name="sql">要执行的 SQL 语句。</param>
        private async Task ExecuteSqlAsync(DAOContext context, string sql)
        {
            try
            {
                _logger?.LogInformation("正在执行 SQL: {Sql}", sql);
                using (var cmd = context.DbConnection.CreateCommand())
                {
                    cmd.CommandText = sql;
                    if (cmd is System.Data.Common.DbCommand dbCmd)
                        await dbCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    else
                        await Task.Run(() => cmd.ExecuteNonQuery()).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("执行同步 SQL 失败: {Sql}. {Message}", sql, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 异步获取数据库中已存在的列名集合。
        /// </summary>
        /// <param name="context">数据库上下文。</param>
        /// <param name="tableName">已转义的表名。</param>
        /// <returns>列名集合。</returns>
        private async Task<HashSet<string>> GetTableColumnsAsync(DAOContext context, string tableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var cmd = context.DbConnection.CreateCommand())
                {
                    cmd.CommandText = $"SELECT * FROM {tableName} WHERE 1=0";
                    if (cmd is System.Data.Common.DbCommand dbCmd)
                    {
                        using (var reader = await dbCmd.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                columns.Add(reader.GetName(i));
                            }
                        }
                    }
                    else
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                columns.Add(reader.GetName(i));
                            }
                        }
                    }
                }
            }
            catch
            {
                var schema = context.DbConnection.GetSchema("Columns", new[] { null, null, tableName });
                foreach (System.Data.DataRow row in schema.Rows)
                {
                    columns.Add(row["COLUMN_NAME"].ToString());
                }
            }
            return columns;
        }

        /// <summary>
        /// 异步检查表是否存在。
        /// </summary>
        /// <param name="context">数据库上下文。</param>
        /// <param name="sqlBuilder">SQL 构建器。</param>
        /// <param name="tableName">原始表名。</param>
        /// <returns>如果表存在则返回 true，否则返回 false。</returns>
        private async Task<bool> TableExistsAsync(DAOContext context, SqlBuilder sqlBuilder, string tableName)
        {
            try
            {
                using (var cmd = context.DbConnection.CreateCommand())
                {
                    cmd.CommandText = sqlBuilder.BuildTableExistsSql(tableName);
                    if (cmd is System.Data.Common.DbCommand dbCmd)
                        await dbCmd.ExecuteScalarAsync().ConfigureAwait(false);
                    else
                        await Task.Run(() => cmd.ExecuteScalar()).ConfigureAwait(false);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
