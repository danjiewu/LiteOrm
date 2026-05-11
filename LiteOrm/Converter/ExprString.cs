using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LiteOrm.Common;

#if NETSTANDARD2_0 || NETSTANDARD2_1 
namespace System.Runtime.CompilerServices
{
    // 这个 Attribute 告诉编译器：这是一个字符串插值处理器
    /// <summary>指示某个类或结构是字符串插值处理器。</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class InterpolatedStringHandlerAttribute : Attribute
    {
    }

    /// <summary>指示应将涉及内插字符串处理程序的方法的哪些参数传递给该处理程序。</summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class InterpolatedStringHandlerArgumentAttribute : Attribute
    {
        /// <summary>初始化新实例，该实例指定应传递给处理程序的单个参数。</summary>
        /// <param name="argument">应传递给处理程序的参数的名称。 对于实例方法，可以使用空字符串 ("") 来引用接收方。</param>
        public InterpolatedStringHandlerArgumentAttribute(string argument)
        {
            Arguments = new string[] { argument };
        }

        /// <summary>初始化新实例，该实例指定应传递给处理程序的多个参数。</summary>
        /// <param name="arguments">应传递给处理程序的参数的名称。 对于实例方法，可以使用空字符串 ("") 来引用接收方。</param>
        public InterpolatedStringHandlerArgumentAttribute(params string[] arguments)
        {
            Arguments = arguments;
        }

        /// <summary>获取应传递给处理程序的参数的名称。</summary>
        public string[] Arguments { get; }
    }
}
#endif

namespace LiteOrm.Common
{
    /// <summary>
    /// 插值字符串处理器，编译时生成，可插入普通变量或 Expr 类型对象构建SQL语句。
    /// 1.Expr 转换为等效 SQL 片段，可以仅插入字段表达式，也可以插入复杂表达式，例如 $"WHERE {Expr.Prop("Age") &gt; 18}" 会转化为 "WHERE Age &gt; @0"并且参数列表包含 @0=18，而 $"WHERE {Expr.Prop("Age")} &gt; 18" 会转化为 "WHERE Age &gt; 18"；
    /// 2.普通值（int、string 等）自动转为命名参数如 @0，防止 SQL 注入，例如 $"WHERE Age &gt; {18}" 转化为 "WHERE Age &gt; @0"，并且参数列表包含 @0=18；
    /// 3.手写 SQL 片段中的 '['、']' 可作为通用引用符占位，在最终执行命令时由当前数据库的 <see cref="ISqlBuilder"/> 替换为真实引用符。
    /// </summary>
    /// <remarks>
    /// 插值字符串处理器按顺序处理插值字符串中的文本和格式化项。对于每个格式化项，如果是 Expr 类型，则调用其 ToSql 方法将其转换为 SQL 片段；如果是普通值，则生成一个参数占位符并将值添加到参数列表中。最终生成的 SQL 字符串和参数列表可以通过 GetSqlResult 和 GetParams 方法获取。
    /// <strong>注意：</strong>Expr 对象依赖构建上下文来处理表达式中的表别名、参数命名等细节，而插值字符串是按顺序访问格式化项，没有作用域概念，因此可能造成表别名不能按作用域自动匹配。
    /// 例如 SelectExpr 在 FromExpr 之前插入，造成解析 SelectExpr 时无法预测对应 FromExpr 自动分配到的表别名。因此对于涉及多个表的复杂查询，建议预先设置好 FromExpr、PropertyExpr 等 Expr 对象的表别名，避免由上下文自动分配。
    /// ExprString创建时已经在上下文中注册主表及别名"T0"，直接在格式化项中再插入主表可能造成主表分配到的表别名错误，可给要插入的主表FromExpr对象预先设定别名"T0"，也可以改用{{From}}占位符。
    /// </remarks>
    [InterpolatedStringHandler]
    public ref struct ExprString
    {
        private ValueStringBuilder _builder;
        private readonly List<KeyValuePair<string, object>> _params = new List<KeyValuePair<string, object>>();
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
            _context = context.CreateSqlBuildContext(true);
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
                string paramName = $"{_params.Count}";
                _builder.Append(_sqlBuilder.ToSqlParam(paramName));
                _params.Add(new KeyValuePair<string, object>(paramName, value));
            }
        }

        /// <summary>
        /// 获取SQL结果字符串
        /// </summary>
        /// <returns>SQL语句字符串</returns>
        public string GetSql()
        {
            return _builder.ToString();
        }

        /// <summary>
        /// 获取参数列表
        /// </summary>
        /// <returns>参数列表</returns>
        public List<KeyValuePair<string, object>> GetParams() => _params;
        /// <summary>
        /// 获取预处理的 SQL 语句和参数列表
        /// </summary>
        /// <returns>预处理的 SQL 对象</returns>

        public PreparedSql GetResult() => new PreparedSql(_builder.ToString(), _params);

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _builder.Dispose();
        }
    }
}
