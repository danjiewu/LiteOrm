using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 更新片段，表示 UPDATE 语句
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public class UpdateExpr : SqlSegment
    {
        /// <summary>
        /// 初始化 UpdateExpr 类的新实例
        /// </summary>
        public UpdateExpr() { }

        /// <summary>
        /// 使用指定的源片段和筛选条件初始化 UpdateExpr 类的新实例
        /// </summary>
        /// <param name="table">源片段</param>
        /// <param name="where">筛选条件表达式</param>
        public UpdateExpr(TableExpr table, LogicExpr where = null)
        {
            Table = table;
            Where = where;
        }

        /// <summary>
        /// 获取或设置更新操作的源片段（TableExpr）
        /// </summary>
        [JsonIgnore]
        public TableExpr Table { get; set; }

        /// <summary>
        /// 使用主表表达式重写源片段属性
        /// </summary>
        public override SqlSegment Source { get => Table; set => Table = (TableExpr)value; }


        /// <summary>
        /// 获取片段类型，返回 Update 类型标识
        /// </summary>
        public override ExprType ExprType => ExprType.Update;

        /// <summary>
        /// 获取或设置要更新的字段和值列表
        /// </summary>
        public List<SetItem> Sets { get; set; } = new List<SetItem>();

        /// <summary>
        /// 获取或设置筛选条件表达式
        /// </summary>
        public LogicExpr Where { get; set; }

        /// <summary>
        /// 判断两个 UpdateExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is UpdateExpr other
            && Equals(Table, other.Table)
            && Sets.SequenceEqual(other.Sets)
            && Equals(Where, other.Where);

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(UpdateExpr).GetHashCode(), Table?.GetHashCode() ?? 0, SequenceHash(Sets), Where?.GetHashCode() ?? 0);

        /// <summary>
        /// 返回更新片段的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            string setStr = Sets is null ? string.Empty : " SET " + string.Join(", ", Sets.Select(s => $"{s.Property} = {s.Value}"));
            return $"UPDATE {Table}{setStr}{(Where != null ? $" WHERE {Where}" : "")}";
        }

        /// <summary>
        /// 克隆 UpdateExpr
        /// </summary>
        public override Expr Clone()
        {
            var u = new UpdateExpr();
            u.Table = (TableExpr)Table?.Clone();
            u.Where = (LogicExpr)Where?.Clone();
            u.Sets = Sets?.Select(s => new SetItem((PropertyExpr)s.Property?.Clone(), (ValueTypeExpr)s.Value?.Clone())).ToList() ?? new List<SetItem>();
            return u;
        }
    }

    /// <summary>
    /// 表示要更新的字段和值的结构体
    /// </summary>
    public struct SetItem
    {
        /// <summary>
        /// 将属性表达式和值表达式的元组隐式转换为 SetItem 结构体实例
        /// </summary>
        /// <param name="tuple">包含属性表达式和值表达式的元组</param>
        public static implicit operator SetItem(Tuple<PropertyExpr, ValueTypeExpr> tuple) => new SetItem { Property = tuple.Item1, Value = tuple.Item2 };

        /// <summary>
        /// 将 SetItem 结构体实例隐式转换为包含属性表达式和值表达式的元组
        /// </summary>
        /// <param name="item">要转换的 SetItem 实例</param>
        public static implicit operator Tuple<PropertyExpr, ValueTypeExpr>(SetItem item) => new Tuple<PropertyExpr, ValueTypeExpr>(item.Property, item.Value);
        /// <summary>
        /// 获取或设置要更新的字段表达式
        /// </summary>
        public PropertyExpr Property { get; set; }
        /// <summary>
        /// 获取或设置要更新的值表达式
        /// </summary>
        public ValueTypeExpr Value { get; set; }
        /// <summary>
        /// 初始化 SetItem 结构的新实例
        /// </summary>
        /// <param name="property">要更新的字段表达式</param>
        /// <param name="value">要更新的值表达式</param>
        public SetItem(PropertyExpr property, ValueTypeExpr value)
        {
            Property = property;
            Value = value;
        }
        /// <summary>
        /// 返回 SetItem 的字符串表示，格式为 "Property = Value"
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString() => $"{Property} = {Value}";
    }
}