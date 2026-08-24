using System;
using System.Diagnostics.CodeAnalysis;

namespace LiteOrm.Common
{
    /// <summary>
    /// 关联的外部实体属性定义
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class ForeignColumnAttribute : Attribute
    {
        /// <summary>
        /// 初始化 <see cref="ForeignColumnAttribute"/> 类的新实例。
        /// </summary>
        /// <param name="foreignType">关联的外部对象类型</param>
        public ForeignColumnAttribute(Type foreignType)
        {
            this.Foreign = foreignType;
        }

        /// <summary>
        /// 初始化 <see cref="ForeignColumnAttribute"/> 类的新实例。
        /// </summary>
        /// <param name="foreignName">关联的外部表名称</param>
        public ForeignColumnAttribute(string foreignName)
        {
            this.Foreign = foreignName;
        }

        /// <summary>
        /// 关联的外部表，可以为外部表对应的Type，也可以为TableJoin中的Alias
        /// </summary>
        public object Foreign { get; private set; }

        ///<summary>
        /// 外部实体的属性名称
        /// </summary>
        public string? Property { get; set; }

        /// <summary>
        /// 获取或设置列值转换器类型。该类型须实现 <see cref="IDbValueConverter"/> 并具有公共无参构造函数，
        /// 由表信息提供器实例化后赋给 <see cref="SqlColumn.DbValueConverter"/>。
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
        public Type? ConverterType { get; set; }
    }
}
