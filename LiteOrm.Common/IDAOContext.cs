using System;

namespace LiteOrm.Common
{
    public interface IDAOContext
    {
        Type ObjectType { get; }

        SqlTable Table { get; }

        ISqlBuilder SqlBuilder { get; }

        SqlBuildContext CreateSqlBuildContext(bool initTable = false);

        TableInfoProvider TableInfoProvider { get; }
    }
}
