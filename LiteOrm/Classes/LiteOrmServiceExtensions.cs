using Autofac;
using Autofac.Builder;
using Autofac.Extensions.DependencyInjection;
using Autofac.Extras.DynamicProxy;
using Castle.DynamicProxy;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm服务提供者扩展方法集合
    /// </summary>
    /// <remarks>
    /// LiteOrmServiceProviderExtensions 提供了用于集成 LiteOrm 框架到依赖注入容器的扩展方法。
    /// 它简化了 LiteOrm 框架与 ASP.NET Core 宿主的集成过程。
    /// 
    /// 主要功能包括：
    /// 1. 框架初始化 - 在宿主构建时初始化 LiteOrm 框架
    /// 2. Autofac集成 - 将 Autofac 集成到依赖注入系统
    /// 3. 服务注册 - 注册所有LiteOrm相关的服务
    /// 
    /// 使用示例：
    /// <code>
    /// var builder = Host.CreateDefaultBuilder(args)
    ///     .RegisterLiteOrm()
    ///     .ConfigureServices(services =>
    ///         ...
    ///     );
    /// </code>
    /// </remarks>
    public static class LiteOrmServiceProviderExtensions
    {
        /// <summary>
        /// 注册LiteOrm框架到主机构建器
        /// </summary>
        /// <param name="hostBuilder">主机构建器</param>
        /// <returns>配置后的主机构建器</returns>
        public static IHostBuilder RegisterLiteOrm(this IHostBuilder hostBuilder)
        {
            var callingAssembly = Assembly.GetCallingAssembly();
            return hostBuilder.UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureContainer<ContainerBuilder>((builder, containerBuilder) =>
                {
                    containerBuilder.RegisterAutoService(callingAssembly);
                });
        }

        /// <summary>
        /// 扫描指定程序集，自动注册标记[AutoRegister]的类型
        /// </summary>
        /// <param name="builder">服务集合</param>
        /// <param name="assemblies">目标程序集（为空则扫描当前域所有程序集）</param>
        /// <returns>服务集合</returns>
        public static ContainerBuilder RegisterAutoService(
            this ContainerBuilder builder,
            params Assembly[] assemblies)
        {
            var assemblyList = new HashSet<Assembly>();

            // 自动加上 LiteOrm 和 LiteOrm.Common 的 Assembly
            assemblyList.Add(typeof(LiteOrmServiceProviderExtensions).Assembly);
            assemblyList.Add(typeof(AutoRegisterAttribute).Assembly);

            // 若指定了程序集，则加入指定列表；否则扫描引用程序集
            if (assemblies.Any())
            {
                foreach (var assembly in assemblies)
                {
                    assemblyList.Add(assembly);
                }
            }
            else
            {
                foreach (var assembly in AssemblyAnalyzer.GetAllReferencedAssemblies())
                {
                    assemblyList.Add(assembly);
                }
            }

            foreach (var assembly in assemblyList)
            {
                assembly.GetTypes()
                     .Where(t => !t.IsAbstract && !t.IsInterface &&
                                (t.GetCustomAttribute<AutoRegisterAttribute>(true)?.Enabled ?? false))
                     .ToList()
                     .ForEach(t => RegisterTypeWithInterception(builder, t));
            }
            return builder;
        }

        /// <summary>
        /// 注册类型并应用拦截配置
        /// </summary>
        /// <param name="builder">容器构建器</param>
        /// <param name="implementationType">要注册的实现类型</param>
        /// <returns>配置后的容器构建器</returns>
        public static ContainerBuilder RegisterTypeWithInterception(this ContainerBuilder builder, Type implementationType)
        {
            if (implementationType.IsGenericTypeDefinition)
                builder.RegisterGeneric(implementationType).AddInterception(implementationType);
            else
                builder.RegisterType(implementationType).AddInterception(implementationType);
            return builder;
        }

        /// <summary>
        /// 为类型注册添加拦截配置
        /// </summary>
        /// <typeparam name="TLimit">限制类型</typeparam>
        /// <typeparam name="TActivatorData">激活器数据类型</typeparam>
        /// <typeparam name="TRegistrationStyle">注册风格类型</typeparam>
        /// <param name="registration">注册构建器</param>
        /// <param name="implementationType">实现类型</param>
        /// <returns>配置后的注册构建器</returns>
        public static IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> AddInterception<TLimit, TActivatorData, TRegistrationStyle>(
           this IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> registration,
           Type implementationType) where TActivatorData : ReflectionActivatorData
        {
            var attribute = implementationType.GetCustomAttribute<AutoRegisterAttribute>(true);
            ServiceLifetime lifetime = attribute?.Lifetime ?? ServiceLifetime.Scoped;
            List<Type> serviceTypes = new List<Type>();

            // 若特性指定了ServiceTypes，直接使用
            if (attribute?.ServiceTypes is not null && attribute.ServiceTypes.Any())
            {
                serviceTypes.AddRange(attribute.ServiceTypes);
            }
            // 否则自动获取所有实现的接口（排除系统接口如IDisposable）
            else
            {
                foreach (var serviceType in implementationType.GetInterfaces()
                    .Where(i => !i.Namespace.StartsWith("System.")
                             && (i.GetCustomAttribute<AutoRegisterAttribute>(true)?.Enabled ?? true)))
                {
                    if (implementationType.IsGenericTypeDefinition && serviceType.IsGenericType)
                    {
                        if (implementationType.GetGenericArguments().Length == serviceType.GenericTypeArguments.Length && serviceType.GenericTypeArguments.All(t => t.DeclaringType == implementationType))
                        {
                            serviceTypes.Add(serviceType.GetGenericTypeDefinition());
                        }
                    }
                    else
                    {
                        serviceTypes.Add(serviceType);
                    }
                }
            }

            var interceptAttribute = implementationType.GetCustomAttribute<InterceptAttribute>() ??
                                   implementationType.GetInterfaces()
                                       .Select(i => i.GetCustomAttribute<InterceptAttribute>())
                                       .FirstOrDefault(a => a is not null);
            if (interceptAttribute is not null)
            {
                registration.EnableInterfaceInterceptors();
            }
            else
            {
                serviceTypes.Add(implementationType);
            }

            if (attribute?.Key != null)
            {
                foreach (var serviceType in serviceTypes)
                {
                    registration.Keyed(attribute.Key, serviceType);
                }
            }
            else
            {
                registration.As(serviceTypes.ToArray());
            }

            registration.PropertiesAutowired()
            .SetLifetime(lifetime);
            return registration;
        }

        /// <summary>
        /// 设置服务的生命周期
        /// </summary>
        /// <typeparam name="TLimit">限制类型</typeparam>
        /// <typeparam name="TActivatorData">激活器数据类型</typeparam>
        /// <typeparam name="TRegistrationStyle">注册风格类型</typeparam>
        /// <param name="registration">注册构建器</param>
        /// <param name="lifetime">服务生命周期</param>
        /// <returns>配置后的注册构建器</returns>
        public static IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> SetLifetime<TLimit, TActivatorData, TRegistrationStyle>(
           this IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> registration,
           ServiceLifetime lifetime)
        {
            return lifetime switch
            {
                ServiceLifetime.Singleton => registration.SingleInstance(),
                ServiceLifetime.Scoped => registration.InstancePerLifetimeScope(),
                ServiceLifetime.Transient => registration.InstancePerDependency(),
                _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null)
            };
        }

        /// <summary>
        /// 注册服务生成器，将接口获取服务通过动态代理转换为从 ServiceProvider 获取服务
        /// </summary>
        /// <typeparam name="TService">获取服务的接口，提供返回服务的属性或方法</typeparam>
        /// <param name="services">服务集合。</param>
        /// <param name="lifetime">服务生命周期。</param>
        /// <returns>返回修改后的服务集合以支持链式调用。</returns>
        public static IServiceCollection AddServiceGenerator<TService>(
            this IServiceCollection services,
            ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TService : class
        {
            var serviceDescriptor = new ServiceDescriptor(typeof(TService),
                sp => new ProxyGenerator().CreateInterfaceProxyWithoutTarget<TService>(sp.GetRequiredService<ServiceGenerateInterceptor>()),
                lifetime);
            services.Add(serviceDescriptor);
            return services;
        }
    }
}
