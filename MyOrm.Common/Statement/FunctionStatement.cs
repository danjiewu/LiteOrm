using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 表示函数调用语句，例如 <c>SUM(column)</c>、<c>COALESCE(a,b)</c> 等。
    /// </summary>
    public sealed class FunctionStatement : Statement
    {
        /// <summary>
        /// 构造函数，初始化空参数列表。
        /// </summary>
        public FunctionStatement()
        {
            Parameters = new List<Statement>();
        }

        /// <summary>
        /// 使用函数名与参数构造函数语句。
        /// </summary>
        /// <param name="functionName">函数名</param>
        /// <param name="parameters">参数语句列表</param>
        public FunctionStatement(string functionName, params Statement[] parameters)
        {
            FunctionName = functionName;
            Parameters = parameters.ToList();
        }

        /// <summary>
        /// 函数名
        /// </summary>
        public string FunctionName { get; set; }

        /// <summary>
        /// 参数语句列表
        /// </summary>
        public List<Statement> Parameters { get; set; }

        /// <inheritdoc/>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            return sqlBuilder.BuildFunctionSql(FunctionName, Parameters.Select(p => p.ToSql(context, sqlBuilder, outputParams)).ToArray());
        }

        public override string ToString()
        {
            return $"{FunctionName}({String.Join(",", Parameters)})";
        }

        public override bool Equals(object obj)
        {
            return obj is FunctionStatement f && f.FunctionName == FunctionName && f.Parameters.SequenceEqual(Parameters);
        }

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
