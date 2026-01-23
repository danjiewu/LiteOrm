using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 外部表信息，用于描述关联的外部表
    /// </summary>
    public class ForeignTable
    {
        /// <summary>
        /// 外部表对应的实体类型
        /// </summary>
        public Type ForeignType { get; set; }

        /// <summary>
        /// 过滤表达式，用于定义关联条件
        /// </summary>
        public string FilterExpression { get; set; }
    }
}
