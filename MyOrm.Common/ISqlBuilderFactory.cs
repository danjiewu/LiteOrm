using System;

namespace MyOrm
{
    public interface ISqlBuilderFactory
    {
        void RegisterSqlBuilder(Type providerType, SqlBuilder sqlBuilder);
        SqlBuilder GetSqlBuilder(Type providerType);
    }
}