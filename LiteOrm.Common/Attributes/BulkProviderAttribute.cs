using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 标记批量插入提供程序对应的数据库连接类型。
    /// <para>该特性由 <c>BulkProviderFactory</c> 读取，用于将批量插入提供程序实现
    /// 映射到对应的数据库连接类型。它替代了原先依赖
    /// <c>AutoRegisterAttribute.Key</c> 的标记方式，使 <c>LiteOrm</c> 核心不再依赖
    /// <c>LiteOrm.Framework</c> 的特性定义。</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class BulkProviderAttribute : Attribute
    {
        /// <summary>
        /// 获取该批量插入提供程序对应的数据库连接类型。
        /// </summary>
        public Type DbConnectionType { get; }

        /// <summary>
        /// 初始化 <see cref="BulkProviderAttribute"/> 类的新实例。
        /// </summary>
        /// <param name="dbConnectionType">数据库连接类型。</param>
        public BulkProviderAttribute(Type dbConnectionType)
        {
            DbConnectionType = dbConnectionType ?? throw new ArgumentNullException(nameof(dbConnectionType));
        }
    }
}