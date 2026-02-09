using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// ÁÐ²Ù×÷Ä£Ê½
    /// </summary>
    [Flags]
    public enum ColumnMode
    {
        /// <summary>
        /// ËùÓÐ²Ù×÷
        /// </summary>
        Full = Read | Update | Insert,
        /// <summary>
        /// ÎÞ
        /// </summary>
        None = 0,
        /// <summary>
        /// ´ÓÊý¾Ý¿âÖÐ¶Á
        /// </summary>
        Read = 1,
        /// <summary>
        /// ÏòÊý¾Ý¿â¸üÐÂ
        /// </summary>
        Update = 2,
        /// <summary>
        /// ÏòÊý¾Ý¿âÌí¼Ó
        /// </summary>
        Insert = 4,
        /// <summary>
        /// Ö»Ð´
        /// </summary>
        Write = Insert | Update,
        /// <summary>
        /// ²»¿É¸ü¸Ä
        /// </summary>
        Final = Insert | Read
    }
}
