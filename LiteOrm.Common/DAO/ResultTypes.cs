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
        protected readonly DbCommand _command;
        protected readonly bool _autoDisposeCommand;

        protected CommandResult(DbCommand command, bool autoDisposeCommand = true)
        {
            _command = command;
            _autoDisposeCommand = autoDisposeCommand;
        }

        public abstract T GetResult();
        public abstract Task<T> GetResultAsync(CancellationToken cancellationToken = default);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

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
        private readonly Func<IDataReader, TResult> _readerFunc;

        public EnumerableResult(DbCommand command, Func<IDataReader, TResult> readerFunc, bool autoDisposeCommand = true)
            : base(command, autoDisposeCommand)
        {
            _readerFunc = readerFunc;
        }

        public IEnumerator<TResult> GetEnumerator()
        {
            using (IDataReader reader = _command.ExecuteReader())
            {
                while (reader.Read())
                {
                    yield return _readerFunc(reader);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new AsyncEnumerator(_command, _readerFunc, cancellationToken);
        }

        public TResult FirstOrDefault()
        {
            using DbDataReader reader = _command.ExecuteReader();
            return reader.Read() ? _readerFunc(reader) : default;
        }

        public async ValueTask<TResult> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
        {
            using DbDataReader reader = await _command.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? _readerFunc(reader) : default;
        }

        public List<TResult> ToList()
        {
            return new List<TResult>(this);
        }

        public async Task<List<TResult>> ToListAsync(CancellationToken cancellationToken = default)
        {
            var list = new List<TResult>();
            await foreach (var item in this.WithCancellation(cancellationToken))
            {
                list.Add(item);
            }
            return list;
        }

        public override List<TResult> GetResult()
        {
            return this.ToList();
        }

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
            private readonly DbCommand _command;
            private readonly Func<IDataReader, TResult> _readerFunc;
            private readonly CancellationToken _cancellationToken;
            private DbDataReader _reader;
            private TResult _current;

            public AsyncEnumerator(DbCommand command, Func<IDataReader, TResult> readerFunc, CancellationToken cancellationToken)
            {
                _command = command;
                _readerFunc = readerFunc;
                _cancellationToken = cancellationToken;
            }

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
                }

                if (await _reader.ReadAsync(_cancellationToken).ConfigureAwait(false))
                {
                    _current = _readerFunc(_reader);
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

        public ValueResult(DbCommand command, Func<object, TResult> resultConverter = null, bool autoDisposeCommand = true)
            : base(command, autoDisposeCommand)
        {
            _resultConverter = resultConverter ?? ((obj) => {
                if (obj != null && obj.GetType() != typeof(TResult))
                {
                    return (TResult)Convert.ChangeType(obj, typeof(TResult));
                }
                return (TResult)obj;
            });
        }

        public override TResult GetResult()
        {
            var scalarValue = _command.ExecuteScalar();
            return _resultConverter(scalarValue);
        }

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
        public NonQueryResult(DbCommand command, bool autoDisposeCommand = true)
            : base(command, autoDisposeCommand)
        {}

        public override int GetResult()
        {
            return _command.ExecuteNonQuery();
        }

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

        public DataTableResult(DbCommand command, Func<IDataReader, DataTable, DataRow> readRow, bool autoDisposeCommand = true)
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