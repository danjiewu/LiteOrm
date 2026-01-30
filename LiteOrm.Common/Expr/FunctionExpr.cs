using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示数据库函数调用表达式，例如 <c>SUM(Column)</c>、<c>COALESCE(Arg1, Arg2)</c> 等。
    /// 此类通用性高，可代表任何数据库端的内置函数或用户自定义函数。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class FunctionExpr : ValueTypeExpr
    {
        /// <summary>
        /// 默认构造，初始化空的参数列表。
        /// </summary>
        public FunctionExpr()
        {
            Parameters = new List<ValueTypeExpr>();
        }

        /// <summary>
        /// 使用函数名及对应的参数表达式初始化 FunctionExpr。
        /// </summary>
        /// <param name="functionName">SQL 函数名。</param>
        /// <param name="parameters">传入函数的参数表达式集合。</param>
        public FunctionExpr(string functionName, params ValueTypeExpr[] parameters)
        {
            FunctionName = functionName;
            Parameters = parameters.ToList();
        }

        /// <summary>
        /// 函数表达式通常被视为带返回值的表达式。
        /// </summary>
        public override bool IsValue => true;

        /// <summary>
        /// 获取或设置目标 SQL 函数名称。
        /// </summary>
        public string FunctionName { get; set; }

        /// <summary>
        /// 获取当前函数的参数列表。
        /// </summary>
        public List<ValueTypeExpr> Parameters { get; }

        /// <summary>
        /// 返回针对该函数的字符串预览（如 "SUM(Column)"）。
        /// </summary>
        public override string ToString()
        {
            return $"{FunctionName}({String.Join(",", Parameters)})";
        }

        /// <summary>
        /// 深度比较两个函数调用是否一致。
        /// </summary>
        /// <param name="obj">要与当前对象进行比较的对象。</param>
        /// <returns>如果指定的对象等于当前对象，则为 true；否则为 false。</returns>
        public override bool Equals(object obj)
        {
            return obj is FunctionExpr f && f.FunctionName == FunctionName && f.Parameters.SequenceEqual(Parameters);
        }

        /// <summary>
        /// 作为默认哈希函数。
        /// </summary>
        /// <returns>当前对象的哈希代码。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = GetType().GetHashCode();
                hashCode = hashCode * HashSeed + FunctionName?.GetHashCode() ?? 0;
                foreach (var param in Parameters)
                {
                    hashCode = hashCode * HashSeed + param?.GetHashCode() ?? 0;
                }
                return hashCode;
            }
        }
    }
}
