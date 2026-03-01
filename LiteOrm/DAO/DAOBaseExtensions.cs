#if NET8_0_OR_GREATER || NET10_0_OR_GREATER
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LiteOrm.Common;

namespace LiteOrm
{
    public static class DAOBaseExtensions
    {
        public static DbCommandProxy MakeExprCommand(this DAOBase dao, [InterpolatedStringHandlerArgument("dao")] ref ExprInterpolatedStringHandler handler)
        {
            return dao.MakeNamedParamCommand(handler.GetSqlResult(), handler.GetParams());
        }
    }
}
#endif
