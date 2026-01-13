using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;


namespace LiteOrm.Common
{
    using SqlExpression = Expression<Func<SqlBuildContext, ISqlBuilder, ICollection<KeyValuePair<string, object>>, string>>;
    /// <summary>
    /// 通过委托生成的 SQL 片段表达式。
    /// </summary>
    public sealed class StaticSqlExpr : Expr
    {
        /// <summary>
        /// 默认构造函数。
        /// </summary>
        public StaticSqlExpr() { }
        /// <summary>
        /// 使用委托构造，可以在生成 SQL 时依据上下文动态生成字符串。
        /// </summary>
        /// <param name="key">表达式的唯一键</param>
        public StaticSqlExpr(string key)
        {
            Key = key;
        }

        private SqlExpression SqlHandler => String.IsNullOrEmpty(Key) ? null : registry[Key];
        /// <summary>
        /// 表达式的唯一键
        /// 
        public string Key { get; set; }
        /// <inheritdoc/>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            return SqlHandler?.Compile()?.Invoke(context, sqlBuilder, outputParams);
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
            return obj is StaticSqlExpr g && g.Key == Key;
        }

        /// <summary>
        /// 作为默认哈希函数。
        /// </summary>
        /// <returns>当前对象的哈希代码。</returns>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), Key?.GetHashCode() ?? 0);
        }

        #region 静态注册表
        private static ConcurrentDictionary<string, SqlExpression> registry = new ConcurrentDictionary<string, SqlExpression>();

        /// <summary>
        /// 注册一个静态 SQL 表达式。
        /// <param name="key">表达式的唯一键</param>
        /// <param name="func">处理上下文并返回 SQL 字符串的委托</param> 
        /// </summary>
        public static StaticSqlExpr Register(string key, SqlExpression func)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (func is null) throw new ArgumentNullException(nameof(func));
            if (registry.ContainsKey(key)) throw new ArgumentException($"键'{key}'已存在注册表中", nameof(key));
            var expr = new StaticSqlExpr(key);
            registry[key] = func;
            return expr;
        }

        /// <summary>
        /// 
        public static StaticSqlExpr GetStaticSqlExpr(string key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (registry.TryGetValue(key, out var expr))
            {
                return new StaticSqlExpr(key);
            }
            throw new KeyNotFoundException($"键'{key}'在注册表中未找到");
        }
        #endregion
    }
}
