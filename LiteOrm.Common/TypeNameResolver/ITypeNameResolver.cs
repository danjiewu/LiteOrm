using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 提供类型名称与 <see cref="Type"/> 之间的双向解析能力。
    /// <para>
    /// 用于 JSON 序列化等场景中，将 <see cref="Type"/> 转换为可存储的名称（正向），
    /// 以及将名称还原为 <see cref="Type"/>（反向）。
    /// </para>
    /// <para>
    /// 默认实现 <see cref="DefaultTypeNameResolver"/> 基于 <see cref="TypeResolverHelper"/>，
    /// 支持自定义注册（<see cref="TypeResolverHelper.Register(string, Type)"/>）和缓存。
    /// </para>
    /// </summary>
    public interface ITypeNameResolver
    {
        /// <summary>
        /// 获取类型的可序列化名称。
        /// </summary>
        /// <param name="type">要获取名称的类型。</param>
        /// <returns>类型的可序列化名称。</returns>
        string GetName(Type type);

        /// <summary>
        /// 根据名称解析类型。
        /// </summary>
        /// <param name="name">类型名称（全名、短名或程序集限定名）。</param>
        /// <returns>匹配到的类型；未找到时返回 null。</returns>
        Type? GetType(string name);
    }
}
