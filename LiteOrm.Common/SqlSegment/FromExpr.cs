using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// From 片段，表示查询的数据源（由主表和连接表构成）
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class FromExpr : Expr, ISourceAnchor, ISqlSegment, IArged
    {
        public FromExpr() { }

        public FromExpr(Type objectType)
        {
            Table = new TableExpr(objectType);
        }

        /// <summary>
        /// 主表表达式
        /// </summary>
        public TableExpr Table { get; set; }

        /// <summary>
        /// 连接表集合
        /// </summary>
        public List<TableJoinExpr> Joins { get; set; } = new List<TableJoinExpr>();

        // 保持向后兼容的便利属性，委托到主表
        public string Alias
        {
            get => Table?.Alias;
            set
            {
                if (Table == null) Table = new TableExpr();
                Table.Alias = value;
            }
        }

        public Type ObjectType
        {
            get => Table?.ObjectType;
            set
            {
                if (Table == null) Table = new TableExpr();
                Table.ObjectType = value;
            }
        }

        public string[] TableArgs
        {
            get => Table?.TableArgs;
            set
            {
                if (Table == null) Table = new TableExpr();
                Table.TableArgs = value;
            }
        }

        public override ExprType ExprType => ExprType.From;

        /// <summary>
        /// 以前的 Source 属性保留以兼容旧代码
        /// </summary>
        public ISqlSegment Source { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is FromExpr other)
            {
                if (!Equals(Table, other.Table)) return false;
                if (!Joins.SequenceEqual(other.Joins)) return false;
                if (!Equals(Source, other.Source)) return false;
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return OrderedHashCodes(
                typeof(FromExpr).GetHashCode(),
                Table?.GetHashCode() ?? 0,
                SequenceHash(Joins),
                Source?.GetHashCode() ?? 0);
        }

        public override string ToString()
        {
            if (Table == null) return string.Empty;
            if (Joins == null || Joins.Count == 0) return Table.ToString();
            return Table + " " + string.Join(" ", Joins);
        }

        public override Expr Clone()
        {
            var f = new FromExpr();
            f.Table = (TableExpr)(Table as Expr)?.Clone() ?? Table;
            f.Joins = Joins?.Select(j => (TableJoinExpr)j.Clone()).ToList() ?? new List<TableJoinExpr>();
            f.Source = (ISqlSegment)(Source as Expr)?.Clone() ?? Source;
            return f;
        }
    }
}
