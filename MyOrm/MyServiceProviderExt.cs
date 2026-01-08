using Autofac;
using Autofac.Builder;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using Autofac.Extras.DynamicProxy;
using Autofac.Features.AttributeFilters;
using Castle.DynamicProxy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MyOrm.Common;
using MyOrm.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Module = Autofac.Module;

namespace MyOrm
{
    // 业务模块
    public class MyOrmModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInterceptor()
                .RegisterAutoService()
                .RegisterBuildCallback(c =>
                {
                    // 注册后置回调
                    SessionManager.Current = c.Resolve<SessionManager>();
                });
        }
    }

    public static class MyServiceProviderExt
    {
        public static IHostBuilder RegisterMyOrm(this IHostBuilder hostBuilder)
        {
            return hostBuilder.UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureContainer<ContainerBuilder>((builder, containerBuilder) =>
                {
                    // 1. 注册基础设施模块
                    containerBuilder.RegisterModule<MyOrmModule>();
                });
        }

        public static ContainerBuilder RegisterInterceptor(this ContainerBuilder builder)
        {
            // 自动发现并注册所有拦截器
            foreach (var assembly in AssemblyAnalyzer.GetAllReferencedAssemblies())
            {
                var interceptorTypes = assembly.GetTypes()
                    .Where(t => typeof(IInterceptor).IsAssignableFrom(t) &&
                               !t.IsAbstract && t.IsClass)
                    .ToList();

                foreach (var interceptorType in interceptorTypes)
                {
                    builder.RegisterType(interceptorType)
                           .AsSelf()
                           .SingleInstance();  // 拦截器通常是单例
                }
            }
            return builder;
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
                                (t.GetCustomAttribute<AutoRegisterAttribute>()?.Enabled ?? false))
                     .ToList()
                     .ForEach(t => RegisterTypeWithInterception(builder, t));
            }
            return builder;
        }

        public static ContainerBuilder RegisterTypeWithInterception(this ContainerBuilder builder, Type implementationType)
        {
            if (implementationType.IsGenericTypeDefinition)
                builder.RegisterGeneric(implementationType).AddInterception(implementationType);
            else
                builder.RegisterType(implementationType).AddInterception(implementationType);
            return builder;
        }

        public static IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> AddInterception<TLimit, TActivatorData, TRegistrationStyle>(
           this IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> registration,
           Type implementationType) where TActivatorData : ReflectionActivatorData
        {
            var attribute = implementationType.GetCustomAttribute<AutoRegisterAttribute>();
            ServiceLifetime lifetime = attribute?.Lifetime ?? ServiceLifetime.Scoped;
            List<Type> serviceTypes = new List<Type>();

            // 1. 若特性指定了ServiceTypes，直接使用
            if (attribute?.ServiceTypes != null && attribute.ServiceTypes.Any())
            {
                serviceTypes.AddRange(attribute.ServiceTypes);
            }
            // 2. 否则自动获取所有实现的接口（排除系统接口如IDisposable）
            else
            {
                foreach (var serviceType in implementationType.GetInterfaces()
                    .Where(i => !i.Namespace.StartsWith("System.")
                             && (i.GetCustomAttribute<AutoRegisterAttribute>()?.Enabled ?? true)))
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
                                       .FirstOrDefault(a => a != null);
            if (interceptAttribute != null)
            {
                registration.EnableInterfaceInterceptors();
            }
            else
            {
                serviceTypes.Add(implementationType);
            }

            registration.As(serviceTypes.ToArray())
               .PropertiesAutowired()
               .SetLifetime(lifetime);
            return registration;
        }

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
        /// 获取实体服务
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="serviceProvider">服务提供者</param>
        /// <returns>实体服务</returns>
        public static IEntityService<T> GetEntityService<T>(this IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<IEntityService<T>>();
        }

        /// <summary>
        /// 获取实体服务
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        /// <param name="entityType">实体类型</param>
        /// <returns>实体服务</returns>
        public static IEntityService GetEntityService(this IServiceProvider serviceProvider, Type entityType)
        {
            var serviceType = typeof(IEntityService<>).MakeGenericType(entityType);
            return serviceProvider.GetRequiredService(serviceType) as IEntityService;
        }

        /// <summary>
        /// 获取实体视图服务
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="serviceProvider">服务提供者</param>
        /// <returns>实体视图服务</returns>
        public static IEntityViewService<T> GetEntityViewService<T>(this IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<IEntityViewService<T>>();
        }

        /// <summary>
        /// 获取实体视图服务
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        /// <param name="entityType">实体类型</param>
        /// <returns>实体视图服务</returns>
        public static IEntityViewService GetEntityViewService(this IServiceProvider serviceProvider, Type entityType)
        {
            var serviceType = typeof(IEntityViewService<>).MakeGenericType(entityType);
            return serviceProvider.GetRequiredService(serviceType) as IEntityViewService;
        }
    }

    public static class AssemblyAnalyzer
    {
        /// <summary>
        /// 获取所有直接引用的程序集名称（包括未加载的）
        /// </summary>
        public static IEnumerable<Assembly> GetAllReferencedAssemblies(Assembly entryAssembly = null)
        {
            entryAssembly ??= Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var result = new List<AssemblyName>();
            result.Add(entryAssembly.GetName());
            result.AddRange(entryAssembly.GetReferencedAssemblies());
            return result.DistinctBy(an => an.FullName).Where(a => !a.FullName.StartsWith("System.") && !a.FullName.StartsWith("Microsoft."))
                    .Select(Assembly.Load)
                    .Where(a => !a.IsDynamic);
        }
    }
}
