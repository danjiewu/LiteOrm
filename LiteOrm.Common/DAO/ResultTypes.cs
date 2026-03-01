using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
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
    public class EnumerableResult<TResult> : CommandResult<TResult>, IEnumerable<TResult>, IAsyncEnumerable<TResult>
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
            using (IDataReader reader = _command.ExecuteReader())
            {
                return reader.Read() ? _readerFunc(reader) : default(TResult);
            }
        }

        public async ValueTask<TResult> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
        {
            using (DbDataReader reader = await _command.ExecuteReaderAsync(CommandBehavior.Default, cancellationToken).ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? _readerFunc(reader) : default(TResult);
            }
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
            _resultConverter = resultConverter ?? ((obj) => (TResult)obj);
        }

        public TResult GetValue()
        {
            var scalarValue = _command.ExecuteScalar();
            return _resultConverter(scalarValue);
        }

        public async Task<TResult> GetValueAsync(CancellationToken cancellationToken = default)
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

        public int Execute()
        {
            return _command.ExecuteNonQuery();
        }

        public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return await _command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}