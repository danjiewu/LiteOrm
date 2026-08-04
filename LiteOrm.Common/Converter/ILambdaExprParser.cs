using System.Collections.Generic;
using System.Linq.Expressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// Lambda 表达式解析器接口，用于将 <see cref="LambdaExpression"/> 转换为框架通用的 <see cref="Expr"/> 模型。
    /// 默认实现为 <see cref="LambdaExprConverter"/>；NativeAOT 场景下可替换为预编译实现。
    /// </summary>
    public interface ILambdaExprParser
    {
        /// <summary>
        /// 解析 Lambda 表达式为 <see cref="Expr"/> 模型。
        /// </summary>
        Expr Parse(LambdaExpression lambda);

        /// <summary>
        /// 解析单个表达式节点为 <see cref="Expr"/> 模型。
        /// </summary>
        Expr Parse(Expression node, Dictionary<string, string>? parameterAliases = null, string? currentAlias = null);
    }
}
