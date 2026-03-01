using System;

namespace LiteOrm.Common
{
    public interface IExprStringBuildContext
    {
        ISqlBuilder SqlBuilder { get; }

        SqlBuildContext CreateSqlBuildContext(bool initTable = false);
    }
}
