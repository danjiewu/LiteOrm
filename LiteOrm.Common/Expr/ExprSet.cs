using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LiteOrm.Common
{

    /// <summary>
    /// 表达式集合，支持 AND / OR / 逗号 / 字符串拼接分隔。通常用于逻辑组合或多选值列表查询。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class ExprSet : Expr, ICollection<Expr>
    {
        /// <summary>
        /// 构造并初始化空集合。
        /// </summary>
        public ExprSet()
        {
        }

        /// <summary>
        /// 使用一组表达式初始化集合。
        /// </summary>
        /// <param name="items">表达式项</param>
        public ExprSet(params Expr[] items)
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }

        /// <summary>
        /// 使用一组表达式初始化集合。
        /// </summary>
        /// <param name="items">表达式项</param>
        public ExprSet(IEnumerable<Expr> items)
        {
            foreach (var item in items)
            {
                Add(item);
            }
        }

        /// <summary>
        /// 使用指定连接类型和表达式项初始化集合。
        /// </summary>
        /// <param name="joinType">连接类型（And/Or/Comma）</param>
        /// <param name="items">表达式项</param>
        public ExprSet(ExprJoinType joinType, params Expr[] items)
        {
            JoinType = joinType;
            foreach (var item in items)
            {
                Add(item);
            }
        }

        /// <summary>
        /// 使用指定连接类型和表达式项初始化集合。
        /// </summary>
        /// <param name="joinType">连接类型（And/Or/Comma）</param>
        /// <param name="items">表达式项</param>
        public ExprSet(ExprJoinType joinType, IEnumerable<Expr> items)
        {
            JoinType = joinType;
            foreach (var item in items)
            {
                Add(item);
            }
        }

        /// <summary>
        /// 指示当前表达式是否为值类型表达式，仅在连接类型为 <see cref="ExprJoinType.List"/> 及 <see cref="ExprJoinType.Concat"/> 时为 true。
        /// </summary>
        [JsonIgnore]
        public override bool IsValue => JoinType == ExprJoinType.List || JoinType == ExprJoinType.Concat;

        /// <summary>
        /// 集合的连接类型
        /// </summary>
        public ExprJoinType JoinType { get; set; }

        /// <summary>
        /// 集合中的表达式项
        /// </summary>
        public ReadOnlyCollection<Expr> Items => items.AsReadOnly();
        private List<Expr> items = new List<Expr>();

        /// <summary>
        /// 获取集合中包含的表达式项数。
        /// </summary>
        public int Count => items.Count;

        /// <summary>
        /// 获取一个值，该值指示集合是否为只读。
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// 将表达式项添加到集合中。
        /// </summary>
        /// <param name="item">要添加的表达式对象。</param>
        public void Add(Expr item)
        {
            if (item is null) item = Null;
            if (item is ExprSet set && set.JoinType == JoinType)
                items.AddRange(set.Items);
            else if (item.IsValue == IsValue)
                items.Add(item);
            else throw new ArgumentException($"无法将表达式项添加到集合中，表达式项的 IsValue 属性必须与集合的 IsValue 属性相同。", nameof(item));
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
        /// 返回表示当前集合表达式的字符串。
        /// </summary>
        /// <returns>表示当前表达式的字符串。</returns>
        public override string ToString()
        {
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
        /// 确定指定的对象是否等于当前对象。
        /// </summary>
        /// <param name="obj">要与当前对象进行比较的对象。</param>
        /// <returns>如果指定的对象等于当前对象，则为 true；否则为 false。</returns>
        public override bool Equals(object obj)
        {
            if (obj is ExprSet set)
            {
                if (set.JoinType != JoinType) return false;
                if (JoinType == ExprJoinType.List || JoinType == ExprJoinType.Concat)
                    return Enumerable.SequenceEqual(items, set.items);
                else
                {
                    var thisSet = new HashSet<Expr>(items);
                    var otherSet = new HashSet<Expr>(set.items);
                    return thisSet.Count == otherSet.Count && thisSet.SetEquals(otherSet);
                }
            }
            return false;
        }

        /// <summary>
        /// 作为默认哈希函数。
        /// </summary>
        /// <returns>当前对象的哈希代码。</returns>
        public override int GetHashCode()
        {
            if (JoinType == ExprJoinType.List || JoinType == ExprJoinType.Concat)
                return OrderedHashCodes(new int[] { GetType().GetHashCode(), (int)JoinType }.Concat(items.Select(s => s?.GetHashCode() ?? 0)).ToArray());
            else
                return OrderedHashCodes(GetType().GetHashCode(), (int)JoinType, items
                .Select(item => item?.GetHashCode() ?? 0)
                .Distinct()
                .Sum());
        }
    }

    /// <summary>
    /// 表达式集合的连接类型枚举。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ExprJoinType
    {
        /// <summary>
        /// 默认连接（通常用于列表）
        /// </summary>
        List = 0,
        /// <summary>
        /// 使用 AND 连接
        /// </summary>
        And = 1,
        /// <summary>
        /// 使用 OR 连接
        /// </summary>
        Or = 2,
        /// <summary>
        /// 使用字符串连接符连接（通常用于字符串拼接）
        /// </summary>
        Concat = 3
    }
}
