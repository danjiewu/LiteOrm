using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class TableJoinExpr : Expr
    {
        public TableJoinExpr() { }

        public TableJoinExpr(TableExpr table, LogicExpr on)
        {
            Table = table;
            On = on;
        }

        public TableExpr Table { get; set; }

        public LogicExpr On { get; set; }

        /// <summary>
        /// 连接类型（如 INNER/LEFT/RIGHT/OUTER/CROSS）
        /// </summary>
        public TableJoinType JoinType { get; set; } = TableJoinType.Left;

        public override ExprType ExprType => ExprType.TableJoin;

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

        public override int GetHashCode()
        {
            return OrderedHashCodes(typeof(TableJoinExpr).GetHashCode(), Table?.GetHashCode() ?? 0, On?.GetHashCode() ?? 0);
        }

        public override string ToString()
        {
            return Table == null ? string.Empty : $"JOIN {Table} ON {On}";
        }

        public override Expr Clone()
        {
            var j = new TableJoinExpr();
            j.Table = (TableExpr)(Table as Expr)?.Clone() ?? Table;
            j.On = (LogicExpr)(On as Expr)?.Clone() ?? On;
            return j;
        }
    }
}
