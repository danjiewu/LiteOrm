using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{

    /// <summary>
    /// 表达式集合，支持通过逻辑运算符（AND / OR）或连接符（Comma / Concat）组合一组子表达式。
    /// 常用于构建复合 WHERE 条件（AND/OR 组）或 SQL 列表（如 IN (@p1, @p2)）。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class ExprSet : Expr, ICollection<Expr>
    {
        /// <summary>
        /// 构造空集合。
        /// </summary>
        public ExprSet()
        {
        }

        /// <summary>
        /// 使用一组现有的表达式初始化集合。
        /// </summary>
        /// <param name="items">要加入集合的子项。</param>
        public ExprSet(params Expr[] items)
        {
            if (items != null)
            {
                int len = items.Length;
                for (int i = 0; i < len; i++)
                {
                    Add(items[i]);
                }
            }
        }

        /// <summary>
        /// 使用一组表达式序列初始化集合。
        /// </summary>
        /// <param name="items">要加入集合的子项序列。</param>
        public ExprSet(IEnumerable<Expr> items)
        {
            if (items != null)
            {
                if (items is IList<Expr> list)
                {
                    int count = list.Count;
                    for (int i = 0; i < count; i++) Add(list[i]);
                }
                else
                {
                    foreach (var item in items)
                    {
                        Add(item);
                    }
                }
            }
        }

        /// <summary>
        /// 使用指定的连接类型和一组初始表达式初始化集合。
        /// </summary>
        /// <param name="joinType">集合内元素的逻辑连接方式。</param>
        /// <param name="items">初始子项。</param>
        public ExprSet(ExprJoinType joinType, params Expr[] items)
        {
            JoinType = joinType;
            if (items != null)
            {
                int len = items.Length;
                for (int i = 0; i < len; i++)
                {
                    Add(items[i]);
                }
            }
        }

        /// <summary>
        /// 使用指定的连接类型和子项序列初始化集合。
        /// </summary>
        public ExprSet(ExprJoinType joinType, IEnumerable<Expr> items)
        {
            JoinType = joinType;
            if (items != null)
            {
                if (items is IList<Expr> list)
                {
                    int count = list.Count;
                    for (int i = 0; i < count; i++) Add(list[i]);
                }
                else
                {
                    foreach (var item in items)
                    {
                        Add(item);
                    }
                }
            }
        }


        /// <summary>
        /// 指示当前集合是否作为值列表存在（如 LIST 形式的 IN 参数或 CONCAT 形式的拼接）。
        /// 返回 true 表示该集合整体可作为一个计算值。
        /// </summary>
        [JsonIgnore]
        public override bool IsValue => JoinType == ExprJoinType.List || JoinType == ExprJoinType.Concat;

        /// <summary>
        /// 获取或设置该集合中子项的 SQL 连接模式。
        /// </summary>
        public ExprJoinType JoinType { get; set; }

        /// <summary>
        /// 获取当前集合中所有子表达式的只读列表。
        /// </summary>
        public ReadOnlyCollection<Expr> Items => items.AsReadOnly();
        private List<Expr> items = new List<Expr>();

        /// <summary>
        /// 获取集合中包含的表达式项数。
        /// </summary>
        public int Count => items.Count;

        /// <summary>
        /// 获取指定索引处的表达式项。
        /// </summary>
        /// <param name="index">集合中要获取的项的索引。</param>
        /// <returns>指定索引处的表达式项。</returns>
        public Expr this[int index] => items[index];

        /// <summary>
        /// 获取一个值，该值指示集合是否为只读。
        /// </summary>

        public bool IsReadOnly => false;

        /// <summary>
        /// 向集合中添加一个表达式。
        /// </summary>
        /// <param name="item">要添加的子表达式。</param>
        /// <remarks>
        /// - 若添加具有相同 JoinType 的 ExprSet，会将其子项平铺添加，以简化树结构和括弧生成的冗余。
        /// - 添加时会验证 IsValue 属性的兼容性。
        /// </remarks>
        public void Add(Expr item)
        {
            if (item is null) item = Null;
            // 拍平相同逻辑类型的嵌套集合或列表类型集合 (如将 (A AND B) AND C 优化为 A AND B AND C)
            if (item is ExprSet set && (set.JoinType == JoinType || set.JoinType == ExprJoinType.List))
            {
                var otherItems = set.items;
                int count = otherItems.Count;
                for (int i = 0; i < count; i++) items.Add(otherItems[i]);
            }
            // 确保逻辑节点类型一致或为通用函数调用
            else if (item.IsValue == IsValue || item is FunctionExpr)
                items.Add(item);
            else throw new ArgumentException($"Failed to add the expression item to the collection. The IsValue property of the expression item must match the IsValue property of the collection.", nameof(item));
        }

        /// <summary>
        /// 将一组表达式项添加到集合中。
        /// </summary>
        /// <param name="items">要添加的表达式对象集合。</param>
        public void AddRange(IEnumerable<Expr> items)
        {

            foreach (var item in items)
            {
                Add(item);
            }
        }


        /// <summary>
        /// 从集合中移除所有表达式项。
        /// </summary>
        public void Clear()
        {
            items.Clear();
        }

        /// <summary>
        /// 确定集合是否包含特定的表达式项。
        /// </summary>
        /// <param name="item">要在集合中查找的对象。</param>
        /// <returns>如果在集合中找到，则为 true；否则为 false。</returns>
        public bool Contains(Expr item)
        {
            return items.Contains(item);
        }

        /// <summary>
        /// 从特定的数组索引开始，将集合的元素复制到一个数组中。
        /// </summary>
        /// <param name="array">作为从集合复制的元素的目标的一维数组。</param>
        /// <param name="arrayIndex">数组中复制开始处的从零开始的索引。</param>
        public void CopyTo(Expr[] array, int arrayIndex)
        {
            items.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// 返回一个循环访问集合的枚举器。
        /// </summary>
        /// <returns>用于循环访问集合的枚举器。</returns>
        public IEnumerator<Expr> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        /// <summary>
        /// 从集合中移除特定表达式项的第一个匹配项。
        /// </summary>
        /// <param name="item">要从集合中移除的对象。</param>
        /// <returns>如果从集合中成功移除，则为 true；否则为 false。</returns>
        public bool Remove(Expr item)
        {
            return items.Remove(item);
        }

        /// <summary>
        /// 返回当前表达式集合的字符串预览。
        /// </summary>
        /// <returns>带有连接词的括号形式字符串，如 "(A AND B AND C)"。</returns>
        public override string ToString()
        {
            int count = items.Count;
            if (count == 0) return string.Empty;
            string joinStr;
            switch (JoinType)
            {
                case ExprJoinType.And: joinStr = " AND "; break;
                case ExprJoinType.Or: joinStr = " OR "; break;
                case ExprJoinType.Concat: joinStr = " || "; break;
                default: joinStr = ","; break;
            }
            return $"({String.Join(joinStr, items)})";
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 比较两个 ExprSet 是否逻辑等价。
        /// 对于 AND/OR 类型集合，元素顺序不影响等价性；
        /// 对于 LIST/CONCAT 类型集合，顺序敏感。
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is ExprSet set)
            {
                if (set.JoinType != JoinType || items.Count != set.items.Count) return false;
                // 对于顺序敏感的连接
                if (JoinType == ExprJoinType.List || JoinType == ExprJoinType.Concat)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (!Object.Equals(items[i], set.items[i])) return false;
                    }
                    return true;
                }
                else
                {
                    // 对于无序逻辑连接
                    if (items.Count == 0) return true;
                    if (items.Count == 1) return items[0].Equals(set.items[0]);

                    // 使用集合比较提高鲁棒性 (降级到 HashSet 如果项数较多)
                    if (items.Count > 10)
                    {
                        var thisSet = new HashSet<Expr>(items);
                        var otherSet = new HashSet<Expr>(set.items);
                        return thisSet.SetEquals(otherSet);
                    }

                    // 项数较少时，直接双重循环
                    for (int i = 0; i < items.Count; i++)
                    {
                        bool found = false;
                        for (int j = 0; j < set.items.Count; j++)
                        {
                            if (items[i].Equals(set.items[j])) { found = true; break; }
                        }
                        if (!found) return false;
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 生成哈希值，确保逻辑等价的集合哈希值尽可能一致。
        /// </summary>
        public override int GetHashCode()
        {
            int hashcode = GetType().GetHashCode();
            hashcode = (hashcode * HashSeed) + (int)JoinType;

            if (JoinType == ExprJoinType.List || JoinType == ExprJoinType.Concat)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    hashcode = (hashcode * HashSeed) + (items[i]?.GetHashCode() ?? 0);
                }
            }
            else
            {
                int itemsHashSum = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    itemsHashSum = unchecked(itemsHashSum + (items[i]?.GetHashCode() ?? 0));
                }
                hashcode = (hashcode * HashSeed) + itemsHashSum;
            }
            return hashcode;
        }

    }
}
