using System;
using System.Collections.Generic;

namespace LiteOrm.Common
{
    /// <summary>
    /// 指定 <see cref="Expr"/> 树的遍历顺序。
    /// </summary>
    public enum ExprVisitOrder
    {
        /// <summary>
        /// 先访问当前节点，再访问子节点。
        /// </summary>
        PreOrder,

        /// <summary>
        /// 先访问子节点，再访问当前节点。
        /// </summary>
        PostOrder,
    }

    /// <summary>
    /// 实现此接口以参与 <see cref="ExprVisitor.VisitAll(IExprNodeVisitor,Expr)"/> 驱动的树遍历。
    /// </summary>
    public interface IExprNodeVisitor
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
        public static bool Visit(Func<Expr, bool> visitor, Expr root)
            => Visit(visitor, root, ExprVisitOrder.PreOrder);

        /// <summary>
        /// 按指定顺序遍历以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树，
        /// 对每个节点调用 <paramref name="visitor"/>。
        /// </summary>
        /// <param name="visitor">
        /// 对每个节点调用的委托。
        /// 返回 <see langword="true"/> 继续遍历；返回 <see langword="false"/> 立即终止整棵树的遍历。
        /// </param>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回 <see langword="true"/>。</param>
        /// <param name="order">遍历顺序。</param>
        /// <returns>
        /// 完整遍历完毕返回 <see langword="true"/>；
        /// 因委托返回 <see langword="false"/> 而提前终止返回 <see langword="false"/>。
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="visitor"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> 不是有效的遍历顺序。</exception>
        public static bool Visit(Func<Expr, bool> visitor, Expr root, ExprVisitOrder order)
        {
            if (root == null) return true;
            if (visitor == null) throw new ArgumentNullException(nameof(visitor));
            if (!Enum.IsDefined(typeof(ExprVisitOrder), order)) throw new ArgumentOutOfRangeException(nameof(order));
            return VisitNode(root, visitor, order);
        }

        /// <summary>
        /// 以前序方式遍历以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树，
        /// 对每个节点调用 <see cref="IExprNodeVisitor.Visit"/>。
        /// </summary>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回 <see langword="true"/>。</param>
        /// <param name="visitor"><see cref="IExprNodeVisitor"/> 实现，不能为 <see langword="null"/>。</param>
        /// <returns>
        /// 完整遍历完毕返回 <see langword="true"/>；
        /// 因 <see cref="IExprNodeVisitor.Visit"/> 返回 <see langword="false"/> 而提前终止返回 <see langword="false"/>。
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="visitor"/> 为 <see langword="null"/>。</exception>
        public static bool VisitAll(this IExprNodeVisitor visitor, Expr root)
            => VisitAll(visitor, root, ExprVisitOrder.PreOrder);

        /// <summary>
        /// 按指定顺序遍历以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树，
        /// 对每个节点调用 <see cref="IExprNodeVisitor.Visit"/>。
        /// </summary>
        /// <param name="visitor"><see cref="IExprNodeVisitor"/> 实现，不能为 <see langword="null"/>。</param>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回 <see langword="true"/>。</param>
        /// <param name="order">遍历顺序。</param>
        /// <returns>
        /// 完整遍历完毕返回 <see langword="true"/>；
        /// 因 <see cref="IExprNodeVisitor.Visit"/> 返回 <see langword="false"/> 而提前终止返回 <see langword="false"/>。
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="visitor"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> 不是有效的遍历顺序。</exception>
        public static bool VisitAll(this IExprNodeVisitor visitor, Expr root, ExprVisitOrder order)
        {
            if (visitor == null) throw new ArgumentNullException(nameof(visitor));
            return Visit(visitor.Visit, root, order);
        }

        private static bool VisitNode(Expr node, Func<Expr, bool> visitor, ExprVisitOrder order)
        {
            if (order == ExprVisitOrder.PreOrder && !visitor(node)) return false;
            if (!VisitChildren(node, visitor, order)) return false;
            return order != ExprVisitOrder.PostOrder || visitor(node);
        }

