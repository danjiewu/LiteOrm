namespace LiteOrm.Common
{
    /// <summary>
    /// ÎªÂß¼­¶þÔª²Ù×÷·ûÌá¹©µÄ±ã½ÝÀ©Õ¹¹¤¾ß¡£
    /// </summary>
    public static class LogicBinaryOperatorExt
    {
        /// <summary>
        /// ¼ì²éÖ¸¶¨µÄ²Ù×÷·ûÊÇ·ñº¬ÓÐ NOT ±êÖ¾¡£
        /// </summary>
        public static bool IsNot(this LogicOperator oper)
        {
            return (oper & LogicOperator.Not) == LogicOperator.Not;
        }

        /// <summary>
        /// »ñÈ¡È¥µô NOT ±êÖ¾ºóµÄÕýÏò²Ù×÷·û¡£
        /// </summary>
        public static LogicOperator Positive(this LogicOperator oper)
        {
            return oper & ~LogicOperator.Not;
        }

        /// <summary>
        /// »ñÈ¡µ±Ç°²Ù×÷·ûµÄ·´Ïò°æ±¾£¨È¡·´£©¡£
        /// </summary>
        public static LogicOperator Opposite(this LogicOperator oper)
        {
            return oper ^ LogicOperator.Not;
        }
    }
}
