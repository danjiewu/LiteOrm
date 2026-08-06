namespace LiteOrm.Common
{
    /// <summary>
    /// 服务生命周期。
    /// <para>与 <c>Microsoft.Extensions.DependencyInjection.ServiceLifetime</c> 数值一致，
    /// 便于与各 DI 容器映射，同时避免对 MS DI 包的依赖。</para>
    /// </summary>
    public enum Lifetime
    {
        /// <summary>
        /// 单例生命周期
        /// </summary>
        Singleton = 0,

        /// <summary>
        /// 作用域生命周期
        /// </summary>
        Scoped = 1,

        /// <summary>
        /// 瞬态生命周期
        /// </summary>
        Transient = 2
    }
}
