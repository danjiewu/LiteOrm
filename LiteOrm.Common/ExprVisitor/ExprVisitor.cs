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
    /// <see cref="BeginVisit"/> 在进入节点时调用（前序），<see cref="EndVisit"/> 在离开节点时调用（后序）。
    /// 遍历总是完整执行，不会中断。
    /// </summary>
    public interface IExprNodeVisitor
    {
        /// <summary>
        /// 进入节点时调用（前序：先父后子）。
        /// </summary>
        /// <param name="node">当前访问的节点。</param>
        void BeginVisit(Expr node);

        /// <summary>
        /// 离开节点时调用（后序：先子后父）。
        /// </summary>
        /// <param name="node">当前访问的节点。</param>
        void EndVisit(Expr node);
    }

    /// <summary>
    /// 提供对 <see cref="Expr"/> 树的多模式遍历能力。
    /// 支持四种遍历模式：
    /// <list type="bullet">
    /// <item><see cref="Func{Expr, bool}"/> 委托：支持短路终止</item>
    /// <item><see cref="Action{Expr}"/> 委托：总是完整遍历</item>
    /// <item><see cref="IExprNodeVisitor"/> 接口：双向通知（进入/离开节点）</item>
    /// <item><see cref="ExprValidator"/> 基类：验证模式，支持短路终止</item>
    /// </list>
    /// </summary>
    public static class ExprVisitor
    {
        /// <summary>
        /// 以前序方式遍历以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树，
        /// 对每个节点调用 <paramref name="visitor"/>。
        /// </summary>
        /// <param name="visitor">
        /// 对每个节点调用的委托。
        /// 返回 <see langword="true"/> 继续遍历；返回 <see langword="false"/> 立即终止整棵树的遍历。
        /// </param>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回 <see langword="true"/>。</param>
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
        /// 对每个节点调用 <paramref name="visitor"/>。遍历总是完整执行，不会中断。
        /// </summary>
        /// <param name="visitor">对每个节点调用的委托，总是完整执行。</param>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回。</param>
        /// <exception cref="ArgumentNullException"><paramref name="visitor"/> 为 <see langword="null"/>。</exception>
        public static void Visit(Action<Expr> visitor, Expr root)
            => Visit(visitor, root, ExprVisitOrder.PreOrder);

        /// <summary>
        /// 按指定顺序遍历以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树，
        /// 对每个节点调用 <paramref name="visitor"/>。遍历总是完整执行，不会中断。
        /// </summary>
        /// <param name="visitor">对每个节点调用的委托，总是完整执行。</param>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回。</param>
        /// <param name="order">遍历顺序。</param>
        /// <exception cref="ArgumentNullException"><paramref name="visitor"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> 不是有效的遍历顺序。</exception>
        public static void Visit(Action<Expr> visitor, Expr root, ExprVisitOrder order)
        {
            if (root == null) return;
            if (visitor == null) throw new ArgumentNullException(nameof(visitor));
            if (!Enum.IsDefined(typeof(ExprVisitOrder), order)) throw new ArgumentOutOfRangeException(nameof(order));
            Visit(node => { visitor(node); return true; }, root, order);
        }

        /// <summary>
        /// 遍历以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树，
        /// 在同一趟遍历中对每个节点调用 <see cref="IExprNodeVisitor.BeginVisit"/>（入栈/进入）和 <see cref="IExprNodeVisitor.EndVisit"/>（出栈/离开）。
        /// 遍历总是完整执行，不会中断。
        /// </summary>
        /// <param name="visitor"><see cref="IExprNodeVisitor"/> 实现，不能为 <see langword="null"/>。</param>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回。</param>
        /// <exception cref="ArgumentNullException"><paramref name="visitor"/> 为 <see langword="null"/>。</exception>
        public static void VisitAll(this IExprNodeVisitor visitor, Expr root)
        {
            if (visitor == null) throw new ArgumentNullException(nameof(visitor));
            if (root == null) return;
            VisitNodeCore(root,
                node => { visitor.BeginVisit(node); return true; },
                node => { visitor.EndVisit(node); return true; });
        }

        /// <summary>
        /// 以前序方式遍历以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树，
        /// 对每个节点调用 <see cref="ExprValidator.Validate"/> 进行验证。
        /// </summary>
        /// <param name="validator"><see cref="ExprValidator"/> 实例，不能为 <see langword="null"/>。</param>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回 <see langword="true"/>。</param>
        /// <returns>
        /// 完整验证通过返回 <see langword="true"/>；
        /// 因验证失败而提前终止返回 <see langword="false"/>。
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="validator"/> 为 <see langword="null"/>。</exception>
        public static bool VisitAll(this ExprValidator validator, Expr root)
            => VisitAll(validator, root, ExprVisitOrder.PreOrder);

        /// <summary>
        /// 按指定顺序遍历以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树，
        /// 对每个节点调用 <see cref="ExprValidator.Validate"/> 进行验证。
        /// 验证失败时自动记录失败节点到 <see cref="ExprValidator.FailedExpr"/>。
        /// </summary>
        /// <param name="validator"><see cref="ExprValidator"/> 实例，不能为 <see langword="null"/>。</param>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回 <see langword="true"/>。</param>
        /// <param name="order">遍历顺序。</param>
        /// <returns>
        /// 完整验证通过返回 <see langword="true"/>；
        /// 因验证失败而提前终止返回 <see langword="false"/>。
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="validator"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> 不是有效的遍历顺序。</exception>
        public static bool VisitAll(this ExprValidator validator, Expr root, ExprVisitOrder order)
        {
            if (validator == null) throw new ArgumentNullException(nameof(validator));
            if (root == null) return true;

            return Visit(node =>
            {
                if (validator.Validate(node)) return true;
                validator.FailedExpr = node;
                return false;
            }, root, order);
        }

        private static bool VisitNode(Expr node, Func<Expr, bool> visitor, ExprVisitOrder order)
        {
            var onEnter = order == ExprVisitOrder.PreOrder ? visitor : null;
            var onLeave = order == ExprVisitOrder.PostOrder ? visitor : null;
            return VisitNodeCore(node, onEnter, onLeave);
        }

        private static bool VisitNodeCore(Expr node, Func<Expr, bool> onEnter, Func<Expr, bool> onLeave)
        {
            if (node == null) return true;
            if (onEnter != null && !onEnter(node)) return false;
            if (!VisitChildrenCore(node, onEnter, onLeave)) return false;
            return onLeave == null || onLeave(node);
        }

        private static bool VisitChildrenCore(Expr node, Func<Expr, bool> onEnter, Func<Expr, bool> onLeave)
        {
            switch (node)
            {
                case ValueBinaryExpr vb:
                    return VisitNodeCore(vb.Left, onEnter, onLeave) && VisitNodeCore(vb.Right, onEnter, onLeave);

                case ValueSet vs:
                    foreach (ValueTypeExpr item in vs.Items)
                        if (!VisitNodeCore(item, onEnter, onLeave)) return false;
                    return true;

                case FunctionExpr func:
                    foreach (ValueTypeExpr p in func.Args)
                        if (!VisitNodeCore(p, onEnter, onLeave)) return false;
                    return true;

                case UnaryExpr u:
                    return VisitNodeCore(u.Operand, onEnter, onLeave);

                case LogicBinaryExpr b:
                    return VisitNodeCore(b.Left, onEnter, onLeave) && VisitNodeCore(b.Right, onEnter, onLeave);

                case AndExpr a:
                    foreach (LogicExpr item in a.Items)
                        if (!VisitNodeCore(item, onEnter, onLeave)) return false;
                    return true;

                case OrExpr o:
                    foreach (LogicExpr item in o.Items)
                        if (!VisitNodeCore(item, onEnter, onLeave)) return false;
                    return true;

                case NotExpr n:
                    return VisitNodeCore(n.Operand, onEnter, onLeave);

                case ForeignExpr f:
                    return VisitNodeCore(f.InnerExpr, onEnter, onLeave);

                case LambdaExpr l:
                    return VisitNodeCore(l.InnerExpr, onEnter, onLeave);

                case SelectItemExpr si:
                    return VisitNodeCore(si.Value, onEnter, onLeave);

                case SelectExpr sel:
                    if (!VisitNodeCore(sel.Source as Expr, onEnter, onLeave)) return false;
                    foreach (SelectItemExpr item in sel.Selects)
                        if (!VisitNodeCore(item, onEnter, onLeave)) return false;
                    if (sel.NextSelects != null)
                    {
                        foreach (SelectExpr nxt in sel.NextSelects)
                            if (!VisitNodeCore(nxt, onEnter, onLeave)) return false;
                    }
                    return true;

                case FromExpr fe:
                    if (!VisitNodeCore(fe.Source as Expr, onEnter, onLeave)) return false;
                    if (fe.Joins != null)
                    {
                        foreach (TableJoinExpr j in fe.Joins)
                            if (!VisitNodeCore(j, onEnter, onLeave)) return false;
                    }
                    return true;

                case TableJoinExpr tje:
                    if (!VisitNodeCore(tje.Source as Expr, onEnter, onLeave)) return false;
                    return VisitNodeCore(tje.On, onEnter, onLeave);

                case TableExpr _:
                    return true;

                case CommonTableExpr cte:
                    return VisitNodeCore(cte.Source as Expr, onEnter, onLeave);

                case WhereExpr w:
                    if (!VisitNodeCore(w.Source as Expr, onEnter, onLeave)) return false;
                    return VisitNodeCore(w.Where, onEnter, onLeave);

                case OrderByExpr o:
                    if (!VisitNodeCore(o.Source as Expr, onEnter, onLeave)) return false;
                    foreach (OrderByItemExpr ob in o.OrderBys)
                        if (!VisitNodeCore(ob.Field, onEnter, onLeave)) return false;
                    return true;

                case GroupByExpr g:
                    if (!VisitNodeCore(g.Source as Expr, onEnter, onLeave)) return false;
                    foreach (ValueTypeExpr item in g.GroupBys)
                        if (!VisitNodeCore(item, onEnter, onLeave)) return false;
                    return true;

                case HavingExpr h:
                    if (!VisitNodeCore(h.Source as Expr, onEnter, onLeave)) return false;
                    return VisitNodeCore(h.Having, onEnter, onLeave);

                case SectionExpr se:
                    return VisitNodeCore(se.Source as Expr, onEnter, onLeave);

                case DeleteExpr d:
                    if (!VisitNodeCore(d.Table as Expr, onEnter, onLeave)) return false;
                    return VisitNodeCore(d.Where, onEnter, onLeave);

                case UpdateExpr upd:
                    if (!VisitNodeCore(upd.Table as Expr, onEnter, onLeave)) return false;
                    foreach (var set in upd.Sets)
                    {
                        if (!VisitNodeCore(set.Property, onEnter, onLeave)) return false;
                        if (!VisitNodeCore(set.Value, onEnter, onLeave)) return false;
                    }
                    return VisitNodeCore(upd.Where, onEnter, onLeave);

                default:
                    return true;
            }
        }
    }
}
