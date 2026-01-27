using System;
using System.Buffers;

namespace LiteOrm.Common
{
    public ref struct ValueStringBuilder
    {
        private char[]? _arrayToReturnToPool;
        private Span<char> _chars;
        private int _length;

        // 构造函数：接受一个初始的栈缓冲区
        public ValueStringBuilder(Span<char> initialBuffer)
        {
            _arrayToReturnToPool = null;
            _chars = initialBuffer;
            _length = 0;
        }

        public static ValueStringBuilder Create(int initialCapacity = 128)
        {
            char[] array = ArrayPool<char>.Shared.Rent(initialCapacity);
            return new ValueStringBuilder(array)
            {
                _arrayToReturnToPool = array
            };
        }

        // 属性：当前长度和容量
        public int Length
        {
            get => _length;
            set
            {
                if (value < 0 || value > _chars.Length) throw new ArgumentOutOfRangeException();
                _length = value;
            }
        }

        public int Capacity => _chars.Length;

        // 核心：追加字符串（优化了空值和单字符情况）
        public void Append(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;

            if (value.Length == 1)
            {
                Append(value[0]);
                return;
            }

            Append(value.AsSpan());
        }

        // 追加字符
        public void Append(char c)
        {
            if (_length >= _chars.Length) Grow(1);
            _chars[_length++] = c;
        }

        // 追加字符序列（最高效的方法）
        public void Append(ReadOnlySpan<char> value)
        {
            if (value.IsEmpty) return;

            int valueLength = value.Length;
            if (_length + valueLength > _chars.Length) Grow(valueLength);

            value.CopyTo(_chars.Slice(_length));
            _length += valueLength;
        }

        // 清空内容（复用缓冲区）
        public void Clear() => _length = 0;

        // 确保容量足够
        private void EnsureCapacity(int capacity)
        {
            if (capacity > _chars.Length) Grow(capacity - _length);
        }

        // 扩容逻辑：租借新数组，复制数据，归还旧数组
        private void Grow(int requiredAdditionalCapacity)
        {
            // 新容量 = Max(旧容量×2, 所需总容量)
            int newCapacity = Math.Max(_chars.Length * 2, _length + requiredAdditionalCapacity);
            char[] newArray = ArrayPool<char>.Shared.Rent(newCapacity);

            // 复制现有数据
            _chars.Slice(0, _length).CopyTo(newArray);

            // 归还旧数组（如果来自池）
            if (_arrayToReturnToPool != null)
            {
                ArrayPool<char>.Shared.Return(_arrayToReturnToPool);
            }

            // 更新引用
            _chars = _arrayToReturnToPool = newArray;
        }

        /// <summary>
        /// 将当前内容转换为字符串
        /// </summary>
        public override string ToString() => _chars.Slice(0, _length).ToString();

        // 获取只读的字符序列（无分配）
        public ReadOnlySpan<char> AsSpan() => _chars.Slice(0, _length);

        // 释放租借的数组回池中
        public void Dispose()
        {
            if (_arrayToReturnToPool != null)
            {
                ArrayPool<char>.Shared.Return(_arrayToReturnToPool);
                // 重置状态，防止重复释放
                _arrayToReturnToPool = null;
                this = default;
            }
        }
    }
}
