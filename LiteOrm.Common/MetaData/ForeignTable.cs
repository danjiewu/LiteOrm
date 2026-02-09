using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// Íâ²¿±íÐÅÏ¢£¬ÓÃÓÚÃèÊö¹ØÁªµÄÍâ²¿±í
    /// </summary>
    public class ForeignTable
    {
        /// <summary>
        /// Íâ²¿±í¶ÔÓ¦µÄÊµÌåÀàÐÍ
        /// </summary>
        public Type ForeignType { get; set; }

        /// <summary>
        /// ¹ýÂË±í´ïÊ½£¬ÓÃÓÚ¶¨Òå¹ØÁªÌõ¼þ
        /// </summary>
        public string FilterExpression { get; set; }
    }
}
