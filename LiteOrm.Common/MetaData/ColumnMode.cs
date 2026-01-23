using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 列操作模式
    /// </summary>
    [Flags]
    public enum ColumnMode
    {
        /// <summary>
        /// 所有操作
        /// </summary>
        Full = Read | Update | Insert,
        /// <summary>
        /// 无
        /// </summary>
        None = 0,
        /// <summary>
        /// 从数据库中读
        /// </summary>
        Read = 1,
        /// <summary>
        /// 向数据库更新
        /// </summary>
        Update = 2,
        /// <summary>
        /// 向数据库添加
        /// </summary>
        Insert = 4,
        /// <summary>
        /// 只写
        /// </summary>
        Write = Insert | Update,
        /// <summary>
        /// 不可更改
        /// </summary>
        Final = Insert | Read
    }
}
