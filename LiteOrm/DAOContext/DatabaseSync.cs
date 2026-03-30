using LiteOrm.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace LiteOrm
{
    /// <summary>
    /// 数据库同步器，负责在 DAO 执行命令前确保相关表结构存在且包含必要列。
    /// </summary>
    public class DatabaseSync : IDisposable
    {
        private readonly ConcurrentDictionary<string, HashSet<string>> _tableColumns = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _tableCreationLocks = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, byte> _createdTables = new(StringComparer.OrdinalIgnoreCase);
        private readonly DAOContextPool _daoContextPool;
        private SqlBuilder sqlBuilder => _daoContextPool.SqlBuilder;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="daoContextPool"></param>
        public DatabaseSync(DAOContextPool daoContextPool)
        {
            _daoContextPool = daoContextPool;
        }
        /// <summary>
        /// 将指定表名标记为已创建，同时缓存已知列名集合。
        /// </summary>
        public void MarkTableCreated(string tableName, IEnumerable<string> columnNames = null)
        {
            var cols = columnNames != null
                ? new HashSet<string>(columnNames, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _tableColumns.TryAdd(tableName, cols);
        }

        /// <summary>
        /// 获取指定表名的创建锁，用于确保同一表名只被创建一次。
        /// </summary>
        internal SemaphoreSlim GetTableCreationLock(string tableName)
            => _tableCreationLocks.GetOrAdd(tableName, _ => new SemaphoreSlim(1, 1));

        /// <summary>
        /// 生成表名和对象类型的联合主键。
        /// </summary>
        private static string GetTableTypeKey(string tableName, Type objectType)
            => $"{tableName}|{objectType.FullName}";

        /// <summary>
        /// 确保指定表已在数据库中存在且包含所有必要的列。
        /// 同步版本，供 DAO 在命令构建前调用。
        /// </summary>
        public void EnsureTable(DAOContext daoContext, Type objectType, string[] tableArgs = null)
        {
            if (typeof(IArged).IsAssignableFrom(objectType) && (tableArgs == null || tableArgs.Length == 0)) return;

            var statements = ResolveEnsureTableDdl(daoContext, objectType, tableArgs);
            if (statements.Count == 0) return;
            ApplyDdl(daoContext, statements);
        }

        /// <summary>
        /// 确保指定表已在数据库中存在且包含所有必要的列。
        /// 异步版本，供初始化器调用。
        /// </summary>
        public async Task EnsureTableAsync(DAOContext daoContext, Type objectType, string[] tableArgs = null)
        {
            if (typeof(IArged).IsAssignableFrom(objectType) && (tableArgs == null || tableArgs.Length == 0)) return;
            var statements = await ResolveEnsureTableDdlAsync(daoContext, objectType, tableArgs).ConfigureAwait(false);
            if (statements.Count == 0) return;
            await ApplyDdlAsync(daoContext, statements).ConfigureAwait(false);
        }

        /// <summary>
        /// 根据实体类型和数据库当前状态，计算出需要执行的 DDL 语句列表（同步版本）。
        /// 内部自动获取连接、加锁、检查数据库、更新列缓存，并在完成后将表标记为已处理。
        /// 若表已处理则直接返回空列表，可安全用于 DDL 预览与导出。
        /// </summary>
        /// <param name="daoContext">DAO 上下文实例。</param>
        /// <param name="objectType">实体类型。</param>
        /// <param name="tableArgs">动态表名参数，适用于实现了 <see cref="IArged"/> 的类型。</param>
        /// <returns>需要执行的 DDL 语句列表（CREATE TABLE、ADD COLUMN、CREATE INDEX）。</returns>
        public List<string> ResolveEnsureTableDdl(DAOContext daoContext, Type objectType, string[] tableArgs = null)
        {
            var tableDefinition = TableInfoProvider.Default.GetTableDefinition(objectType);
            if (tableDefinition == null) return new List<string>();

            string tableName = tableArgs != null && tableArgs.Length > 0
                ? string.Format(tableDefinition.Name, tableArgs)
                : tableDefinition.Name;
            string tableTypeKey = GetTableTypeKey(tableName, objectType);

            if (_createdTables.ContainsKey(tableTypeKey))
                return new List<string>();

            var sem = GetTableCreationLock(tableName);
            sem.Wait(10000);
            try
            {
                if (_createdTables.ContainsKey(tableTypeKey))
                    return new List<string>();

                var statements = ResolveEnsureTableDdlCore(daoContext, tableName, tableDefinition.Columns);
                _createdTables.TryAdd(tableTypeKey, 0);
                return statements;

            }
            finally { sem.Release(); }
        }

        /// <summary>
        /// 根据实体类型和数据库当前状态，计算出需要执行的 DDL 语句列表（异步版本）。
        /// </summary>
        public async Task<List<string>> ResolveEnsureTableDdlAsync(DAOContext daoContext, Type objectType, string[] tableArgs = null)
        {
            var tableDefinition = TableInfoProvider.Default.GetTableDefinition(objectType);
            if (tableDefinition == null) return new List<string>();

            string tableName = tableArgs != null && tableArgs.Length > 0
                ? string.Format(tableDefinition.Name, tableArgs)
                : tableDefinition.Name;
            string tableTypeKey = GetTableTypeKey(tableName, objectType);

            if (_createdTables.ContainsKey(tableTypeKey))
                return new List<string>();

            var sem = GetTableCreationLock(tableName);
            await sem.WaitAsync(10000).ConfigureAwait(false);
            try
            {
                if (_createdTables.ContainsKey(tableTypeKey))
                    return new List<string>();
                var statements = await ResolveEnsureTableDdlCoreAsync(daoContext, tableName, tableDefinition.Columns).ConfigureAwait(false);
                _createdTables.TryAdd(tableTypeKey, 0);
                return statements;

            }
            finally { sem.Release(); }
        }

        /// <summary>
        /// 清空表结构缓存及已处理标记，以便下次调用时重新检查数据库状态。
        /// </summary>
        public void ClearTableCache()
        {
            _tableColumns.Clear();
            _createdTables.Clear();
        }

        private List<string> ResolveEnsureTableDdlCore(
            DAOContext daoContext, string tableName, IReadOnlyList<ColumnDefinition> cols)
        {
            var statements = new List<string>();

            if (!_tableColumns.ContainsKey(tableName))
            {
                // 表不在缓存中，需要检查数据库
                if (!TableExistsSync(daoContext, sqlBuilder, tableName))
                {
                    // 表不存在，生成建表语句
                    statements.Add(sqlBuilder.BuildCreateTableSql(tableName, cols));
                    foreach (var col in cols.Where(c => c.IsIndex || c.IsUnique))
                        statements.Add(sqlBuilder.BuildCreateIndexSql(tableName, col));
                    _tableColumns[tableName] = new HashSet<string>(cols.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    // 表存在，生成补列语句
                    var existingCols = GetExistingColumnsSync(daoContext, sqlBuilder.ToSqlName(tableName));
                    var missing = cols.Where(c => !existingCols.Contains(c.Name)).ToList();
                    if (missing.Count > 0)
                    {
                        statements.Add(sqlBuilder.BuildAddColumnsSql(tableName, missing));
                        foreach (var col in missing.Where(c => c.IsIndex || c.IsUnique))
                            statements.Add(sqlBuilder.BuildCreateIndexSql(tableName, col));
                        foreach (var c in missing) existingCols.Add(c.Name);
                    }
                    foreach (var c in cols) existingCols.Add(c.Name);
                    _tableColumns[tableName] = existingCols;
                }
            }
            else
            {
                // 表在缓存中，检查是否需要添加列
                var knownCols = _tableColumns[tableName];
                var missing = cols.Where(c => !knownCols.Contains(c.Name)).ToList();
                if (missing.Count > 0)
                {
                    var existingCols = GetExistingColumnsSync(daoContext, sqlBuilder.ToSqlName(tableName));
                    var actualMissing = missing.Where(c => !existingCols.Contains(c.Name)).ToList();
                    if (actualMissing.Count > 0)
                    {
                        statements.Add(sqlBuilder.BuildAddColumnsSql(tableName, actualMissing));
                        foreach (var col in actualMissing.Where(c => c.IsIndex || c.IsUnique))
                            statements.Add(sqlBuilder.BuildCreateIndexSql(tableName, col));
                    }
                    var newCols = new HashSet<string>(knownCols, StringComparer.OrdinalIgnoreCase);
                    foreach (var c in missing) newCols.Add(c.Name);
                    _tableColumns[tableName] = newCols;
                }
            }

            return statements;
        }

        private async Task<List<string>> ResolveEnsureTableDdlCoreAsync(
            DAOContext daoContext, string tableName, IReadOnlyList<ColumnDefinition> cols)
        {
            var statements = new List<string>();

            if (!_tableColumns.ContainsKey(tableName))
            {
                // 表不在缓存中，需要检查数据库
                if (!await TableExistsAsync(daoContext, tableName).ConfigureAwait(false))
                {
                    // 表不存在，生成建表语句
                    statements.Add(sqlBuilder.BuildCreateTableSql(tableName, cols));
                    foreach (var col in cols.Where(c => c.IsIndex || c.IsUnique))
                        statements.Add(sqlBuilder.BuildCreateIndexSql(tableName, col));
                    _tableColumns[tableName] = new HashSet<string>(cols.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    // 表存在，生成补列语句
                    var existingCols = await GetExistingColumnsAsync(daoContext, sqlBuilder.ToSqlName(tableName)).ConfigureAwait(false);
                    var missing = cols.Where(c => !existingCols.Contains(c.Name)).ToList();
                    if (missing.Count > 0)
                    {
                        statements.Add(sqlBuilder.BuildAddColumnsSql(tableName, missing));
                        foreach (var col in missing.Where(c => c.IsIndex || c.IsUnique))
                            statements.Add(sqlBuilder.BuildCreateIndexSql(tableName, col));
                        foreach (var c in missing) existingCols.Add(c.Name);
                    }
                    foreach (var c in cols) existingCols.Add(c.Name);
                    _tableColumns[tableName] = existingCols;
                }
            }
            else
            {
                // 表在缓存中，检查是否需要添加列
                var knownCols = _tableColumns[tableName];
                var missing = cols.Where(c => !knownCols.Contains(c.Name)).ToList();
                if (missing.Count > 0)
                {
                    var existingCols = await GetExistingColumnsAsync(daoContext, sqlBuilder.ToSqlName(tableName)).ConfigureAwait(false);
                    var actualMissing = missing.Where(c => !existingCols.Contains(c.Name)).ToList();
                    if (actualMissing.Count > 0)
                    {
                        statements.Add(sqlBuilder.BuildAddColumnsSql(tableName, actualMissing));
                        foreach (var col in actualMissing.Where(c => c.IsIndex || c.IsUnique))
                            statements.Add(sqlBuilder.BuildCreateIndexSql(tableName, col));
                    }
                    var newCols = new HashSet<string>(knownCols, StringComparer.OrdinalIgnoreCase);
                    foreach (var c in missing) newCols.Add(c.Name);
                    _tableColumns[tableName] = newCols;
                }
            }

            return statements;
        }

        private void ApplyDdl(DAOContext daoContext, List<string> statements)
        {
            foreach (var sql in statements)
            {
                if (sql.TrimStart().StartsWith("CREATE", StringComparison.OrdinalIgnoreCase)
                    && sql.IndexOf("INDEX", StringComparison.OrdinalIgnoreCase) >= 0)
                    try { ExecuteSqlSync(daoContext, sql); } catch { }
                else
                    ExecuteSqlSync(daoContext, sql);
            }
        }

        private async Task ApplyDdlAsync(DAOContext daoContext, List<string> statements)
        {
            foreach (var sql in statements)
            {
                if (sql.TrimStart().StartsWith("CREATE", StringComparison.OrdinalIgnoreCase)
                    && sql.IndexOf("INDEX", StringComparison.OrdinalIgnoreCase) >= 0)
                    try { await ExecuteSqlAsync(daoContext, sql).ConfigureAwait(false); } catch { }
                else
                    await ExecuteSqlAsync(daoContext, sql).ConfigureAwait(false);
            }
        }

        private bool TableExistsSync(DAOContext daoContext, SqlBuilder sqlBuilder, string tableName)
        {
            try
            {
                using var cmd = daoContext.DbConnection.CreateCommand();
                cmd.CommandText = $"SELECT 1 FROM {sqlBuilder.ToSqlName(tableName)} WHERE 1=0";
                cmd.ExecuteScalar();
                return true;
            }
            catch { return false; }
        }

        private async Task<bool> TableExistsAsync(DAOContext daoContext, string tableName)
        {
            try
            {
                using var cmd = daoContext.CreateCommand();
                cmd.CommandText = $"SELECT 1 FROM {sqlBuilder.ToSqlName(tableName)} WHERE 1=0";
                await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                return true;
            }
            catch { return false; }
        }

        private HashSet<string> GetExistingColumnsSync(DAOContext daoContext, string quotedTableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var cmd = daoContext.CreateCommand();
                cmd.CommandText = $"SELECT * FROM {quotedTableName} WHERE 1=0";
                using var reader = cmd.ExecuteReader();
                for (int i = 0; i < reader.FieldCount; i++)
                    columns.Add(reader.GetName(i));
            }
            catch
            {
                var schema = daoContext.DbConnection.GetSchema("Columns", new[] { null, null, quotedTableName });
                foreach (DataRow row in schema.Rows)
                    columns.Add(row["COLUMN_NAME"].ToString());
            }
            return columns;
        }

        private async Task<HashSet<string>> GetExistingColumnsAsync(DAOContext daoContext, string quotedTableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var cmd = daoContext.CreateCommand();
                cmd.CommandText = $"SELECT * FROM {quotedTableName} WHERE 1=0";
                using var reader = await cmd.ExecuteReaderAsync(CancellationToken.None).ConfigureAwait(false);
                for (int i = 0; i < reader.FieldCount; i++)
                    columns.Add(reader.GetName(i));
            }
            catch
            {
                var schema = daoContext.DbConnection.GetSchema("Columns", new[] { null, null, quotedTableName });
                foreach (DataRow row in schema.Rows)
                    columns.Add(row["COLUMN_NAME"].ToString());
            }
            return columns;
        }

        private void ExecuteSqlSync(DAOContext daoContext, string sql)
        {
            using var cmd = daoContext.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private async Task ExecuteSqlAsync(DAOContext daoContext, string sql)
        {
            using var cmd = daoContext.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        /// <summary>
        /// 释放当前对象占用的所有资源。
        /// </summary>
        /// <remarks>
        /// 该方法会释放所有表创建锁的信号量，并清空表列缓存和已创建表的标记。
        /// </remarks>
        public void Dispose()
        {
            foreach (var sem in _tableCreationLocks.Values)
            {
                sem.Dispose();
            }

            _tableColumns.Clear();
            _createdTables.Clear();
        }
    }
}
