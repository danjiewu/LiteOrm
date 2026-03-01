#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LiteOrm.Common;

namespace LiteOrm.Common
{
    [InterpolatedStringHandler]
    public ref struct ExprString
    {
        private ValueStringBuilder _builder;
        private readonly List<KeyValuePair<string, object>> _params =  new List<KeyValuePair<string, object>>();
        private readonly SqlBuildContext _context;
        private readonly ISqlBuilder _sqlBuilder;

        public ExprString(int literalLength, int formattedCount, IExprStringBuildContext context)
        {
            _builder = ValueStringBuilder.Create(literalLength + formattedCount * 16);
            _context = context.CreateSqlBuildContext();
            _sqlBuilder = context.SqlBuilder;
        }

        public void AppendLiteral(string literal)
        {
            _builder.Append(literal);
        }

        public void AppendFormatted<T>(T value)
        {
            if (value is Expr expr)
            {
                expr.ToSql(ref _builder, _context, _sqlBuilder, _params);
            }
            else if (value != null)
            {
                string paramName = $"@p{_params.Count}";
                _builder.Append(paramName);
                _params.Add(new KeyValuePair<string, object>(paramName, value));
            }
        }

        public string GetSqlResult()
        {
            return _builder.ToString();
        }

        public List<KeyValuePair<string, object>> GetParams() => _params;

        public void Dispose()
        {
            _builder.Dispose();
        }
    }
}
#endif
