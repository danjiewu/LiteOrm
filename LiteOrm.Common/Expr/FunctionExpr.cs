using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示函数调用表达式，例如 <c>SUM(column)</c>、<c>COALESCE(a,b)</c> 等。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class FunctionExpr : Expr
    {
        /// <summary>
        /// 构造函数，初始化空参数列表。
        /// </summary>
        public FunctionExpr()
        {
            Parameters = new List<Expr>();
        }

        /// <summary>
        /// 使用函数名与参数构造函数表达式。
        /// </summary>
        /// <param name="functionName">函数名</param>
        /// <param name="parameters">参数表达式列表</param>
        public FunctionExpr(string functionName, params Expr[] parameters)
        {
            FunctionName = functionName;
            Parameters = parameters.ToList();
        }

        /// <summary>
        /// 函数表达式为值类型
        /// </summary>
        public override bool IsValue => true;
        /// <summary>
        /// 函数名
        /// </summary>
        public string FunctionName { get; set; }

        /// <summary>
        /// 参数表达式列表
        /// </summary>
        public List<Expr> Parameters { get; }

        /// <summary>
        /// 返回表示当前函数的字符串。
        /// </summary>
        /// <returns>表示当前函数的字符串。</returns>
        public override string ToString()
        {
            return $"{FunctionName}({String.Join(",", Parameters)})";
        }

        /// <summary>
        /// 确定指定的对象是否等于当前对象。
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
