using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 选择片段，表示 SELECT 语句
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public class SelectExpr : ValueTypeExpr, ISourceAnchor
    {
        /// <summary>
        /// 初始化 SelectExpr 类的新实例
        /// </summary>
        public SelectExpr() { }

        /// <summary>
        /// 使用指定的源片段和选择字段列表初始化 SelectExpr 类的新实例
        /// </summary>
        /// <param name="source">源片段</param>
        /// <param name="selects">要选择的字段表达式列表</param>
        public SelectExpr(ISqlSegment source, params SelectItemExpr[] selects)
        {
            Source = source;
            Selects = selects?.ToList() ?? new List<SelectItemExpr>();
        }

        /// <summary>
        /// 获取或设置查询的源片段（From表达式）
        /// </summary>
        public ISqlSegment Source { get; set; }

        /// <summary>
        /// 使用指定的源片段和选择字段列表初始化 SelectExpr 类的新实例
        /// </summary>
        /// <param name="source">源片段</param>
        /// <param name="selects">要选择的字段表达式列表</param>
        public SelectExpr(ISqlSegment source, params ValueTypeExpr[] selects)
        {
            Source = source;
            Selects = selects?.Select(s => s is SelectItemExpr si ? si : new SelectItemExpr(s)).ToList() ?? new List<SelectItemExpr>();
        }

        /// <summary>
        /// 获取片段类型，返回 Select 类型标识
        /// </summary>
        public SqlSegmentType SegmentType => SqlSegmentType.Select;

        /// <summary>
        /// 获取或设置要选择的字段表达式列表
        /// </summary>
        public List<SelectItemExpr> Selects { get; set; } = new List<SelectItemExpr>();

        /// <summary>
        /// 判断两个 SelectExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is SelectExpr other && Equals(Source, other.Source) && Selects.SequenceEqual(other.Selects);

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(SelectExpr).GetHashCode(), Source?.GetHashCode() ?? 0, SequenceHash(Selects));

        /// <summary>
        /// 返回选择片段的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString() => $"SELECT {string.Join(", ", Selects)} FROM {Source}";
    }

    /// <summary>
    /// 选择项表达式，表示 SELECT 字段及其可选别名
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public class SelectItemExpr : ValueTypeExpr
    {
        /// <summary>
        /// 初始化 SelectItemExpr 类的新实例
        /// </summary>
        /// <param name="value">值表达式</param>
        /// <exception cref="ArgumentNullException">当 value 为 null 时抛出</exception>
        public SelectItemExpr(ValueTypeExpr value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            Value = value;
        }

        /// <summary>
        /// 初始化 SelectItemExpr 类的新实例（带别名）
        /// </summary>
        /// <param name="value">值表达式</param>
        /// <param name="aliasName">别名名称</param>
        /// <exception cref="ArgumentNullException">当 value 为 null 时抛出</exception>
        public SelectItemExpr(ValueTypeExpr value, string aliasName)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            Value = value;
            Name = aliasName;
        }

        /// <summary>
        /// 获取或设置选择项的值表达式
        /// </summary>
        public new ValueTypeExpr Value { get; set; }

        private string _name;
        
        /// <summary>
        /// 获取或设置选择项的别名
        /// </summary>
        public string Name 
        { 
            get => _name;
            set
            {
                if (value != null && !LiteOrm.Common.Const.ValidNameRegex.IsMatch(value))
                {
                    throw new ArgumentException("Alias name contains illegal characters. Only letters, numbers, and underscores are allowed.", nameof(Name));
                }
                _name = value;
            }
        }

        /// <summary>
        /// 判断两个 SelectItemExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is SelectItemExpr other && Name == other.Name && Equals(Value, other.Value);

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(SelectItemExpr).GetHashCode(), Name?.GetHashCode() ?? 0, Value?.GetHashCode() ?? 0);

        /// <summary>
        /// 返回选择项的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString() => string.IsNullOrEmpty(Name) ? Value?.ToString() : $"{Value} AS {Name}";
    }
}
