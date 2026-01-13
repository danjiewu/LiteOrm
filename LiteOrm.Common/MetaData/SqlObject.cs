using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm.Common
{
    /// <summary>
    /// SQL 对象基类。
    /// </summary>
    public abstract class SqlObject
    {
        private string name;
        /// <summary>
        /// 获取或设置 SQL 对象的名称。
        /// </summary>
        public virtual string Name
        {
            get { return name; }
            internal set
            {
                name = value;
            }
        }

        /// <summary>
        /// 使用指定的 SQL 构建器获取格式化后的名称。
        /// </summary>
        /// <param name="sqlBuilder">SQL 构建器实例。</param>
        /// <returns>格式化后的名称字符串。</returns>
        public virtual string FormattedName(ISqlBuilder sqlBuilder)
        {
            return sqlBuilder.ToSqlName(Name);
        }

        /// <summary>
        /// 使用指定的 SQL 构建器获取格式化后的 SQL 表达式片段。
        /// </summary>
        /// <param name="sqlBuilder">SQL 构建器实例。</param>
        /// <returns>格式化后的 SQL 表达式字符串。</returns>
        public abstract string FormattedExpression(ISqlBuilder sqlBuilder);


        /// <summary>
        /// 获取对象的字符串表示形式。
        /// </summary>
        /// <returns>包含名称的字符串。</returns>
        public override string ToString()
        {
            return Name;
        }
    }
}
