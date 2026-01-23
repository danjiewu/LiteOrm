namespace LiteOrm.Common
{
    /// <summary>
    /// 列操作模式的扩展方法
    /// </summary>
    public static class ColumnModeExt
    {
        /// <summary>
        /// 检查列模式是否允许插入操作
        /// </summary>
        /// <param name="mode">列操作模式</param>
        /// <returns>如果允许插入则返回true，否则返回false</returns>
        public static bool CanInsert(this ColumnMode mode)
        {
            return (mode & ColumnMode.Insert) != ColumnMode.None;
        }

        /// <summary>
        /// 检查列模式是否允许更新操作
        /// </summary>
        /// <param name="mode">列操作模式</param>
        /// <returns>如果允许更新则返回true，否则返回false</returns>
        public static bool CanUpdate(this ColumnMode mode)
        {
            return (mode & ColumnMode.Update) != ColumnMode.None;
        }

        /// <summary>
        /// 检查列模式是否允许读取操作
        /// </summary>
        /// <param name="mode">列操作模式</param>
        /// <returns>如果允许读取则返回true，否则返回false</returns>
        public static bool CanRead(this ColumnMode mode)
        {
            return (mode & ColumnMode.Read) != ColumnMode.None;
        }

    }
}
