using LiteOrm.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// DAO上下文连接池，用于管理和复用数据库连接
    /// </summary>
    /// <remarks>
    /// DAOContextPool 是一个连接池管理类，用于高效地管理数据库连接，
    /// 避免频繁创建和销毁数据库连接的性能开销。
    /// 
    /// 主要功能包括：
    /// 1. 连接池管理 - 维护一个可复用的连接队列
    /// 2. 连接创建 - 按需创建新的数据库连接
    /// 3. 连接验证 - 验证池中的连接是否仍然有效
    /// 4. 连接复用 - 从池中获取可用的连接进行复用
    /// 5. 连接回收 - 将使用完的连接返回到池中
    /// 6. 生命周期管理 - 监控连接在池中的存活时间
    /// 7. 线程安全 - 使用锁机制确保多线程安全
    /// 8. 资源释放 - 实现 IDisposable 接口以正确释放所有资源
    /// 
    /// 该类通常由 DAOContextPoolFactory 进行创建和管理。
    /// 
    /// 使用示例：
    /// <code>
    /// var pool = new DAOContextPool(typeof(SqlConnection), connectionString);
    /// pool.PoolSize = 20;
    /// 
    /// // 获取连接
    /// var context = pool.PeekContext();
    /// 
    /// // 使用连接进行数据库操作
    /// // ...
    /// 
    /// // 将连接返回到池中
    /// pool.ReturnContext(context);
    /// 
    /// // 释放资源
    /// pool.Dispose();
    /// </code>
    /// </remarks>
    public class DAOContextPool : IDisposable
    {
        private readonly Queue<DAOContext> _pool = new Queue<DAOContext>();
        private readonly object _poolLock = new object();
        private bool _disposed = false;
        private SemaphoreSlim _semaphore = new SemaphoreSlim(100, 100);
        private int _maxPoolSize = 100;
        private readonly List<DAOContextPool> _readOnlyPools = new List<DAOContextPool>();
        private int _readOnlyIndex = 0;
        private readonly ConcurrentDictionary<string, HashSet<string>> _tableColumns = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _tableCreationLocks = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, byte> _createdTables = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 初始化 <see cref="DAOContextPool"/> 类的新实例。
        /// </summary>
        /// <param name="providerType">数据库提供程序类型。</param>
        /// <param name="connectionString">数据库连接字符串。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="providerType"/> 或 <paramref name="connectionString"/> 为 null 时抛出。</exception>
        public DAOContextPool(Type providerType, string connectionString)
        {
            ProviderType = providerType ?? throw new ArgumentNullException(nameof(providerType));
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            Name = providerType.Name;
        }

        /// <summary>
        /// 获取或设置连接池的缓冲大小（闲置连接数量）。
        /// </summary>
        public int PoolSize { get; set; } = 20;

        /// <summary>
        /// 获取或设置连接池的最大连接数限制。
        /// </summary>
        public int MaxPoolSize
        {
            get => _maxPoolSize;
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "Max connection count must be greater than 0");
                if (_maxPoolSize != value)
                {
                    _maxPoolSize = value;
                    _semaphore?.Dispose();
                    _semaphore = new SemaphoreSlim(value, value);
                }
            }
        }

        /// <summary>
        /// 获取数据库提供程序类型。
        /// </summary>
        public Type ProviderType { get; }

        /// <summary>
        /// 获取数据库连接字符串。
        /// </summary>
        public string ConnectionString { get; }

        /// <summary>
        /// 获取或设置连接在池中的最长存活时间。
        /// </summary>
        public TimeSpan KeepAliveDuration { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// 获取或设置连接池的名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 最大参数数量限制，0表示无限制，默认为2000。
        /// </summary>
        public int ParamCountLimit { get; set; } = 2000;

        /// <summary>
        /// 获取或设置该连接池是否为只读连接池。
        /// </summary>
        public bool IsReadOnlyPool { get; set; }

        /// <summary>
        /// 获取只读池对应的主库连接池，仅只读池有效。
        /// </summary>
        internal DAOContextPool MasterPool { get; private set; }
        /// <summary>
        /// 获取或设置该连接池是否开启自动建表同步。
        /// </summary>
        public bool SyncTable { get; set; }

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
        /// 获取该连接池对应数据库的 SQL 构建器。
        /// </summary>
        public SqlBuilder SqlBuilder => SqlBuilderFactory.Instance.GetSqlBuilder(ProviderType, Name);

        /// <summary>
        /// 确保指定表已在数据库中存在且包含所有必要的列。
        /// 同步版本，供 DAO 在命令构建前调用。
        /// </summary>
        public void EnsureTable(Type objectType, string[] tableArgs = null)
        {
            if (!SyncTable || typeof(IArged).IsAssignableFrom(objectType) && (tableArgs == null || tableArgs.Length == 0)) return;
            // 只读池直接转发给主库执行
            if (MasterPool != null) { MasterPool.EnsureTable(objectType, tableArgs); return; }

            var statements = ResolveEnsureTableDdl(objectType, tableArgs);
            if (statements.Count == 0) return;

            var ctx = PeekContextInternal();
            try { ApplyDdl(ctx.DbConnection, statements); }
            finally { ReturnContext(ctx); }
        }

        /// <summary>
        /// 确保指定表已在数据库中存在且包含所有必要的列。
        /// 异步版本，供初始化器调用。
        /// </summary>
        public async Task EnsureTableAsync(Type objectType, string[] tableArgs = null)
        {
            if (!SyncTable || typeof(IArged).IsAssignableFrom(objectType) && (tableArgs == null || tableArgs.Length == 0)) return;
            // 只读池直接转发给主库执行
            if (MasterPool != null) { await MasterPool.EnsureTableAsync(objectType, tableArgs).ConfigureAwait(false); return; }

            var statements = await ResolveEnsureTableDdlAsync(objectType, tableArgs).ConfigureAwait(false);
            if (statements.Count == 0) return;

            var ctx = await PeekContextInternalAsync().ConfigureAwait(false);
            try
            {
                await ApplyDdlAsync(ctx.DbConnection, statements).ConfigureAwait(false);
            }
            finally { ReturnContext(ctx); }
        }

        /// <summary>
        /// 根据实体类型和数据库当前状态，计算出需要执行的 DDL 语句列表（同步版本）。
        /// 内部自动获取连接、加锁、检查数据库、更新列缓存，并在完成后将表标记为已处理。
        /// 若表已处理则直接返回空列表，可安全用于 DDL 预览与导出。
        /// </summary>
        /// <param name="objectType">实体类型。</param>
        /// <param name="tableArgs">动态表名参数，适用于实现了 <see cref="IArged"/> 的类型。</param>
        /// <returns>需要执行的 DDL 语句列表（CREATE TABLE、ADD COLUMN、CREATE INDEX）。</returns>
        public List<string> ResolveEnsureTableDdl(Type objectType, string[] tableArgs = null)
        {
            if (MasterPool != null)
                return MasterPool.ResolveEnsureTableDdl(objectType, tableArgs);

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

                var ctx = PeekContextInternal();
                try
                {
                    var statements = ResolveEnsureTableDdlCore(ctx.DbConnection, SqlBuilder, tableName, tableDefinition.Columns);
                    _createdTables.TryAdd(tableTypeKey, 0);
                    return statements;
                }
                finally { ReturnContext(ctx); }
            }
            finally { sem.Release(); }
        }

        /// <summary>
        /// 根据实体类型和数据库当前状态，计算出需要执行的 DDL 语句列表（异步版本）。
        /// </summary>
        public async Task<List<string>> ResolveEnsureTableDdlAsync(Type objectType, string[] tableArgs = null)
        {
            if (MasterPool != null)
                return await MasterPool.ResolveEnsureTableDdlAsync(objectType, tableArgs).ConfigureAwait(false);

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

                var ctx = await PeekContextInternalAsync().ConfigureAwait(false);
                try
                {
                    var statements = await ResolveEnsureTableDdlCoreAsync(ctx.DbConnection, SqlBuilder, tableName, tableDefinition.Columns).ConfigureAwait(false);
                    _createdTables.TryAdd(tableTypeKey, 0);
                    return statements;
                }
                finally { ReturnContext(ctx); }
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
            DbConnection connection, SqlBuilder sqlBuilder,
            string tableName, IReadOnlyList<ColumnDefinition> cols)
        {
            var statements = new List<string>();

            if (!_tableColumns.ContainsKey(tableName))
            {
                // 表不在缓存中，需要检查数据库
                if (!TableExistsSync(connection, sqlBuilder, tableName))
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
                    var existingCols = GetExistingColumnsSync(connection, sqlBuilder.ToSqlName(tableName));
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
                    var existingCols = GetExistingColumnsSync(connection, sqlBuilder.ToSqlName(tableName));
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
            DbConnection connection, SqlBuilder sqlBuilder,
            string tableName, IReadOnlyList<ColumnDefinition> cols)
        {
            var statements = new List<string>();

            if (!_tableColumns.ContainsKey(tableName))
            {
                // 表不在缓存中，需要检查数据库
                if (!await TableExistsAsync(connection, sqlBuilder, tableName).ConfigureAwait(false))
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
                    var existingCols = await GetExistingColumnsAsync(connection, sqlBuilder.ToSqlName(tableName)).ConfigureAwait(false);
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
                    var existingCols = await GetExistingColumnsAsync(connection, sqlBuilder.ToSqlName(tableName)).ConfigureAwait(false);
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

        private void ApplyDdl(DbConnection connection, List<string> statements)
        {
            foreach (var sql in statements)
            {
                if (sql.TrimStart().StartsWith("CREATE", StringComparison.OrdinalIgnoreCase)
                    && sql.IndexOf("INDEX", StringComparison.OrdinalIgnoreCase) >= 0)
                    try { ExecuteSqlSync(connection, sql); } catch { }
                else
                    ExecuteSqlSync(connection, sql);
            }
        }

        private async Task ApplyDdlAsync(DbConnection connection, List<string> statements)
        {
            foreach (var sql in statements)
            {
                if (sql.TrimStart().StartsWith("CREATE", StringComparison.OrdinalIgnoreCase)
                    && sql.IndexOf("INDEX", StringComparison.OrdinalIgnoreCase) >= 0)
                    try { await ExecuteSqlAsync(connection, sql).ConfigureAwait(false); } catch { }
                else
                    await ExecuteSqlAsync(connection, sql).ConfigureAwait(false);
            }
        }

        private bool TableExistsSync(DbConnection connection, SqlBuilder sqlBuilder, string tableName)
        {
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"SELECT 1 FROM {sqlBuilder.ToSqlName(tableName)} WHERE 1=0";
                cmd.ExecuteScalar();
                return true;
            }
            catch { return false; }
        }

        private async Task<bool> TableExistsAsync(DbConnection connection, SqlBuilder sqlBuilder, string tableName)
        {
            try
            {
                using var cmd = (DbCommand)connection.CreateCommand();
                cmd.CommandText = $"SELECT 1 FROM {sqlBuilder.ToSqlName(tableName)} WHERE 1=0";
                await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                return true;
            }
            catch { return false; }
        }

        private HashSet<string> GetExistingColumnsSync(DbConnection connection, string quotedTableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"SELECT * FROM {quotedTableName} WHERE 1=0";
                using var reader = cmd.ExecuteReader();
                for (int i = 0; i < reader.FieldCount; i++)
                    columns.Add(reader.GetName(i));
            }
            catch
            {
                var schema = connection.GetSchema("Columns", new[] { null, null, quotedTableName });
                foreach (DataRow row in schema.Rows)
                    columns.Add(row["COLUMN_NAME"].ToString());
            }
            return columns;
        }

        private async Task<HashSet<string>> GetExistingColumnsAsync(DbConnection connection, string quotedTableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var cmd = (DbCommand)connection.CreateCommand();
                cmd.CommandText = $"SELECT * FROM {quotedTableName} WHERE 1=0";
                using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
                for (int i = 0; i < reader.FieldCount; i++)
                    columns.Add(reader.GetName(i));
            }
            catch
            {
                var schema = connection.GetSchema("Columns", new[] { null, null, quotedTableName });
                foreach (DataRow row in schema.Rows)
                    columns.Add(row["COLUMN_NAME"].ToString());
            }
            return columns;
        }

        private void ExecuteSqlSync(DbConnection connection, string sql)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }

        private async Task ExecuteSqlAsync(DbConnection connection, string sql)
        {
            using var cmd = (DbCommand)connection.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 添加只读数据库连接池。
        /// </summary>
        /// <param name="config">只读数据库配置。</param>
        public void AddReadOnlyPool(LiteOrm.Common.ReadOnlyDataSourceConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.ConnectionString)) return;

            var pool = new DAOContextPool(ProviderType, config.ConnectionString)
            {
                Name = $"{Name}_ReadOnly_{_readOnlyPools.Count}",
                PoolSize = config.PoolSize ?? PoolSize,
                MaxPoolSize = config.MaxPoolSize ?? MaxPoolSize,
                KeepAliveDuration = config.KeepAliveDuration ?? KeepAliveDuration,
                ParamCountLimit = config.ParamCountLimit ?? ParamCountLimit,
                IsReadOnlyPool = true
            };
            pool.MasterPool = this;
            _readOnlyPools.Add(pool);
        }

        /// <summary>
        /// 从连接池中获取一个可用的DAO上下文。
        /// </summary>
        /// <param name="readOnly">是否优先使用只读连接池，默认为 false。</param>
        /// <returns>一个可用的 <see cref="DAOContext"/> 实例。</returns>
        /// <exception cref="ObjectDisposedException">当连接池已被释放时抛出。</exception>
        public DAOContext PeekContext(bool readOnly = false)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DAOContextPool));

            if (readOnly && _readOnlyPools.Count > 0)
            {
                int index = Interlocked.Increment(ref _readOnlyIndex);
                return _readOnlyPools[Math.Abs(index) % _readOnlyPools.Count].PeekContext();
            }

            return PeekContextInternal();
        }

        /// <summary>
        /// 异步从连接池中获取一个可用的DAO上下文。
        /// </summary>
        /// <returns>表示异步操作的任务，结果为一个可用的 <see cref="DAOContext"/> 实例。</returns>
        /// <exception cref="ObjectDisposedException">当连接池已被释放时抛出。</exception>
        public async Task<DAOContext> PeekContextAsync(bool readOnly = false)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DAOContextPool));

            if (readOnly && _readOnlyPools.Count > 0)
            {
                int index = Interlocked.Increment(ref _readOnlyIndex);
                return await _readOnlyPools[(index & 0x7FFFFFFF) % _readOnlyPools.Count].PeekContextAsync().ConfigureAwait(false);
            }

            return await PeekContextInternalAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 内部获取DAO上下文，不进行初始化检查。
        /// </summary>
        /// <returns>一个可用的 <see cref="DAOContext"/> 实例。</returns>
        internal DAOContext PeekContextInternal()
        {
            lock (_poolLock)
            {
                // 尝试从池中获取可用的上下文
                while (_pool.Count > 0)
                {
                    var context = _pool.Dequeue();

                    // 检查连接是否仍然有效
                    if (IsContextValid(context))
                    {
                        context.EnsureConnectionOpen();
                        return context;
                    }

                    // 无效则销毁，这会通对应的信号量释放计数
                    context.Dispose();
                }
            }

            // 池为空，创建新连接
            var newContext = CreateNewContext();
            newContext.EnsureConnectionOpen();
            return newContext;
        }

        /// <summary>
        /// 内部异步获取DAO上下文，不进行初始化检查。
        /// </summary>
        /// <returns>一个可用的 <see cref="DAOContext"/> 实例。</returns>
        internal async Task<DAOContext> PeekContextInternalAsync()
        {
            DAOContext contextToUse = null;
            lock (_poolLock)
            {
                // 尝试从池中获取可用的上下文
                while (_pool.Count > 0)
                {
                    var context = _pool.Dequeue();

                    // 检查连接是否仍然有效
                    if (IsContextValid(context))
                    {
                        contextToUse = context;
                        break;
                    }

                    // 无效则销毁
                    context.Dispose();
                }
            }

            if (contextToUse != null)
            {
                await contextToUse.EnsureConnectionOpenAsync().ConfigureAwait(false);
                return contextToUse;
            }


            // 池为空，创建新连接
            var newContext = CreateNewContext();
            await newContext.EnsureConnectionOpenAsync().ConfigureAwait(false);
            return newContext;
        }


        /// <summary>
        /// 将DAO上下文返回到连接池中。
        /// </summary>
        /// <param name="context">要返回的DAO上下文。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="context"/> 为 null 时抛出。</exception>
        public void ReturnContext(DAOContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (_disposed)
            {
                context.Dispose();
                return;
            }

            lock (_poolLock)
            {
                // 重置上下文状态
                context.Reset();

                // 如果连接无效，销毁
                if (!IsContextValid(context))
                {
                    context.Dispose();
                    return;
                }

                // 如果池已满，销毁最旧的连接并添加新连接
                if (_pool.Count >= PoolSize)
                {
                    _pool.Dequeue().Dispose();
                }

                _pool.Enqueue(context);
            }
        }

        /// <summary>
        /// 当连接被释放时调用，用于释放信号量计数。
        /// </summary>
        internal void OnContextDisposed()
        {
            if (!_disposed)
            {
                try { _semaphore.Release(); } catch { }
            }
        }

        private bool IsContextValid(DAOContext context)
        {
            if (context is null)
                return false;

            // 检查连接是否存活
            if (KeepAliveDuration != TimeSpan.Zero &&
                context.LastActiveTime + KeepAliveDuration < DateTime.Now)
            {
                return false;
            }

            // 检查连接状态
            try
            {
                var connection = context.DbConnection;
                if (connection.State == ConnectionState.Broken)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 创建一个新的数据库连接上下文。
        /// </summary>
        /// <returns>新创建的 <see cref="DAOContext"/> 实例。</returns>
        private DAOContext CreateNewContext()
        {
            if (!_semaphore.Wait(10000))
                throw new InvalidOperationException("Maximum connection limit reached, cannot create a new connection.");
            try
            {
                var connection = Activator.CreateInstance(ProviderType) as DbConnection;
                if (connection == null)
                    throw new InvalidOperationException("Failed to create a database connection instance.");

                connection.ConnectionString = ConnectionString;
                var context = new DAOContext(connection, this);
                return context;
            }
            catch
            {
                _semaphore.Release();
                throw;
            }
        }

        /// <summary>
        /// 释放连接池及其所有成员占用的资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放连接池及其所有成员占用的资源。
        /// </summary>
        /// <param name="disposing">指示是否是主动调用Dispose方法。</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    lock (_poolLock)
                    {
                        while (_pool.Count > 0)
                        {
                            var context = _pool.Dequeue();
                            context.Dispose();
                        }
                    }

                    foreach (var pool in _readOnlyPools)
                    {
                        pool.Dispose();
                    }

                    foreach (var sem in _tableCreationLocks.Values)
                    {
                        sem.Dispose();
                    }

                    _tableColumns.Clear();
                    _createdTables.Clear();

                    _semaphore?.Dispose();
                }

                // 释放非托管资源

                _disposed = true;
            }
        }
    }
}
