#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm.Common
{
    /// <summary>
    /// 值比较工具类
    /// </summary>
    public class ValueEquality
    {
        // 缓存类型到转换器的映射
        private static readonly Dictionary<Type, Func<object, decimal?>> DecimalConverters =
            new Dictionary<Type, Func<object, decimal?>>
        {
        { typeof(byte), o => (byte)o },
        { typeof(sbyte), o => (sbyte)o },
        { typeof(short), o => (short)o },
        { typeof(ushort), o => (ushort)o },
        { typeof(int), o => (int)o },
        { typeof(uint), o => (uint)o },
        { typeof(long), o => (long)o },
        { typeof(ulong), o => (ulong)o }
        };

        private static readonly Dictionary<Type, Func<object, double?>> DoubleConverters =
            new Dictionary<Type, Func<object, double?>>
        {
        { typeof(byte), o => (byte)o },
        { typeof(sbyte), o => (sbyte)o },
        { typeof(short), o => (short)o },
        { typeof(ushort), o => (ushort)o },
        { typeof(int), o => (int)o },
        { typeof(uint), o => (uint)o },
        { typeof(long), o => (long)o },
        { typeof(ulong), o => (ulong)o },
        { typeof(decimal), o => decimal.ToDouble((decimal)o) },
        { typeof(float), o => (float)o },
        { typeof(double), o => (double)o }
        };

        private static bool TryConvertToDecimal(object val, out decimal? result)
        {
            var type = val.GetType();
            if (DecimalConverters.TryGetValue(type, out var converter))
            {
                result = converter(val);
                return true;
            }
            result = null;
            return false;
        }

        private static bool TryConvertToDouble(object val, out double? result)
        {
            var type = val.GetType();
            if (DoubleConverters.TryGetValue(type, out var converter))
            {
                result = converter(val);
                return true;
            }
            result = null;
            return false;
        }

        /// <summary>
        /// 判断值相等
        /// </summary>
        /// <param name="val1">左值</param>
        /// <param name="val2">右值</param>
        /// <param name="depth">递归深度</param>
        /// <returns></returns>
        public static bool ValueEquals(object? val1, object? val2, int depth = 0)
        {
            if (val1 is null && val2 is null) return true;
            if (val1 is null || val2 is null) return false;
            if (TryConvertToDecimal(val1, out var dec1) && TryConvertToDecimal(val2, out var dec2))
            {
                return dec1 == dec2;
            }
            if (TryConvertToDouble(val1, out var dbl1) && TryConvertToDouble(val2, out var dbl2))
            {
                return dbl1 == dbl2;
            }
            //递归最大深度限制
            if (depth >= 10)
            {
                return val1.Equals(val2);
            }

            // 处理集合比较
            if (val1 is IList list1 && val2 is IList list2)
            {
                if (list1.Count != list2.Count) return false;
                for (int i = 0; i < list1.Count; i++)
                {
                    if (!ValueEquals(list1[i], list2[i], depth + 1)) return false;
                }
                return true;
            }
            if (val1 is IEnumerable enum1 && val2 is IEnumerable enum2
                && !(val1 is string) && !(val2 is string))
            {
                var e1 = enum1.GetEnumerator();
                var e2 = enum2.GetEnumerator();
                using (var disposable1 = e1 as IDisposable)
                {
                    using (var disposable2 = e2 as IDisposable)
                    {
                        while (true)
                        {
                            bool m1 = e1.MoveNext();
                            bool m2 = e2.MoveNext();
                            if (m1 != m2) return false;
                            if (!m1) break;
                            if (!ValueEquals(e1.Current, e2.Current, depth + 1)) return false;
                        }
                    }
                }
            }
            // 使用默认的 Equals 方法进行比较
            return val1.Equals(val2);
        }

        /// <summary>
        /// 获取值的哈希码。
        /// </summary>
        /// <param name="val">对象值。</param>
        /// <param name="depth">递归深度。</param>
        /// <returns>哈希码。</returns>
        public static int GetValueHashCode(object? val, int depth = 0)
        {
            if (val is null) return 0;
            if (TryConvertToDecimal(val, out var dec))
            {
                return dec.GetHashCode();
            }
            if (TryConvertToDouble(val, out var dbl))
            {
                return dbl.GetHashCode();
            }
            if (depth >= 10)
            {
                return val.GetHashCode();
            }
            unchecked
            {
                // 处理集合哈希码
                if (val is IList list)
                {
                    int hash = 17;
                    for (int i = 0; i < list.Count; i++)
                    {
                        hash = hash * 31 + GetValueHashCode(list[i], depth + 1);
                    }
                    return hash;
                }
                if (val is IEnumerable enumerable && !(val is string))
                {
                    int hash = 17;
                    foreach (var item in enumerable)
                    {
                        hash = hash * 31 + GetValueHashCode(item, depth + 1);
                    }
                    return hash;
                }
            }
            // 使用默认的 GetHashCode 方法
            return val.GetHashCode();
        }
    }
}
