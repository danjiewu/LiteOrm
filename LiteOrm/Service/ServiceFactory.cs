using Microsoft.Extensions.DependencyInjection;
using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteOrm.Service
{
    /// <summary>
    /// 服务工厂接口。
    /// </summary>
    public interface IServiceFactory
    {
        /// <summary>
        /// 获取指定类型的服务。
        /// </summary>
        /// <typeparam name="T">服务类型。</typeparam>
        /// <returns>服务实例。</returns>
        T GetService<T>() where T : class;

        /// <summary>
        /// 获取指定类型的服务。
        /// </summary>
        /// <param name="serviceType">服务类型。</param>
        /// <returns>服务实例。</returns>
        object GetService(Type serviceType);
    }
}
