using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

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
    public abstract class CommandResult<T> : IDisposable
    {
        /// <summary>
        /// 要执行的数据库命令对象，子类通过该对象执行相应的数据库操作以获取结果。
        /// </summary>
        protected readonly DbCommandProxy _command;
        /// <summary>
        /// 标记是否在释放时自动销毁命令对象，默认为 true。若为 true，则在调用 Dispose 方法时会自动调用 _command.Dispose() 来释放数据库命令对象占用的资源；如果为 false，则需要由外部代码负责管理命令对象的生命周期，确保在适当的时候手动调用 _command.Dispose() 来释放资源。
        /// </summary>
        protected readonly bool _autoDisposeCommand;

        /// <summary>
        /// 初始化 <see cref="CommandResult{T}"/> 类的新实例。
        /// </summary>
        /// <param name="command">要执行的数据库命令。</param>
        /// <param name="autoDisposeCommand">是否在释放时自动销毁命令对象，默认为 true。</param>
        protected CommandResult(DbCommandProxy command, bool autoDisposeCommand = true)
        {
            _command = command;
            _autoDisposeCommand = autoDisposeCommand;
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
            if (disposing && _autoDisposeCommand && _command != null)
            {
                _command.Dispose();
            }
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
        /// <param name="command">要执行的数据库命令。</param>
        /// <param name="readerFunc">将 <see cref="IDataReader"/> 的一行数据转换为 <typeparamref name="TResult"/> 实例的委托。</param>
        /// <param name="autoDisposeCommand">是否在释放时自动销毁命令对象，默认为 true。</param>
        public EnumerableResult(DbCommandProxy command, Func<DbDataReader, TResult> readerFunc = null, bool autoDisposeCommand = true)
            : base(command, autoDisposeCommand)
        {
            _readerFunc = readerFunc;
        }

        /// <summary>
        /// 返回遍历结果集的枚举器。
        /// </summary>
        /// <returns>用于遍历结果集的 <see cref="IEnumerator{T}"/>。</returns>
        public IEnumerator<TResult> GetEnumerator()
        {
            using (DbDataReader reader = _command.ExecuteReader())
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
            return new AsyncEnumerator(_command, _readerFunc, cancellationToken);
        }

        /// <summary>
        /// 获取结果集中的第一个元素，若结果集为空则返回默认值。
        /// </summary>
        /// <returns>第一个元素，或类型的默认值。</returns>
        public TResult FirstOrDefault()
        {
            using DbDataReader reader = _command.ExecuteReader();
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
            using DbDataReader reader = await _command.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken).ConfigureAwait(false);
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
            private readonly DbCommandProxy _command;
            private readonly Func<DbDataReader, TResult> _readerFunc;
            private readonly CancellationToken _cancellationToken;
            private DbDataReader _reader;
            private TResult _current;
            private Func<DbDataReader, TResult> _func;

            /// <summary>
            /// 初始化 <see cref="AsyncEnumerator"/> 类的新实例。
            /// </summary>
            /// <param name="command">要执行的数据库命令。</param>
            /// <param name="readerFunc">将数据行转换为元素的委托。</param>
            /// <param name="cancellationToken">取消令牌。</param>
            public AsyncEnumerator(DbCommandProxy command, Func<DbDataReader, TResult> readerFunc, CancellationToken cancellationToken)
            {
                _command = command;
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
                    _reader = await _command.ExecuteReaderAsync(CommandBehavior.Default, _cancellationToken).ConfigureAwait(false);
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
        /// <param name="command">要执行的数据库命令。</param>
        /// <param name="resultConverter">将标量结果转换为 <typeparamref name="TResult"/> 的委托，为 null 时使用默认转换。</param>
        /// <param name="autoDisposeCommand">是否在释放时自动销毁命令对象，默认为 true。</param>
        public ValueResult(DbCommandProxy command, Func<object, TResult> resultConverter = null, bool autoDisposeCommand = true)
            : base(command, autoDisposeCommand)
        {
            _resultConverter = resultConverter ?? ((obj) =>
            {
                return (TResult)command.SqlBuilder.ConvertFromDbValue(obj, typeof(TResult));
            });
        }

        /// <summary>
        /// 执行命令并返回标量结果。
        /// </summary>
        /// <returns>命令执行的标量结果。</returns>
        public override TResult GetResult()
        {
            var scalarValue = _command.ExecuteScalar();
            return _resultConverter(scalarValue);
        }

        /// <summary>
        /// 异步执行命令并返回标量结果。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，包含命令执行的标量结果。</returns>
        public override async Task<TResult> GetResultAsync(CancellationToken cancellationToken = default)
        {
            var scalarValue = await _command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
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
        /// <param name="command">要执行的数据库命令。</param>
        /// <param name="autoDisposeCommand">是否在释放时自动销毁命令对象，默认为 true。</param>
        public NonQueryResult(DbCommandProxy command, bool autoDisposeCommand = true)
            : base(command, autoDisposeCommand)
        { }

        /// <summary>
        /// 执行命令并返回受影响的行数。
        /// </summary>
        /// <returns>受影响的行数。</returns>
        public override int GetResult()
        {
            return _command.ExecuteNonQuery();
        }

        /// <summary>
        /// 异步执行命令并返回受影响的行数。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，包含受影响的行数。</returns>
        public override async Task<int> GetResultAsync(CancellationToken cancellationToken = default)
        {
            return await _command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
        /// <param name="command">要执行的数据库命令。</param>
        /// <param name="readRow">将 <see cref="IDataReader"/> 的一行数据转换为 <see cref="DataRow"/> 的委托。</param>
        /// <param name="autoDisposeCommand">是否在释放时自动销毁命令对象，默认为 true。</param>
        public DataTableResult(DbCommandProxy command, Func<IDataReader, DataTable, DataRow> readRow, bool autoDisposeCommand = true)
            : base(command, autoDisposeCommand)
        {
            _readRow = readRow;
            _dataTable = null;
            _hasLoaded = false;
        }

        /// <summary>
        /// 获取DataTable结果
        /// </summary>
        /// <returns>DataTable结果</returns>
        public override DataTable GetResult()
        {
            if (!_hasLoaded)
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
            if (!_hasLoaded)
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
            using (var reader = _command.ExecuteReader())
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
                _hasLoaded = true;
            }
        }

        /// <summary>
        /// 异步加载数据到DataTable
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task LoadDataAsync(CancellationToken cancellationToken = default)
        {
            _dataTable = new DataTable();
            using (var reader = await _command.ExecuteReaderAsync(cancellationToken))
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
                _hasLoaded = true;
            }
        }
    }
}