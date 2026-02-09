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
    /// LiteOrm ±íÍ¬²½³õÊ¼»¯Æ÷£¬¸ºÔð×Ô¶¯Í¬²½Êý¾Ý¿â±í½á¹¹£¨´´½¨±í¡¢Ìí¼ÓÁÐ¡¢´´½¨Ë÷Òý£©¡£
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
        /// ³õÊ¼»¯ <see cref="LiteOrmTableSyncInitializer"/> ÀàµÄÐÂÊµÀý
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
        /// Æô¶¯Ê±Ö´ÐÐ±íÍ¬²½Âß¼­¡£
        /// </summary>
        public void Start()
        {
            SyncTables();
        }

        /// <summary>
        /// ×Ô¶¯Í¬²½Êý¾Ý¿â½á¹¹¡£
        /// </summary>
        private void SyncTables()
        {
            var syncDataSources = _dataSourceProvider.Where(ds => ds.SyncTable).ToList();
            if (!syncDataSources.Any()) return;

            _logger?.LogInformation("¿ªÊ¼×Ô¶¯Í¬²½Êý¾Ý¿â½á¹¹...");

            // »ñÈ¡È«²¿ÒÑ¼ÓÔØ³ÌÐò¼¯ÖÐµÄ±íÊµÌåÓ³Éä¶¨Òå
            var assemblies = AssemblyAnalyzer.GetAllReferencedAssemblies();
            var tableTypes = assemblies.SelectMany(a => a.GetTypes())
                                       .Where(t => !t.IsAbstract && t.GetCustomAttribute<TableAttribute>() != null)
                                       .ToList();

            // °´Êý¾ÝÔ´Ãû³ÆºÍ±íÃû¶ÔÊµÌåÀàÐÍ½øÐÐ·Ö×é
            var tableGroups = tableTypes.GroupBy(t =>
            {
                var attr = t.GetCustomAttribute<TableAttribute>();
                return (DataSource: attr.DataSource ?? _dataSourceProvider.DefaultDataSourceName, TableName: attr.TableName ?? t.Name);
            }).ToList();

            // Ñ­»·Ö´ÐÐ¸÷¸öÊý¾ÝÔ´µÄÍ¬²½ÈÎÎñ
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
                        _logger?.LogInformation("ÕýÔÚÍ¬²½Êý¾ÝÔ´ '{DataSource}'£¬°üº¬ {Count} ¸ö±íºÍÓ³ÉäÊµÌå", ds.Name, currentDsGroups.Count);

                        var context = pool.PeekContextInternal();
                        try
                        {
                            using (await context.AcquireScopeAsync())
                            {
                                await context.EnsureConnectionOpenAsync();
                                foreach (var group in currentDsGroups)
                                {
                                    string tableName = group.Key.TableName;
                                    // ºÏ²¢ÔÚ´Ë±íÃûÉÏÓ³ÉäµÄÊµÌå¶¨Òå£¨Ö§³Ö¶àÊµÌåÓ³Éäµ½Í¬Ò»¸ö±í£©
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

                                    // ¼ì²é±íÊÇ·ñ´æÔÚ
                                    if (!await TableExistsAsync(context, sqlBuilder, tableName))
                                    {
                                        _logger?.LogInformation("ÕýÔÚÊý¾ÝÔ´ '{DataSource}' ÖÐ´´½¨±í '{TableName}'", ds.Name, tableName);
                                        string createSql = sqlBuilder.BuildCreateTableSql(tableName, allColumns.Values);
                                        await ExecuteSqlAsync(context, createSql);

                                        // ´´½¨Ë÷Òý (°üº¬Ë÷ÒýÁÐºÍÎ¨Ò»ÁÐ)
                                        foreach (var col in allColumns.Values.Where(c => c.IsIndex || c.IsUnique))
                                        {
                                            try
                                            {
                                                string indexSql = sqlBuilder.BuildCreateIndexSql(tableName, col);
                                                await ExecuteSqlAsync(context, indexSql);
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger?.LogWarning("Îª±í '{TableName}' µÄÁÐ '{ColumnName}' ´´½¨Ë÷ÒýÊ§°Ü: {Message}", tableName, col.Name, ex.Message);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // ±íÒÑ´æÔÚ£¬¼ì²é²¢²¹È«È±Ê§ÁÐ
                                        var existingColumns = await GetTableColumnsAsync(context, sqlBuilder.ToSqlName(tableName));
                                        var missingColumns = allColumns.Values.Where(col => !existingColumns.Contains(col.Name)).ToList();
                                        if (missingColumns.Any())
                                        {
                                            _logger?.LogInformation("ÕýÔÚÏòÊý¾ÝÔ´ '{DataSource}' µÄ±í '{TableName}' Ìí¼Ó {Count} ¸öÐÂÁÐ", ds.Name, tableName, missingColumns.Count);
                                            string addColsSql = sqlBuilder.BuildAddColumnsSql(tableName, missingColumns);
                                            await ExecuteSqlAsync(context, addColsSql);

                                            // ÎªÐÂÌí¼ÓµÄÁÐ´´½¨Ë÷Òý (ÒÑÓÐÁÐ²»ÓÃÐÂ½¨Ë÷Òý)
                                            foreach (var col in missingColumns.Where(c => c.IsIndex || c.IsUnique))
                                            {
                                                try
                                                {
                                                    string indexSql = sqlBuilder.BuildCreateIndexSql(tableName, col);
                                                    await ExecuteSqlAsync(context, indexSql);
                                                }
                                                catch (Exception ex)
                                                {
                                                    _logger?.LogWarning("Îª±í '{TableName}' µÄÐÂÁÐ '{ColumnName}' ´´½¨Ë÷ÒýÊ§°Ü: {Message}", tableName, col.Name, ex.Message);
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
                    _logger?.LogError(ex, "Í¬²½Êý¾ÝÔ´ '{DataSource}' Ê±·¢ÉúÒì³£", ds.Name);
                    throw;
                }
                finally
                {
                    // ±ê¼Ç¸ÃÊý¾ÝÔ´³õÊ¼»¯Íê³É
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

            _logger?.LogInformation("Êý¾Ý¿â±í½á¹¹Í¬²½Íê³É");
        }

        /// <summary>
        /// Òì²½Ö´ÐÐÍ¬²½ SQL Óï¾ä¡£
        /// </summary>
        /// <param name="context">Êý¾Ý¿âÉÏÏÂÎÄ¡£</param>
        /// <param name="sql">ÒªÖ´ÐÐµÄ SQL Óï¾ä¡£</param>
        private async Task ExecuteSqlAsync(DAOContext context, string sql)
        {
            try
            {
                _logger?.LogInformation("ÕýÔÚÖ´ÐÐ SQL: {Sql}", sql);
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
                _logger?.LogWarning("Ö´ÐÐÍ¬²½ SQL Ê§°Ü: {Sql}. {Message}", sql, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Òì²½»ñÈ¡Êý¾Ý¿âÖÐÒÑ´æÔÚµÄÁÐÃû¼¯ºÏ¡£
        /// </summary>
        /// <param name="context">Êý¾Ý¿âÉÏÏÂÎÄ¡£</param>
        /// <param name="tableName">ÒÑ×ªÒåµÄ±íÃû¡£</param>
        /// <returns>ÁÐÃû¼¯ºÏ¡£</returns>
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
        /// Òì²½¼ì²é±íÊÇ·ñ´æÔÚ¡£
        /// </summary>
        /// <param name="context">Êý¾Ý¿âÉÏÏÂÎÄ¡£</param>
        /// <param name="sqlBuilder">SQL ¹¹½¨Æ÷¡£</param>
        /// <param name="tableName">Ô­Ê¼±íÃû¡£</param>
        /// <returns>Èç¹û±í´æÔÚÔò·µ»Ø true£¬·ñÔò·µ»Ø false¡£</returns>
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
