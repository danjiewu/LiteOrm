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
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public FromExpr() { Source = new TableExpr(); }

        /// <summary>
        /// 根据对象类型初始化
        /// </summary>
        /// <param name="objectType">对象类型</param>
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

        /// <summary>
        /// 别名
        /// </summary>
        public string Alias
        {
            get => Source?.Alias;
            set { Source.Alias = value; }
        }

        /// <summary>
        /// 对象类型
        /// </summary>
        public Type ObjectType
        {
            get => Source?.ObjectType;
            set
            { Source.ObjectType = value; }
        }

        /// <summary>
        /// 表参数
        /// </summary>
        public string[] TableArgs
        {
            get => Source?.TableArgs;
            set
            { Source.TableArgs = value; }
        }

        /// <summary>
        /// 表达式类型
        /// </summary>
        public override ExprType ExprType => ExprType.From;

        /// <summary>
        /// 获取或设置源片段（主表表达式）
        /// </summary>
        ISqlSegment ISqlSegment.Source { get => Source; set => Source = (TableExpr)value; }

        /// <summary>
        /// 判断两个 FromExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
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

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode()
        {
            return OrderedHashCodes(
                typeof(FromExpr).GetHashCode(),
                Source?.GetHashCode() ?? 0,
                SequenceHash(Joins),
                Source?.GetHashCode() ?? 0);
        }

        /// <summary>
        /// 返回 FromExpr 的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            if (Source == null) return string.Empty;
            if (Joins == null || Joins.Count == 0) return Source.ToString();
            return Source + " " + string.Join(" ", Joins);
        }

        /// <summary>
        /// 克隆 FromExpr
        /// </summary>
        public override Expr Clone()
        {
            var f = new FromExpr();
            f.Source = (TableExpr)Source?.Clone();
            f.Joins = Joins?.Select(j => (TableJoinExpr)j.Clone()).ToList() ?? new List<TableJoinExpr>();
            return f;
        }
    }
}
