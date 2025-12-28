using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm
{
    public class DAOContextPool : IDisposable
    {
        private readonly Queue<DAOContext> _pool = new Queue<DAOContext>();
        private readonly object _poolLock = new object();
        private bool _disposed = false;

        public int PoolSize { get; set; } = 20;
        public Type ProviderType { get; }
        public string ConnectionString { get; }
        public TimeSpan KeepAliveDuration { get; set; } = TimeSpan.FromMinutes(30);
        public string Name { get; set; }

        public DAOContextPool(Type providerType, string connectionString)
        {
            ProviderType = providerType ?? throw new ArgumentNullException(nameof(providerType));
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            Name = providerType.Name;
        }

        public DAOContext PeekContext()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DAOContextPool));

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

                    // 无效则销毁
                    context.Dispose();
                }

                // 池为空，创建新连接
                return CreateNewContext();
            }
        }

        public void ReturnContext(DAOContext context)
        {
            if (context == null)
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

                // 如果池已满，销毁多余连接
                if (_pool.Count >= PoolSize)
                {
                    _pool.Dequeue()?.Dispose();
                    return;
                }

                _pool.Enqueue(context);
            }
        }

        private bool IsContextValid(DAOContext context)
        {
            if (context == null)
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

        private DAOContext CreateNewContext()
        {
            var connection = Activator.CreateInstance(ProviderType) as DbConnection;
            if (connection == null)
                throw new InvalidOperationException($"无法创建类型为 {ProviderType} 的数据库连接");

            connection.ConnectionString = ConnectionString;

            var context = new DAOContext(connection, this);

            return context;
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                lock (_poolLock)
                {
                    while (_pool.Count > 0)
                    {
                        _pool.Dequeue()?.Dispose();
                    }

                    _disposed = true;
                }
            }
        }
    }
}
