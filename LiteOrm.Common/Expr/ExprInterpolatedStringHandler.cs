#if !NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LiteOrm.Common
{
    [InterpolatedStringHandler]
    public ref struct ExprInterpolatedStringHandler
    {
        private ValueStringBuilder _builder;
        private readonly List<KeyValuePair<string, object>> _params =  new List<KeyValuePair<string, object>>();
        private readonly SqlBuildContext _context;
        private readonly ISqlBuilder _sqlBuilder;

        public ExprInterpolatedStringHandler(int literalLength, int formattedCount, DAOBase dao)
        {
            _builder = ValueStringBuilder.Create(literalLength + formattedCount * 16);
            _context = dao.Context;
            _sqlBuilder = dao.SqlBuilder;
        }

        public void AppendLiteral(string literal)
        {
            _builder.Append(literal);
        }

        public void AppendFormatted(Expr expr)
        {
            if (expr != null)
            {
                expr.ToSql(ref _builder, _context, _sqlBuilder, _params);
            }
        }

        public void AppendFormatted<T>(T value)
        {
            if (value is Expr expr)
            {
                AppendFormatted(expr);
            }
            else if (value != null)
            {
                var paramName = $"p{_params.Count}";
                _builder.Append(_sqlBuilder.ToSqlParamName(paramName));
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
