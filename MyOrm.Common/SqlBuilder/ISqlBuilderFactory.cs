using MyOrm.Common;
using System;

namespace MyOrm
{
    /// <summary>
    /// 提供根据数据库驱动类型获取相应 SQL 构建器的工厂接口。
    /// </summary>
    public interface ISqlBuilderFactory
    {
        /// <summary>
        /// 根据数据库提供程序类型获取对应的 SQL 构建器。
        /// </summary>
        /// <param name="providerType">数据库提供程序（如 SqlConnection）的类型。</param>
        /// <returns>返回适配该数据库的 SQL 构建器实例。</returns>
        ISqlBuilder GetSqlBuilder(Type providerType);
    }
}