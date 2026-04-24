using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace LiteOrm.Common
{
    /// <summary>
    /// 非泛型可枚举结果接口。
    /// 提供同步与异步方式获取查询结果、将结果转换为列表以及对结果逐项执行操作的抽象支持。
    /// 该接口用于在不关心元素类型的情况下操作查询结果。
    /// </summary>
    public interface IEnumerableResult : IEnumerable
    {
        /// <summary>
        /// 获取第一个元素，若不存在则返回默认值
        /// </summary>
        /// <returns>第一个元素或默认值</returns>
        object FirstOrDefault();

        /// <summary>
        /// 异步获取第一个元素，若不存在则返回默认值
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>第一个元素或默认值</returns>
        ValueTask<object> FirstOrDefaultAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 将结果转换为列表
        /// </summary>
        /// <returns>结果列表</returns>
        IList GetResult();

        /// <summary>
        /// 异步将结果转换为列表
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>结果列表</returns>
        Task<IList> GetResultAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 对每个元素执行指定的操作
        /// </summary>
        /// <param name="action">要对每个元素执行的操作</param>
        void ForEach(Action<object> action);

        /// <summary>
        /// 异步对每个元素执行指定的操作
        /// </summary>
        /// <param name="action">要对每个元素执行的操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task ForEachAsync(Action<object> action, CancellationToken cancellationToken = default);
    }


    /// <summary>
    /// 表达式查询结果的基类。
    /// 封装了构造数据库命令、执行并返回不同结果类型的通用逻辑。
    /// 支持由预构造的 <see cref="DbCommandProxy"/>（用于复用）或按需创建命令两种模式。
    /// 实现应负责在合适的时机释放执行命令（如果命令为按需创建）。
    /// </summary>
    /// <typeparam name="T">表示由该结果返回的具体结果类型。</typeparam>
    public abstract class CommandResult<T> : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// 要执行的DAO对象，子类通过该对象执行相应的数据库操作以获取结果。
        /// </summary>
        protected readonly DAOBase _dao;
        /// <summary>
        /// 预处理的 SQL 语句和参数列表。
        /// </summary>
        protected readonly PreparedSql _sql;
        /// <summary>
        /// 预定义的数据库命令代理实例，子类可以直接使用该实例执行数据库操作以获取结果，适用于需要重复执行同一命令的场景。
        /// </summary>
        protected readonly DbCommandProxy _preparedCommand;

        private DbCommandProxy _executedCommand;

        /// <summary>
        /// 初始化 <see cref="CommandResult{T}"/> 类的新实例。
        /// </summary>
        /// <param name="dao">要执行的DAO对象。</param>
        /// <param name="sql">预处理的 SQL 语句和参数列表。</param>
        /// <exception cref="ArgumentNullException">dao 或 sql 为 null 时抛出。</exception>
        internal protected CommandResult(DAOBase dao, PreparedSql sql)
        {
            if (dao == null) throw new ArgumentNullException(nameof(dao));
            if (sql == null) throw new ArgumentNullException(nameof(sql));
            _dao = dao;
            _sql = sql;
        }

        /// <summary>
        /// 初始化 <see cref="CommandResult{T}"/> 类的新实例，并使用预定义的数据库命令代理实例。
        /// </summary>
        /// <param name="preparedCommand">预定义的数据库命令代理实例。</param>
        /// <exception cref="ArgumentNullException">preparedCommand 为 null 时抛出。</exception>
        internal protected CommandResult(DbCommandProxy preparedCommand)
        {
            if (preparedCommand == null) throw new ArgumentNullException(nameof(preparedCommand));
            _preparedCommand = preparedCommand;
        }

        /// <summary>
        /// 获取用于执行当前结果的 <see cref="DbCommandProxy"/>。
        /// 如果构造时传入了预定义的命令（用于重用），则返回该命令；否则按需创建并缓存一次性命令。
        /// 调用方不应在每次读取后立即释放预定义命令；对于按需创建命令，释放责任由 <see cref="CommandResult{T}"/> 管理。
        /// </summary>
        /// <returns>用于执行查询的 <see cref="DbCommandProxy"/> 实例。</returns>
        internal protected DbCommandProxy GetCommand()
        {
            return _executedCommand ??= _preparedCommand ?? _dao.MakeNamedParamCommand(_sql);
        }

        /// <summary>
        /// 异步获取用于执行当前结果的 <see cref="DbCommandProxy"/>。
        /// 对于按需创建的命令，会在内部异步创建并缓存用于随后调用；对于预定义命令直接返回。
        /// </summary>
        /// <param name="cancellationToken">取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>表示异步操作的任务，结果为命令代理实例。</returns>
        internal protected async Task<DbCommandProxy> GetCommandAsync(CancellationToken cancellationToken = default)
        {
            return _executedCommand ??= _preparedCommand ?? await _dao.MakeNamedParamCommandAsync(_sql, cancellationToken);
        }

        /// <summary>
        /// 同步执行命令并返回结果。
        /// </summary>
        /// <returns>命令执行结果。</returns>
        public abstract T GetResult();

        /// <summary>
        /// 异步执行命令并返回结果。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，包含命令执行结果。</returns>
        public abstract Task<T> GetResultAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 释放当前对象占用的资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放非托管资源，并可选择性地释放托管资源。
        /// </summary>
        /// <param name="disposing">如果为 true，则同时释放托管资源；如果为 false，则只释放非托管资源。</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_preparedCommand != null) return;
                _executedCommand?.Dispose();
            }
        }

        /// <summary>
        /// 异步释放非托管资源，并可选择性地释放托管资源。
        /// </summary>
        /// <returns>表示异步操作的任务。</returns>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 异步释放非托管资源，并可选择性地释放托管资源。
        /// </summary>
        /// <returns>表示异步操作的任务。</returns>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (_preparedCommand != null) return;
