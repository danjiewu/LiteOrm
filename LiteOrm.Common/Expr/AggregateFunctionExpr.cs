using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 聚合函数表达式，用于表示数据库聚合函数如 COUNT、SUM、AVG、MAX、MIN 等
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class AggregateFunctionExpr : ValueTypeExpr
    {
        /// <summary>
        /// 表示 COUNT 聚合函数的静态实例
        /// </summary>
        public readonly static AggregateFunctionExpr Count = new AggregateFunctionExpr("Count", Expr.Const(1));

        /// <summary>
        /// 初始化默认的聚合函数表达式
        /// </summary>
        public AggregateFunctionExpr() { }

        /// <summary>
        /// 使用指定的函数名、表达式和是否去重选项初始化聚合函数表达式
        /// </summary>
        /// <param name="functionName">聚合函数名称（如 Count、Sum、Avg、Max、Min）</param>
        /// <param name="expression">要聚合的字段表达式</param>
        /// <param name="isDistinct">是否对字段值去重（默认为 false）</param>
        public AggregateFunctionExpr(string functionName, ValueTypeExpr expression, bool isDistinct = false)
        {
            FunctionName = functionName;
            Expression = expression;
            IsDistinct = isDistinct;
        }

        /// <summary>
        /// 获取或设置要聚合的字段表达式
        /// </summary>
        public ValueTypeExpr Expression { get; set; }

        /// <summary>
        /// 获取或设置聚合函数名称
        /// </summary>
        public string FunctionName { get; set; }

        /// <summary>
        /// 获取或设置是否对字段值去重
        /// </summary>
        public bool IsDistinct { get; set; }

        /// <summary>
        /// 获取一个值，指示此表达式是否为值类型
        /// </summary>
        public override bool IsValue => true;

        /// <summary>
        /// 判断当前对象是否与指定对象相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is AggregateFunctionExpr other && FunctionName == other.FunctionName && Equals(Expression, other.Expression) && IsDistinct == other.IsDistinct;

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(AggregateFunctionExpr).GetHashCode(), FunctionName?.GetHashCode() ?? 0, Expression?.GetHashCode() ?? 0, IsDistinct.GetHashCode());

        /// <summary>
        /// 返回表达式的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString() => $"{FunctionName}({(IsDistinct ? "DISTINCT " : "")}{Expression})";
    }
}
