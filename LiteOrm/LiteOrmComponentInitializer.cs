using Autofac;
using LiteOrm.Common;
using LiteOrm.Oracle;
using LiteOrm.SQLite;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;


namespace LiteOrm
{
    /// <summary>
    /// 在应用程序的依赖注入容器中为 LiteOrm 组件提供初始化逻辑。
    /// </summary>
    /// <remarks>此类通常注册为单例，负责在应用程序启动期间配置核心 LiteOrm 服务，例如会话管理和表信息提供程序。它应被用于确保 LiteOrm 组件在使用之前已正确设置。</remarks>
    [AutoRegister(Lifetime =ServiceLifetime.Singleton)]
    public class LiteOrmComponentInitializer : IComponentInitializer
    {
        
        /// <summary>
        /// 使用指定的组件上下文初始化应用程序范围的服务并注册自定义 SQL 函数。
        /// </summary>
        /// <remarks>此方法设置当前会话管理器和表信息提供程序，并注册用于 SQLite 查询的 'AddHours' SQL 函数。在应用程序启动期间调用此方法以确保所需服务可用。</remarks>
        /// <param name="componentContext">用于解析和配置所需服务的组件上下文。不能为 null。</param>
        public void Initialize(IComponentContext componentContext)
        {
            SessionManager.Current = componentContext.Resolve<SessionManager>();
            TableInfoProvider.Default = componentContext.Resolve<TableInfoProvider>();
            SQLiteBuilder.Instance.RegisterFunctionSqlHandler("AddHours", (functionName, args) => $"DATETIME({args[0]}, '+' || {args[1]} || ' hours')");
        }
    }
}
