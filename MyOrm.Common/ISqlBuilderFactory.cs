using MyOrm.Common;
using System;

namespace MyOrm
{
    public interface ISqlBuilderFactory
    {
        ISqlBuilder GetSqlBuilder(Type providerType);
    }
}