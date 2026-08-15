namespace LiteOrm.Common
{
    /// <summary>
    /// 列操作模式的扩展方法
    /// </summary>
    public static class ColumnModeExt
    {
        /// <summary>
        /// 检查列模式是否允许插入操作。
        /// 计算列（<see cref="ColumnMode.Computed"/>）不允许插入。
        /// </summary>
        /// <param name="mode">列操作模式</param>
        /// <returns>如果允许插入则返回true，否则返回false</returns>
        public static bool CanInsert(this ColumnMode mode)
        {
            return (mode & ColumnMode.Insert) == ColumnMode.Insert && (mode & ColumnMode.Computed) == 0;
        }

        /// <summary>
        /// 检查列模式是否允许更新操作。
        /// 计算列（<see cref="ColumnMode.Computed"/>）不允许更新。
        /// </summary>
        /// <param name="mode">列操作模式</param>
        /// <returns>如果允许更新则返回true，否则返回false</returns>
        public static bool CanUpdate(this ColumnMode mode)
        {
            return (mode & ColumnMode.Update) == ColumnMode.Update && (mode & ColumnMode.Computed) == 0;
        }

        /// <summary>
        /// 检查列模式是否允许读取操作。
        /// 计算列（<see cref="ColumnMode.Computed"/>）按表达式读取，视为可读。
        /// </summary>
        /// <param name="mode">列操作模式</param>
        /// <returns>如果允许读取则返回true，否则返回false</returns>
        public static bool CanRead(this ColumnMode mode)
        {
            return (mode & ColumnMode.Read) == ColumnMode.Read || (mode & ColumnMode.Computed) == ColumnMode.Computed;
        }

        /// <summary>
        /// 检查列模式是否为计算列（非实际列）。
        /// </summary>
        /// <param name="mode">列操作模式</param>
        /// <returns>如果为计算列则返回true，否则返回false</returns>
        public static bool IsComputed(this ColumnMode mode)
        {
            return (mode & ColumnMode.Computed) == ColumnMode.Computed;
        }

    }
}
