using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 集合操作类型枚举
    /// </summary>
    public enum SelectSetType
    {
        /// <summary>
        /// 并集所有（包含重复行）
        /// </summary>
        UnionAll,
        /// <summary>
        /// 并集（去除重复行）
        /// </summary>
        Union,
        /// <summary>
        /// 交集
        /// </summary>
        Intersect,
        /// <summary>
        /// 差集
        /// </summary>
        Except
    }
    /// <summary>
    /// 选择片段，表示 SELECT 语句
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public class SelectExpr : SqlSegment, ISourceAnchor, ISelectAnchor
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
        /// 获取片段类型，返回 Select 类型标识
        /// </summary>
        public override ExprType ExprType => ExprType.Select;

        /// <summary>
        /// 获取或设置要选择的字段表达式列表
        /// </summary>
        public List<SelectItemExpr> Selects { get; set; } = new List<SelectItemExpr>();

        /// <summary>
        /// 获取或设置 SelectExpr 的别名
        /// </summary>
        public string Alias { get; set; }

        private List<SelectExpr> _nextSelects;
        /// <summary>
        /// 后续的 Select 表达式列表（用于表示多项集合操作链），每个元素自身包含 SetType，表示与前一查询之间的集合运算符
        /// 懒加载：若为 null 则在 getter 中创建新列表。
        /// </summary>
        public List<SelectExpr> NextSelects
        {
            get => _nextSelects ??= new List<SelectExpr>();
            set => _nextSelects = value;
        }
        /// <summary>
        /// 当与后续 select 连用时，表示连接类型（UNION / INTERSECT / EXCEPT），通常由 NextSelects 中的节点决定
        /// </summary>
        public SelectSetType SetType { get; set; }
        /// <summary>
        /// 判断两个 SelectExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is SelectExpr other && Equals(Source, other.Source) && Selects.SequenceEqual(other.Selects) && Alias == other.Alias;

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(SelectExpr).GetHashCode(), Source?.GetHashCode() ?? 0, SequenceHash(Selects), Alias?.GetHashCode() ?? 0);

        /// <summary>
        /// 返回选择片段的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            string selectPart = $"SELECT {string.Join(", ", Selects)} FROM {Source}";
            if (!string.IsNullOrEmpty(Alias))
            {
                selectPart += $" AS {Alias}";
            }
            if (_nextSelects != null && _nextSelects.Count > 0)
            {
                foreach (var nxt in _nextSelects)
                {
                    string op = nxt.SetType switch
                    {
                        SelectSetType.Union => "UNION",
                        SelectSetType.UnionAll => "UNION ALL",
                        SelectSetType.Intersect => "INTERSECT",
                        SelectSetType.Except => "EXCEPT",
                        _ => "UNION"
                    };
                    selectPart += $" {op} {nxt}";
                }
            }
            return selectPart;
        }

        /// <summary>
        /// 克隆 SelectExpr
        /// </summary>
        public override Expr Clone()
        {
            var s = new SelectExpr();
            s.Source = (SqlSegment)(Source as Expr)?.Clone() ?? Source;
            s.Alias = Alias;
            s.Selects = Selects?.Select(si => (SelectItemExpr)si.Clone()).ToList() ?? new List<SelectItemExpr>();
            s.SetType = SetType;
            if (_nextSelects != null)
            {
                s._nextSelects = _nextSelects.Select(ns => (SelectExpr)ns.Clone()).ToList();
            }
            return s;
        }
        
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
            Alias = aliasName;
        }

        /// <summary>
        /// 获取或设置选择项的值表达式
        /// </summary>
        public new ValueTypeExpr Value { get; set; }

        private string _alias;

        /// <summary>
        /// 获取或设置选择项的别名
        /// </summary>
        public string Alias
        {
            get => _alias;
            set
            {
                ThrowIfInvalidSqlName(nameof(Alias), value);
                _alias = value;
            }
        }

        /// <summary>
        /// 判断两个 SelectItemExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is SelectItemExpr other && Alias == other.Alias && Equals(Value, other.Value);

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(SelectItemExpr).GetHashCode(), Alias?.GetHashCode() ?? 0, Value?.GetHashCode() ?? 0);

        /// <summary>
        /// 返回选择项的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString() => string.IsNullOrEmpty(Alias) ? Value?.ToString() : $"{Value} AS {Alias}";

        /// <summary>
        /// 表达式类型标识
        /// </summary>
        public override ExprType ExprType => global::LiteOrm.Common.ExprType.SelectItem;

        /// <summary>
        /// 克隆 SelectItemExpr
        /// </summary>
        public override Expr Clone()
        {
            return new SelectItemExpr((ValueTypeExpr)Value.Clone(), Alias);
        }
    }
}