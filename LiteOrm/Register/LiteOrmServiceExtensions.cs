using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        /// <para>
        /// 通过 <paramref name="configureOptions"/> 回调驱动注册期配置；若要使用注入工厂方式配置，
        /// 可调用 <see cref="AddLiteOrmOptions"/> 注册 <see cref="LiteOrmOptions"/> 工厂，
        /// 运行时可通过 DI 解析到该工厂构造的选项（覆盖 <paramref name="configureOptions"/>）。
        /// </para>
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

            // 将选项注册进 DI：若已通过 AddLiteOrmOptions 注册了工厂，则以工厂构造的选项为准（运行时覆盖 configureOptions）。
            services.TryAddSingleton(options);

            // 在此注册 LiteOrm 所需的服务
            services.TryAddSingleton<IDataSourceProvider>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var dataSourceProvider = new DataSourceProvider();
                dataSourceProvider.LoadConfiguration(configuration.GetSection("LiteOrm"));
                return dataSourceProvider;
            });
            services.TryAddSingleton<DAOContextPoolFactory>(sp =>
            {
                return new DAOContextPoolFactory(sp.GetRequiredService<IDataSourceProvider>());
            });

            services.TryAddSingleton<TableInfoProvider>(_ => TableInfoProvider.Instance);

            // Scoped 服务——每个作用域获得独立的 SessionManager。
            // 通过工厂构造并绑定 SessionManager.Current，使 Current 仅作为供外部使用的便捷入口，
            // 而 DAO 内的会话由构造参数注入，不再依赖 Current。
            services.TryAddScoped<SessionManager>(sp =>
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
                services.TryAddScoped(typeof(ObjectDAO<>));
                services.TryAddScoped(typeof(ObjectViewDAO<>));
                services.TryAddScoped(typeof(IObjectDAO<>), typeof(ObjectDAO<>));
                services.TryAddScoped(typeof(IObjectViewDAO<>), typeof(ObjectViewDAO<>));
                services.TryAddScoped(typeof(EntityService<>));
                services.TryAddScoped(typeof(EntityViewService<>));
                services.TryAddScoped(typeof(IEntityService<>), typeof(EntityService<>));
                services.TryAddScoped(typeof(IEntityViewService<>), typeof(EntityViewService<>));
                services.TryAddScoped(typeof(IEntityServiceAsync<>), typeof(EntityService<>));
                services.TryAddScoped(typeof(IEntityViewServiceAsync<>), typeof(EntityViewService<>));
            }

            // 追加自定义服务注册
            options.ConfigureServices?.Invoke(services);

            return services;
        }

        /// <summary>
        /// 以工厂方式注册 <see cref="LiteOrmOptions"/> 到 DI 容器，便于在配置服务(<see cref="IConfiguration"/>)或其他 DI 服务基础上构造参数。
        /// <para>
        /// 工厂接收 <see cref="IServiceProvider"/>，可从其中解析 <see cref="IConfiguration"/> 等依赖后返回选项。
        /// 若同时调用了 <see cref="AddLiteOrm(IServiceCollection, Action{LiteOrmOptions})"/>，则运行时解析
        /// <see cref="LiteOrmOptions"/> 时以本工厂构造的选项为准（覆盖 configure 回调默认值）。
        /// </para>
        /// </summary>
        /// <param name="services">服务集合。</param>
        /// <param name="optionsFactory">选项工厂，接收 <see cref="IServiceProvider"/>，返回 <see cref="LiteOrmOptions"/>。</param>
        /// <returns>返回修改后的服务集合以支持链式调用。</returns>
        /// <example>
        /// <code>
        /// builder.ConfigureServices(services =>
        ///     services.AddLiteOrmOptions(sp =>
        ///     {
        ///         var config = sp.GetRequiredService&lt;IConfiguration&gt;();
        ///         return new LiteOrmOptions
        ///         {
        ///             AutoRegisterServices = config.GetValue&lt;bool&gt;("LiteOrm:AutoRegisterServices"),
        ///         };
        ///     }));
        /// </code>
        /// </example>
        public static IServiceCollection AddLiteOrmOptions(
            this IServiceCollection services,
            Func<IServiceProvider, LiteOrmOptions> optionsFactory)
        {
            if (optionsFactory is null)
                throw new ArgumentNullException(nameof(optionsFactory));
            services.AddSingleton(optionsFactory);
            return services;
        }
    }
}
