using System;
using System.Reflection;

namespace MyOrm.Common
{
    /// <summary>
    /// 表信息提供类
    /// </summary>
    public abstract class TableInfoProvider
    {
        /// <summary>
        /// 获取对象类型所对应的表定义
        /// </summary>
        /// <param name="objectType">对象类型</param>
        /// <returns>对应的表信息</returns>
        public abstract TableDefinition GetTableDefinition(Type objectType);

        /// <summary>
        /// 获取表信息
        /// </summary>
        /// <param name="objectType">对象类型</param>
        /// <returns></returns>
        public abstract TableView GetTableView(Type objectType);
    }
}