        private static bool VisitChildren(Expr node, Func<Expr, bool> visitor, ExprVisitOrder order)
        {
            switch (node)
            {
                case ValueBinaryExpr vb:
                    return VisitChild(vb.Left, visitor, order) && VisitChild(vb.Right, visitor, order);

                case ValueSet vs:
                    foreach (ValueTypeExpr item in vs.Items)
                        if (!VisitNode(item, visitor, order)) return false;
                    return true;

                case FunctionExpr func:
                    foreach (ValueTypeExpr p in func.Args)
                        if (!VisitNode(p, visitor, order)) return false;
                    return true;

                case UnaryExpr u:
                    return VisitChild(u.Operand, visitor, order);

                case LogicBinaryExpr b:
                    return VisitChild(b.Left, visitor, order) && VisitChild(b.Right, visitor, order);

                case AndExpr a:
                    foreach (LogicExpr item in a.Items)
                        if (!VisitNode(item, visitor, order)) return false;
                    return true;

                case OrExpr o:
                    foreach (LogicExpr item in o.Items)
                        if (!VisitNode(item, visitor, order)) return false;
                    return true;

                case NotExpr n:
                    return VisitChild(n.Operand, visitor, order);

                case ForeignExpr f:
                    return VisitChild(f.InnerExpr, visitor, order);

                case LambdaExpr l:
                    return VisitChild(l.InnerExpr, visitor, order);

                case SelectItemExpr si:
                    return VisitChild(si.Value, visitor, order);

                case SelectExpr sel:
                    if (!VisitChild(sel.Source as Expr, visitor, order)) return false;
                    foreach (SelectItemExpr item in sel.Selects)
                        if (!VisitNode(item, visitor, order)) return false;
                    if (sel.NextSelects != null)
                    {
                        foreach (SelectExpr nxt in sel.NextSelects)
                            if (!VisitNode(nxt, visitor, order)) return false;
                    }
                    return true;

                case FromExpr fe:
                    if (!VisitChild(fe.Source as Expr, visitor, order)) return false;
                    if (fe.Joins != null)
                    {
                        foreach (TableJoinExpr j in fe.Joins)
                            if (!VisitNode(j, visitor, order)) return false;
                    }
                    return true;

                case TableJoinExpr tje:
                    if (!VisitChild(tje.Source as Expr, visitor, order)) return false;
                    return VisitChild(tje.On, visitor, order);

                case TableExpr _:
                    return true;

                case CommonTableExpr cte:
                    return VisitChild(cte.Source as Expr, visitor, order);

                case WhereExpr w:
                    if (!VisitChild(w.Source as Expr, visitor, order)) return false;
                    return VisitChild(w.Where, visitor, order);

                case OrderByExpr o:
                    if (!VisitChild(o.Source as Expr, visitor, order)) return false;
                    foreach (OrderByItemExpr ob in o.OrderBys)
                        if (!VisitNode(ob.Field, visitor, order)) return false;
                    return true;

                case GroupByExpr g:
                    if (!VisitChild(g.Source as Expr, visitor, order)) return false;
                    foreach (ValueTypeExpr item in g.GroupBys)
                        if (!VisitNode(item, visitor, order)) return false;
                    return true;

                case HavingExpr h:
                    if (!VisitChild(h.Source as Expr, visitor, order)) return false;
                    return VisitChild(h.Having, visitor, order);

                case SectionExpr se:
                    return VisitChild(se.Source as Expr, visitor, order);

                case DeleteExpr d:
                    if (!VisitChild(d.Table as Expr, visitor, order)) return false;
                    return VisitChild(d.Where, visitor, order);

                case UpdateExpr upd:
                    if (!VisitChild(upd.Table as Expr, visitor, order)) return false;
                    foreach (var set in upd.Sets)
                    {
                        if (!VisitNode(set.Property, visitor, order)) return false;
                        if (!VisitNode(set.Value, visitor, order)) return false;
                    }
                    return VisitChild(upd.Where, visitor, order);

                // ValueExpr, PropertyExpr, GenericSqlExpr, …
                default:
                    return true;
            }
        }

        private static bool VisitChild(Expr child, Func<Expr, bool> visitor, ExprVisitOrder order)
            => child == null || VisitNode(child, visitor, order);
    }
}
