using System;
using System.Collections.Generic;

namespace LiteOrm.Common
{
    /// <summary>
    /// 实现此接口以参与 <see cref="ExprVisitor.Visit(Expr, IExprVisitor)"/> 驱动的树遍历。
    /// </summary>
    public interface IExprVisitor
    {
        /// <summary>
        /// 对树中的每个节点调用。
        /// </summary>
        /// <param name="node">当前访问的节点。</param>
        /// <returns>
        /// 返回 <see langword="true"/> 继续遍历子节点；
        /// 返回 <see langword="false"/> 立即终止整棵树的遍历。
        /// </returns>
        bool Visit(Expr node);
    }

    /// <summary>
    /// 提供对 <see cref="Expr"/> 树的委托驱动遍历能力。
    /// </summary>
    public static class ExprVisitor
    {
        /// <summary>
        /// 以前序方式遍历以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树，
        /// 对每个节点调用 <paramref name="visitor"/>。
        /// </summary>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回 <see langword="true"/>。</param>
        /// <param name="visitor">
        /// 对每个节点调用的委托。
        /// 返回 <see langword="true"/> 继续遍历；返回 <see langword="false"/> 立即终止整棵树的遍历。
        /// </param>
        /// <returns>
        /// 完整遍历完毕返回 <see langword="true"/>；
        /// 因委托返回 <see langword="false"/> 而提前终止返回 <see langword="false"/>。
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="visitor"/> 为 <see langword="null"/>。</exception>
        public static bool Visit(Expr root, Func<Expr, bool> visitor)
        {
            if (root == null) return true;
            if (visitor == null) throw new ArgumentNullException(nameof(visitor));
            return VisitNode(root, visitor);
        }

        /// <summary>
        /// 以前序方式遍历以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树，
        /// 对每个节点调用 <see cref="IExprVisitor.Visit"/>。
        /// </summary>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回 <see langword="true"/>。</param>
        /// <param name="visitor"><see cref="IExprVisitor"/> 实现，不能为 <see langword="null"/>。</param>
        /// <returns>
        /// 完整遍历完毕返回 <see langword="true"/>；
        /// 因 <see cref="IExprVisitor.Visit"/> 返回 <see langword="false"/> 而提前终止返回 <see langword="false"/>。
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="visitor"/> 为 <see langword="null"/>。</exception>
        public static bool Visit(Expr root, IExprVisitor visitor)
        {
            if (visitor == null) throw new ArgumentNullException(nameof(visitor));
            return Visit(root, visitor.Visit);
        }

        private static bool VisitNode(Expr node, Func<Expr, bool> visitor)
        {
            // Pre-order: invoke the visitor for this node before descending into children.
            if (!visitor(node)) return false;

            switch (node)
            {
                case ValueBinaryExpr vb:
                    return VisitChild(vb.Left, visitor)
                        && VisitChild(vb.Right, visitor);

                case ValueSet vs:
                    foreach (ValueTypeExpr item in vs.Items)
                        if (!VisitNode(item, visitor)) return false;
                    return true;

                case FunctionExpr func:
                    foreach (ValueTypeExpr p in func.Parameters)
                        if (!VisitNode(p, visitor)) return false;
                    return true;

                case AggregateFunctionExpr agg:
                    return VisitChild(agg.Expression, visitor);

                case UnaryExpr u:
                    return VisitChild(u.Operand, visitor);

                case LogicBinaryExpr b:
                    return VisitChild(b.Left, visitor)
                        && VisitChild(b.Right, visitor);

                case LogicSet s:
                    foreach (LogicExpr item in s.Items)
                        if (!VisitNode(item, visitor)) return false;
                    return true;

                case NotExpr n:
                    return VisitChild(n.Operand, visitor);

                case ForeignExpr f:
                    return VisitChild(f.InnerExpr, visitor);

                case LambdaExpr l:
                    return VisitChild(l.InnerExpr, visitor);

                case SelectItemExpr si:
                    return VisitChild(si.Value, visitor);

                case SelectExpr sel:
                    foreach (SelectItemExpr item in sel.Selects)
                        if (!VisitNode(item, visitor)) return false;
                    return true;

                case WhereExpr w:
                    return VisitChild(w.Where, visitor);

                case DeleteExpr d:
                    return VisitChild(d.Where, visitor);

                case HavingExpr h:
                    return VisitChild(h.Having, visitor);

                case GroupByExpr g:
                    foreach (ValueTypeExpr item in g.GroupBys)
                        if (!VisitNode(item, visitor)) return false;
                    return true;

                case OrderByExpr o:
                    foreach ((ValueTypeExpr expr, bool _) in o.OrderBys)
                        if (!VisitNode(expr, visitor)) return false;
                    return true;

                case UpdateExpr upd:
                    foreach ((PropertyExpr prop, ValueTypeExpr val) in upd.Sets)
                    {
                        if (!VisitNode(prop, visitor)) return false;
                        if (!VisitNode(val, visitor)) return false;
                    }
                    return VisitChild(upd.Where, visitor);

                // ValueExpr, PropertyExpr, GenericSqlExpr, FromExpr, SectionExpr, …
                default:
                    return true;
            }
        }

        private static bool VisitChild(Expr child, Func<Expr, bool> visitor)
            => child == null || VisitNode(child, visitor);
    }
}
