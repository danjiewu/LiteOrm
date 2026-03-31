using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm
{

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

    public class FunctionExprValidator : IExprNodeVisitor
    {
        public static readonly FunctionExprValidator AllowAll = new FunctionExprValidator(FunctionPolicy.AllowAll);
        public static readonly FunctionExprValidator AllowRegisted = new FunctionExprValidator(FunctionPolicy.AllowRegisted);
        public static readonly FunctionExprValidator Disallow = new FunctionExprValidator(FunctionPolicy.Disallow);
        public FunctionExprValidator(FunctionPolicy functionPolicy) { FunctionPolicy = functionPolicy; }
        public FunctionPolicy FunctionPolicy { get; }

        public bool Visit(Expr node)
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
