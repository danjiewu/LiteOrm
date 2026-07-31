using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace LiteOrm.Common
{
    /// <summary>
    /// SQL 参数。包含参数名、值及可选的数据库类型。
    /// </summary>
    public class Param
    {
        /// <summary>
        /// 参数名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 参数值。
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// 可选的数据库类型。设置后将在创建数据库参数时指定 <see cref="System.Data.Common.DbParameter.DbType"/>。
        /// </summary>
        public DbType? DbType { get; set; }

        /// <summary>
        /// 初始化 <see cref="Param"/> 类的新实例。
        /// </summary>
        /// <param name="name">参数名称。</param>
        /// <param name="value">参数值。</param>
        /// <param name="dbType">可选的数据库类型。</param>
        public Param(string name, object? value, DbType? dbType = null)
        {
            Name = name;
            Value = value;
            DbType = dbType;
        }

        /// <summary>
        /// 从 <see cref="KeyValuePair{TKey, TValue}"/> 隐式转换为 <see cref="Param"/>。
        /// </summary>
        public static implicit operator Param(KeyValuePair<string, object> kv)
            => new Param(kv.Key, kv.Value);

        /// <summary>
        /// 从 <see cref="Param"/> 隐式转换为 <see cref="KeyValuePair{TKey, TValue}"/>。
        /// </summary>
        public static implicit operator KeyValuePair<string, object>(Param p)
            => new KeyValuePair<string, object>(p.Name, p.Value!);

        /// <inheritdoc/>
        public override string ToString() => $"{Name}={Value}";
    }

    /// <summary>
    /// 包含命名参数的 SQL 语句。
    /// </summary>
    public class PreparedSql
    {
        /// <summary>
        /// 使用指定的 SQL 文本和参数列表初始化 <see cref="PreparedSql"/>。
        /// </summary>
        /// <param name="sql">SQL 文本片段。</param>
        /// <param name="paramsList">参数化查询所需的参数集合。</param>
        public PreparedSql(string sql, IEnumerable<Param> paramsList)
        {
            Sql = sql;
            Params = paramsList.ToList();
        }

        /// <summary>
        /// 使用指定的 SQL 文本和键值对参数列表初始化 <see cref="PreparedSql"/>。
        /// </summary>
        /// <param name="sql">SQL 文本片段。</param>
        /// <param name="paramsList">参数化查询所需的键值对集合。</param>
        public PreparedSql(string sql, IEnumerable<KeyValuePair<string, object>> paramsList)
        {
            Sql = sql;
            Params = paramsList.Select(kv => (Param)kv).ToList();
        }

        /// <summary>
        /// 获取生成的 SQL 语句。
        /// </summary>
        public string Sql { get; }

        /// <summary>
        /// 获取 SQL 语句中引用的参数列表（按 0, 1, 2... 命名，或按数据库方言命名）。
        /// </summary>
        public List<Param> Params { get; }

        /// <summary>
        /// 返回调试友好的生成的 SQL 及其参数列表。
        /// </summary>
        public override string ToString()
        {
            return $"SQL: {Sql} \nParams : {String.Join("\n", Params)}";
        }
    }
}
