using LiteOrm.Common;
using LiteOrm.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

            // Scoped 服务——每个作用域获得独立的 SessionManager
            services.AddScoped<SessionManager>();
            // 泛型 DAO 与服务（Scoped）。     
            services.AddScoped(typeof(ObjectDAO<>));
            services.AddScoped(typeof(ObjectViewDAO<>));
            services.AddScoped(typeof(IObjectDAO<>), typeof(ObjectDAO<>));
            services.AddScoped(typeof(IObjectViewDAO<>), typeof(ObjectViewDAO<>));
            services.AddScoped(typeof(EntityService<>));
            services.AddScoped(typeof(EntityViewService<>));
            services.AddScoped(typeof(IEntityService<>), typeof(EntityService<>));
            services.AddScoped(typeof(IEntityViewService<>), typeof(EntityViewService<>));
            return services;
        }
    }
}
