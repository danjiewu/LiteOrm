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
        Final = Insert | Read,
        /// <summary>
        /// 计算列（非实际列）：不生成物理列、不参与插入/更新；
        /// 查询时按 <see cref="ColumnDefinition.Expression"/> 以表达式返回结果，
        /// 查询条件中引用该属性时同样按表达式生成。
        /// </summary>
        Computed = 8
    }
}
