using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 值类型表达式集合，用于列表（IN 查询）或字符串拼接（CONCAT）
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class ValueSet : ValueTypeExpr, ICollection<ValueTypeExpr>
    {
        /// <summary>
        /// 初始化默认的值集合
        /// </summary>
        public ValueSet() { }

        /// <summary>
        /// 使用指定的值类型表达式数组初始化值集合
        /// </summary>
        /// <param name="items">要添加的值类型表达式数组</param>
        public ValueSet(params ValueTypeExpr[] items)
        {
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        /// <summary>
        /// 使用指定的值类型表达式集合初始化值集合
        /// </summary>
        /// <param name="items">要添加的值类型表达式集合</param>
        public ValueSet(IEnumerable<ValueTypeExpr> items)
        {
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        /// <summary>
        /// 使用指定的连接类型和值类型表达式数组初始化值集合
        /// </summary>
        /// <param name="joinType">值之间的连接类型（List 或 Concat）</param>
        /// <param name="items">要添加的值类型表达式数组</param>
        public ValueSet(ValueJoinType joinType, params ValueTypeExpr[] items)
        {
            JoinType = joinType;
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        /// <summary>
        /// 使用指定的连接类型和值类型表达式集合初始化值集合
        /// </summary>
        /// <param name="joinType">值之间的连接类型（List 或 Concat）</param>
        /// <param name="items">要添加的值类型表达式集合</param>
        public ValueSet(ValueJoinType joinType, IEnumerable<ValueTypeExpr> items)
        {
            JoinType = joinType;
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        /// <summary>
        /// 获取或设置值之间的连接类型
        /// </summary>
        public ValueJoinType JoinType { get; set; } = ValueJoinType.List;

        /// <summary>
        /// 获取集合中的值类型表达式只读集合
        /// </summary>
        public ReadOnlyCollection<ValueTypeExpr> Items => items.AsReadOnly();
        private List<ValueTypeExpr> items = new List<ValueTypeExpr>();

        /// <summary>
        /// 获取集合中的元素数量
        /// </summary>
        public int Count => items.Count;

        /// <summary>
        /// 获取一个值，指示集合是否为只读
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// 获取或设置指定索引处的值类型表达式
        /// </summary>
        /// <param name="index">要获取或设置的索引</param>
        /// <returns>指定索引处的值类型表达式</returns>
        public ValueTypeExpr this[int index] => items[index];

        /// <summary>
        /// 向集合中添加一个值类型表达式
        /// </summary>
        /// <param name="item">要添加的值类型表达式</param>
        public void Add(ValueTypeExpr item)
        {
            if (item is null) item = Null;
            if (item is ValueSet set && set.JoinType == JoinType)
            {
                items.AddRange(set.items);
            }
            else
            {
                items.Add(item);
            }
        }

        /// <summary>
        /// 向集合中添加多个值类型表达式
        /// </summary>
        /// <param name="items">要添加的值类型表达式集合</param>
        public void AddRange(IEnumerable<ValueTypeExpr> items)
        {
            foreach (var item in items) Add(item);
        }

        /// <summary>
        /// 清空集合中的所有元素
        /// </summary>
        public void Clear() => items.Clear();

        /// <summary>
        /// 判断集合是否包含指定的值类型表达式
        /// </summary>
        /// <param name="item">要查找的值类型表达式</param>
        /// <returns>如果包含返回 true，否则返回 false</returns>
        public bool Contains(ValueTypeExpr item) => items.Contains(item);

        /// <summary>
        /// 将集合中的元素复制到指定的数组中
        /// </summary>
        /// <param name="array">目标数组</param>
        /// <param name="arrayIndex">开始复制的索引位置</param>
        public void CopyTo(ValueTypeExpr[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

        /// <summary>
        /// 从集合中移除指定的值类型表达式
        /// </summary>
        /// <param name="item">要移除的值类型表达式</param>
        /// <returns>如果成功移除返回 true，否则返回 false</returns>
        public bool Remove(ValueTypeExpr item) => items.Remove(item);

        /// <summary>
        /// 返回集合的枚举器
        /// </summary>
        /// <returns>集合的枚举器</returns>
        public IEnumerator<ValueTypeExpr> GetEnumerator() => items.GetEnumerator();

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
                ValueJoinType.Concat => " || ",
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
            if (obj is ValueSet set)
            {
                if (set.JoinType != JoinType || items.Count != set.items.Count) return false;
                // 顺序逐项比较
                for (int i = 0; i < items.Count; i++)
                {
                    if (!Object.Equals(items[i], set.items[i])) return false;
                }
                return true;
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
            foreach (var item in items)
            {
                hashcode = (hashcode * HashSeed) + (item?.GetHashCode() ?? 0);
            }
            return hashcode;
        }
    }
}
