using System;
using System.Collections.Generic;
using System.Linq;

namespace LiteOrm.Common
{
    /// <summary>
    /// 用于比较两个列表是否相等的相等性比较器。
    /// </summary>
    /// <typeparam name="T">列表中元素的类型。</typeparam>
    public class ListEqualityComparer<T> : IEqualityComparer<List<T>>
    {
        /// <summary>
        /// 确定指定的两个列表是否相等。
        /// </summary>
        /// <param name="x">第一个要比较的列表。</param>
        /// <param name="y">第二个要比较的列表。</param>
        /// <returns>如果指定的列表相等，则为 true；否则为 false。</returns>
        public bool Equals(List<T> x, List<T> y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            if (x.Count != y.Count) return false;

            return x.SequenceEqual(y);
        }

        /// <summary>
        /// 返回指定列表的哈希代码。
        /// </summary>
        /// <param name="obj">要获取其哈希代码的列表。</param>
        /// <returns>指定列表的哈希代码。</returns>
        public int GetHashCode(List<T> obj)
        {
            if (obj is null) return 0;

            unchecked
            {
                int hash = 17;
                foreach (var item in obj)
                {
                    hash = hash * 31 + (item?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }
    }
}
