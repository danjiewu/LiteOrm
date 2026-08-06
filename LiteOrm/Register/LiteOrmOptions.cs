using Microsoft.Extensions.DependencyInjection;
using System;

namespace LiteOrm
{
    /// <summary>
    /// <see cref="LiteOrmServiceExtensions.AddLiteOrm(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{LiteOrmOptions})"/>
    /// 的配置选项。
    /// </summary>
    public class LiteOrmOptions
    {
        /// <summary>
        /// 是否自动注册自定义服务与 DAO（由 LiteOrm.Generators 源生成器在编译期生成注册代码）。
        /// 默认为 <c>true</c>。
        /// </summary>
        public bool AutoRegisterServices { get; set; } = true;

        /// <summary>
        /// 追加自定义服务注册的钩子，在 LiteOrm 核心服务注册完成后执行。
        /// </summary>
        public Action<IServiceCollection>? ConfigureServices { get; set; }
    }
}
