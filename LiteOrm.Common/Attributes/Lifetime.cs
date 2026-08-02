using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 服务生命周期枚举
    /// </summary>
    public enum Lifetime
    {
        /// <summary>
        /// 单例模式，整个应用程序生命周期内只有一个实例
        /// </summary>
        Singleton,
        /// <summary>
        /// 作用域模式，每个作用域创建一个实例
        /// </summary>
        Scoped,
        /// <summary>
        /// 瞬态模式，每次请求都创建新实例
        /// </summary>
        Transient
    }
}