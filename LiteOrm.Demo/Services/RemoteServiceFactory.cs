namespace LiteOrm.Demo.Services
{
    /// <summary>
    /// 远程服务工厂接口。
    /// <para>
    /// 通过 <see cref="LiteOrm.LiteOrmRemoteExtensions.AddRemoteServiceGenerator{TService}"/>
    /// 注册为动态代理。访问属性时，<c>RemoteServiceGenerateInterceptor</c> 自动从 DI 容器
    /// 解析对应的远程服务代理（由 <c>RemoteServiceInvokeInterceptor</c> 转发调用到远程服务端）。
    /// </para>
    /// <para>
    /// 使用方式与本地 <see cref="ServiceFactory"/> 一致，调用方无需感知远程/本地差异：
    /// <code>
    /// var remoteFactory = scope.ServiceProvider.GetRequiredService&lt;RemoteServiceFactory&gt;();
    /// var users = await remoteFactory.UserService.GetByUserNameAsync("alice");
    /// </code>
    /// </para>
    /// </summary>
    public interface RemoteServiceFactory
    {
        /// <summary>远程用户服务。</summary>
        IUserService UserService { get; }

        /// <summary>远程销售服务。</summary>
        ISalesService SalesService { get; }

        /// <summary>远程综合业务服务。</summary>
        IBusinessService BusinessService { get; }

        /// <summary>远程部门服务。</summary>
        IDepartmentService DepartmentService { get; }
    }
}
