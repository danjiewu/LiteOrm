namespace LiteOrm.Common
{
    /// <summary>
    /// 自动注册的服务类型范围。
    /// <para>控制带 <c>[AutoRegister]</c> 特性的类型在依赖注入容器中注册哪些服务类型。</para>
    /// </summary>
    public enum RegisterPolicy
    {
        /// <summary>
        /// 注册实现类型自身及其所有符合条件的接口（默认）。
        /// </summary>
        All = 0,

        /// <summary>
        /// 仅注册实现类型自身。
        /// </summary>
        Self = 1,

        /// <summary>
        /// 仅注册实现类型实现的接口（排除 System 及 LiteOrm 标记接口）。
        /// </summary>
        Interface = 2
    }
}