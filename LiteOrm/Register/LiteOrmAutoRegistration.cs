using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm 自动注册中心。
    /// <para>
    /// 根据运行模式采用不同的注册来源：
    /// <list type="bullet">
    /// <item><description>AOT 模式（<see cref="RuntimeFeature.IsDynamicCodeSupported"/> 为 <c>false</c>）：
    /// 使用 LiteOrm.Generators 源生成器在编译期生成的注册代码（通过模块初始化器登记到本类）。</description></item>
    /// <item><description>非 AOT 模式（常规 JIT）：直接扫描程序集中带 <c>[AutoRegister]</c> 特性的类型进行运行时注册，
    /// 此时源生成器不会生成注册代码，避免重复注册。</description></item>
    /// </list>
    /// <see cref="LiteOrmServiceExtensions.AddLiteOrm(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
    /// 在启用自动注册时统一应用到 <see cref="IServiceCollection"/>。
    /// </para>
    /// </summary>
    public static class LiteOrmAutoRegistration
    {
        private static readonly object _syncRoot = new object();
        private static readonly List<Action<IServiceCollection>> _registrations = new List<Action<IServiceCollection>>();

        /// <summary>
        /// 登记一条自动注册回调（由 LiteOrm.Generators 生成的模块初始化器在 AOT 模式下调用）。
        /// </summary>
        /// <param name="registration">接收 <see cref="IServiceCollection"/> 的注册动作。</param>
        public static void Add(Action<IServiceCollection> registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));
            lock (_syncRoot)
            {
                _registrations.Add(registration);
            }
        }

        /// <summary>
        /// 将自动注册应用到 <paramref name="services"/>。
        /// <para>非 AOT 模式下扫描程序集进行运行时注册；AOT 模式下应用源生成器登记的注册回调。</para>
        /// </summary>
        /// <param name="services">服务集合。</param>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050",
            Justification = "RegisterByAssemblyScan is only called when RuntimeFeature.IsDynamicCodeSupported is true (JIT mode); under AOT, the ApplyGenerated path is used instead.")]
        [UnconditionalSuppressMessage("Trimming", "IL2026",
            Justification = "RegisterByAssemblyScan (RequiresUnreferencedCode) is guarded by RuntimeFeature.IsDynamicCodeSupported; under AOT, ApplyGenerated is used and no assembly scanning occurs.")]
