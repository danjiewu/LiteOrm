using System;
using System.Collections.Generic;
using System.Text;

namespace MyOrm.Service
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface, Inherited = true)]
    public class TransactionAttribute : Attribute
    {
        public TransactionAttribute()
        {
            IsTransaction = true;
        }

        public TransactionAttribute(bool isTransaction)
        {
            IsTransaction = isTransaction;
        }

        public bool IsTransaction { get; set; }
    }

}
