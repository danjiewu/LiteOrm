using Autofac;
using Autofac.Builder;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Autofac.Extras.DynamicProxy;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm服务提供者扩展方法集合
    /// </summary>
    /// <remarks>
    /// MyServiceProviderExt 提供了用于集成 LiteOrm 框架到依赖注入容器的扩展方法。
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
            return hostBuilder.UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureContainer<ContainerBuilder>((builder, containerBuilder) =>
                {
                    containerBuilder.RegisterAutoService()
                    .RegisterBuildCallback(c =>
                    {
                        // 注册后置回调
                        foreach (var initializer in c.Resolve<IEnumerable<IComponentInitializer>>())
                        initializer.Initialize(c);
                    });
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
            // 若未指定程序集，扫描当前应用域已加载的所有程序集（排除系统程序集）
            var targetAssemblies = assemblies.Any()
                ? assemblies
                : AssemblyAnalyzer.GetAllReferencedAssemblies();

            foreach (var assembly in targetAssemblies)
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
    }

    /// <summary>
    /// 程序集分析器
    /// </summary>
    public static class AssemblyAnalyzer
    {
        /// <summary>
        /// 获取所有直接引用的程序集名称（包括未加载的）
        /// </summary>
        /// <param name="entryAssembly">入口程序集，如果为null则使用当前入口程序集或执行程序集</param>
        /// <returns>所有引用的程序集集合</returns>
        public static IEnumerable<Assembly> GetAllReferencedAssemblies(Assembly entryAssembly = null)
        {
            entryAssembly ??= Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var result = new List<AssemblyName>();
            result.Add(entryAssembly.GetName());
            result.AddRange(entryAssembly.GetReferencedAssemblies());
            return result.GroupBy(an => an.FullName).Select(g => g.First()).Where(a => !a.FullName.StartsWith("System.") && !a.FullName.StartsWith("Microsoft."))
                    .Select(Assembly.Load)
                    .Where(a => !a.IsDynamic);
        }
    }
}
