using LiteOrm.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LiteOrm
{
    /// <summary>
    /// 函数 SQL 生成委托，将函数表达式直接写入 <see cref="ValueStringBuilder"/>。
    /// </summary>
    /// <param name="outSql"></param>
    /// <param name="expr"></param>
    /// <param name="context"></param>
    /// <param name="sqlBuilder"></param>
    /// <param name="outputParams"></param>
    public delegate void FunctionSqlHandler(ref ValueStringBuilder outSql, FunctionExpr expr, SqlBuildContext context, SqlBuilder sqlBuilder, ICollection<Param> outputParams);

    /// <summary>
    /// 简单函数 SQL 生成委托，直接提供函数名称和参数列表，适用于仅需调整函数格式，不需要自定义解析参数的场景。
    /// </summary>
    /// <param name="outSql"></param>
    /// <param name="functionName"></param>
    /// <param name="arguments"></param>
    public delegate void SimpleFunctionSqlHandler(ref ValueStringBuilder outSql, string functionName, ICollection<string> arguments);

    internal class SqlHandlerMap
    {
        private readonly ConcurrentDictionary<string, FunctionSqlHandler> _functionSqlHandlers = new ConcurrentDictionary<string, FunctionSqlHandler>(StringComparer.OrdinalIgnoreCase);

        public void RegisterFunctionSqlHandler(string functionName, FunctionSqlHandler handler)
        {
            if (string.IsNullOrWhiteSpace(functionName)) throw new ArgumentNullException(nameof(functionName));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _functionSqlHandlers[functionName] = handler;
        }

        public bool TryGetFunctionSqlHandler(string functionName, out FunctionSqlHandler? handler)
        {
            return _functionSqlHandlers.TryGetValue(functionName, out handler);
        }
    }   
}
