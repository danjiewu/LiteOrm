using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    public class StringArrayEqualityComparer : IEqualityComparer<string[]>
    {
        public static readonly StringArrayEqualityComparer Instance = new StringArrayEqualityComparer();
        public bool Equals(string[] x, string[] y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;
            if (x.Length != y.Length) return false;

            // 顺序敏感的比较
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i]) return false;
            }
            return true;
        }

        public int GetHashCode(string[] obj)
        {
            if (obj == null) return 0;

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
