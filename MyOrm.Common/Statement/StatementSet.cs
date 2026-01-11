using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{

    /// <summary>
    /// 多语句集合（AND / OR / 逗号分隔），通常用于组合多个条件或生成列列表等。
    /// </summary>
    public sealed class StatementSet : Statement, ICollection<Statement>
    {
        /// <summary>
        /// 构造并初始化空集合。
        /// </summary>
        public StatementSet()
        {
        }

        /// <summary>
        /// 使用一组语句初始化集合。
        /// </summary>
        /// <param name="items">语句项</param>
        public StatementSet(params Statement[] items)
        {
            Items.AddRange(items);
        }

        /// <summary>
        /// 使用指定连接类型和语句项初始化集合。
        /// </summary>
        /// <param name="joinType">连接类型（And/Or/Comma）</param>
        /// <param name="items">语句项</param>
        public StatementSet(StatementJoinType joinType, params Statement[] items)
        {
            JoinType = joinType;
            foreach (var item in items)
            {
                Add(item);
            }
        }

        /// <summary>
        /// 集合的连接类型
        /// </summary>
        public StatementJoinType JoinType { get; set; }

        /// <summary>
        /// 集合中的语句项
        /// </summary>
        public List<Statement> Items { get; } = new List<Statement>();

        public int Count => Items.Count;

        public bool IsReadOnly => false;

        public void Add(Statement item)
        {
            item ??= Null;
            if (item is StatementSet set && set.JoinType == JoinType)
                Items.AddRange(set.Items);
            else
                Items.Add(item);
        }

        public void Clear()
        {
            Items.Clear();
        }

        public bool Contains(Statement item)
        {
            return Items.Contains(item);
        }

        public void CopyTo(Statement[] array, int arrayIndex)
        {
            Items.CopyTo(array, arrayIndex);
        }

        public IEnumerator<Statement> GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        public bool Remove(Statement item)
        {
            return Items.Remove(item);
        }

        /// <inheritdoc/>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (JoinType == StatementJoinType.Concat)
                return sqlBuilder.BuildConcatSql(Items.Select(s => s.ToSql(context, sqlBuilder, outputParams)).ToArray());
            string joinStr = JoinType switch
            {
                StatementJoinType.And => " AND ",
                StatementJoinType.Or => " OR ",
                StatementJoinType.Concat => " || ",
                _ => ","
            };
            return $"({String.Join(joinStr, Items.Select(s => s.ToSql(context, sqlBuilder, outputParams)))})";
        }

        public override string ToString()
        {
            string joinStr = JoinType switch
            {
                StatementJoinType.And => " AND ",
                StatementJoinType.Or => " OR ",
                StatementJoinType.Concat => " || ",
                _ => ","
            };
            return $"({String.Join(joinStr, Items)})";
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override bool Equals(object obj)
        {
            if (obj is StatementSet set)
            {
                if (set.JoinType != JoinType || set.Count != Count) return false;
                HashSet<Statement> matched = new(Items);
                foreach (var item in set.Items)
                {
                    if (!matched.Contains(item)) return false;
                }
                return true;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), JoinType.GetHashCode(), Items.Sum(s => s?.GetHashCode() ?? 0));
        }
    }

    /// <summary>
    /// 语句集合的连接类型枚举。
    /// </summary>
    public enum StatementJoinType
    {
        /// <summary>
        /// 默认连接（通常用于列表）
        /// </summary>
        Default = 0,
        /// <summary>
        /// 使用 AND 连接
        /// </summary>
        And = 1,
        /// <summary>
        /// 使用 OR 连接
        /// </summary>
        Or = 2,
        /// <summary>
        /// 使用字符串连接符连接（通常用于字符串拼接）
        /// </summary>
        Concat = 3
    }
}
