using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm 服务注册扩展方法
    /// </summary>
    public static class LiteOrmServiceExtensions
    {
        /// <summary>
        /// 添加 LiteOrm 服务到依赖注入容器
        /// </summary>
        /// <param name="services">依赖注入服务集合</param>
        /// <returns>配置后的服务集合</returns>
        public static IServiceCollection AddLiteOrm(this IServiceCollection services)
        {
            return AddLiteOrm(services, null);
        }

        /// <summary>
        /// 添加 LiteOrm 服务到依赖注入容器，并配置选项。
        /// </summary>
        /// <param name="services">依赖注入服务集合</param>
        /// <param name="configureOptions">配置选项的回调函数。</param>
        /// <returns>配置后的服务集合</returns>
        public static IServiceCollection AddLiteOrm(this IServiceCollection services, Action<LiteOrmOptions>? configureOptions)
        {
            var options = new LiteOrmOptions();
            try
            {
                configureOptions?.Invoke(options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to initialize LiteOrm options", ex);
            }

            // 在此注册 LiteOrm 所需的服务
            services.AddSingleton<IDataSourceProvider>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var dataSourceProvider = new DataSourceProvider();
                dataSourceProvider.LoadConfiguration(configuration.GetSection("LiteOrm"));
                return dataSourceProvider;
            });
            services.AddSingleton<DAOContextPoolFactory>(sp =>
            {
                return new DAOContextPoolFactory(sp.GetRequiredService<IDataSourceProvider>());
            });

            services.AddSingleton<TableInfoProvider, AttributeTableInfoProvider>();

            // Scoped 服务——每个作用域获得独立的 SessionManager。
            // 通过工厂构造并绑定 SessionManager.Current，使 Current 仅作为供外部使用的便捷入口，
            // 而 DAO 内的会话由构造参数注入，不再依赖 Current。
            services.AddScoped<SessionManager>(sp =>
            {
                var sessionManager = new SessionManager(
                    sp.GetRequiredService<DAOContextPoolFactory>(),
                    sp.GetService<ILogger<SessionManager>>());
                SessionManager.SetCurrent(() => sp.GetRequiredService<SessionManager>());
                return sessionManager;
            });
           

            // 自动注册自定义服务与 DAO（源生成器生成的注册代码 + 可选程序集扫描）
            if (options.AutoRegisterServices)
            {
                LiteOrmAutoRegistration.Apply(services);
            }
            else
            {
                // 泛型 DAO 与服务（Scoped）。
                services.AddScoped(typeof(ObjectDAO<>));
                services.AddScoped(typeof(ObjectViewDAO<>));
                services.AddScoped(typeof(IObjectDAO<>), typeof(ObjectDAO<>));
                services.AddScoped(typeof(IObjectViewDAO<>), typeof(ObjectViewDAO<>));
                services.AddScoped(typeof(EntityService<>));
                services.AddScoped(typeof(EntityViewService<>));
                services.AddScoped(typeof(IEntityService<>), typeof(EntityService<>));
                services.AddScoped(typeof(IEntityViewService<>), typeof(EntityViewService<>));
            }

            // 追加自定义服务注册
            options.ConfigureServices?.Invoke(services);

            return services;
        }
    }
}
