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
        public FromExpr() { Source = new TableExpr(); }

        public FromExpr(Type objectType)
        {
            Source = new TableExpr(objectType);
        }

        /// <summary>
        /// 主表表达式
        /// </summary>
        public TableExpr Source
        {
            get
            {
                if (field is null) field = new TableExpr();
                return field;
            }
            set;
        }

        /// <summary>
        /// 连接表集合
        /// </summary>
        public List<TableJoinExpr> Joins { get; set; } = new List<TableJoinExpr>();

        // 保持向后兼容的便利属性，委托到主表
        public string Alias
        {
            get => Source?.Alias;
            set { Source.Alias = value; }
        }

        public Type ObjectType
        {
            get => Source?.ObjectType;
            set
            { Source.ObjectType = value; }
        }

        public string[] TableArgs
        {
            get => Source?.TableArgs;
            set
            { Source.TableArgs = value; }
        }

        public override ExprType ExprType => ExprType.From;

        /// <summary>
        /// 以前的 Source 属性保留以兼容旧代码
        /// </summary>
        ISqlSegment ISqlSegment.Source { get => Source; set => Source = (TableExpr)value; }

        public override bool Equals(object obj)
        {
            if (obj is FromExpr other)
            {
                if (!Equals(Source, other.Source)) return false;
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
                Source?.GetHashCode() ?? 0,
                SequenceHash(Joins),
                Source?.GetHashCode() ?? 0);
        }

        public override string ToString()
        {
            if (Source == null) return string.Empty;
            if (Joins == null || Joins.Count == 0) return Source.ToString();
            return Source + " " + string.Join(" ", Joins);
        }

        public override Expr Clone()
        {
            var f = new FromExpr();
            f.Source = (TableExpr)Source?.Clone();
            f.Joins = Joins?.Select(j => (TableJoinExpr)j.Clone()).ToList() ?? new List<TableJoinExpr>();
            return f;
        }
    }
}
