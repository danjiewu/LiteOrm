using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm
{
    /// <summary>
    /// 指定函数表达式在查询中的使用策略。
    /// </summary>
    public enum FunctionPolicy
    {
        /// <summary>
        /// 允许在任何表达式中使用函数表达式。
        /// </summary>
        AllowAll,
        /// <summary>
        /// 仅允许预注册的函数表达式。
        /// </summary>
        AllowRegisted,
        /// <summary>
        /// 不允许使用函数表达式。
        /// </summary>
        Disallow
    }

    /// <summary>
    /// 函数表达式验证器，用于验证查询中函数表达式的使用是否符合既定策略。
    /// 该类实现 <see cref="IExprNodeVisitor"/> 接口，遍历表达式树并检查 <see cref="FunctionExpr"/> 节点。
    /// </summary>
    public class FunctionExprValidator : ExprValidator
    {
        /// <summary>
        /// 获取一个预配置的验证器实例，该验证器允许所有函数表达式。
        /// </summary>
        public static readonly FunctionExprValidator AllowAll = new FunctionExprValidator(FunctionPolicy.AllowAll);

        /// <summary>
        /// 获取一个预配置的验证器实例，该验证器仅允许预注册的函数表达式。
        /// </summary>
        public static readonly FunctionExprValidator AllowRegisted = new FunctionExprValidator(FunctionPolicy.AllowRegisted);

        /// <summary>
        /// 获取一个预配置的验证器实例，该验证器不允许任何函数表达式。
        /// </summary>
        public static readonly FunctionExprValidator Disallow = new FunctionExprValidator(FunctionPolicy.Disallow);

        /// <summary>
        /// 使用指定的函数策略初始化 <see cref="FunctionExprValidator"/> 类的新实例。
        /// </summary>
        /// <param name="functionPolicy">函数表达式验证策略。</param>
        public FunctionExprValidator(FunctionPolicy functionPolicy) { FunctionPolicy = functionPolicy; }

        /// <summary>
        /// 获取当前验证器所使用的函数策略。
        /// </summary>
        public FunctionPolicy FunctionPolicy { get; }

        /// <summary>
        /// 验证指定的表达式节点。
        /// 当节点为 <see cref="FunctionExpr"/> 时，根据当前策略判断是否允许该函数表达式。
        /// </summary>
        /// <param name="node">要访问的表达式节点。</param>
        /// <returns>
        /// 如果允许该表达式继续遍历（或该节点不是函数表达式），返回 <c>true</c>；
        /// 如果拒绝该函数表达式（不符合策略），返回 <c>false</c>。
        /// </returns>
        public override bool Validate(Expr node)
        {
            if (node == null) return true;
            else if (node is FunctionExpr funcExpr)
            {
                switch (FunctionPolicy)
                {
                    case FunctionPolicy.AllowAll:
                        return true; // 允许所有函数表达式
                    case FunctionPolicy.AllowRegisted:
                        return SqlBuilder.Instance.TryGetFunctionSqlHandler<SqlBuilder>(funcExpr.FunctionName, out _); // 仅允许预注册的函数表达式
                    case FunctionPolicy.Disallow:
                        return false; // 不允许使用函数表达式
                }
            }
            return true; // 对于非函数表达式，继续遍历子节点
        }
    }
}
