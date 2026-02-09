namespace LiteOrm.Common
{
    /// <summary>
    /// ÁÐ²Ù×÷Ä£Ê½µÄÀ©Õ¹·½·¨
    /// </summary>
    public static class ColumnModeExt
    {
        /// <summary>
        /// ¼ì²éÁÐÄ£Ê½ÊÇ·ñÔÊÐí²åÈë²Ù×÷
        /// </summary>
        /// <param name="mode">ÁÐ²Ù×÷Ä£Ê½</param>
        /// <returns>Èç¹ûÔÊÐí²åÈëÔò·µ»Øtrue£¬·ñÔò·µ»Øfalse</returns>
        public static bool CanInsert(this ColumnMode mode)
        {
            return (mode & ColumnMode.Insert) != ColumnMode.None;
        }

        /// <summary>
        /// ¼ì²éÁÐÄ£Ê½ÊÇ·ñÔÊÐí¸üÐÂ²Ù×÷
        /// </summary>
        /// <param name="mode">ÁÐ²Ù×÷Ä£Ê½</param>
        /// <returns>Èç¹ûÔÊÐí¸üÐÂÔò·µ»Øtrue£¬·ñÔò·µ»Øfalse</returns>
        public static bool CanUpdate(this ColumnMode mode)
        {
            return (mode & ColumnMode.Update) != ColumnMode.None;
        }

        /// <summary>
        /// ¼ì²éÁÐÄ£Ê½ÊÇ·ñÔÊÐí¶ÁÈ¡²Ù×÷
        /// </summary>
        /// <param name="mode">ÁÐ²Ù×÷Ä£Ê½</param>
        /// <returns>Èç¹ûÔÊÐí¶ÁÈ¡Ôò·µ»Øtrue£¬·ñÔò·µ»Øfalse</returns>
        public static bool CanRead(this ColumnMode mode)
        {
            return (mode & ColumnMode.Read) != ColumnMode.None;
        }

    }
}
