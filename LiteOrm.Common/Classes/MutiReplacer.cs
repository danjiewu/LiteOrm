using System;
using System.Collections.Generic;

namespace LiteOrm.Common
{
    /// <summary>
    /// 基于Trie树的字符串替换工具类
    /// </summary>
    public class MutiReplacer
    {
        /// <summary>
        /// 字典树节点
        /// </summary>
        private class TrieNode
        {
            public Dictionary<char, TrieNode> Children { get; set; } = new Dictionary<char, TrieNode>();
            public string Key { get; set; }
            public string Replacement { get; set; }
            public bool IsEndOfWord { get; set; }
        }

        private readonly TrieNode root = new TrieNode();

        /// <summary>
        /// 向Trie树中插入一个键值对
        /// </summary>
        /// <param name="key">要插入的键</param>
        /// <param name="replacement">对应的替换值</param>
        public void Insert(string key, string replacement)
        {
            TrieNode node = root;
            foreach (char c in key)
            {
                if (!node.Children.TryGetValue(c, out TrieNode value))
                {
                    value = new TrieNode();
                    node.Children[c] = value;
                }
                node = value;
            }
            node.IsEndOfWord = true;
            node.Key = key;
            node.Replacement = replacement;
        }

        /// <summary>
        /// 在给定位置查找最长匹配
        /// </summary>
        /// <param name="text">待搜索的文本</param>
        /// <param name="startIndex">起始索引</param>
        /// <returns>返回匹配的TrieNode节点，如果没有找到则返回null</returns>
        private TrieNode FindLongestMatch(string text, int startIndex)
        {
            TrieNode node = root;
            TrieNode maxLengthNode = null;

            for (int i = startIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (!node.Children.ContainsKey(c))
                {
                    break;
                }
                node = node.Children[c];
                if (node.IsEndOfWord)
                {
                    maxLengthNode = node;
                }
            }

            return maxLengthNode;
        }

        /// <summary>
        /// 进行替换操作
        /// </summary>
        /// <param name="text">待替换的文本</param>
        /// <param name="replacementProvider">自定义替换提供委托，返回替换值或null表示使用默认替换。为空时使用默认替换。</param>
        /// <returns>替换后的文本</returns>
        public string Replace(string text, Func<string, string> replacementProvider = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            Span<char> initialBuffer = stackalloc char[text.Length];
            var result = new ValueStringBuilder(initialBuffer);
            int i = 0;

            while (i < text.Length)
            {
                TrieNode matchNode = FindLongestMatch(text, i);
                if (matchNode != null)
                {
                    string replacement;
                    if (replacementProvider != null)
                    {
                        replacement = replacementProvider(matchNode.Key);
                        if (replacement == null)
                        {
                            replacement = matchNode.Replacement;
                        }
                    }
                    else
                    {
                        replacement = matchNode.Replacement;
                    }
                    result.Append(replacement);
                    i += matchNode.Key.Length;
                }
                else
                {
                    result.Append(text[i]);
                    i++;
                }
            }

            string finalResult = result.ToString();
            result.Dispose();
            return finalResult;
        }
    }
}
