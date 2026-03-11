using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace LiteOrm
{
    /// <summary>
    /// 程序集分析器
    /// </summary>
    public static class AssemblyAnalyzer
    {
        /// <summary>
        /// 获取所有直接引用的程序集名称及当前加载的程序集
        /// </summary>
        /// <param name="entryAssembly">入口程序集</param>
        /// <returns>所有相关的程序集集合</returns>
        public static IEnumerable<Assembly> GetAllReferencedAssemblies(Assembly entryAssembly = null)
        {
            var result = new HashSet<Assembly>();
            var visited = new HashSet<Assembly>();

            // 自动加上 LiteOrm 和 LiteOrm.Common 的 Assembly
            result.Add(typeof(LiteOrmServiceProviderExtensions).Assembly);
            result.Add(typeof(AutoRegisterAttribute).Assembly);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && !IsSystemAssembly(assembly))
                {
                    result.Add(assembly);
                }
            }

            entryAssembly ??= Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                ScanAssemblies(entryAssembly, result, visited);
            }

            return result;
        }

        private static void ScanAssemblies(Assembly assembly, HashSet<Assembly> result, HashSet<Assembly> visited)
        {
            if (!visited.Add(assembly)) return;
            if (IsSystemAssembly(assembly)) return;

            result.Add(assembly);

            foreach (var referencedName in assembly.GetReferencedAssemblies())
            {
                try
                {
                    var referencedAssembly = Assembly.Load(referencedName);
                    ScanAssemblies(referencedAssembly, result, visited);
                }
                catch { }
            }
        }

        private static bool IsSystemAssembly(Assembly a)
        {
            var name = a.FullName;
            return name.StartsWith("System.") ||
                   name.StartsWith("Microsoft.") ||
                   name.StartsWith("mscorlib") ||
                   name.StartsWith("netstandard") ||
                   name.StartsWith("Autofac.") ||
                   name.StartsWith("Castle.") ||
                   name.StartsWith("xunit.");
        }
    }
}
