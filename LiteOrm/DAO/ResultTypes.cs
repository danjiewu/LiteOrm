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
    /// 可枚举结果的非泛型接口
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
    /// 表达式结果的基类
    /// </summary>
    /// <typeparam name="T">结果类型</typeparam>
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
        /// 创建数据库命令。
        /// </summary>
        /// <returns>数据库命令代理实例。</returns>
        internal protected DbCommandProxy GetCommand()
        {
            return _executedCommand ??= _preparedCommand ?? _dao.MakeNamedParamCommand(_sql);
        }

        /// <summary>
        /// 异步创建数据库命令。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，包含数据库命令代理实例。</returns>
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
    /// 可枚举结果类，对应ExecuteReader方式
    /// </summary>
    /// <typeparam name="TResult">元素类型</typeparam>
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

        public EnumerableResult(DbCommandProxy preparedCommand, Func<DbDataReader, TResult> readerFunc = null)
            : base(preparedCommand)
        {
            _readerFunc = readerFunc;
        }

        /// <summary>
        /// 返回遍历结果集的枚举器。
        /// </summary>
        /// <returns>用于遍历结果集的 <see cref="IEnumerator{T}"/>。</returns>
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
        /// 获取结果集中的第一个元素，若结果集为空则返回默认值。
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
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，包含第一个元素或类型的默认值。</returns>
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
            /// <param name="command">要执行的数据库命令。</param>
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
    /// 值结果类，对应ExecuteScalar方式
    /// </summary>
    /// <typeparam name="TResult">值类型</typeparam>
    public class ValueResult<TResult> : CommandResult<TResult>
    {
        private readonly Func<object, TResult> _resultConverter;

        /// <summary>
        /// 初始化 <see cref="ValueResult{TResult}"/> 类的新实例。
        /// </summary>
        /// <param name="dao">要执行的数据库命令。</param>
        /// <param name="sql">预处理的 SQL 语句和参数列表。</param>
        /// <param name="resultConverter">将标量结果转换为 <typeparamref name="TResult"/> 的委托，为 null 时使用默认转换。</param>
        public ValueResult(DAOBase dao, PreparedSql sql, Func<object, TResult> resultConverter = null)
            : base(dao, sql)
        {
            _resultConverter = resultConverter ?? ((obj) =>
            {
                return (TResult)dao.SqlBuilder.ConvertFromDbValue(obj, typeof(TResult));
            });
        }

        public ValueResult(DbCommandProxy preparedCommand, Func<object, TResult> resultConverter = null)
            : base(preparedCommand)
        {
            _resultConverter = resultConverter ?? ((obj) =>
            {
                return (TResult)preparedCommand.SqlBuilder.ConvertFromDbValue(obj, typeof(TResult));
            });
        }

        /// <summary>
        /// 执行命令并返回标量结果。
        /// </summary>
        /// <returns>命令执行的标量结果。</returns>
        public override TResult GetResult()
        {
            var command = GetCommand();
            var scalarValue = command.ExecuteScalar();
            return _resultConverter(scalarValue);
        }

        /// <summary>
        /// 异步执行命令并返回标量结果。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，包含命令执行的标量结果。</returns>
        public override async Task<TResult> GetResultAsync(CancellationToken cancellationToken = default)
        {
            var command = await GetCommandAsync(cancellationToken).ConfigureAwait(false);
            var scalarValue = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return _resultConverter(scalarValue);
        }
    }

    /// <summary>
    /// 空结果类，对应ExecuteNonQuery方式
    /// </summary>
    public class NonQueryResult : CommandResult<int>
    {
        /// <summary>
        /// 初始化 <see cref="NonQueryResult"/> 类的新实例。
        /// </summary>
        /// <param name="dao">要执行的数据库DAO对象。</param>
        /// <param name="sql">预处理的 SQL 语句和参数列表。</param>
        public NonQueryResult(DAOBase dao, PreparedSql sql)
            : base(dao, sql)
        { }

        public NonQueryResult(DbCommandProxy preparedCommand)
            : base(preparedCommand)
        { }

        /// <summary>
        /// 执行命令并返回受影响的行数。
        /// </summary>
        /// <returns>受影响的行数。</returns>
        public override int GetResult()
        {
            var command = GetCommand();
            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// 异步执行命令并返回受影响的行数。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，包含受影响的行数。</returns>
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
        private readonly Func<IDataReader, DataTable, DataRow> _readRow;
        private DataTable _dataTable;
        private bool _hasLoaded;

        /// <summary>
        /// 初始化 <see cref="DataTableResult"/> 类的新实例。
        /// </summary>
        /// <param name="dao">要执行的数据库DAO对象。</param>
        /// <param name="sql">预处理的 SQL 语句和参数列表。</param>
        /// <param name="readRow">将 <see cref="IDataReader"/> 的一行数据转换为 <see cref="DataRow"/> 的委托。</param>
        public DataTableResult(DAOBase dao, PreparedSql sql, Func<IDataReader, DataTable, DataRow> readRow)
            : base(dao, sql)
        {
            _readRow = readRow;
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
        /// 加载数据到DataTable
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
                    if (_readRow != null)
                    {
                        row = _readRow(reader, _dataTable);
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
        /// 异步加载数据到DataTable
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
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
                    if (_readRow != null)
                    {
                        row = _readRow(reader, _dataTable);
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