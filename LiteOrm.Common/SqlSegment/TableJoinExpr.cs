using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表连接表达式
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class TableJoinExpr : SqlSegment
    {
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public TableJoinExpr() { }

        /// <summary>
        /// 根据源片段和连接条件初始化
        /// </summary>
        /// <param name="source">源片段</param>
        /// <param name="on">连接条件</param>
        public TableJoinExpr(SourceExpr source, LogicExpr on)
        {
            base.Source = source;
            On = on;
        }


        /// <summary>
        /// 使用源片段重写 Source 属性，确保它始终是一个 SourceExpr 类型
        /// </summary>
        public new SourceExpr Source { get => (SourceExpr)base.Source; set => base.Source = (SourceExpr)value; }

        /// <summary>
        /// 连接条件
        /// </summary>
        public LogicExpr On { get; set; }

        /// <summary>
        /// 连接类型（如 INNER/LEFT/RIGHT/OUTER/CROSS）
        /// </summary>
        public TableJoinType JoinType { get; set; } = TableJoinType.Left;

        /// <summary>
        /// 表达式类型
        /// </summary>
        public override ExprType ExprType => ExprType.TableJoin;

        /// <summary>
        /// 判断两个 TableJoinExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj)
        {
            if (obj is TableJoinExpr other)
            {
                if (!Equals(Source, other.Source)) return false;
                if (!Equals(JoinType, other.JoinType)) return false;
                if (!Equals(On, other.On)) return false;
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
            return OrderedHashCodes(typeof(TableJoinExpr).GetHashCode(), JoinType.GetHashCode(), base.Source?.GetHashCode() ?? 0, On?.GetHashCode() ?? 0);
        }

        /// <summary>
        /// 返回 TableJoinExpr 的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            if (base.Source == null) return string.Empty;
            return On == null ? $"{JoinType} JOIN {base.Source}" : $"{JoinType} JOIN {base.Source} ON {On}";
        }

        /// <summary>
        /// 克隆 TableJoinExpr
        /// </summary>
        public override Expr Clone()
        {
            var j = new TableJoinExpr();
            j.Source = (SourceExpr)Source?.Clone();
            j.On = (LogicExpr)On?.Clone();
            j.JoinType = JoinType;
            return j;
        }
    }
}
