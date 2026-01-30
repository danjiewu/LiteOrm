using System;
using System.Reflection;

namespace LiteOrm.Common
{

    /// <summary>
    /// 基本列信息
    /// </summary>
    public abstract class SqlColumn : SqlObject
    {
        private PropertyInfo _property;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="property">列对应的实体属性</param>
        internal SqlColumn(PropertyInfo property)
        {
            _property = property;
            PropertyName = Name = property.Name;
        }

        /// <summary>
        /// 所属的表信息
        /// </summary>
        public SqlTable Table { get; internal set; }

        /// <summary>
        /// 属性名
        /// </summary>
        public string PropertyName { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public abstract ColumnDefinition Definition { get; }
        /// <summary>
        /// 列所对应的属性类型
        /// </summary>
        /// 
        public Type PropertyType
        {
            get { return _property.PropertyType; }
        }

        /// <summary>
        /// 列对应的属性
        /// </summary>
        public PropertyInfo Property { get { return _property; } }

        /// <summary>
        /// 关联的外部对象类型
        /// </summary>
        public Type ForeignType { get { return ForeignTable?.ForeignType; } }

        /// <summary>
        /// 关联的外部表信息
        /// </summary>
        public ForeignTable ForeignTable { get; internal set; }

        /// <summary>
        /// 关联的外部对象别名
        /// </summary>
        public string ForeignAlias { get; internal set; }

        /// <summary>
        /// 赋值
        /// </summary>
        /// <param name="target">要赋值的对象</param>
        /// <param name="value">值</param>
        public virtual void SetValue(object target, object value)
        {
            if (target == null) throw new ArgumentNullException("target");
            try
            {
                Property.SetValueFast(target, value);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Value {value} can not be assigned to {Property.DeclaringType.Name}.{Property.Name}", e);
            }
        }


        /// <summary>
        /// 取值
        /// </summary>
        /// <param name="target">对象</param>
        /// <returns>值</returns>
        public virtual object GetValue(object target)
        {
            if (target == null) throw new ArgumentNullException("target");
            return Property.GetValueFast(target);
        }
    }
}
