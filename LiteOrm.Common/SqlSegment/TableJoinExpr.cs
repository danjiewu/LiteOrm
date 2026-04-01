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
        /// 根据表表达式和连接条件初始化
        /// </summary>
        /// <param name="table">表表达式</param>
        /// <param name="on">连接条件</param>
        public TableJoinExpr(TableExpr table, LogicExpr on)
        {
            Table = table;
            On = on;
        }

        /// <summary>
        /// 表表达式
        /// </summary>
        [JsonIgnore]
        public TableExpr Table { get; set; }

        /// <summary>
        /// 使用主表表达式重写源片段属性
        /// </summary>
        public override SqlSegment Source { get => Table; set => Table = (TableExpr)value; }

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
                if (!Equals(Table, other.Table)) return false;
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
            return OrderedHashCodes(typeof(TableJoinExpr).GetHashCode(), Table?.GetHashCode() ?? 0, On?.GetHashCode() ?? 0);
        }

        /// <summary>
        /// 返回 TableJoinExpr 的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            return Table == null ? string.Empty : $"JOIN {Table} ON {On}";
        }

        /// <summary>
        /// 克隆 TableJoinExpr
        /// </summary>
        public override Expr Clone()
        {
            var j = new TableJoinExpr();
            j.Table = (TableExpr)(Table as Expr)?.Clone() ?? Table;
            j.On = (LogicExpr)(On as Expr)?.Clone() ?? On;
            j.JoinType = JoinType;
            return j;
        }
    }
}
