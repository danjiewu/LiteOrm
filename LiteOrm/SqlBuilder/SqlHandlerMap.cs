using LiteOrm.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LiteOrm
{
    internal class SqlHandlerMap
    {
        private readonly ConcurrentDictionary<string, Func<string, IList<KeyValuePair<string, Expr>>, string>> FunctionSqlHandlers = new ConcurrentDictionary<string, Func<string, IList<KeyValuePair<string, Expr>>, string>>(StringComparer.OrdinalIgnoreCase);
        public void RegisterFunctionSqlHandler(string functionName, Func<string, IList<KeyValuePair<string, Expr>>, string> handler)
        {
            if (string.IsNullOrWhiteSpace(functionName)) throw new ArgumentNullException(nameof(functionName));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            FunctionSqlHandlers[functionName] = handler;
        }

        public bool TryGetFunctionSqlHandler(string functionName, out Func<string, IList<KeyValuePair<string, Expr>>, string> handler)
        {
            return FunctionSqlHandlers.TryGetValue(functionName, out handler);
        }
    }
}
