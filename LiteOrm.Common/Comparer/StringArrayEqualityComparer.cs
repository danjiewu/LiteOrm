using System.Collections.Generic;

namespace LiteOrm.Common
{
    /// <summary>
    /// 字符串数组相等比较器，实现顺序敏感的比较
    /// </summary>
    public class StringArrayEqualityComparer : IEqualityComparer<string[]>
    {
        /// <summary>
        /// StringArrayEqualityComparer的单例实例
        /// </summary>
        public static readonly StringArrayEqualityComparer Instance = new StringArrayEqualityComparer();

        /// <summary>
        /// 比较两个字符串数组是否相等（顺序敏感）
        /// </summary>
        /// <param name="x">第一个字符串数组</param>
        /// <param name="y">第二个字符串数组</param>
        /// <returns>如果两个数组长度相同且对应位置的元素相等，则返回true；否则返回false</returns>
        public bool Equals(string[] x, string[] y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            if (x.Length != y.Length) return false;

            // 顺序敏感的比较
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// 获取字符串数组的哈希码
        /// </summary>
        /// <param name="obj">字符串数组</param>
        /// <returns>数组的哈希码</returns>
        public int GetHashCode(string[] obj)
        {
            if (obj is null) return 0;

            // 计算数组的哈希码
            int hash = 17;
            foreach (string item in obj)
            {
                hash = hash * 31 + (item?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }
}
