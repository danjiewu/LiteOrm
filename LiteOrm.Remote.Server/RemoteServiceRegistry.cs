using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace LiteOrm.Service
{
    /// <summary>
    /// 远程服务注册表。维护 ServiceName → 服务接口类型的映射，供服务端根据请求中的 ServiceName 解析目标服务类型。
    /// 支持开放泛型接口（如 <c>IEntityService&lt;&gt;</c>）的注册与闭合查找：
    /// 注册时按基名（如 "IEntityService"）存储开放泛型定义；
    /// 查找时若精确匹配失败，则解析类型参数并构造闭合泛型类型（如 <c>IEntityService&lt;User&gt;</c>）。
    /// </summary>
    public class RemoteServiceRegistry
    {
        private readonly Dictionary<string, Type> _services = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Type> _openGenerics = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, Type> _closedGenericCache = new();

        /// <summary>
        /// 注册一个服务接口类型。
        /// 非泛型类型按 <see cref="RemoteServiceNameUtil.GetServiceName"/> 生成的 ServiceName 存储；
        /// 开放泛型定义按基名（去除 `n 后缀）存储，查找时再动态构造闭合类型。
        /// </summary>
        /// <param name="serviceInterfaceType">服务接口类型。</param>
        public void Register(Type serviceInterfaceType)
        {
            if (serviceInterfaceType is null) throw new ArgumentNullException(nameof(serviceInterfaceType));

            if (serviceInterfaceType.IsGenericTypeDefinition)
            {
                var baseName = GetBaseName(serviceInterfaceType);
                _openGenerics[baseName] = serviceInterfaceType;
            }
            else
            {
                var name = RemoteServiceNameUtil.GetServiceName(serviceInterfaceType);
                _services[name] = serviceInterfaceType;
            }
        }

        /// <summary>
        /// 注册一个服务接口类型。
        /// </summary>
        /// <typeparam name="TServiceInterface">服务接口类型。</typeparam>
        public void Register<TServiceInterface>() => Register(typeof(TServiceInterface));

        /// <summary>
        /// 尝试根据 ServiceName 获取服务接口类型。
        /// 查找顺序：闭合泛型缓存 → 精确匹配 → 开放泛型匹配（解析类型参数并构造闭合类型）。
        /// </summary>
        /// <param name="serviceName">服务名称。</param>
        /// <param name="serviceType">输出参数，匹配到的服务接口类型。</param>
        /// <returns>是否找到匹配的服务。</returns>
        public bool TryGetServiceType(string serviceName, out Type serviceType)
        {
            if (string.IsNullOrEmpty(serviceName))
            {
                serviceType = null!;
                return false;
            }

            // 闭合泛型缓存
            if (_closedGenericCache.TryGetValue(serviceName, out serviceType!))
                return true;

            // 精确匹配（非泛型或已注册的闭合泛型）
            if (_services.TryGetValue(serviceName, out serviceType!))
                return true;

            // 开放泛型匹配：解析 "IEntityService<User>" → baseName="IEntityService", args=["User"]
            var ltIndex = serviceName.IndexOf('<');
            if (ltIndex > 0)
            {
                var baseName = serviceName.Substring(0, ltIndex);
                if (_openGenerics.TryGetValue(baseName, out var openGeneric))
                {
                    var gtIndex = serviceName.LastIndexOf('>');
                    if (gtIndex > ltIndex)
                    {
                        var argsPart = serviceName.Substring(ltIndex + 1, gtIndex - ltIndex - 1);
                        var argNames = argsPart.Split(',').Select(s => s.Trim()).ToArray();
                        var genericParams = openGeneric.GetGenericArguments();

                        if (genericParams.Length == argNames.Length)
                        {
                            var typeArgs = new Type[argNames.Length];
                            bool allResolved = true;
                            for (int i = 0; i < argNames.Length; i++)
                            {
                                typeArgs[i] = FindTypeByName(argNames[i]);
                                if (typeArgs[i] is null)
                                {
                                    allResolved = false;
                                    break;
                                }
                            }

                            if (allResolved)
                            {
                                serviceType = openGeneric.MakeGenericType(typeArgs);
                                _closedGenericCache[serviceName] = serviceType;
                                return true;
                            }
                        }
                    }
                }
            }

            serviceType = null!;
            return false;
        }

        /// <summary>
        /// 获取已注册的所有服务名称（包含非泛型服务名与开放泛型基名）。
        /// </summary>
        public IEnumerable<string> GetRegisteredServiceNames() => _services.Keys.Concat(_openGenerics.Keys);

        private static string GetBaseName(Type type)
        {
            int backtickIndex = type.Name.IndexOf('`');
            return backtickIndex > 0 ? type.Name.Substring(0, backtickIndex) : type.Name;
        }

        /// <summary>
        /// 按类型名称查找类型。先尝试全名精确匹配，再回退到短名匹配。
        /// </summary>
        private static Type FindTypeByName(string typeName)
        {
            // 精确全名匹配
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }

            // 短名匹配
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var match = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                    if (match != null) return match;
                }
                catch (ReflectionTypeLoadException)
                {
                    // 跳过加载失败的程序集
                }
            }

            return null!;
        }
    }
}
