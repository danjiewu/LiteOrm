using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 逻辑表达式集合，支持通过 AND 或 OR 连接多个逻辑表达式
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class LogicSet : LogicExpr, ICollection<LogicExpr>
    {
        /// <summary>
        /// 初始化默认的逻辑表达式集合
        /// </summary>
        public LogicSet() { }

        /// <summary>
        /// 使用指定的逻辑表达式数组初始化逻辑表达式集合
        /// </summary>
        /// <param name="items">要添加的逻辑表达式数组</param>
        public LogicSet(params LogicExpr[] items)
        {
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        /// <summary>
        /// 使用指定的逻辑表达式集合初始化逻辑表达式集合
        /// </summary>
        /// <param name="items">要添加的逻辑表达式集合</param>
        public LogicSet(IEnumerable<LogicExpr> items)
        {
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        /// <summary>
        /// 使用指定的连接类型和逻辑表达式数组初始化逻辑表达式集合
        /// </summary>
        /// <param name="joinType">表达式之间的连接类型（AND 或 OR）</param>
        /// <param name="items">要添加的逻辑表达式数组</param>
        public LogicSet(LogicJoinType joinType, params LogicExpr[] items)
        {
            JoinType = joinType;
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        /// <summary>
        /// 使用指定的连接类型和逻辑表达式集合初始化逻辑表达式集合
        /// </summary>
        /// <param name="joinType">表达式之间的连接类型（AND 或 OR）</param>
        /// <param name="items">要添加的逻辑表达式集合</param>
        public LogicSet(LogicJoinType joinType, IEnumerable<LogicExpr> items)
        {
            JoinType = joinType;
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        /// <summary>
        /// 获取或设置表达式之间的连接类型
        /// </summary>
        public LogicJoinType JoinType { get; set; } = LogicJoinType.And;

        /// <summary>
        /// 获取集合中的逻辑表达式只读集合
        /// </summary>
        public ReadOnlyCollection<LogicExpr> Items => items.AsReadOnly();
        private List<LogicExpr> items = new List<LogicExpr>();

        /// <summary>
        /// 获取集合中的元素数量
        /// </summary>
        public int Count => items.Count;

        /// <summary>
        /// 获取一个值，指示集合是否为只读
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// 获取或设置指定索引处的逻辑表达式
        /// </summary>
        /// <param name="index">要获取或设置的索引</param>
        /// <returns>指定索引处的逻辑表达式</returns>
        public LogicExpr this[int index] => items[index];

        /// <summary>
        /// 向集合中添加一个逻辑表达式
        /// </summary>
        /// <param name="item">要添加的逻辑表达式</param>
        public void Add(LogicExpr item)
        {
            if (item is null) return;
            if (item is LogicSet set && set.JoinType == JoinType)
            {
                items.AddRange(set.items);
            }
            else
            {
                items.Add(item);
            }
        }

        /// <summary>
        /// 向集合中添加多个逻辑表达式
        /// </summary>
        /// <param name="items">要添加的逻辑表达式集合</param>
        public void AddRange(IEnumerable<LogicExpr> items)
        {
            foreach (var item in items) Add(item);
        }

        /// <summary>
        /// 清空集合中的所有元素
        /// </summary>
        public void Clear() => items.Clear();

        /// <summary>
        /// 判断集合是否包含指定的逻辑表达式
        /// </summary>
        /// <param name="item">要查找的逻辑表达式</param>
        /// <returns>如果包含返回 true，否则返回 false</returns>
        public bool Contains(LogicExpr item) => items.Contains(item);

        /// <summary>
        /// 将集合中的元素复制到指定的数组中
        /// </summary>
        /// <param name="array">目标数组</param>
        /// <param name="arrayIndex">开始复制的索引位置</param>
        public void CopyTo(LogicExpr[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

        /// <summary>
        /// 从集合中移除指定的逻辑表达式
        /// </summary>
        /// <param name="item">要移除的逻辑表达式</param>
        /// <returns>如果成功移除返回 true，否则返回 false</returns>
        public bool Remove(LogicExpr item) => items.Remove(item);

        /// <summary>
        /// 返回集合的枚举器
        /// </summary>
        /// <returns>集合的枚举器</returns>
        public IEnumerator<LogicExpr> GetEnumerator() => items.GetEnumerator();

        /// <summary>
        /// 返回集合的枚举器（非泛型实现）
        /// </summary>
        /// <returns>集合的枚举器</returns>
        IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

        /// <summary>
        /// 返回表达式的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            if (items.Count == 0) return string.Empty;
            string joinStr = JoinType switch
            {
                LogicJoinType.And => " AND ",
                LogicJoinType.Or => " OR ",
                _ => ","
            };
            return $"({String.Join(joinStr, items)})";
        }

        /// <summary>
        /// 判断当前对象是否与指定对象相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj)
        {
            if (obj is LogicSet set)
            {
                if (set.JoinType != JoinType || items.Count != set.items.Count) return false;
                if (items.Count == 0) return true;
                
                // 执行逻辑项的比较
                var thisSet = new HashSet<LogicExpr>(items);
                var otherSet = new HashSet<LogicExpr>(set.items);
                return thisSet.SetEquals(otherSet);
            }
            return false;
        }

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode()
        {
            int hashcode = GetType().GetHashCode();
            hashcode = (hashcode * HashSeed) + (int)JoinType;
            int itemsHashSum = 0;
            foreach (var item in items)
            {
                itemsHashSum = unchecked(itemsHashSum + (item?.GetHashCode() ?? 0));
            }
            return (hashcode * HashSeed) + itemsHashSum;
        }
    }
}
