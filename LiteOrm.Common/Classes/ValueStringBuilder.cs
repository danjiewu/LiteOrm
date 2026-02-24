#nullable enable
using System;
using System.Buffers;

namespace LiteOrm.Common
{
    /// <summary>
    /// 提供一种高效的、基于栈或池的字符串构建器，支持 ReadOnlySpan 操作以减少内存分配。
    /// </summary>
    public ref struct ValueStringBuilder
    {
        private char[]? _arrayToReturnToPool;
        private Span<char> _chars;
        private int _length;

        /// <summary>
        /// 使用指定的初始缓冲区初始化 <see cref="ValueStringBuilder"/> 的新实例。
        /// </summary>
        /// <param name="initialBuffer">初始字符缓冲区。</param>
        public ValueStringBuilder(Span<char> initialBuffer)
        {
            _arrayToReturnToPool = null;
            _chars = initialBuffer;
            _length = 0;
        }

        /// <summary>
        /// 创建一个具有指定初始容量的 <see cref="ValueStringBuilder"/> 实例。
        /// 缓冲区将从 <see cref="ArrayPool{T}.Shared"/> 租借。
        /// </summary>
        /// <param name="initialCapacity">初始容量，默认为 128。</param>
        /// <returns>一个新的 <see cref="ValueStringBuilder"/> 实例。</returns>
        public static ValueStringBuilder Create(int initialCapacity = 128)
        {
            char[] array = ArrayPool<char>.Shared.Rent(initialCapacity);
            return new ValueStringBuilder(array)
            {
                _arrayToReturnToPool = array
            };
        }

        /// <summary>
        /// 获取或设置当前构建器中字符的长度。
        /// </summary>
        public int Length
        {
            get => _length;
            set
            {
                if (value < 0 || value > _chars.Length) throw new ArgumentOutOfRangeException();
                _length = value;
            }
        }

        /// <summary>
        /// 获取当前构建器的总容量。
        /// </summary>
        public int Capacity => _chars.Length;

        /// <summary>
        /// 将指定字符串的副本追加到此实例。
        /// </summary>
        /// <param name="value">要追加的字符串。</param>
        public void Append(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;

            if (value!.Length == 1)
            {
                Append(value[0]);
                return;
            }

            Append(value.AsSpan());
        }

        /// <summary>
        /// 将指定 Unicode 字符的副本追加到此实例。
        /// </summary>
        /// <param name="c">要追加的字符。</param>
        public ValueStringBuilder Append(char c)
        {
            if (_length >= _chars.Length) Grow(1);
            _chars[_length++] = c;
            return this;
        }

        /// <summary>
        /// 将指定字符序列的副本追加到此实例。
        /// </summary>
        /// <param name="value">要追加的字符序列。</param>
        public ValueStringBuilder Append(ReadOnlySpan<char> value)
        {
            if (value.IsEmpty) return this;

            int valueLength = value.Length;
            if (_length + valueLength > _chars.Length) Grow(valueLength);

            value.CopyTo(_chars.Slice(_length));
            _length += valueLength;
            return this;
        }

        /// <summary>
        /// 从此实例中移除所有字符，使长度为零。
        /// </summary>
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

        /// <summary>
        /// 获取表示当前内容的只读字符序列。
        /// </summary>
        /// <returns>只读字符序列。</returns>
        public ReadOnlySpan<char> AsSpan() => _chars.Slice(0, _length);

        /// <summary>
        /// 释放由此实例使用的资源，包括归还租借的数组到池中。
        /// </summary>
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
