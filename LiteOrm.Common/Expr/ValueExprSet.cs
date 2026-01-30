using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 值类型表达式集合，用于列表（IN 参数）或字符串拼接（CONCAT）。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class ValueExprSet : ValueTypeExpr, ICollection<ValueTypeExpr>
    {
        public ValueExprSet() { }

        public ValueExprSet(params ValueTypeExpr[] items)
        {
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        public ValueExprSet(IEnumerable<ValueTypeExpr> items)
        {
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        public ValueExprSet(ValueJoinType joinType, params ValueTypeExpr[] items)
        {
            JoinType = joinType;
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        public ValueExprSet(ValueJoinType joinType, IEnumerable<ValueTypeExpr> items)
        {
            JoinType = joinType;
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        public override bool IsValue => true;

        public ValueJoinType JoinType { get; set; } = ValueJoinType.List;

        public ReadOnlyCollection<ValueTypeExpr> Items => items.AsReadOnly();
        private List<ValueTypeExpr> items = new List<ValueTypeExpr>();

        public int Count => items.Count;
        public bool IsReadOnly => false;

        public ValueTypeExpr this[int index] => items[index];

        public void Add(ValueTypeExpr item)
        {
            if (item is null) item = Null;
            if (item is ValueExprSet set && set.JoinType == JoinType)
            {
                items.AddRange(set.items);
            }
            else
            {
                items.Add(item);
            }
        }

        public void AddRange(IEnumerable<ValueTypeExpr> items)
        {
            foreach (var item in items) Add(item);
        }

        public void Clear() => items.Clear();
        public bool Contains(ValueTypeExpr item) => items.Contains(item);
        public void CopyTo(ValueTypeExpr[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);
        public bool Remove(ValueTypeExpr item) => items.Remove(item);
        public IEnumerator<ValueTypeExpr> GetEnumerator() => items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

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

        public override bool Equals(object obj)
        {
            if (obj is ValueExprSet set)
            {
                if (set.JoinType != JoinType || items.Count != set.items.Count) return false;
                // 顺序敏感比较
                for (int i = 0; i < items.Count; i++)
                {
                    if (!Object.Equals(items[i], set.items[i])) return false;
                }
                return true;
            }
            return false;
        }

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
