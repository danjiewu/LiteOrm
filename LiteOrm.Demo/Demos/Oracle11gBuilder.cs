using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm.Demo.Demos
{
    public class Oracle11gBuilder : OracleBuilder
    {
        /// <summary> 
        /// 获取 <see cref="Oracle11gBuilder"/> 的单例实例，适用于 Oracle 11g 及以上版本。 
        /// </summary> 
        public readonly static new Oracle11gBuilder Instance = new Oracle11gBuilder();

        /// <summary> 
        /// 将结构化的 SQL 片段组装成最终的 SELECT 语句 (Oracle 实现)。 
        /// 使用 ROW_NUMBER() OVER(...) 双层嵌套子查询实现分页，兼容所有 Oracle 版本。 
        /// </summary> 
        public override void BuildSelectSql(ref SqlValueStringBuilder subSelect, ref ValueStringBuilder result, int indent)
        {
            bool hasPaging = subSelect.Take > 0;

            if (hasPaging)
            {
                // 外层：过滤 ROW_NUMBER() 范围 
                result.Append($"SELECT * FROM (");
                result.NewLine(indent);
            }

            // 内层：实际数据查询 
            result.Append("SELECT ");
            result.Append(subSelect.Select.AsSpan());

            if (hasPaging)
            {
                // 内层：计算 ROW_NUMBER()，ORDER BY 移至 OVER 子句 
                result.Append(",ROW_NUMBER() OVER (ORDER BY ");
                if (subSelect.OrderBy.Length > 0)
                    result.Append(subSelect.OrderBy.AsSpan());
                else
                    result.Append('1');
                result.Append(") AS \"RN__\"");
            }

            if (subSelect.From.Length > 0)
            {
                result.NewLine(indent);
                result.Append("FROM ");
                result.Append(subSelect.From.AsSpan());
            }

            if (subSelect.Where.Length > 0)
            {
                result.NewLine(indent);
                result.Append("WHERE ");
                result.Append(subSelect.Where.AsSpan());
            }

            if (subSelect.GroupBy.Length > 0)
            {
                result.NewLine(indent);
                result.Append("GROUP BY ");
                result.Append(subSelect.GroupBy.AsSpan());
            }

            if (subSelect.Having.Length > 0)
            {
                result.NewLine(indent);
                result.Append("HAVING ");
                result.Append(subSelect.Having.AsSpan());
            }

            if (hasPaging)
            {
                // 关闭内层子查询，提供别名供外层层引用               
                result.Append(") \"__T\"");
                result.NewLine(indent);
                // 按 ROW_NUMBER() 范围过滤（1-based，skip 条之后，共取 take 条） 
                result.Append("WHERE \"RN__\" > ");
                result.Append(subSelect.Skip.ToString());
                result.Append(" AND \"RN__\" <= ");
                result.Append((subSelect.Skip + subSelect.Take).ToString());
            }
            else
            {
                if (subSelect.OrderBy.Length > 0)
                {
                    result.NewLine(indent); 
                    result.Append("ORDER BY ");
                    result.Append(subSelect.OrderBy.AsSpan());
                }
            }
        }
    }
}
