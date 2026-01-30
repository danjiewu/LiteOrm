using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 逻辑表达式集合，支持通过 AND 或 OR 组合一组子表达式。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class LogicSet : LogicExpr, ICollection<LogicExpr>
    {
        public LogicSet() { }

        public LogicSet(params LogicExpr[] items)
        {
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        public LogicSet(IEnumerable<LogicExpr> items)
        {
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        public LogicSet(LogicJoinType joinType, params LogicExpr[] items)
        {
            JoinType = joinType;
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        public LogicSet(LogicJoinType joinType, IEnumerable<LogicExpr> items)
        {
            JoinType = joinType;
            if (items != null)
            {
                foreach (var item in items) Add(item);
            }
        }

        public LogicJoinType JoinType { get; set; } = LogicJoinType.And;

        public ReadOnlyCollection<LogicExpr> Items => items.AsReadOnly();
        private List<LogicExpr> items = new List<LogicExpr>();

        public int Count => items.Count;
        public bool IsReadOnly => false;

        public LogicExpr this[int index] => items[index];

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

        public void AddRange(IEnumerable<LogicExpr> items)
        {
            foreach (var item in items) Add(item);
        }

        public void Clear() => items.Clear();
        public bool Contains(LogicExpr item) => items.Contains(item);
        public void CopyTo(LogicExpr[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);
        public bool Remove(LogicExpr item) => items.Remove(item);
        public IEnumerator<LogicExpr> GetEnumerator() => items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

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

        public override bool Equals(object obj)
        {
            if (obj is LogicSet set)
            {
                if (set.JoinType != JoinType || items.Count != set.items.Count) return false;
                if (items.Count == 0) return true;
                
                // 无序逻辑连接比较
                var thisSet = new HashSet<LogicExpr>(items);
                var otherSet = new HashSet<LogicExpr>(set.items);
                return thisSet.SetEquals(otherSet);
            }
            return false;
        }

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
