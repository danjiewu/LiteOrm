using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示外键关联查询表达式。
    /// 该表达式通常用于构建基于 EXISTS 子查询的关联表过滤条件。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class ForeignExpr : Expr
    {
        /// <summary>
        /// 获取或设置针对关联表的内部过滤表达式。
        /// </summary>
        public Expr InnerExpr { get; set; }

        /// <summary>
        /// 获取或设置当前实体中定义关联关系的属性名称（该属性需标记 ForeignType 特性）。
        /// </summary>
        public new string Foreign { get; set; }

        /// <summary>
        /// 初始化 <see cref="ForeignExpr"/> 类的新实例。
        /// </summary>
        public ForeignExpr() { }

        /// <summary>
        /// 使用指定的外键属性名和内部表达式初始化 <see cref="ForeignExpr"/> 类的新实例。
        /// </summary>
        /// <param name="foreign">外键属性名称。</param>
        /// <param name="expr">内部过滤表达式。</param>
        public ForeignExpr(string foreign, Expr expr)
        {
            Foreign = foreign;
            InnerExpr = expr;
        }

        /// <summary>
        /// 比较两个 ForeignExpr 是否相等。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is ForeignExpr f && f.Foreign == Foreign && Equals(f.InnerExpr, InnerExpr);
        }

        /// <summary>
        /// 生成哈希值。
        /// </summary>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), Foreign?.GetHashCode() ?? 0, InnerExpr?.GetHashCode() ?? 0);
        }
    }
}
