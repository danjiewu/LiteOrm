using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace LiteOrm.Common
{
    /// <summary>
    /// 使用 <see cref="ExprVisitor"/> 检测 <see cref="Expr"/> 树中的循环引用。
    /// 循环引用是指某个节点通过其子节点或 <see cref="SqlSegment.Source"/> 链
    /// 最终引用回自身，形成引用环。
    /// </summary>
    /// <remarks>
    /// 检测基于引用相等性（ReferenceEquals），而非值相等性。
    /// 使用 <see cref="IExprNodeVisitor"/> 接口在遍历过程中跟踪当前路径，
    /// 当某个节点在路径中重复出现时即判定为循环引用。
    /// 检测到循环时通过 <see cref="CancellationTokenSource.Cancel()"/> 中断遍历，
    /// 不使用异常控制流。
    ///
    /// 注意：由于 Expr 子类可能重写 Equals/GetHashCode 进行递归的值比较，
    /// 在存在循环引用时调用这些方法会导致栈溢出。因此检测器内部始终使用
    /// 引用相等性比较器来跟踪已访问节点。
    /// </remarks>
    public static class CycleDetector
    {
        /// <summary>
        /// 检测结果，包含循环节点和从根到循环节点的路径信息。
        /// </summary>
        public class CycleResult
        {
            /// <summary>
            /// 造成循环的节点（即第二次出现的节点，与路径中首次出现的节点引用相同）。
            /// </summary>
            public Expr CycleNode { get; internal set; }

            /// <summary>
            /// 从根节点到循环节点的路径（包含重复节点，路径中首次出现即为重复节点的父节点之一）。
            /// </summary>
            public IReadOnlyList<Expr> Path { get; internal set; }

            /// <summary>
            /// 是否检测到循环引用。
            /// </summary>
            public bool HasCycle => CycleNode != null;
        }

        /// <summary>
        /// 检测以 <paramref name="root"/> 为根的 <see cref="Expr"/> 树是否包含循环引用。
        /// </summary>
        /// <param name="root">要检测的表达式树根节点，为 <see langword="null"/> 时返回 <see langword="false"/>。</param>
        /// <returns>存在循环引用返回 <see langword="true"/>，否则返回 <see langword="false"/>。</returns>
        public static bool HasCycle(Expr root)
        {
            return Detect(root).HasCycle;
        }

        /// <summary>
        /// 在 <see cref="Expr"/> 树中查找第一个循环引用节点。
        /// </summary>
        /// <param name="root">要检测的表达式树根节点，为 <see langword="null"/> 时返回 <see langword="null"/>。</param>
        /// <returns>造成循环的节点；无循环时返回 <see langword="null"/>。</returns>
        public static Expr FindCycle(Expr root)
        {
            return Detect(root).CycleNode;
        }

        /// <summary>
        /// 对 <see cref="Expr"/> 树进行详细的循环引用检测，
        /// 返回包含循环节点和从根到循环节点路径的 <see cref="CycleResult"/>。
        /// </summary>
        /// <param name="root">要检测的表达式树根节点，为 <see langword="null"/> 时返回无循环的结果。</param>
        /// <returns>包含检测结果的 <see cref="CycleResult"/> 实例。</returns>
        public static CycleResult Detect(Expr root)
        {
            if (root == null) return new CycleResult();

            var result = new CycleResult();
            var pathSet = new HashSet<Expr>(ReferenceEqualityComparer.Instance);
            var pathList = new List<Expr>();

            using (var cts = new CancellationTokenSource())
            {
                var visitor = new CycleDetectingVisitor(pathSet, pathList, result, cts);
                ExprVisitor.Visit(visitor, root, cts.Token);
            }

            return result;
        }

        /// <summary>
        /// 基于引用相等性的 <see cref="Expr"/> 比较器。
        /// 使用 <see cref="object.ReferenceEquals"/> 和 <see cref="RuntimeHelpers.GetHashCode"/>
        /// 避免在存在循环引用时因值相等性比较导致栈溢出。
        /// </summary>
        private sealed class ReferenceEqualityComparer : IEqualityComparer<Expr>
        {
            /// <summary>
            /// 单例实例。
            /// </summary>
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            private ReferenceEqualityComparer() { }

            /// <summary>
            /// 基于引用相等性比较两个 Expr 实例。
            /// </summary>
            public bool Equals(Expr x, Expr y) => ReferenceEquals(x, y);

            /// <summary>
            /// 基于对象标识（而非其重写的 GetHashCode）获取哈希码。
            /// </summary>
            public int GetHashCode(Expr obj) => RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>
        /// 内部访问者，实现 <see cref="IExprNodeVisitor"/> 接口，
        /// 在 BeginVisit 时将节点加入当前路径集合并在离开时移除。
        /// 若节点已在路径集合中，则设置 <see cref="CycleResult"/> 并通过
        /// <see cref="CancellationTokenSource.Cancel()"/> 中断遍历。
        /// </summary>
        private class CycleDetectingVisitor : IExprNodeVisitor
        {
            private readonly HashSet<Expr> _pathSet;
            private readonly List<Expr> _pathList;
            private readonly CycleResult _result;
            private readonly CancellationTokenSource _cts;

            public CycleDetectingVisitor(HashSet<Expr> pathSet, List<Expr> pathList, CycleResult result, CancellationTokenSource cts)
            {
                _pathSet = pathSet;
                _pathList = pathList;
                _result = result;
                _cts = cts;
            }

            /// <summary>
            /// 进入节点时调用：检查是否已在当前路径中，若是则记录循环信息并取消遍历。
            /// </summary>
            public void BeginVisit(Expr node, CancellationToken cancellationToken)
            {
                if (!_pathSet.Add(node))
                {
                    // 节点已在当前路径中，形成循环引用
                    _pathList.Add(node);
                    _result.CycleNode = node;
                    _result.Path = new List<Expr>(_pathList);
                    _cts.Cancel();
                    return;
                }
                _pathList.Add(node);
            }

            /// <summary>
            /// 离开节点时调用：从当前路径中移除。
            /// </summary>
            public void EndVisit(Expr node, CancellationToken cancellationToken)
            {
                _pathSet.Remove(node);
                _pathList.RemoveAt(_pathList.Count - 1);
            }
        }
    }
}