#endif
        public static void Apply(IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            if (RuntimeFeature.IsDynamicCodeSupported)
            {
                RegisterByAssemblyScan(services);
            }
            else
            {
                ApplyGenerated(services);
            }
        }

        private static void ApplyGenerated(IServiceCollection services)
        {
            Action<IServiceCollection>[] snapshot;
            lock (_syncRoot)
            {
                snapshot = _registrations.ToArray();
            }
            foreach (var registration in snapshot)
            {
                registration(services);
            }
        }

        /// <summary>
        /// 扫描相关程序集，运行时注册所有带 <c>[AutoRegister]</c> 特性的类型。
        /// 仅用于非 AOT 模式（动态代码可用，允许反射）。
        /// </summary>
        [RequiresDynamicCode("Assembly scanning via Assembly.GetTypes requires JIT; not supported under NativeAOT. Use the source-generated registration path instead.")]
        [RequiresUnreferencedCode("Assembly scanning may load types that are trimmed away in AOT/trimmed deployments.")]
        private static void RegisterByAssemblyScan(IServiceCollection services)
        {
            var assemblyList = new HashSet<Assembly>();
            foreach (var assembly in AssemblyAnalyzer.GetAllReferencedAssemblies())
            {
                assemblyList.Add(assembly);
            }

            foreach (var assembly in assemblyList)
            {
                IEnumerable<Type> types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.OfType<Type>();
                }

                foreach (var type in types)
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    var attr = type.GetCustomAttribute<AutoRegisterAttribute>(true);
                    if (attr == null || !attr.Enabled) continue;
                    RegisterSingleType(services, type, attr);
                }
            }
        }

        private static void RegisterSingleType(IServiceCollection services, Type implementationType, AutoRegisterAttribute attr)
        {
            var lifetime = ToServiceLifetime(attr.Lifetime);
            var serviceTypes = GetServiceTypes(implementationType, attr);

            RegisterType(services, lifetime, attr.Key, null, implementationType);
            foreach (var serviceType in serviceTypes)
            {
                if (serviceType == implementationType) continue;
                RegisterType(services, lifetime, attr.Key, serviceType, implementationType);
            }
        }

        private static ServiceLifetime ToServiceLifetime(Lifetime lifetime) => lifetime switch
        {
            Lifetime.Scoped => ServiceLifetime.Scoped,
            Lifetime.Transient => ServiceLifetime.Transient,
            _ => ServiceLifetime.Singleton
        };

        [UnconditionalSuppressMessage("Trimming", "IL2067",
            Justification = "DI Add* overloads require types to expose PublicConstructors; RegisterType is only called on the JIT runtime-registration path (RegisterByAssemblyScan), where types come from assembly scanning.")]
        private static void RegisterType(IServiceCollection services, ServiceLifetime lifetime, object? key, Type? serviceType, Type implementationType)
        {
#if NET8_0_OR_GREATER
            if (key != null)
            {
                if (serviceType == null)
                {
                    switch (lifetime)
                    {
                        case ServiceLifetime.Scoped: services.AddKeyedScoped(implementationType, key, implementationType); return;
                        case ServiceLifetime.Transient: services.AddKeyedTransient(implementationType, key, implementationType); return;
                        default: services.AddKeyedSingleton(implementationType, key, implementationType); return;
                    }
                }
                else
                {
                    switch (lifetime)
                    {
                        case ServiceLifetime.Scoped: services.AddKeyedScoped(serviceType, key, implementationType); return;
                        case ServiceLifetime.Transient: services.AddKeyedTransient(serviceType, key, implementationType); return;
                        default: services.AddKeyedSingleton(serviceType, key, implementationType); return;
                    }
                }
            }
#endif
            if (serviceType == null)
            {
                switch (lifetime)
                {
                    case ServiceLifetime.Scoped: services.AddScoped(implementationType); break;
                    case ServiceLifetime.Transient: services.AddTransient(implementationType); break;
                    default: services.AddSingleton(implementationType); break;
                }
            }
            else
            {
                switch (lifetime)
                {
                    case ServiceLifetime.Scoped: services.AddScoped(serviceType, implementationType); break;
                    case ServiceLifetime.Transient: services.AddTransient(serviceType, implementationType); break;
                    default: services.AddSingleton(serviceType, implementationType); break;
                }
            }
        }

        /// <summary>
        /// 判断是否为 LiteOrm 的非泛型标记接口（这些接口仅作为约定标记，不作为服务注册类型）。
        /// 与源生成器 <c>AutoRegisterGenerator.IsExcludedMarkerInterface</c> 保持一致。
        /// </summary>
        private static bool IsExcludedMarkerInterface(Type serviceType)
        {
            if (serviceType.IsGenericType) return false;
            return serviceType.FullName is "LiteOrm.Common.IObjectViewDAO"
                or "LiteOrm.Common.IObjectDAO"
                or "LiteOrm.Common.IObjectDAOAsync"
                or "LiteOrm.Service.IEntityService"
                or "LiteOrm.Service.IEntityServiceAsync"
                or "LiteOrm.Service.IEntityViewService"
                or "LiteOrm.Service.IEntityViewServiceAsync";
        }

        /// <summary>
        /// 计算类型应注册的服务类型集合，与源生成器语义一致。
        /// <para>
        /// <see cref="RegisterPolicy.All"/>（默认）：非 System、非标记接口 + 实现类型自身；
        /// <see cref="RegisterPolicy.Interface"/>：仅已实现接口（排除带 <c>[AutoRegister(false)]</c> 的接口）；
        /// <see cref="RegisterPolicy.Self"/>：仅实现类型自身。
        /// </para>
        /// </summary>
        [UnconditionalSuppressMessage("Trimming", "IL2070",
            Justification = "GetInterfaces() operates on runtime-scanned types; it is only used on the JIT registration path (RegisterByAssemblyScan), and interfaces + their members are preserved under AOT by the source generator.")]
        private static List<Type> GetServiceTypes(Type implementationType, AutoRegisterAttribute attr)
        {
            var serviceTypes = new List<Type>();

            if (attr.Policy == RegisterPolicy.Self)
            {
                serviceTypes.Add(implementationType);
                return serviceTypes;
            }

            foreach (var serviceType in implementationType.GetInterfaces()
                .Where(i => i.Namespace != null
                         && i.Namespace != "System"
                         && !i.Namespace.StartsWith("System.")
                         && !IsExcludedMarkerInterface(i)
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

            if (attr.Policy == RegisterPolicy.All && !serviceTypes.Contains(implementationType))
                serviceTypes.Add(implementationType);

            return serviceTypes;
        }
    }
}
