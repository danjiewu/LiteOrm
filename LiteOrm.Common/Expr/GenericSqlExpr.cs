using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;


namespace LiteOrm.Common
{
    using SqlGenerateHandler = Func<SqlBuildContext, ISqlBuilder, ICollection<KeyValuePair<string, object>>, object, string>;
    /// <summary>
    /// 通过委托生成的 SQL 片段表达式类。
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class GenericSqlExpr : Expr
    {
        /// <summary>
        /// 默认构造函数。
        /// </summary>
        public GenericSqlExpr() { }
        /// <summary>
        /// 使用委托构造，返回生成 SQL 时所需的动态片段字符串。
        /// </summary>
        /// <param name="key">表达式唯一键。</param>
        public GenericSqlExpr(string key)
        {
            Key = key;
        }

        /// <summary>
        /// 生成该 SQL 所需委托的内部参数。
        /// </summary>
        public object Arg { get; set; }

        private SqlGenerateHandler SqlHandler => String.IsNullOrEmpty(Key) ? null : _registry[Key].SqlHandler;

        /// <summary>
        /// 表达式唯一键。
        /// </summary> 
        public string Key { get; set; }

        /// <summary>
        /// 表示该表达式是否为值类型表达式。
        /// </summary>
        public override bool IsValue => String.IsNullOrEmpty(Key) ? false : _registry[Key].IsValue;


        internal string GenerateSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            return SqlHandler?.Invoke(context, sqlBuilder, outputParams, Arg);
        }

        /// <summary>
        /// 返回表示当前表达式的字符串。
        /// </summary>
        /// <returns>表示当前表达式的字符串。</returns>
        public override string ToString()
        {
            return $"[Sql:{Key}]";
        }

        /// <summary>
        /// 确定指定的对象是否等于当前对象。
        /// </summary>
        /// <param name="obj">要与当前对象进行比较的对象。</param>
        /// <returns>如果指定的对象等于当前对象，则为 true；否则为 false。</returns>
        public override bool Equals(object obj)
        {
            return obj is GenericSqlExpr g && g.Key == Key;
        }

        /// <summary>
        /// 获取默认哈希码。
        /// </summary>
        /// <returns>当前对象的哈希代码。</returns>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), Key?.GetHashCode() ?? 0);
        }

        #region 静态注册表
        private class InnerSqlExpr
        {
            public bool IsValue { get; set; }
            public SqlGenerateHandler SqlHandler { get; set; }
        }

        private static readonly ConcurrentDictionary<string, InnerSqlExpr> _registry = new ConcurrentDictionary<string, InnerSqlExpr>();

        /// <summary>
        /// 注册一个动态 SQL 表达式类。
        /// <param name="key">表达式唯一键。</param>
        /// <param name="func">生成该表达式 SQL 字符串的委托。</param> 
        /// <param name="isValue">指示该表达式是否为值类型表达式。</param>
        /// </summary>
        public static GenericSqlExpr Register(string key, SqlGenerateHandler func, bool isValue = false)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (func is null) throw new ArgumentNullException(nameof(func));
            if (_registry.ContainsKey(key))
            {
                return new GenericSqlExpr(key);
            }
            _registry[key] = new InnerSqlExpr { IsValue = isValue, SqlHandler = func };
            return new GenericSqlExpr(key);
        }

        ///<summary>
        /// 获取已注册的静态 SQL 表达式。
        /// <para name="key">表达式唯一键。</para>
        /// </summary>
        public static GenericSqlExpr GetStaticSqlExpr(string key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (_registry.TryGetValue(key, out var expr))
            {
                return new GenericSqlExpr(key);
            }
            throw new KeyNotFoundException($"键 '{key}' 在注册表中未找到。");
        }

        /// <summary>
        /// 获取已注册的静态 SQL 表达式并设置参数。
        /// </summary>
        /// <param name="key">表达式唯一键。</param>
        /// <param name="arg">参数。</param>
        /// <returns>GenericSqlExpr 实例。</returns>
        public static GenericSqlExpr Get(string key, object arg)
        {
            var expr = GetStaticSqlExpr(key);
            expr.Arg = arg;
            return expr;
        }
        #endregion
    }
}
