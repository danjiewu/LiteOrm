using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 提供按名称解析 <see cref="Type"/> 上属性的扩展方法。
    /// 采用 <see cref="BindingFlags.DeclaredOnly"/> 沿继承链从最派生类型逐级向上查找，
    /// 天然规避派生类用 <c>new</c> 隐藏基类同名属性时 <see cref="Type.GetProperty(string, BindingFlags)"/>
    /// 所抛的 <see cref="AmbiguousMatchException"/>，且优先返回当前层（最派生）上真正生效的属性。
    /// </summary>
    public static class PropertyLookup
    {
        /// <summary>
        /// 按名称解析属性（扩展方法）。失败返回 <c>null</c>。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <param name="name">属性名。</param>
        /// <param name="flags">绑定标志；为 <see cref="BindingFlags.Default"/> 时按公共实例/静态解析。</param>
        [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "type comes from GetType()/typeof/an annotated parameter; entity properties are preserved by the source generator under AOT and can be reflected safely.")]
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Static analysis cannot carry annotations while cascading along BaseType, but properties on entity inheritance chains are all preserved by the source generator under AOT.")]
        public static PropertyInfo? Find(this Type type, string name, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
        {
            if (string.IsNullOrEmpty(name)) return null;

            if (flags == BindingFlags.Default)
                flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

            // DeclaredOnly 只返回当前层自身的定义，不会命中被 new 隐藏的基类同名属性，
            // 因此不会触发多重匹配；本层未找到时再逐级向上查找基类。
            BindingFlags levelFlags = flags | BindingFlags.DeclaredOnly;
            for (Type? current = type; current is not null; current = current.BaseType)
            {
                PropertyInfo? property = current.GetProperty(name, levelFlags, null, null, Type.EmptyTypes, null);
                if (property is not null) return property;
            }
            return null;
        }
    }
}