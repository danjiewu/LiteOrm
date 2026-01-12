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
        /// 获取对象类型所对应的表定义。
        /// </summary>
        /// <param name="objectType">实体对象类型。</param>
        /// <returns>返回对应的 <see cref="TableDefinition"/> 信息。</returns>
        public abstract TableDefinition GetTableDefinition(Type objectType);

        /// <summary>
        /// 获取指定类型的视图信息（包含关联查询信息）。
        /// </summary>
        /// <param name="objectType">实体对象类型。</param>
        /// <returns>返回对应的 <see cref="TableView"/> 信息。</returns>
        public abstract TableView GetTableView(Type objectType);
    }
}
