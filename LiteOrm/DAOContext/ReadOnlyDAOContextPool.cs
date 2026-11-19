using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace LiteOrm
{
    /// <summary>
    /// 只读连接池。维护权重、连续失败计数与冷却状态。
    /// </summary>
    public sealed class ReadOnlyDAOContextPool : DAOContextPool
    {
        private int _fails;
        private long _blockedUntil;

        internal ReadOnlyDAOContextPool(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type providerType,
            string? connectionString)
            : base(providerType, connectionString)
        {
            IsReadOnlyPool = true;
        }

        /// <summary>
        /// 连续失败上限，0 表示不启用。
        /// </summary>
        public int FailureLimit { get; set; } = 3;

        /// <summary>
        /// 冷却时长。
        /// </summary>
        public TimeSpan Cooldown { get; set; } = TimeSpan.FromSeconds(30);

        internal bool Blocked => Interlocked.Read(ref _blockedUntil) > DateTime.UtcNow.Ticks;

        internal void MarkFailed()
        {
            if (FailureLimit <= 0) return;
            if (Interlocked.Increment(ref _fails) >= FailureLimit)
                Interlocked.Exchange(ref _blockedUntil, DateTime.UtcNow.Add(Cooldown).Ticks);
        }

        internal void ClearFailures()
        {
            Interlocked.Exchange(ref _fails, 0);
            Interlocked.Exchange(ref _blockedUntil, 0);
        }
    }
}
