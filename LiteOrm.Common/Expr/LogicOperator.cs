using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// Ö§³ÖµÄÂß¼­¶þÔª²Ù×÷·û¡£
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LogicOperator
    {
        /// <summary>
        /// µÈÓÚ
        /// </summary>
        Equal = 0,
        /// <summary>
        /// ´óÓÚ
        /// </summary>
        GreaterThan = 1,
        /// <summary>
        /// Ð¡ÓÚ
        /// </summary>
        LessThan = 2,
        /// <summary>
        /// ÒÔÖ¸¶¨×Ö·û´®Îª¿ªÍ·
        /// </summary>
        StartsWith = 3,
        /// <summary>
        /// ÒÔÖ¸¶¨×Ö·û´®Îª½áÎ²
        /// </summary>
        EndsWith = 4,
        /// <summary>
        /// °üº¬Ö¸¶¨×Ö·û´®
        /// </summary>
        Contains = 5,
        /// <summary>
        /// Æ¥Åä×Ö·û´®Í¨Åä·û
        /// </summary>
        Like = 6,
        /// <summary>
        /// ÊÇ·ñÔÚ¼¯ºÏÄÚ
        /// </summary>
        In = 7,
        /// <summary>
        /// ÕýÔò±í´ïÊ½Æ¥Åä
        /// </summary>
        RegexpLike = 8,
        /// <summary>
        /// Âß¼­·Ç±êÊ¶¡£
        /// </summary>
        Not = 64,
        /// <summary>
        /// ²»µÈÓÚ
        /// </summary>
        NotEqual = Equal | Not,
        /// <summary>
        /// ´óÓÚµÈÓÚ
        /// </summary>
        GreaterThanOrEqual = LessThan | Not,
        /// <summary>
        /// Ð¡ÓÚµÈÓÚ
        /// </summary>
        LessThanOrEqual = GreaterThan | Not,
        /// <summary>
        /// ²»ÒÔÖ¸¶¨×Ö·û´®Îª¿ªÍ·
        /// </summary>
        NotStartsWith = StartsWith | Not,
        /// <summary>
        /// ²»ÒÔÖ¸¶¨×Ö·û´®Îª½áÎ²
        /// </summary>
        NotEndsWith = EndsWith | Not,
        /// <summary>
        /// ²»°üº¬Ö¸¶¨×Ö·û´®
        /// </summary>
        NotContains = Contains | Not,
        /// <summary>
        /// ²»Æ¥Åä×Ö·û´®Í¨Åä·û
        /// </summary>
        NotLike = Like | Not,
        /// <summary>
        /// ²»ÔÚ¼¯ºÏÄÚ
        /// </summary>
        NotIn = In | Not,
        /// <summary>
        /// ²»Æ¥ÅäÕýÔò±í´ïÊ½
        /// </summary>
        NotRegexpLike = RegexpLike | Not
    }
}
