using Autofac;
using Microsoft.Extensions.DependencyInjection;


namespace LiteOrm.Common
{
    /// <summary>
    /// 定义在给定上下文中初始化组件的约定。
    /// </summary>
    /// <remarks>此接口的实现负责执行组件正确运行所需的任何设置或配置。初始化过程可能取决于提供的上下文，并且应在组件使用之前完成。</remarks>
    public interface IComponentInitializer
    {
        /// <summary>
        /// 使用指定的组件上下文初始化实例。
        /// </summary>
        /// <param name="componentContext">提供对初始化所需的注册组件和服务访问权限的上下文。不能为 null。</param>
        void Initialize(IComponentContext componentContext);
    }
}
