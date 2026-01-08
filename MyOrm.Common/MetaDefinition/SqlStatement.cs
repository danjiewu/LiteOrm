using System;
using System.Collections.Generic;
using System.Text;

namespace MyOrm.Common
{
    /// <summary>
    /// SQL项
    /// </summary>
    public abstract class SqlStatement
    {
        private string name;
        /// <summary>
        /// 名称
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
        /// 格式化的名称
        /// </summary>
        /// <param name="sqlBuilder"></param>
        /// <returns></returns>
        public virtual string FormattedName(ISqlBuilder sqlBuilder)
        {
            return sqlBuilder.ToSqlName(Name);
        }

        /// <summary>
        /// 格式化的表达式
        /// </summary>
        /// <param name="sqlBuilder"></param>
        /// <returns></returns>
        public abstract string FormattedExpression(ISqlBuilder sqlBuilder);


        /// <summary>
        /// 得到名称的字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name;
        }
    }
}
