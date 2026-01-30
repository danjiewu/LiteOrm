using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// 提供一个带锁保护的 <see cref="IDataReader"/> 包装类。
    /// </summary>
    /// <remarks>
    /// 该类用于在执行查询并返回读取器时，保持对 <see cref="DAOContext"/> 的锁定，
    /// 直到数据读取完毕并释放此包装器。这确保了在延迟读取期间，底层连接不会被其他线程占用。
    /// </remarks>
    public class AutoLockDataReader : DbDataReader, IAsyncDisposable
    {
        /// <summary>
        /// 内部包装的原始数据读取器。
        /// </summary>
        private readonly DbDataReader _innerReader;

        /// <summary>
        /// 锁定的作用域对象，释放此对象将释放底层上下文的信号量。
        /// </summary>
        private readonly IDisposable _scope;

        /// <summary>
        /// 指示对象是否已被释放。
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 初始化 <see cref="AutoLockDataReader"/> 类的新实例。
        /// </summary>
        /// <param name="innerReader">内部数据读取器实例。</param>
        /// <param name="scope">需要管理的锁定作用域。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="innerReader"/> 或 <paramref name="scope"/> 为 null 时抛出。</exception>
        public AutoLockDataReader(DbDataReader innerReader, IDisposable scope)
        {
            _innerReader = innerReader ?? throw new ArgumentNullException(nameof(innerReader));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        #region 锁管理
        /// <summary>
        /// 确保当前对象未被释放。
        /// </summary>
        /// <exception cref="ObjectDisposedException">当对象已释放时抛出。</exception>
        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AutoLockDataReader));
        }
        #endregion

        #region IDataReader 实现 - 转发到内部 Reader
        /// <summary>
        /// 获取指定列的列值。
        /// </summary>
        /// <param name="i">列的从零开始的索引。</param>
        public override object this[int i]
        {
            get
            {
                EnsureNotDisposed();
                return _innerReader[i];
            }
        }

        /// <summary>
        /// 获取具有指定名称的列的列值。
        /// </summary>
        /// <param name="name">列名。</param>
        public override object this[string name]
        {
            get
            {
                EnsureNotDisposed();
                return _innerReader[name];
            }
        }

        /// <summary>
        /// 获取一个值，该值指示当前行的嵌套深度。
        /// </summary>
        public override int Depth
        {
            get
            {
                EnsureNotDisposed();
                return _innerReader.Depth;
            }
        }

        /// <summary>
        /// 获取一个值，该值指示数据读取器是否已关闭。
        /// </summary>
        public override bool IsClosed
        {
            get
            {
                // 即使 disposed 也返回内部状态
                return _disposed || _innerReader.IsClosed;
            }
        }

        /// <summary>
        /// 获取通过执行 SQL 语句而更改、插入或删除的行数。
        /// </summary>
        public override int RecordsAffected
        {
            get
            {
                EnsureNotDisposed();
                return _innerReader.RecordsAffected;
            }
        }

        /// <summary>
        /// 获取当前行中的列数。
        /// </summary>
        public override int FieldCount
        {
            get
            {
                EnsureNotDisposed();
                return _innerReader.FieldCount;
            }
        }

        /// <summary>
        /// 获取一个值，该值指示 <see cref="DbDataReader"/> 是否包含一行或多行。
        /// </summary>
        public override bool HasRows
        {
            get
            {
                EnsureNotDisposed();
                return _innerReader.HasRows;
            }
        }

        /// <summary>
        /// 关闭 <see cref="IDataReader"/> 对象。
        /// </summary>
        public override void Close()
        {
            EnsureNotDisposed();
            _innerReader.Close();
        }

        /// <summary>
        /// 将读取器推进到结果集的下一条记录。
        /// </summary>
        /// <returns>如果还有更多行，则为 true；否则为 false。</returns>
        public override bool Read()
        {
            EnsureNotDisposed();
            return _innerReader.Read();
        }

        /// <summary>
        /// 异步将读取器推进到结果集的下一条记录。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，其结果为如果还有更多行则为 true，否则为 false。</returns>
        public override Task<bool> ReadAsync(CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _innerReader.ReadAsync(cancellationToken);
        }

        /// <summary>
        /// 在读取批处理 SQL 语句的结果时，使数据读取器前进到下一个结果。
        /// </summary>
        /// <returns>如果还有更多结果集，则为 true；否则为 false。</returns>
        public override bool NextResult()
        {
            EnsureNotDisposed();
            return _innerReader.NextResult();
        }

        /// <summary>
        /// 异步在读取批处理 SQL 语句的结果时，使数据读取器前进到下一个结果。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，其结果为如果还有更多结果集则为 true，否则为 false。</returns>
        public override Task<bool> NextResultAsync(CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _innerReader.NextResultAsync(cancellationToken);
        }

        /// <summary>
        /// 获取指定列的布尔值。
        /// </summary>
        public override bool GetBoolean(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetBoolean(i);
        }

        /// <summary>
        /// 获取指定列的 8 位无符号整数值。
        /// </summary>
        public override byte GetByte(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetByte(i);
        }

        /// <summary>
        /// 从指定列偏移量开始，将字节流从指定的列索引读入作为偏移量开始的缓冲区。
        /// </summary>
        public override long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            EnsureNotDisposed();
            return _innerReader.GetBytes(i, fieldOffset, buffer, bufferoffset, length);
        }

        /// <summary>
        /// 获取指定列的字符值。
        /// </summary>
        public override char GetChar(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetChar(i);
        }

        /// <summary>
        /// 从指定列偏移量开始，将字符流从指定的列索引读入作为偏移量开始的缓冲区。
        /// </summary>
        public override long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            EnsureNotDisposed();
            return _innerReader.GetChars(i, fieldoffset, buffer, bufferoffset, length);
        }

        /// <summary>
        /// 返回指定列序号的 <see cref="IDataReader"/>。
        /// </summary>
        protected override DbDataReader GetDbDataReader(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetData(i) as DbDataReader;
        }

        /// <summary>
        /// 获取指定列的数据类型名称。
        /// </summary>
        public override string GetDataTypeName(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetDataTypeName(i);
        }

        /// <summary>
        /// 获取指定列的日期和时间数据值。
        /// </summary>
        public override DateTime GetDateTime(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetDateTime(i);
        }

        /// <summary>
        /// 获取指定列的固定精度数值。
        /// </summary>
        public override decimal GetDecimal(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetDecimal(i);
        }

        /// <summary>
        /// 获取指定列的双精度浮点数。
        /// </summary>
        public override double GetDouble(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetDouble(i);
        }

        /// <summary>
        /// 获取作为指定列类型的 <see cref="Type"/>。
        /// </summary>
        public override Type GetFieldType(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetFieldType(i);
        }

        /// <summary>
        /// 获取指定列的单精度浮点数。
        /// </summary>
        public override float GetFloat(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetFloat(i);
        }

        /// <summary>
        /// 获取指定列的全局唯一标识符 (GUID) 值。
        /// </summary>
        public override Guid GetGuid(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetGuid(i);
        }

        /// <summary>
        /// 获取指定列的 16 位有符号整数值。
        /// </summary>
        public override short GetInt16(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetInt16(i);
        }

        /// <summary>
        /// 获取指定列的 32 位有符号整数值。
        /// </summary>
        public override int GetInt32(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetInt32(i);
        }

        /// <summary>
        /// 获取指定列的 64 位有符号整数值。
        /// </summary>
        public override long GetInt64(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetInt64(i);
        }

        /// <summary>
        /// 获取指定列的名称。
        /// </summary>
        public override string GetName(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetName(i);
        }

        /// <summary>
        /// 返回指定列的索引。
        /// </summary>
        public override int GetOrdinal(string name)
        {
            EnsureNotDisposed();
            return _innerReader.GetOrdinal(name);
        }

        /// <summary>
        /// 返回一个 <see cref="DataTable"/>，它描述 <see cref="IDataReader"/> 的列元数据。
        /// </summary>
        public override DataTable GetSchemaTable()
        {
            EnsureNotDisposed();
            return _innerReader.GetSchemaTable();
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER || NET8_0_OR_GREATER || NET10_0_OR_GREATER
        /// <summary>
        /// 异步返回一个 <see cref="DataTable"/>，它描述 <see cref="IDataReader"/> 的列元数据。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，其结果为 DataTable。</returns>
        public override Task<DataTable> GetSchemaTableAsync(CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _innerReader.GetSchemaTableAsync(cancellationToken);
        }
#endif

        /// <summary>
        /// 获取指定列的字符串值。
        /// </summary>
        public override string GetString(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetString(i);
        }

        /// <summary>
        /// 获取指定列的值。
        /// </summary>
        public override object GetValue(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetValue(i);
        }

        /// <summary>
        /// 获取当前行所有列的值。
        /// </summary>
        public override int GetValues(object[] values)
        {
            EnsureNotDisposed();
            return _innerReader.GetValues(values);
        }

        /// <summary>
        /// 获取一个值，该值指示列是否包含空值。
        /// </summary>
        public override bool IsDBNull(int i)
        {
            EnsureNotDisposed();
            return _innerReader.IsDBNull(i);
        }

        /// <summary>
        /// 异步获取一个值，该值指示列是否包含空值。
        /// </summary>
        /// <param name="i">列的从零开始的索引。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，其结果为如果列包含空值则为 true，否则为 false。</returns>
        public override Task<bool> IsDBNullAsync(int i, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _innerReader.IsDBNullAsync(i, cancellationToken);
        }

        /// <summary>
        /// 以异步方式获取指定列的值。
        /// </summary>
        /// <typeparam name="T">要返回的对象类型。</typeparam>
        /// <param name="i">列的从零开始的索引。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，其结果为指定列的值。</returns>
        public override Task<T> GetFieldValueAsync<T>(int i, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            return _innerReader.GetFieldValueAsync<T>(i, cancellationToken);
        }

        /// <summary>
        /// 获取指定列的值。
        /// </summary>
        /// <typeparam name="T">要返回的对象类型。</typeparam>
        /// <param name="i">列的从零开始的索引。</param>
        /// <returns>指定列的值。</returns>
        public override T GetFieldValue<T>(int i)
        {
            EnsureNotDisposed();
            return _innerReader.GetFieldValue<T>(i);
        }

        /// <summary>
        /// 返回可用于循环访问结果集中的行的枚举数。
        /// </summary>
        /// <returns>可用于循环访问结果集中的行的枚举数。</returns>
        public override System.Collections.IEnumerator GetEnumerator()
        {
            EnsureNotDisposed();
            return _innerReader.GetEnumerator();
        }
        #endregion

        #region IDisposable 和 IAsyncDisposable 实现
        /// <summary>
        /// 释放由当前 <see cref="AutoLockDataReader"/> 占用的资源。
        /// </summary>
        public new void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放由当前 <see cref="AutoLockDataReader"/> 占用的托管资源和可选的非托管资源。
        /// </summary>
        /// <param name="disposing">如果为 true，则释放托管资源和非托管资源。</param>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        _innerReader.Dispose();
                    }
                    finally
                    {
                        _scope.Dispose();
                    }
                }
                base.Dispose(disposing);
                _disposed = true;
            }
        }


        /// <summary>
        /// 以异步方式释放由当前 <see cref="AutoLockDataReader"/> 占用的资源。
        /// </summary>
        /// <returns>表示异步操作的任务。</returns>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER || NET8_0_OR_GREATER || NET10_0_OR_GREATER
        public override async ValueTask DisposeAsync()
#else
        public async ValueTask DisposeAsync()
#endif
        {
            await DisposeAsyncCore().ConfigureAwait(false);
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER || NET8_0_OR_GREATER || NET10_0_OR_GREATER
            await base.DisposeAsync();
#else
            base.Dispose();
#endif
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 核心异步释放逻辑。
        /// </summary>
        protected virtual async ValueTask DisposeAsyncCore()
        {
            if (!_disposed)
            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER || NET8_0_OR_GREATER || NET10_0_OR_GREATER
                await _innerReader.DisposeAsync().ConfigureAwait(false);
#else
                if (_innerReader is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    _innerReader.Dispose();
                }
#endif

                _scope.Dispose();
                _disposed = true;
            }
        }
        #endregion
    }
}
