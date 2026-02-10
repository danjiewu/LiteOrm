using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示生成的 SQL 片段结果结构，用于分阶段构建复杂的 SQL 语句。
    /// </summary>
    public ref struct SqlValueStringBuilder
    {
        /// <summary>SELECT 子句片段。</summary>
        public ValueStringBuilder Select;
        /// <summary>FROM 子句片段。</summary>
        public ValueStringBuilder From;
        /// <summary>WHERE 子句片段。</summary>
        public ValueStringBuilder Where;
        /// <summary>GROUP BY 子句片段。</summary>
        public ValueStringBuilder GroupBy;
        /// <summary>HAVING 子句片段。</summary>
        public ValueStringBuilder Having;
        /// <summary>ORDER BY 子句片段。</summary>
        public ValueStringBuilder OrderBy;
        /// <summary>分页跳过的记录数。</summary>
        public int Skip;
        /// <summary>分页获取的记录数。</summary>
        public int Take;

        /// <summary>
        /// 构造函数，使用默认容量的堆分配
        /// </summary>
        public SqlValueStringBuilder()
        {
            this.Select = ValueStringBuilder.Create(256);
            this.From = ValueStringBuilder.Create(256);
            this.Where = ValueStringBuilder.Create(256);
            this.GroupBy = ValueStringBuilder.Create(256);
            this.Having = ValueStringBuilder.Create(256);
            this.OrderBy = ValueStringBuilder.Create(256);
            this.Skip = 0;
            this.Take = 0;
        }

        /// <summary>
        /// 释放所有部分
        /// </summary>
        public void Dispose()
        {
            this.Select.Dispose();
            this.From.Dispose();
            this.Where.Dispose();
            this.GroupBy.Dispose();
            this.Having.Dispose();
            this.OrderBy.Dispose();
        }
    }
}