#if NETSTANDARD2_0
            if(_executedCommand != null) _executedCommand.Dispose();
#else
            if (_executedCommand != null) await _executedCommand.DisposeAsync();
#endif
        }
    }

    /// <summary>
    /// 可枚举结果类型，基于 <see cref="DbCommandProxy.ExecuteReader()"/> 的执行方式。
    /// 提供同步和异步的逐行枚举、一次性读取为列表以及 FirstOrDefault 等便捷方法。
    /// 支持使用 DAO/PreparedSql 按需创建命令，或通过预构造的 <see cref="DbCommandProxy"/> 重用命令。
    /// </summary>
    /// <typeparam name="TResult">查询行转换后返回的元素类型。</typeparam>
    public class EnumerableResult<TResult> : CommandResult<List<TResult>>, IEnumerable<TResult>, IAsyncEnumerable<TResult>, IEnumerableResult
    {
        private readonly Func<DbDataReader, TResult> _readerFunc;

        /// <summary>
        /// 初始化 <see cref="EnumerableResult{TResult}"/> 类的新实例。
        /// </summary>
        /// <param name="dao">要执行的DAO对象。</param>
        /// <param name="sql">预处理的 SQL 语句和参数列表。</param>
        /// <param name="readerFunc">将 <see cref="IDataReader"/> 的一行数据转换为 <typeparamref name="TResult"/> 实例的委托。</param>
        public EnumerableResult(DAOBase dao, PreparedSql sql, Func<DbDataReader, TResult> readerFunc = null)
            : base(dao, sql)
        {
            _readerFunc = readerFunc;
        }

        /// <summary>
        /// 初始化 <see cref="EnumerableResult{TResult}"/> 类的新实例，并使用预定义的数据库命令代理实例。
        /// </summary>
        /// <param name="preparedCommand">预定义的数据库命令代理实例。</param>
        /// <param name="readerFunc">将 <see cref="IDataReader"/> 的一行数据转换为 <typeparamref name="TResult"/> 实例的委托。</param>
        public EnumerableResult(DbCommandProxy preparedCommand, Func<DbDataReader, TResult> readerFunc = null)
            : base(preparedCommand)
        {
            _readerFunc = readerFunc;
        }

        /// <summary>
        /// 获取同步枚举器，用于逐行读取并转换结果集。
        /// 使用完毕后枚举器会关闭底层的 <see cref="DbDataReader"/> 并释放相关命令（若为按需创建）。
        /// </summary>
        /// <returns>用于同步遍历结果集的 <see cref="IEnumerator{T}"/>。</returns>
        public IEnumerator<TResult> GetEnumerator()
        {
            var command = GetCommand();
            using (DbDataReader reader = command.ExecuteReader())
            {
                var func = _readerFunc ?? DataReaderConverter.GetConverter<TResult>(reader);
                while (reader.Read())
                {
                    yield return func(reader);
                }
            }
        }

        /// <summary>
        /// 返回遍历结果集的非泛型枚举器。
        /// </summary>
        /// <returns>用于遍历结果集的 <see cref="IEnumerator"/>。</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 返回异步遍历结果集的枚举器。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>用于异步遍历结果集的 <see cref="IAsyncEnumerator{T}"/>。</returns>
        public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new AsyncEnumerator(GetCommandAsync, _readerFunc, cancellationToken);
        }

        /// <summary>
        /// 同步获取结果集中的第一个元素，若结果集为空则返回默认值。
        /// 该方法会在读取完成后关闭底层 reader 并释放命令资源（若为按需创建）。
        /// </summary>
        /// <returns>第一个元素，或类型的默认值。</returns>
        public TResult FirstOrDefault()
        {
            var command = GetCommand();
            using DbDataReader reader = command.ExecuteReader();
            if (!reader.Read()) return default;
            var func = _readerFunc ?? DataReaderConverter.GetConverter<TResult>(reader);
            return func(reader);
        }

        /// <summary>
        /// 异步获取结果集中的第一个元素，若结果集为空则返回默认值。
        /// 在方法返回后会立即关闭 reader 并释放命令资源（若为按需创建）。
        /// </summary>
        /// <param name="cancellationToken">取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>包含第一个元素或默认值的任务。</returns>
        public async ValueTask<TResult> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
        {
            var command = await GetCommandAsync(cancellationToken).ConfigureAwait(false);
            using DbDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return default;
            var func = _readerFunc ?? DataReaderConverter.GetConverter<TResult>(reader);
            return func(reader);
        }

        /// <summary>
        /// 将结果集转换为列表。
        /// </summary>
        /// <returns>包含所有元素的 <see cref="List{T}"/>。</returns>
        public List<TResult> ToList()
        {
            return new List<TResult>(this);
        }

        /// <summary>
        /// 异步将结果集转换为列表。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，包含所有元素的列表。</returns>
        public async Task<List<TResult>> ToListAsync(CancellationToken cancellationToken = default)
        {
            var list = new List<TResult>();
            await foreach (var item in this.WithCancellation(cancellationToken))
            {
                list.Add(item);
            }
            return list;
        }

        /// <summary>
        /// 执行命令并以列表形式返回所有结果。
        /// </summary>
        /// <returns>包含所有元素的 <see cref="List{T}"/>。</returns>
        public override List<TResult> GetResult()
        {
            return this.ToList();
        }

        /// <summary>
        /// 异步执行命令并以列表形式返回所有结果。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，包含所有元素的列表。</returns>
        public override async Task<List<TResult>> GetResultAsync(CancellationToken cancellationToken = default)
        {
            return await this.ToListAsync(cancellationToken);
        }

        /// <summary>
        /// 对每个元素执行指定的操作
        /// </summary>
        /// <param name="action">要对每个元素执行的操作</param>
        public void ForEach(Action<TResult> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            foreach (var item in this)
            {
                action(item);
            }
        }

        /// <summary>
        /// 异步对每个元素执行指定的操作
        /// </summary>
        /// <param name="action">要对每个元素执行的操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task ForEachAsync(Action<TResult> action, CancellationToken cancellationToken = default)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            await foreach (var item in this.WithCancellation(cancellationToken))
            {
                action(item);
            }
        }

        private class AsyncEnumerator : IAsyncEnumerator<TResult>
        {
            private readonly Func<DbDataReader, TResult> _readerFunc;
            private readonly CancellationToken _cancellationToken;
            private Func<CancellationToken, Task<DbCommandProxy>> _commandFunc;
            private DbDataReader _reader;
            private TResult _current;
            private Func<DbDataReader, TResult> _func;

            /// <summary>
            /// 初始化 <see cref="AsyncEnumerator"/> 类的新实例。
            /// </summary>
            /// <param name="commandFunc">用于获取要执行的数据库命令的委托。</param>
            /// <param name="readerFunc">将数据行转换为元素的委托。</param>
            /// <param name="cancellationToken">取消令牌。</param>
            public AsyncEnumerator(Func<CancellationToken, Task<DbCommandProxy>> commandFunc, Func<DbDataReader, TResult> readerFunc, CancellationToken cancellationToken)
            {
                _commandFunc = commandFunc;
                _readerFunc = readerFunc;
                _cancellationToken = cancellationToken;
            }

            /// <summary>
            /// 获取枚举器当前位置的元素。
            /// </summary>
            public TResult Current => _current;

            public async ValueTask DisposeAsync()
            {
                if (_reader != null)
                {
                    _reader.Dispose();
                    await Task.CompletedTask;
                }
            }

            public async ValueTask<bool> MoveNextAsync()
            {
                _cancellationToken.ThrowIfCancellationRequested();

                if (_reader == null)
                {
                    var command = await _commandFunc(_cancellationToken).ConfigureAwait(false);
                    _reader = await command.ExecuteReaderAsync(CommandBehavior.Default, _cancellationToken).ConfigureAwait(false);
                    _func = _readerFunc ?? DataReaderConverter.GetConverter<TResult>(_reader);
                }

                if (await _reader.ReadAsync(_cancellationToken).ConfigureAwait(false))
                {
                    _current = _func(_reader);
                    return true;
                }

                await DisposeAsync().ConfigureAwait(false);
                return false;
            }
        }

        // IEnumerableResult接口的显式实现
        object IEnumerableResult.FirstOrDefault()
        {
            return FirstOrDefault();
        }

        async ValueTask<object> IEnumerableResult.FirstOrDefaultAsync(CancellationToken cancellationToken)
        {
            return await FirstOrDefaultAsync(cancellationToken);
        }

        IList IEnumerableResult.GetResult()
        {
            return GetResult();
        }

        async Task<IList> IEnumerableResult.GetResultAsync(CancellationToken cancellationToken)
        {
            return await GetResultAsync(cancellationToken);
        }

        void IEnumerableResult.ForEach(Action<object> action)
        {
            ForEach(item => action(item));
        }

        async Task IEnumerableResult.ForEachAsync(Action<object> action, CancellationToken cancellationToken)
        {
            await ForEachAsync(item => action(item), cancellationToken);
        }
    }

    /// <summary>
    /// 标量值结果类型，对应 <see cref="DbCommandProxy.ExecuteScalar()"/> 执行方式。
    /// 用于返回单个值（第一行第一列）的查询结果，并提供同步/异步访问。
    /// </summary>
    /// <typeparam name="TResult">表示转换后返回的值类型。</typeparam>
    public class ValueResult<TResult> : CommandResult<TResult>
    {
        private readonly Func<object, TResult> _resultConverter;

        /// <summary>
        /// 使用 DAO 与 PreparedSql 初始化一个标量结果对象，按需创建命令执行查询。
        /// </summary>
        /// <param name="dao">用于创建命令并执行查询的 <see cref="DAOBase"/>。</param>
        /// <param name="sql">预处理的 SQL 与参数集合。</param>
        /// <param name="resultConverter">可选的转换器，将数据库原始值转换为 TResult。</param>
        public ValueResult(DAOBase dao, PreparedSql sql, Func<object, TResult> resultConverter = null)
            : base(dao, sql)
        {
            _resultConverter = resultConverter ?? ((obj) =>
            {
                return (TResult)dao.SqlBuilder.ConvertFromDbValue(obj, typeof(TResult));
            });
        }

        /// <summary>
        /// 使用已准备好的 <see cref="DbCommandProxy"/> 初始化一个标量结果对象，以支持命令重用。
        /// </summary>
        /// <param name="preparedCommand">预构建并可能缓存的数据库命令代理。</param>
        /// <param name="resultConverter">可选的转换器，将数据库原始值转换为 TResult。</param>
        public ValueResult(DbCommandProxy preparedCommand, Func<object, TResult> resultConverter = null)
            : base(preparedCommand)
        {
            _resultConverter = resultConverter ?? ((obj) =>
            {
                return (TResult)preparedCommand.SqlBuilder.ConvertFromDbValue(obj, typeof(TResult));
            });
        }

        /// <summary>
        /// 同步执行标量查询并返回转换后的结果。
        /// 如果命令为按需创建，则在方法结束时释放命令资源。
        /// </summary>
        /// <returns>转换后的标量结果。</returns>
        public override TResult GetResult()
        {
            var command = GetCommand();
            var scalarValue = command.ExecuteScalar();
            return _resultConverter(scalarValue);
        }

        /// <summary>
        /// 异步执行标量查询并返回转换后的结果。
        /// 若命令按需创建，则在方法结束时释放相关命令资源。
        /// </summary>
        /// <param name="cancellationToken">取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>包含转换后标量值的任务。</returns>
        public override async Task<TResult> GetResultAsync(CancellationToken cancellationToken = default)
        {
            var command = await GetCommandAsync(cancellationToken).ConfigureAwait(false);
            var scalarValue = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return _resultConverter(scalarValue);
        }
    }

    /// <summary>
    /// 非查询结果类型，对应 <see cref="DbCommandProxy.ExecuteNonQuery()"/> 的执行方式。
    /// 用于执行不返回行的命令（如 UPDATE/DELETE/INSERT）并返回受影响的行数。
    /// 支持使用按需创建的命令或传入的预构建命令。
    /// </summary>
    public class NonQueryResult : CommandResult<int>
    {
        /// <summary>
        /// 使用 DAO 与 PreparedSql 初始化一个 NonQueryResult，用于按需创建命令并执行非查询操作。
        /// </summary>
        /// <param name="dao">用于创建命令并执行的 <see cref="DAOBase"/>。</param>
        /// <param name="sql">预处理的 SQL 与参数集合。</param>
        public NonQueryResult(DAOBase dao, PreparedSql sql)
            : base(dao, sql)
        { }

        /// <summary>
        /// 使用已准备好的 <see cref="DbCommandProxy"/> 初始化 NonQueryResult，以支持命令重用。
        /// </summary>
        /// <param name="preparedCommand">预构建并可能缓存的数据库命令代理。</param>
        public NonQueryResult(DbCommandProxy preparedCommand)
            : base(preparedCommand)
        { }

        /// <summary>
        /// 同步执行非查询命令并返回受影响的行数。
        /// 对于按需创建的命令，执行后会由本对象负责释放命令资源。
        /// </summary>
        /// <returns>受影响的行数。</returns>
        public override int GetResult()
        {
            var command = GetCommand();
            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// 异步执行非查询命令并返回受影响的行数。
        /// 对于按需创建的命令，执行后会由本对象负责释放命令资源。
        /// </summary>
        /// <param name="cancellationToken">取消操作的 <see cref="CancellationToken"/>。</param>
        /// <returns>包含受影响行数的任务。</returns>
        public override async Task<int> GetResultAsync(CancellationToken cancellationToken = default)
        {
            var command = await GetCommandAsync(cancellationToken).ConfigureAwait(false);
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// DataTable结果类，对应返回DataTable的查询方式
    /// </summary>
    public class DataTableResult : CommandResult<DataTable>
    {
        /// <summary>
        /// 读取 IDataReader 的一行数据并将其转换为 DataRow 的委托，允许用户自定义行转换逻辑，为空时使用默认转换逻辑。
        /// </summary>
        public Func<IDataReader, DataTable, DataRow> ReadRowHandler;
        private DataTable _dataTable;

        /// <summary>
        /// 初始化 <see cref="DataTableResult"/> 类的新实例。
        /// </summary>
        /// <param name="dao">要执行的数据库DAO对象。</param>
        /// <param name="sql">预处理的 SQL 语句和参数列表。</param>
        /// <param name="readRowHandler">将 <see cref="IDataReader"/> 的一行数据转换为 <see cref="DataRow"/> 的委托。</param>
        public DataTableResult(DAOBase dao, PreparedSql sql, Func<IDataReader, DataTable, DataRow> readRowHandler = null)
            : base(dao, sql)
        {
            ReadRowHandler = readRowHandler;
            _dataTable = null;
        }

        /// <summary>
        /// 使用已准备好的 <see cref="DbCommandProxy"/> 初始化 DataTableResult，适用于需要重用同一命令的场景。
        /// </summary>
        /// <param name="preparedCommand">预构建并可能缓存的数据库命令代理。</param>
        /// <param name="readRowHandler">可选的行映射委托。</param>
        public DataTableResult(DbCommandProxy preparedCommand, Func<IDataReader, DataTable, DataRow> readRowHandler = null)
            : base(preparedCommand)
        {
            ReadRowHandler = readRowHandler;
            _dataTable = null;
        }

        /// <summary>
        /// 获取DataTable结果
        /// </summary>
        /// <returns>DataTable结果</returns>
        public override DataTable GetResult()
        {
            if (_dataTable == null)
            {
                LoadData();
            }
            return _dataTable;
        }

        /// <summary>
        /// 异步获取DataTable结果
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>DataTable结果</returns>
        public override async Task<DataTable> GetResultAsync(CancellationToken cancellationToken = default)
        {
            if (_dataTable == null)
            {
                await LoadDataAsync(cancellationToken);
            }
            return _dataTable;
        }

        /// <summary>
        /// 同步将查询结果加载到 <see cref="DataTable"/>。
        /// 方法内部会创建命令并执行 <see cref="DbCommandProxy.ExecuteReader()"/>，读取完成后会关闭 reader 并释放命令（若为按需创建）。
        /// </summary>
        private void LoadData()
        {
            _dataTable = new DataTable();
            var command = GetCommand();

            using (var reader = command.ExecuteReader())
            {
                if (_dataTable.Columns.Count == 0)
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        _dataTable.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
                    }
                }

                _dataTable.BeginLoadData();
                while (reader.Read())
                {
                    DataRow row;
                    if (ReadRowHandler != null)
                    {
                        row = ReadRowHandler(reader, _dataTable);
                    }
                    else
                    {
                        row = _dataTable.NewRow();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                        }
                    }
                    _dataTable.Rows.Add(row);
                }
                _dataTable.EndLoadData();
            }
        }

        /// <summary>
        /// 异步将查询结果加载到 <see cref="DataTable"/>。
        /// 异步方法会异步创建命令和 reader，并在完成后释放相关资源。
        /// </summary>
        /// <param name="cancellationToken">取消操作的 <see cref="CancellationToken"/>。</param>
        private async Task LoadDataAsync(CancellationToken cancellationToken = default)
        {
            _dataTable = new DataTable();
            var command = await GetCommandAsync(cancellationToken).ConfigureAwait(false);
            using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                if (_dataTable.Columns.Count == 0)
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        _dataTable.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
                    }
                }

                _dataTable.BeginLoadData();
                while (await reader.ReadAsync(cancellationToken))
                {
                    DataRow row;
                    if (ReadRowHandler != null)
                    {
                        row = ReadRowHandler(reader, _dataTable);
                    }
                    else
                    {
                        row = _dataTable.NewRow();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[i] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
                        }
                    }
                    _dataTable.Rows.Add(row);
                }
                _dataTable.EndLoadData();
            }
        }
    }
}