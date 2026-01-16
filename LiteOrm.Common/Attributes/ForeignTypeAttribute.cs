using System;
using System.Collections.Generic;
using System.Text;

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
        /// 附加筛选条件
        /// </summary>
        public string FilterExpression { get; set; }
    }
}
