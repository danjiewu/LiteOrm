using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 关联的外部实体类型定义
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ForeignTypeAttribute : System.Attribute
    {
        private Type _objectType;
        /// <summary>
        /// 关联的外部实体类型
        /// </summary>
        /// <param name="objectType">外部实体的类型</param>
        public ForeignTypeAttribute(Type objectType)
        {
            this._objectType = objectType;
        }

        /// <summary>
        /// 外部实体类型
        /// </summary>
        public Type ObjectType
        {
            get { return _objectType; }
        }

        /// <summary>
        /// 别名
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// 联合查询连接类型（如 Left Join）。
        /// </summary>
        public TableJoinType JoinType { get; set; } = TableJoinType.Left;

        /// <summary>
        /// 是否自动扩展连接的外表。当AutoExpand为true并且作为外表被引用时，自动将本表关联的外表引入连接。默认为false，即不自动扩展连接的外表。
        /// </summary>
        public bool AutoExpand { get; set; } = false;
    }
}
