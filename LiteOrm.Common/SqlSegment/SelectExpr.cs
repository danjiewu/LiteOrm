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
    public class SelectExpr : SqlSegment, ISelectAnchor
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
        public SelectExpr(SqlSegment source, params SelectItemExpr[] selects)
        {
            Source = source;
            Selects = selects?.ToList() ?? new List<SelectItemExpr>();
        }

        /// <summary>
        /// 使用指定的源片段和选择字段列表初始化 SelectExpr 类的新实例
        /// </summary>
        /// <param name="source">源片段</param>
        /// <param name="selects">要选择的字段表达式列表</param>
        public SelectExpr(SqlSegment source, params ValueTypeExpr[] selects)
        {
            Source = source;
            Selects = selects?.Select(s => s is SelectItemExpr si ? si : new SelectItemExpr(s)).ToList() ?? new List<SelectItemExpr>();
        }

        /// <summary>
        /// 获取一个值，指示此表达式是否为值类型
        /// </summary>
        public override bool IsValue => true;

        /// <summary>
        /// 获取片段类型，返回 Select 类型标识
        /// </summary>
        public override SqlSegmentType SegmentType => SqlSegmentType.Select;

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

    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public class SelectItemExpr : ValueTypeExpr
    {
        public SelectItemExpr(ValueTypeExpr value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            Value = value;
        }

        public SelectItemExpr(ValueTypeExpr value, string aliasName)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            Value = value;
            Name = aliasName;
        }
        public ValueTypeExpr Value { get; set; }
        public string Name { get; set; }
        public override bool Equals(object obj) => obj is SelectItemExpr other && Name == other.Name && Equals(Value, other.Value);
        public override int GetHashCode() => OrderedHashCodes(typeof(SelectItemExpr).GetHashCode(), Name?.GetHashCode() ?? 0, Value?.GetHashCode() ?? 0);
        public override string ToString() => string.IsNullOrEmpty(Name) ? Value?.ToString() : $"{Value} AS {Name}";
    }
}
