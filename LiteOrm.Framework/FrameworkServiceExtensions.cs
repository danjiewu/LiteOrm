using Autofac;
using Autofac.Builder;
using Autofac.Extras.DynamicProxy;
using Autofac.Extensions.DependencyInjection;
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InterceptAttribute = LiteOrm.Common.InterceptAttribute;

namespace LiteOrm.Framework
{
    /// <summary>
    /// LiteOrm Framework 扩展方法集合 - 提供 Autofac DI 容器 + Castle DynamicProxy 拦截器的集成入口。
    /// </summary>
    /// <remarks>
    /// FrameworkServiceExtensions 提供了将 LiteOrm 框架集成到 Autofac DI 容器的扩展方法，
    /// 同时启用 Castle DynamicProxy 拦截器支持。
    ///
    /// 主要功能包括：
    /// 1. Autofac 容器集成 - 通过 <see cref="RegisterLiteOrmFramework(IHostBuilder, Action{LiteOrmServiceExtensions.LiteOrmOptions})"/>
    ///    使用 Autofac 作为 DI 容器；
    /// 2. 自动服务注册 - 扫描程序集中带 [AutoRegister] 特性的类型并注册到 Autofac；
    /// 3. 拦截器应用 - 读取 <see cref="InterceptAttribute"/> 并自动应用 Castle DynamicProxy 拦截；
    /// 4. SqlBuilder 注册 - 注册自定义 SqlBuilder 到 SqlBuilderFactory；
    /// 5. 作用域跟踪 - 通过 Autofac ILifetimeScope 事件自动跟踪作用域变化。
    ///
    /// 使用示例：
    /// <code>
    /// var builder = Host.CreateDefaultBuilder(args)
    ///     .RegisterLiteOrmFramework(options =>
    ///     {
    ///         options.Assemblies = new[] { typeof(MyService).Assembly };
    ///     });
    /// </code>
    /// </remarks>
    public static class FrameworkServiceExtensions
    {
        /// <summary>
        /// 使用 Autofac 容器注册 LiteOrm 框架到主机构建器。
        /// </summary>
        /// <param name="hostBuilder">主机构建器。</param>
        /// <returns>配置后的主机构建器。</returns>
        public static IHostBuilder RegisterLiteOrmFramework(this IHostBuilder hostBuilder)
        {
            return RegisterLiteOrmFramework(hostBuilder, null);
        }

