using System;
using System.Security.Cryptography;

namespace LiteOrm
{
    /// <summary>
    /// 生成短随机标识符的工具，使用 Base62 字符集（0-9, A-Z, a-z）。
    /// </summary>
    public static class ShortId
    {
        private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        /// <summary>
        /// 生成指定位数的 Base62 随机字符串。默认生成 8 位。
        /// </summary>
        /// <param name="length">生成字符串长度，默认为 8。</param>
        /// <returns>Base62 随机字符串。</returns>
        public static string NewId(int length = 8)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than 0");

            var bytes = new byte[length];
            FillRandom(bytes);

#if NETSTANDARD2_0
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = Base62Chars[bytes[i] % Base62Chars.Length];
            }
            return new string(chars);
#else
            // 使用 string.Create 直接在新分配的字符串内部缓冲区上写入，避免中间 char[] 拷贝
            return string.Create(length, bytes, static (span, src) =>
            {
                for (int i = 0; i < src.Length; i++)
                {
                    span[i] = Base62Chars[src[i] % Base62Chars.Length];
                }
            });
#endif
        }

        private static void FillRandom(byte[] buffer)
        {
#if NETSTANDARD2_0
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(buffer);
            }
#else
            RandomNumberGenerator.Fill(buffer);
#endif
        }
    }
}
