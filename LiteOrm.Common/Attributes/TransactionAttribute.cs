using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm.Service
{
    /// <summary>
    /// 事务特性，用于标记需要事务支持的方法、类或接口
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface, Inherited = true)]
    public class TransactionAttribute : Attribute
    {
        /// <summary>
        /// 初始化 <see cref="TransactionAttribute"/> 类的新实例，默认启用事务
        /// </summary>
        public TransactionAttribute()
        {
            IsTransaction = true;
        }

        /// <summary>
        /// 初始化 <see cref="TransactionAttribute"/> 类的新实例，并指定是否启用事务
        /// </summary>
        /// <param name="isTransaction">是否启用事务</param>
        public TransactionAttribute(bool isTransaction)
        {
            IsTransaction = isTransaction;
        }

        /// <summary>
        /// 获取或设置是否启用事务
        /// </summary>
        public bool IsTransaction { get; set; }
    }

}
