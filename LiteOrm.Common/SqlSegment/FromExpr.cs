using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Collections.ObjectModel;

namespace LiteOrm.Common
{
    /// <summary>
    /// From 片段，表示查询的数据源（由主表和连接表构成）
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class FromExpr : SqlSegment, ISourceAnchor
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
        /// 根据主表表达式初始化
        /// </summary>
        /// <param name="source">主表表达式</param>
        public FromExpr(SourceExpr source)
        {
            Source = source;
        }

        /// <summary>
        /// 使用源片段重写 Source 属性，确保它始终是一个 SourceExpr 类型
        /// </summary>
        public new SourceExpr Source { get => (SourceExpr)base.Source; set => base.Source = (SourceExpr)value; }       

        private List<TableJoinExpr> _joins = new List<TableJoinExpr>();
        /// <summary>
        /// 连接表集合
        /// </summary>
        public List<TableJoinExpr> Joins => _joins;

        /// <summary>
        /// 表达式类型
        /// </summary>
        public override ExprType ExprType => ExprType.From;

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
                if (_joins is null && other._joins is not null) return false;
                if (_joins is not null && other._joins is null) return false;
                if (_joins is not null && other._joins is not null && !_joins.SequenceEqual(other._joins)) return false;
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
                SequenceHash(_joins),
                Source?.GetHashCode() ?? 0);
        }

        /// <summary>
        /// 返回 FromExpr 的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            if (Source == null) return string.Empty;
            return Source.ToString();
        }

        /// <summary>
        /// 克隆 FromExpr
        /// </summary>
        public override Expr Clone()
        {
            var f = new FromExpr();
            f.Source = (TableExpr)Source?.Clone();
            f._joins = _joins?.Select(j => (TableJoinExpr)j.Clone()).ToList();
            return f;
        }
    }
}