        /// <summary>
        /// 使用 Autofac 容器注册 LiteOrm 框架到主机构建器，并允许配置选项。
        /// </summary>
        /// <param name="hostBuilder">主机构建器。</param>
        /// <param name="configureOptions">配置选项的回调函数。</param>
        /// <returns>配置后的主机构建器。</returns>
        public static IHostBuilder RegisterLiteOrmFramework(this IHostBuilder hostBuilder, Action<LiteOrmServiceExtensions.LiteOrmOptions>? configureOptions)
        {
            var options = new LiteOrmServiceExtensions.LiteOrmOptions();
            try
            {
                configureOptions?.Invoke(options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize LiteOrm options", ex);
            }

            return hostBuilder.UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureServices(services =>
                {
                    // 显式注册核心服务（MS DI 注册会被 AutofacServiceProviderFactory 转移到 Autofac 容器）
                    services.AddCoreLiteOrmServices();
                })
                .ConfigureContainer<ContainerBuilder>(builder =>
                {
                    var logger = options.LoggerFactory?.CreateLogger(nameof(FrameworkServiceExtensions));

                    // 自动扫描并注册标记 [AutoRegister] 的服务（Autofac 版，含拦截器支持）
                    try
                    {
                        builder.RegisterAutoService(logger, options.Assemblies ?? Array.Empty<Assembly>());
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Failed to register LiteOrm services automatically", ex);
                    }

                    // 注册自定义 SqlBuilder
                    foreach (var kvp in options.SqlBuilders)
                    {
                        try
                        {
                            SqlBuilderFactory.Instance.RegisterSqlBuilder(kvp.Key, kvp.Value);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException($"Failed to register SqlBuilder for data source '{kvp.Key}'", ex);
                        }
                    }

                    foreach (var kvp in options.SqlBuildersByType)
                    {
                        try
                        {
                            SqlBuilderFactory.Instance.RegisterSqlBuilder(kvp.Key, kvp.Value);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException($"Failed to register SqlBuilder for connection type '{kvp.Key.FullName}'", ex);
                        }
                    }

                    // 注册作用域跟踪和全局实例初始化
                    if (options.RegisterScope)
                    {
                        builder.RegisterBuildCallback(container =>
                        {
                            var lifetimeScope = container as ILifetimeScope;
                            if (lifetimeScope != null)
                            {
                                var sp = new AutofacServiceProvider(lifetimeScope);

                                // 设置全局 TableInfoProvider（DI 解析的实例优先于默认的 AttributeTableInfoProvider）
                                var tableInfoProvider = sp.GetService<TableInfoProvider>();
                                if (tableInfoProvider != null)
                                {
                                    TableInfoProvider.Default = tableInfoProvider;
                                }

                                // 设置根作用域的 ServiceProvider，使 SessionManager.Current 在根作用域可用
                                SessionManager.SetCurrentServiceProvider(sp);

                                // 注册子作用域跟踪
                                ScopeExtensions.RegisterScope(lifetimeScope);
                            }
                        });
                    }
                });
        }

        /// <summary>
        /// 扫描指定程序集，自动注册标记 [AutoRegister] 的类型到 Autofac 容器（含拦截器支持）。
        /// </summary>
        /// <param name="builder">Autofac 容器构建器。</param>
        /// <param name="assemblies">目标程序集（为空则扫描当前域所有引用程序集）。</param>
        /// <returns>容器构建器。</returns>
        public static ContainerBuilder RegisterAutoService(
            this ContainerBuilder builder,
            params Assembly[] assemblies)
        {
            return RegisterAutoService(builder, null, assemblies);
        }

        /// <summary>
        /// 扫描指定程序集，自动注册标记 [AutoRegister] 的类型到 Autofac 容器（含拦截器支持），
        /// 并通过 <paramref name="logger"/> 输出扫描日志。
        /// </summary>
        /// <param name="builder">Autofac 容器构建器。</param>
        /// <param name="logger">日志记录器（为 null 时跳过日志输出）。</param>
        /// <param name="assemblies">目标程序集（为空则扫描当前域所有引用程序集）。</param>
        /// <returns>容器构建器。</returns>
        public static ContainerBuilder RegisterAutoService(
            this ContainerBuilder builder,
            ILogger? logger,
            params Assembly[] assemblies)
        {
            var assemblyList = new HashSet<Assembly>();
            assemblyList.Add(typeof(LiteOrmServiceExtensions).Assembly);
            assemblyList.Add(typeof(AutoRegisterAttribute).Assembly);

            if (assemblies != null && assemblies.Any())
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

            logger?.LogDebug("Scanning {Count} assemblies to register LiteOrm services (Autofac)", assemblyList.Count);

            foreach (var assembly in assemblyList)
            {
                IEnumerable<Type> types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    logger?.LogWarning(ex, "Failed to load types from assembly '{Assembly}', some types will be skipped", assembly.FullName);
                    types = ex.Types.OfType<Type>();
                }

                var registrableTypes = types
                    .Where(t => !t.IsAbstract && !t.IsInterface && (t.GetCustomAttribute<AutoRegisterAttribute>(true)?.Enabled ?? false))
                    .ToList();

                logger?.LogDebug("Scanned assembly '{Assembly}': found {Count} registrable type(s)",
                    assembly.GetName().Name, registrableTypes.Count);

                foreach (var t in registrableTypes)
                {
                    var attr = t.GetCustomAttribute<AutoRegisterAttribute>(true);
                    logger?.LogDebug(
                        "Registering {Kind} service '{Type}' [Lifetime={Lifetime}, AutoActivate={AutoActivate}]",
                        t.IsGenericTypeDefinition ? "generic" : "regular",
                        t.FullName,
                        attr?.Lifetime ?? Lifetime.Scoped,
                        attr?.AutoActivate ?? false);

                    RegisterSingleType(builder, t, attr, logger);
                }
            }

            return builder;
        }

        /// <summary>
        /// 将单个带 [AutoRegister] 的类型注册到 Autofac 容器（含拦截器支持）。
        /// </summary>
        private static void RegisterSingleType(
            ContainerBuilder builder,
            Type implementationType,
            AutoRegisterAttribute? attr,
            ILogger? logger)
        {
            if (implementationType.IsGenericTypeDefinition)
            {
                var registration = builder.RegisterGeneric(implementationType);
                ApplyRegistrationSettings(registration, implementationType, attr, logger);
            }
            else
            {
                var registration = builder.RegisterType(implementationType);
                ApplyRegistrationSettings(registration, implementationType, attr, logger);
            }
        }

