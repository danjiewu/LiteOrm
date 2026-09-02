using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// <see cref="ITypeNameResolver"/> 的初始化工具。
    /// <para>
    /// 提供一个「扫描类型 → 筛选 → 自定义名称生成 → 注册」的链式流程，
    /// 将符合条件的类型批量注册到 <see cref="TypeResolverHelper"/> 的全局名称映射
    /// （所有基于 <see cref="TypeResolverHelper"/> 的解析器，如 <see cref="DefaultTypeNameResolver"/> 与
    /// <see cref="DefaultServiceTypeResolver"/>，都会优先命中这些自定义注册）。
    /// </para>
    /// <para>
    /// 典型用途：
    /// <list type="bullet">
    /// <item>AOT / 裁剪模式下，编译期源生成器之外的运行时「显式类型传入」或「程序集扫描 + 预注册」，
    /// 使 <see cref="TypeResolverHelper.FindType"/> 无需反射即可解析自定义类型；</item>
    /// <item>为起止进程（客户端与服务端）统一注册业务 DTO / 多态参数类型，保证跨进程类型名一致；</item>
    /// <item>为同名类型提供不带冲突的自定义名称（键）。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 注意：扫描程序集并反射获取类型在 AOT/裁剪环境下不可用，此时应使用
    /// <see cref="ScanTypes(IEnumerable{Type})"/> 显式传入类型，或依赖源生成器预注册。
    /// </para>
    /// </summary>
    public static class TypeResolverInitializer
    {
        /// <summary>
        /// 开始扫描一组程序集中的类型，返回类型扫描构建器。
        /// </summary>
        /// <param name="assemblies">要扫描的程序集。</param>
        /// <returns>类型扫描构建器。</returns>
#if NET7_0_OR_GREATER
        [RequiresDynamicCode("Scanning assemblies for types via reflection requires dynamic code; AOT publish should use ScanTypes with explicitly known types.")]
#endif
        public static TypeScanBuilder Scan(params Assembly[] assemblies) => new TypeScanBuilder().From(assemblies);

        /// <summary>
        /// 开始扫描一组程序集中的类型，返回类型扫描构建器。
        /// </summary>
        /// <param name="assemblies">要扫描的程序集。</param>
        /// <returns>类型扫描构建器。</returns>
#if NET7_0_OR_GREATER
        [RequiresDynamicCode("Scanning assemblies for types via reflection requires dynamic code; AOT publish should use ScanTypes with explicitly known types.")]
#endif
        public static TypeScanBuilder Scan(IEnumerable<Assembly> assemblies) => new TypeScanBuilder().From(assemblies);

        /// <summary>
        /// 从一组显式提供的类型开始，返回类型扫描构建器。
        /// <para>适用于 AOT/裁剪模式或不想反射整个程序集的情形。</para>
        /// </summary>
        /// <param name="types">显式提供的类型。</param>
        /// <returns>类型扫描构建器。</returns>
        public static TypeScanBuilder ScanTypes(IEnumerable<Type> types) => new TypeScanBuilder().FromTypes(types);
    }

    /// <summary>
    /// 类型扫描与注册构建器（由 <see cref="TypeResolverInitializer"/> 创建）。
    /// <para>
    /// 以链式方式依次指定类型来源、筛选条件与名称生成方式，最后调用 <see cref="Register"/> 批量注册
    /// 到 <see cref="TypeResolverHelper"/>。
    /// </para>
    /// </summary>
    public sealed class TypeScanBuilder
    {
        private readonly HashSet<Type> _types = new();
        private Func<Type, bool>? _filter;
        private Func<Type, string>? _nameSelector;

        internal TypeScanBuilder() { }

        /// <summary>
        /// 追加要扫描的程序集（其所有类型都作为候选，进入后续筛选）。
        /// </summary>
#if NET7_0_OR_GREATER
        [RequiresDynamicCode("Scanning assemblies for types via reflection requires dynamic code; AOT publish should use ScanTypes with explicitly known types.")]
#endif
        public TypeScanBuilder From(params Assembly[] assemblies)
        {
            if (assemblies is null) throw new ArgumentNullException(nameof(assemblies));
            foreach (var asm in assemblies)
            {
                if (asm is null) continue;
                foreach (var type in GetLoadableTypes(asm))
                    _types.Add(type);
            }
            return this;
        }

        /// <summary>
        /// 追加要扫描的程序集（其所有类型都作为候选，进入后续筛选）。
        /// </summary>
#if NET7_0_OR_GREATER
        [RequiresDynamicCode("Scanning assemblies for types via reflection requires dynamic code; AOT publish should use ScanTypes with explicitly known types.")]
#endif
        public TypeScanBuilder From(IEnumerable<Assembly> assemblies)
        {
            if (assemblies is null) throw new ArgumentNullException(nameof(assemblies));
            foreach (var asm in assemblies)
            {
                if (asm is null) continue;
                foreach (var type in GetLoadableTypes(asm))
                    _types.Add(type);
            }
            return this;
        }

        /// <summary>
        /// 追加一组显式提供的类型（进入后续筛选）。
        /// </summary>
        public TypeScanBuilder FromTypes(IEnumerable<Type> types)
        {
            if (types is null) throw new ArgumentNullException(nameof(types));
            foreach (var type in types)
            {
                if (type is not null)
                    _types.Add(type);
            }
            return this;
        }

        /// <summary>
        /// 追加一条类型筛选条件。可多次调用，多个条件按「且」关系叠加。
        /// </summary>
        /// <param name="predicate">类型筛选谓词，返回 true 表示保留该类型。</param>
        public TypeScanBuilder Where(Func<Type, bool> predicate)
        {
            if (predicate is null) throw new ArgumentNullException(nameof(predicate));
            // 将组合前的谓词捕获进闭包，避免引用会变化的 _filter 字段导致自递归
            var previous = _filter;
            _filter = previous is null ? predicate : t => previous(t) && predicate(t);
            return this;
        }

        /// <summary>
        /// 便捷筛选：只保留可以赋予 <paramref name="type"/>（是它的子类或实现）的具体类，
        /// 且不是该类型自身。
        /// </summary>
        public TypeScanBuilder WhereConcreteAssignableTo(Type type)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));
            return Where(t => t != type && t.IsClass && !t.IsAbstract && type.IsAssignableFrom(t));
        }

        /// <summary>
        /// 便捷筛选：只保留可以赋予类型参数 <typeparamref name="T"/> 的具体类
        /// （T 为接口或基类），且不是 T 自身。
        /// </summary>
        public TypeScanBuilder WhereConcreteAssignableTo<T>() => WhereConcreteAssignableTo(typeof(T));

        /// <summary>
        /// 自定义名称（键）生成方式。默认使用 <see cref="TypeResolverHelper.GetName(Type)"/> 生成的短名。
        /// <para>
        /// 注册后，<see cref="TypeResolverHelper.GetName"/> 对这类类型返回这里生成的自定义名称，
        /// <see cref="TypeResolverHelper.FindType"/> 亦可通过该名称解析回原类型。
        /// </para>
        /// </summary>
        /// <param name="nameSelector">从类型生成名称（键）的委托。</param>
        public TypeScanBuilder NamedBy(Func<Type, string> nameSelector)
        {
            _nameSelector = nameSelector ?? throw new ArgumentNullException(nameof(nameSelector));
            return this;
        }

        /// <summary>
        /// 执行注册：将当前候选类型按名称生成方式逐一注册到 <see cref="TypeResolverHelper"/>。
        /// </summary>
        /// <returns>实际注册的类型数量。</returns>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2073",
            Justification = "Runtime-scanned/collected types are stored without static Type annotations, then registered into the global TypeResolverHelper map; under AOT use ScanTypes with known types so required members are preserved, making these points carried through dynamic/annotation-loss edges unreachable.")]
        [UnconditionalSuppressMessage("Trimming", "IL2072",
            Justification = "See IL2073 suppression: types flowing from the un-annotated HashSet<Type> back into Register(All) is an intentional runtime-registration boundary.")]
#endif
        public int Register()
        {
            var nameSelector = _nameSelector ?? TypeResolverHelper.GetName;
            int count = 0;
            foreach (var type in _types)
            {
                if (_filter is not null && !_filter(type))
                    continue;
                TypeResolverHelper.Register(nameSelector(type), type);
                count++;
            }
            return count;
        }

        /// <summary>
        /// 获取已扫描的候选类型（未应用筛选）。
        /// </summary>
        internal IReadOnlyCollection<Type> CandidateTypes => _types;

        /// <summary>
        /// 安全地获取程序集中的类型；因依赖加载失败产生的类型会自动忽略，不中断整体扫描。
        /// </summary>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2026",
            Justification = "Assembly.GetTypes is only reached when RuntimeFeature.IsDynamicCodeSupported is true (JIT); under AOT this method returns empty before reaching it.")]
#endif
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
                throw new InvalidOperationException("Dynamic code generation is not supported on this platform.");
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t is not null)!;
            }
            catch
            {
                return Enumerable.Empty<Type>();
            }
        }
    }
}