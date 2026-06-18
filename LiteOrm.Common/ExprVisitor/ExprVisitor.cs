using System;
using System.Collections.Generic;
using System.Threading;

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
    /// 实现此接口以参与 <see cref="ExprVisitor.Visit(IExprNodeVisitor, Expr, CancellationToken)"/> 驱动的树遍历。
    /// <see cref="BeginVisit"/> 在进入节点时调用（前序），<see cref="EndVisit"/> 在离开节点时调用（后序）。
    /// </summary>
    public interface IExprNodeVisitor
    {
        /// <summary>
        /// 进入节点时调用（前序：先父后子）。
        /// </summary>
        /// <param name="node">当前访问的节点。</param>
        /// <param name="cancellationToken">用于中断遍历的取消令牌。</param>
        void BeginVisit(Expr node, CancellationToken cancellationToken);

        /// <summary>
        /// 离开节点时调用（后序：先子后父）。
        /// </summary>
        /// <param name="node">当前访问的节点。</param>
        /// <param name="cancellationToken">用于中断遍历的取消令牌。</param>
        void EndVisit(Expr node, CancellationToken cancellationToken);
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
    /// 所有遍历方法均支持通过 <see cref="CancellationToken"/> 进行外部中断。
    /// </summary>
    public static class ExprVisitor
    {
        /// <summary>
        /// 按指定顺序遍历以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树，
        /// 对每个节点调用 <paramref name="visitor"/>。
        /// </summary>
        /// <param name="visitor">
        /// 对每个节点调用的委托。
        /// 返回 <see langword="true"/> 继续遍历；返回 <see langword="false"/> 立即终止整棵树的遍历。
        /// </param>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回 <see langword="true"/>。</param>
        /// <param name="order">遍历顺序，默认为 <see cref="ExprVisitOrder.PreOrder"/>。</param>
        /// <param name="cancellationToken">用于中断遍历的取消令牌，默认为 <see cref="CancellationToken.None"/>。</param>
        /// <returns>
        /// 完整遍历完毕返回 <see langword="true"/>；
        /// 因委托返回 <see langword="false"/> 或取消令牌被取消而提前终止返回 <see langword="false"/>。
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="visitor"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> 不是有效的遍历顺序。</exception>
        public static bool Visit(Func<Expr, bool> visitor, Expr root, ExprVisitOrder order = ExprVisitOrder.PreOrder, CancellationToken cancellationToken = default)
        {
            if (root == null) return true;
            if (visitor == null) throw new ArgumentNullException(nameof(visitor));
            if (!Enum.IsDefined(typeof(ExprVisitOrder), order)) throw new ArgumentOutOfRangeException(nameof(order));
            return VisitNode(root, visitor, order, cancellationToken);
        }

        /// <summary>
        /// 按指定顺序遍历以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树，
        /// 对每个节点调用 <paramref name="visitor"/>。遍历总是完整执行，不会中断，
        /// 除非通过 <paramref name="cancellationToken"/> 取消。
        /// </summary>
        /// <param name="visitor">对每个节点调用的委托，总是完整执行。</param>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回。</param>
        /// <param name="order">遍历顺序，默认为 <see cref="ExprVisitOrder.PreOrder"/>。</param>
        /// <param name="cancellationToken">用于中断遍历的取消令牌，默认为 <see cref="CancellationToken.None"/>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="visitor"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> 不是有效的遍历顺序。</exception>
        public static void Visit(Action<Expr> visitor, Expr root, ExprVisitOrder order = ExprVisitOrder.PreOrder, CancellationToken cancellationToken = default)
        {
            if (root == null) return;
            if (visitor == null) throw new ArgumentNullException(nameof(visitor));
            if (!Enum.IsDefined(typeof(ExprVisitOrder), order)) throw new ArgumentOutOfRangeException(nameof(order));
            Visit(node => { visitor(node); return true; }, root, order, cancellationToken);
        }

        /// <summary>
        /// 遍历以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树，
        /// 在同一趟遍历中对每个节点调用 <see cref="IExprNodeVisitor.BeginVisit"/>（入栈/进入）和 <see cref="IExprNodeVisitor.EndVisit"/>（出栈/离开）。
        /// 遍历可以通过 <paramref name="cancellationToken"/> 中断。
        /// </summary>
        /// <param name="visitor"><see cref="IExprNodeVisitor"/> 实现，不能为 <see langword="null"/>。</param>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回。</param>
        /// <param name="cancellationToken">用于中断遍历的取消令牌，默认为 <see cref="CancellationToken.None"/>。</param>
        /// <exception cref="ArgumentNullException"><paramref name="visitor"/> 为 <see langword="null"/>。</exception>
        public static void Visit(this IExprNodeVisitor visitor, Expr root, CancellationToken cancellationToken = default)
        {
            if (visitor == null) throw new ArgumentNullException(nameof(visitor));
            if (root == null) return;
            VisitNodeCore(root,
                node => { visitor.BeginVisit(node, cancellationToken); return !cancellationToken.IsCancellationRequested; },
                node => { visitor.EndVisit(node, cancellationToken); return !cancellationToken.IsCancellationRequested; },
                cancellationToken);
        }

        /// <summary>
        /// 按指定顺序遍历以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树，
        /// 对每个节点调用 <see cref="ExprValidator.Validate"/> 进行验证。
        /// 验证失败时自动记录失败节点到 <see cref="ExprValidator.FailedExpr"/>。
        /// </summary>
        /// <param name="validator"><see cref="ExprValidator"/> 实例，不能为 <see langword="null"/>。</param>
        /// <param name="root">遍历的根节点，为 <see langword="null"/> 时直接返回 <see langword="true"/>。</param>
        /// <param name="order">遍历顺序，默认为 <see cref="ExprVisitOrder.PreOrder"/>。</param>
        /// <param name="cancellationToken">用于中断遍历的取消令牌，默认为 <see cref="CancellationToken.None"/>。</param>
        /// <returns>
        /// 完整验证通过返回 <see langword="true"/>；
        /// 因验证失败而提前终止或取消令牌被取消返回 <see langword="false"/>。
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="validator"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> 不是有效的遍历顺序。</exception>
        public static bool Validate(this ExprValidator validator, Expr root, ExprVisitOrder order = ExprVisitOrder.PreOrder, CancellationToken cancellationToken = default)
        {
            if (validator == null) throw new ArgumentNullException(nameof(validator));
            if (root == null) return true;

            return Visit(node =>
            {
                if (validator.Validate(node)) return true;
                validator.FailedExpr = node;
                return false;
            }, root, order, cancellationToken);
        }

        private static bool VisitNode(Expr node, Func<Expr, bool> visitor, ExprVisitOrder order, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return false;

            var onEnter = order == ExprVisitOrder.PreOrder ? visitor : null;
            var onLeave = order == ExprVisitOrder.PostOrder ? visitor : null;
            return VisitNodeCore(node, onEnter, onLeave, cancellationToken);
        }

        private static bool VisitNodeCore(Expr node, Func<Expr, bool> onEnter, Func<Expr, bool> onLeave, CancellationToken cancellationToken)
        {
            if (node == null) return true;
            if (cancellationToken.IsCancellationRequested) return false;

            if (onEnter != null && !onEnter(node))
            {
                // onEnter 返回 false 有两种情况：
                // 1. Func<Expr, bool> 模式：委托主动终止，无需调用 onLeave
                // 2. IExprNodeVisitor 模式：BeginVisit 触发了取消，需要调用 onLeave 保持配对
                if (cancellationToken.IsCancellationRequested)
                {
                    onLeave?.Invoke(node);
                }
                return false;
            }

            // onEnter 回调可能触发了取消，在此检查以避免继续遍历子节点
            if (cancellationToken.IsCancellationRequested)
            {
                // 仍然调用 onLeave 以保持 Begin/End 配对
                onLeave?.Invoke(node);
                return false;
            }

            bool childrenResult = VisitChildrenCore(node, onEnter, onLeave, cancellationToken);

            if (onLeave != null) onLeave(node);

            return childrenResult && !cancellationToken.IsCancellationRequested;
        }

        private static bool VisitChildrenCore(Expr node, Func<Expr, bool> onEnter, Func<Expr, bool> onLeave, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return false;

            switch (node)
            {
                case ValueBinaryExpr vb:
                    return VisitNodeCore(vb.Left, onEnter, onLeave, cancellationToken) && VisitNodeCore(vb.Right, onEnter, onLeave, cancellationToken);

                case ValueSet vs:
                    foreach (ValueTypeExpr item in vs.Items)
                        if (!VisitNodeCore(item, onEnter, onLeave, cancellationToken)) return false;
                    return true;

                case FunctionExpr func:
                    foreach (ValueTypeExpr p in func.Args)
                        if (!VisitNodeCore(p, onEnter, onLeave, cancellationToken)) return false;
                    return true;

                case UnaryExpr u:
                    return VisitNodeCore(u.Operand, onEnter, onLeave, cancellationToken);

                case LogicBinaryExpr b:
                    return VisitNodeCore(b.Left, onEnter, onLeave, cancellationToken) && VisitNodeCore(b.Right, onEnter, onLeave, cancellationToken);

                case AndExpr a:
                    foreach (LogicExpr item in a.Items)
                        if (!VisitNodeCore(item, onEnter, onLeave, cancellationToken)) return false;
                    return true;

                case OrExpr o:
                    foreach (LogicExpr item in o.Items)
                        if (!VisitNodeCore(item, onEnter, onLeave, cancellationToken)) return false;
                    return true;

                case NotExpr n:
                    return VisitNodeCore(n.Operand, onEnter, onLeave, cancellationToken);

                case ForeignExpr f:
                    return VisitNodeCore(f.InnerExpr, onEnter, onLeave, cancellationToken);

                case LambdaExpr l:
                    return VisitNodeCore(l.InnerExpr, onEnter, onLeave, cancellationToken);

                case SelectItemExpr si:
                    return VisitNodeCore(si.Value, onEnter, onLeave, cancellationToken);

                case SelectExpr sel:
                    if (!VisitNodeCore(sel.Source as Expr, onEnter, onLeave, cancellationToken)) return false;
                    foreach (SelectItemExpr item in sel.Selects)
                        if (!VisitNodeCore(item, onEnter, onLeave, cancellationToken)) return false;
                    if (sel.NextSelects != null)
                    {
                        foreach (SelectExpr nxt in sel.NextSelects)
                            if (!VisitNodeCore(nxt, onEnter, onLeave, cancellationToken)) return false;
                    }
                    return true;

                case FromExpr fe:
                    if (!VisitNodeCore(fe.Source as Expr, onEnter, onLeave, cancellationToken)) return false;
                    if (fe.Joins != null)
                    {
                        foreach (TableJoinExpr j in fe.Joins)
                            if (!VisitNodeCore(j, onEnter, onLeave, cancellationToken)) return false;
                    }
                    return true;

                case TableJoinExpr tje:
                    if (!VisitNodeCore(tje.Source as Expr, onEnter, onLeave, cancellationToken)) return false;
                    return VisitNodeCore(tje.On, onEnter, onLeave, cancellationToken);

                case TableExpr _:
                    return true;

                case CommonTableExpr cte:
                    return VisitNodeCore(cte.Source as Expr, onEnter, onLeave, cancellationToken);

                case WhereExpr w:
                    if (!VisitNodeCore(w.Source as Expr, onEnter, onLeave, cancellationToken)) return false;
                    return VisitNodeCore(w.Where, onEnter, onLeave, cancellationToken);

                case OrderByExpr o:
                    if (!VisitNodeCore(o.Source as Expr, onEnter, onLeave, cancellationToken)) return false;
                    foreach (OrderByItemExpr ob in o.OrderBys)
                        if (!VisitNodeCore(ob.Field, onEnter, onLeave, cancellationToken)) return false;
                    return true;

                case GroupByExpr g:
                    if (!VisitNodeCore(g.Source as Expr, onEnter, onLeave, cancellationToken)) return false;
                    foreach (ValueTypeExpr item in g.GroupBys)
                        if (!VisitNodeCore(item, onEnter, onLeave, cancellationToken)) return false;
                    return true;

                case HavingExpr h:
                    if (!VisitNodeCore(h.Source as Expr, onEnter, onLeave, cancellationToken)) return false;
                    return VisitNodeCore(h.Having, onEnter, onLeave, cancellationToken);

                case SectionExpr se:
                    return VisitNodeCore(se.Source as Expr, onEnter, onLeave, cancellationToken);

                case DeleteExpr d:
                    if (!VisitNodeCore(d.Table as Expr, onEnter, onLeave, cancellationToken)) return false;
                    return VisitNodeCore(d.Where, onEnter, onLeave, cancellationToken);

                case UpdateExpr upd:
                    if (!VisitNodeCore(upd.Table as Expr, onEnter, onLeave, cancellationToken)) return false;
                    foreach (var set in upd.Sets)
                    {
                        if (!VisitNodeCore(set.Property, onEnter, onLeave, cancellationToken)) return false;
                        if (!VisitNodeCore(set.Value, onEnter, onLeave, cancellationToken)) return false;
                    }
                    return VisitNodeCore(upd.Where, onEnter, onLeave, cancellationToken);

                default:
                    return true;
            }
        }
    }
}