        /// <summary>
        /// 将生命周期、拦截器和服务类型等通用配置应用到注册构建器上。
        /// </summary>
        private static void ApplyRegistrationSettings<TLimit, TActivatorData, TRegistrationStyle>(
            IRegistrationBuilder<TLimit, TActivatorData, TRegistrationStyle> registration,
            Type implementationType,
            AutoRegisterAttribute? attr,
            ILogger? logger)
        {
            // 设置生命周期
            var lifetime = attr?.Lifetime ?? Lifetime.Scoped;
            switch (lifetime)
            {
                case Lifetime.Singleton:
                    registration.SingleInstance();
                    break;
                case Lifetime.Scoped:
                    registration.InstancePerLifetimeScope();
                    break;
                case Lifetime.Transient:
                    registration.InstancePerDependency();
                    break;
            }

            // 设置自动激活
            if (attr?.AutoActivate ?? false)
            {
                registration.AutoActivate();
            }

            // 检测 InterceptAttribute
            var interceptAttribute = implementationType.GetCustomAttribute<InterceptAttribute>()
                ?? implementationType.GetInterfaces()
                    .Select(i => i.GetCustomAttribute<InterceptAttribute>())
                    .FirstOrDefault(a => a is not null);

            if (interceptAttribute != null)
            {
                // 应用 Castle DynamicProxy 拦截
                registration.EnableInterfaceInterceptors()
                    .InterceptedBy(interceptAttribute.InterceptorType);
                logger?.LogDebug(
                    "Applied interception to '{Type}' with interceptor '{Interceptor}'",
                    implementationType.FullName, interceptAttribute.InterceptorType.FullName);
            }

            // 注册服务类型
            // 注意：Autofac 中 .As() 会覆盖默认的自身注册，因此需要显式注册 AsSelf() 以保证具体类型可解析
            var serviceTypes = GetServiceTypes(implementationType, attr);
            bool hasAdditionalServices = false;
            foreach (var serviceType in serviceTypes)
            {
                if (serviceType == implementationType)
                    continue;

                hasAdditionalServices = true;

                if (attr?.Key != null)
                {
                    registration.Keyed(attr.Key, serviceType);
                }
                else
                {
                    registration.As(serviceType);
                }
            }

            // 当有额外的服务类型注册且未启用拦截器时，显式注册自身以保证具体类型可解析
            // （Autofac 中 .As() 会覆盖默认的自身注册）
            // 注意：启用 EnableInterfaceInterceptors 时不能注册非接口类型作为服务，
            // 否则 EnsureInterfaceInterceptionApplies 会抛出异常
            if (hasAdditionalServices && interceptAttribute == null)
            {
                registration.As(implementationType);
            }
        }

        /// <summary>
        /// 计算类型应注册的服务类型集合。
        /// </summary>
        private static List<Type> GetServiceTypes(Type implementationType, AutoRegisterAttribute? attr)
        {
            var serviceTypes = new List<Type>();

            if (attr?.ServiceTypes is not null && attr.ServiceTypes.Any())
            {
                serviceTypes.AddRange(attr.ServiceTypes);
            }
            else
            {
                foreach (var serviceType in implementationType.GetInterfaces()
                    .Where(i => i.Namespace != null
                             && !i.Namespace.StartsWith("System.")
                             && i.Namespace != "System"
                             && (i.GetCustomAttribute<AutoRegisterAttribute>(true)?.Enabled ?? true)))
                {
                    if (implementationType.IsGenericTypeDefinition && serviceType.IsGenericType)
                    {
                        if (implementationType.GetGenericArguments().Length == serviceType.GenericTypeArguments.Length
                            && serviceType.GenericTypeArguments.All(t => t.DeclaringType == implementationType))
                        {
                            serviceTypes.Add(serviceType.GetGenericTypeDefinition());
                        }
                    }
                    else if (!implementationType.IsGenericTypeDefinition)
                    {
                        serviceTypes.Add(serviceType);
                    }
                }
            }

            // 没有拦截特性时，将实现类型自身也注册为服务
            var hasIntercept = implementationType.GetCustomAttribute<InterceptAttribute>() != null
                || implementationType.GetInterfaces()
                    .Any(i => i.GetCustomAttribute<InterceptAttribute>() != null);

            if (!hasIntercept && !serviceTypes.Contains(implementationType))
            {
                serviceTypes.Add(implementationType);
            }

            return serviceTypes;
        }
    }
}
