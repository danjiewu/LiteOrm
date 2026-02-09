using System;
using System.Collections.Generic;
using System.Reflection;

namespace LiteOrm
{
    /// <summary>
    /// ³ÌÐò¼¯·ÖÎöÆ÷
    /// </summary>
    public static class AssemblyAnalyzer
    {
        /// <summary>
        /// »ñÈ¡ËùÓÐÖ±½ÓÒýÓÃµÄ³ÌÐò¼¯Ãû³Æ¼°µ±Ç°¼ÓÔØµÄ³ÌÐò¼¯
        /// </summary>
        /// <param name="entryAssembly">Èë¿Ú³ÌÐò¼¯</param>
        /// <returns>ËùÓÐÏà¹ØµÄ³ÌÐò¼¯¼¯ºÏ</returns>
        public static IEnumerable<Assembly> GetAllReferencedAssemblies(Assembly entryAssembly = null)
        {
            var result = new HashSet<Assembly>();

            // 1. »ñÈ¡ËùÓÐÒÑ¾­¼ÓÔØµÄ·ÇÏµÍ³³ÌÐò¼¯
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && !IsSystemAssembly(assembly))
                {
                    result.Add(assembly);
                }
            }

            // 2. ´ÓÈë¿Ú³ÌÐò¼¯¿ªÊ¼µÝ¹é²éÕÒ
            entryAssembly ??= Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                ScanAssemblies(entryAssembly, result);
            }

            return result;
        }

        private static void ScanAssemblies(Assembly assembly, HashSet<Assembly> result)
        {
            if (result.Contains(assembly)) return;
            if (IsSystemAssembly(assembly)) return;

            result.Add(assembly);

            foreach (var referencedName in assembly.GetReferencedAssemblies())
            {
                try
                {
                    var referencedAssembly = Assembly.Load(referencedName);
                    ScanAssemblies(referencedAssembly, result);
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
                   name.StartsWith("xunit.");
        }
    }
}
