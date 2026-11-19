using System;
using System.Collections.Generic;

namespace LiteOrm
{
    internal sealed class LruCache<TKey, TValue> where TKey : notnull
    {
        private readonly int _capacity;
        private readonly object _syncLock = new object();
        private readonly Dictionary<TKey, LinkedListNode<Entry>> _map;
        private readonly LinkedList<Entry> _order = new LinkedList<Entry>();

        public LruCache(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _map = new Dictionary<TKey, LinkedListNode<Entry>>(capacity);
        }

        public int Count
        {
            get { lock (_syncLock) return _map.Count; }
        }

        public bool TryGet(TKey key, out TValue value)
        {
            lock (_syncLock)
            {
                if (_map.TryGetValue(key, out LinkedListNode<Entry>? node))
                {
                    _order.Remove(node);
                    _order.AddFirst(node);
                    value = node.Value.Value;
                    return true;
                }
            }

            value = default!;
            return false;
        }

        public void Set(TKey key, TValue value)
        {
            lock (_syncLock)
            {
                if (_map.TryGetValue(key, out LinkedListNode<Entry>? existing))
                {
                    _order.Remove(existing);
                    _map.Remove(key);
                }

                var node = _order.AddFirst(new Entry(key, value));
                _map[key] = node;

                while (_map.Count > _capacity && _order.Last != null)
                {
                    LinkedListNode<Entry> last = _order.Last;
                    _order.RemoveLast();
                    _map.Remove(last.Value.Key);
                }
            }
        }

        public void Clear()
        {
            lock (_syncLock)
            {
                _map.Clear();
                _order.Clear();
            }
        }

        private readonly struct Entry
        {
            public Entry(TKey key, TValue value)
            {
                Key = key;
                Value = value;
            }

            public TKey Key { get; }
            public TValue Value { get; }
        }
    }
}
