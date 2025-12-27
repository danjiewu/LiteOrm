using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MyOrm.Common;
using MyOrm.MyOrm;
using MyOrm.Service;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm
{
    public class MyServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
    {
        // 步骤1：创建容器构建器（复用原生IServiceCollection）
        public IServiceCollection CreateBuilder(IServiceCollection services)
        {
            services.AddAutoRegisteredServices();
            // 添加MyOrm全局服务
            services.AddMyOrm();
            // 添加自动注册服务
            
            return services;
        }

        // 步骤2：创建自定义ServiceProvider（核心）
        public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
        {
            // 先构建原生ServiceProvider（作为默认容器）
            var defaultProvider = containerBuilder.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true // 根容器解析Scoped服务时抛异常
            });
            // 用自定义MyServiceProvider包装默认容器
            return new MyServiceProvider(containerBuilder, defaultProvider);
        }
    }

    public static class MyServiceProviderExt
    {
        /// <summary>
        /// 扫描指定程序集，自动注册标记[AutoRegister]的类型
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="assemblies">目标程序集（为空则扫描当前域所有程序集）</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddAutoRegisteredServices(
            this IServiceCollection services,
            params Assembly[] assemblies)
        {
            // 若未指定程序集，扫描当前应用域已加载的所有程序集（排除系统程序集）
            var targetAssemblies = assemblies.Any()
                ? assemblies
                : AssemblyAnalyzer.GetAllReferencedAssemblies()
                    .Where(a => !a.FullName.StartsWith("System.") && !a.FullName.StartsWith("Microsoft."))
                    .Select(Assembly.Load)
                    .Where(a => !a.IsDynamic);

            foreach (var assembly in targetAssemblies)
            {
                // 筛选符合条件的类型：非抽象类、非接口、非泛型定义、标记[AutoRegister]
                var typesToRegister = assembly.GetTypes()
                    .Where(t =>
                        t.IsClass
                        && !t.IsAbstract
                        && !t.IsGenericTypeDefinition
                        && t.GetCustomAttribute<AutoRegisterAttribute>() != null);

                foreach (var type in typesToRegister)
                {
                    RegisterType(services, type);
                }
            }

            return services;
        }

        private static void RegisterType(IServiceCollection services, Type implementationType)
        {
            var attribute = implementationType.GetCustomAttribute<AutoRegisterAttribute>()!;
            if (!attribute.Enabled) return;

            Type[] serviceTypes;

            // 1. 若特性指定了ServiceTypes，直接使用
            if (attribute.ServiceTypes != null && attribute.ServiceTypes.Any())
            {
                serviceTypes = attribute.ServiceTypes;
            }
            // 2. 否则自动获取所有实现的接口（排除系统接口如IDisposable）
            else
            {
                serviceTypes = implementationType.GetInterfaces()
                    .Where(i => !i.Namespace.StartsWith("System.")
                             && !i.IsGenericTypeDefinition)
                    .ToArray();

                // 若没有接口，注册自身
                if (!serviceTypes.Any())
                    serviceTypes = new[] { implementationType };
            }

            // 3. 批量注册所有服务类型指向同一实现类型
            foreach (var serviceType in serviceTypes)
            {
                services.Add(ServiceDescriptor.Describe(serviceType, implementationType, attribute.Lifetime));
            }
        }

        /// <summary>
        /// 添加 MyOrm 服务（使用默认配置）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddMyOrm(this IServiceCollection services)
        {
            return services.AddMyOrm(null);
        }

        /// <summary>
        /// 添加 MyOrm 服务（传入 IConfiguration）
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddMyOrm(this IServiceCollection services, IConfiguration configuration)
        {
            // 注册 SqlBuilderFactory 为单例
            services.AddSingleton<SqlBuilderFactory>();
            // 注册 DAOContextPoolFactory
            services.AddDAOContextPoolFactory(configuration);

            // 注册 TableInfoProvider（这里使用工厂方法，因为需要 DAOContextPoolFactory）
            services.AddSingleton<TableInfoProvider>(serviceProvider =>
            {
                var factory = serviceProvider.GetRequiredService<DAOContextPoolFactory>();
                return new AttributeTableInfoProvider(serviceProvider)
                {
                    DefaultConnectionName = factory.DefaultConnectionName
                };
            });

            // 注册 SessionManager 为 Scoped（每个请求一个会话）
            services.AddScoped<SessionManager>();

            // 注册 GenericServiceBuilder 为单例
            services.AddSingleton<IGenericServiceBuilder, GenericServiceBuilder>();

            // 注册自动实体服务
            services.AddAutoEntityServices();

            return services;
        }

        /// <summary>
        /// 添加 DAOContextPoolFactory 服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddDAOContextPoolFactory(
            this IServiceCollection services,
            IConfiguration configuration = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            // 注册工厂为单例
            services.TryAddSingleton<DAOContextPoolFactory>(serviceProvider =>
            {
                // 如果传入配置，使用传入的配置
                if (configuration != null)
                {
                    return new DAOContextPoolFactory(configuration);
                }

                // 否则尝试从服务提供者获取配置
                var config = serviceProvider.GetService<IConfiguration>();
                if (config?.GetSection("MyOrm") != null)
                {
                    return new DAOContextPoolFactory(config.GetSection("MyOrm"));
                }

                // 如果没有配置，创建空的工厂
                return new DAOContextPoolFactory();
            });

            return services;
        }

        /// <summary>
        /// 自动注册所有实体服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <returns>服务集合</returns>
        private static IServiceCollection AddAutoEntityServices(this IServiceCollection services)
        {
            // 扫描所有程序集，查找标记了 [Table] 特性的实体类
            var assemblies = AssemblyAnalyzer.GetAllReferencedAssemblies()
                .Select(Assembly.Load)
                .Where(a => !a.IsDynamic);

            foreach (var assembly in assemblies)
            {
                var entityTypes = assembly.GetTypes()
                    .Where(t => t.IsClass &&
                                !t.IsAbstract &&
                                t.GetCustomAttribute<TableAttribute>() != null);

                foreach (var entityType in entityTypes)
                {
                    // 注册 IEntityService<T>
                    var entityServiceType = typeof(IEntityService<>).MakeGenericType(entityType);
                    var entityServiceImplType = typeof(EntityService<>).MakeGenericType(entityType);
                    services.AddScoped(entityServiceType, entityServiceImplType);

                    // 注册 IEntityViewService<T>
                    var entityViewServiceType = typeof(IEntityViewService<>).MakeGenericType(entityType);
                    var entityViewServiceImplType = typeof(EntityViewService<>).MakeGenericType(entityType);
                    services.AddScoped(entityViewServiceType, entityViewServiceImplType);
                }
            }

            return services;
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

        /// <summary>
        /// 获取 DAOContextPoolFactory
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        /// <returns>连接池工厂</returns>
        public static DAOContextPoolFactory GetDAOContextPoolFactory(this IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<DAOContextPoolFactory>();
        }

        /// <summary>
        /// 获取数据库上下文
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        /// <param name="poolName">连接池名称</param>
        /// <returns>数据库上下文</returns>
        public static DAOContext GetDAOContext(this IServiceProvider serviceProvider, string poolName = null)
        {
            var factory = serviceProvider.GetDAOContextPoolFactory();
            return factory.PickContext(poolName);
        }

        /// <summary>
        /// 获取 SessionManager
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        /// <returns>会话管理器</returns>
        public static SessionManager GetSessionManager(this IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<SessionManager>();
        }
    }

    public static class AssemblyAnalyzer
    {
        /// <summary>
        /// 获取所有直接引用的程序集名称（包括未加载的）
        /// </summary>
        public static IEnumerable<AssemblyName> GetAllReferencedAssemblies(Assembly entryAssembly = null)
        {
            entryAssembly ??= Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var result = new List<AssemblyName>();
            result.Add(entryAssembly.GetName());
            result.AddRange(entryAssembly.GetReferencedAssemblies());
            return result.DistinctBy(an => an.FullName);
        }
    }
}
