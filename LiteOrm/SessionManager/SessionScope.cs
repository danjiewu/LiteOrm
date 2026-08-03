using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// 手动会话作用域。创建时将指定会话设置为当前异步上下文的 <see cref="SessionManager.Current"/>，
    /// Dispose 时恢复之前的当前会话（若存在）。
    /// </summary>
    /// <remarks>
    /// LiteOrm 核心不依赖 DI 容器。手动构造 <see cref="SessionManager"/> 后，通过
    /// <see cref="SessionManagerExtensions.BeginScope"/> 或直接 new 本类型进入会话作用域，
    /// 使 DAO/Service 通过 <see cref="SessionManager.Current"/> 获取会话。生命周期与
    /// LiteOrm.DependencyInjection 中 DI 容器的内置 scope 周期保持一致（进入时设置，退出时恢复）。
    /// </remarks>
    public sealed class SessionScope : IDisposable
    {
        private readonly SessionManager _session;
        private readonly SessionManager? _previous;

        /// <summary>
        /// 初始化 <see cref="SessionScope"/> 类的新实例。
        /// </summary>
        /// <param name="sessionManager">要激活的会话实例。</param>
        public SessionScope(SessionManager sessionManager)
        {
            if (sessionManager is null) throw new ArgumentNullException(nameof(sessionManager));
            _session = sessionManager;
            _previous = SessionManager.Current;
            SessionManager.SetCurrent(() => sessionManager);
        }

        /// <summary>
        /// 当前作用域激活的会话实例。
        /// </summary>
        public SessionManager Session => _session;

        /// <summary>
        /// 恢复进入作用域之前的当前会话。
        /// </summary>
        public void Dispose()
        {
            if (_previous is null)
                SessionManager.SetCurrent(null);
            else
                SessionManager.SetCurrent(() => _previous);
        }
    }
}
