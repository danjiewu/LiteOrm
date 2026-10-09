using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace LiteOrm.Tests.Infrastructure
{
    /// <summary>
    /// 覆盖数据库宕机场景下 <see cref="DAOContextPool"/> 的信号量许可归还行为。
    /// <para>
    /// 回归点：连接创建/打开失败时，若失败路径上的 <see cref="DAOContext"/> 未被 Dispose，
    /// 每失败一次就永久泄漏一个信号量许可，累积到 <see cref="DAOContextPool.MaxPoolSize"/> 后
    /// 即使数据库恢复也无法再建连（抛出 "Maximum connection limit reached"）。
    /// </para>
    /// </summary>
    public class DAOContextPoolOutageTests
    {
        private const string ConnectionString = "Data Source=Faultable";

        private static DAOContextPool CreatePool(int maxPoolSize)
        {
            var pool = new DAOContextPool(typeof(FaultableDbConnection), ConnectionString)
            {
                MaxPoolSize = maxPoolSize
            };
            return pool;
        }

        /// <summary>
        /// 同步取连接：数据库宕机期间反复失败不得泄漏许可；恢复后必须能立即建连。
        /// </summary>
        [Fact]
        public void PeekContext_DatabaseDown_DoesNotLeakPermits_AndRecovers()
        {
            const int maxPoolSize = 3;
            const int attempts = 10; // 远超 MaxPoolSize，若泄漏则第 4 次起会抛连接上限异常

            FaultableDbConnection.Reset();
            FaultableDbConnection.DatabaseAvailable = false;
            using var pool = CreatePool(maxPoolSize);

            try
            {
                for (int i = 0; i < attempts; i++)
                {
                    var ex = Assert.Throws<InvalidOperationException>(() => pool.PeekContext());
                    Assert.DoesNotContain("Maximum connection limit", ex.Message);
                    Assert.Contains("Simulated database outage", ex.Message);
                }

                Assert.Equal(attempts, FaultableDbConnection.OpenAttempts);

                // 数据库恢复后应能正常取到连接
                FaultableDbConnection.DatabaseAvailable = true;
                var context = pool.PeekContext();
                Assert.NotNull(context);
                Assert.Equal(ConnectionState.Open, context.DbConnection.State);
                pool.ReturnContext(context);
            }
            finally
            {
                FaultableDbConnection.Reset();
            }
        }

        /// <summary>
        /// 异步取连接：数据库宕机期间反复失败不得泄漏许可；恢复后必须能立即建连。
        /// </summary>
        [Fact]
        public async Task PeekContextAsync_DatabaseDown_DoesNotLeakPermits_AndRecovers()
        {
            const int maxPoolSize = 3;
            const int attempts = 10;

            FaultableDbConnection.Reset();
            FaultableDbConnection.DatabaseAvailable = false;
            using var pool = CreatePool(maxPoolSize);

            try
            {
                for (int i = 0; i < attempts; i++)
                {
                    var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                        async () => await pool.PeekContextAsync());
                    Assert.DoesNotContain("Maximum connection limit", ex.Message);
                    Assert.Contains("Simulated database outage", ex.Message);
                }

                Assert.Equal(attempts, FaultableDbConnection.OpenAttempts);

                FaultableDbConnection.DatabaseAvailable = true;
                var context = await pool.PeekContextAsync();
                Assert.NotNull(context);
                Assert.Equal(ConnectionState.Open, context.DbConnection.State);
                pool.ReturnContext(context);
            }
            finally
            {
                FaultableDbConnection.Reset();
            }
        }

        /// <summary>
        /// 池中已存在但物理连接已关闭的连接，在数据库宕机时重新打开失败，
        /// 该上下文应被销毁归还许可，且不得影响后续恢复。
        /// </summary>
        [Fact]
        public void PeekContext_PooledConnectionReopenFails_DisposesAndRecovers()
        {
            const int maxPoolSize = 2;

            FaultableDbConnection.Reset();
            using var pool = CreatePool(maxPoolSize);

            try
            {
                // 占满全部许可并取得两个独立上下文，关闭其物理连接后归还入池
                var first = pool.PeekContext();
                var second = pool.PeekContext();
                first.DbConnection.Close();
                second.DbConnection.Close();
                pool.ReturnContext(first);
                pool.ReturnContext(second);

                FaultableDbConnection.DatabaseAvailable = false;

                // 池内连接重开失败与新建连接失败均须归还许可；次数超过 MaxPoolSize 仍不得触发连接上限
                for (int i = 0; i < maxPoolSize + 2; i++)
                {
                    var ex = Assert.Throws<InvalidOperationException>(() => pool.PeekContext());
                    Assert.DoesNotContain("Maximum connection limit", ex.Message);
                    Assert.Contains("Simulated database outage", ex.Message);
                }

                // 恢复后仍可建连（许可未被泄漏）
                FaultableDbConnection.DatabaseAvailable = true;
                var context = pool.PeekContext();
                Assert.NotNull(context);
                Assert.Equal(ConnectionState.Open, context.DbConnection.State);
                pool.ReturnContext(context);
            }
            finally
            {
                FaultableDbConnection.Reset();
            }
        }

        /// <summary>
        /// 可控开关的假连接：<see cref="DatabaseAvailable"/> 为 false 时打开连接抛异常，
        /// 用于模拟数据库宕机。
        /// </summary>
        internal sealed class FaultableDbConnection : DbConnection
        {
            private static volatile bool _databaseAvailable = true;
            private static int _openAttempts;

            public static bool DatabaseAvailable
            {
                get => _databaseAvailable;
                set => _databaseAvailable = value;
            }

            public static int OpenAttempts => Volatile.Read(ref _openAttempts);

            public static void Reset()
            {
                _databaseAvailable = true;
                Volatile.Write(ref _openAttempts, 0);
            }

            private ConnectionState _state = ConnectionState.Closed;
            private string _connectionString = string.Empty;

            [System.Diagnostics.CodeAnalysis.AllowNull]
            public override string ConnectionString
            {
                get => _connectionString;
                set => _connectionString = value ?? string.Empty;
            }

            public override string Database => "Faultable";
            public override string DataSource => "Faultable";
            public override string ServerVersion => "1.0";
            public override ConnectionState State => _state;

            public override void Open()
            {
                Interlocked.Increment(ref _openAttempts);
                if (!_databaseAvailable)
                    throw new InvalidOperationException("Simulated database outage: connection cannot be opened.");
                _state = ConnectionState.Open;
            }

            public override Task OpenAsync(CancellationToken cancellationToken)
            {
                try
                {
                    Open();
                    return Task.CompletedTask;
                }
                catch (Exception ex)
                {
                    return Task.FromException(ex);
                }
            }

            public override void Close() => _state = ConnectionState.Closed;

            public override void ChangeDatabase(string databaseName)
            {
            }

            protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
                => throw new NotSupportedException();

            protected override DbCommand CreateDbCommand()
                => throw new NotSupportedException();
        }
    }
}