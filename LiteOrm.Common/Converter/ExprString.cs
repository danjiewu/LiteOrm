#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LiteOrm.Common;

namespace LiteOrm.Common
{
    /// <summary>
    /// 插值字符串处理器，用于在编译时构建SQL语句
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct ExprString
    {
        private ValueStringBuilder _builder;
        private readonly List<KeyValuePair<string, object>> _params =  new List<KeyValuePair<string, object>>();
        private readonly SqlBuildContext _context;
        private readonly ISqlBuilder _sqlBuilder;

        /// <summary>
        /// 初始化插值字符串处理器
        /// </summary>
        /// <param name="literalLength">字面量长度</param>
        /// <param name="formattedCount">格式化参数数量</param>
        /// <param name="context">表达式字符串构建上下文</param>
        public ExprString(int literalLength, int formattedCount, IExprStringBuildContext context)
        {
            _builder = ValueStringBuilder.Create(literalLength + formattedCount * 16);
            _context = context.CreateSqlBuildContext();
            _sqlBuilder = context.SqlBuilder;
        }

        /// <summary>
        /// 添加字面量字符串
        /// </summary>
        /// <param name="literal">字面量字符串</param>
        public void AppendLiteral(string literal)
        {
            _builder.Append(literal);
        }

        /// <summary>
        /// 添加格式化值
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="value">格式化值</param>
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

        /// <summary>
        /// 获取SQL结果字符串
        /// </summary>
        /// <returns>SQL语句字符串</returns>
        public string GetSqlResult()
        {
            return _builder.ToString();
        }

        /// <summary>
        /// 获取参数列表
        /// </summary>
        /// <returns>参数列表</returns>
        public List<KeyValuePair<string, object>> GetParams() => _params;

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _builder.Dispose();
        }
    }
}
#endif
