using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// ÖµÀàÐÍ±í´ïÊ½¼¯ºÏµÄÁ¬½Ó·½Ê½¡£
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ValueJoinType
    {
        /// <summary>
        /// ¶ººÅ·Ö¸ôÁÐ±í£¨Èç IN (@p1, @p2)£©
        /// </summary>
        List = 0,
        /// <summary>
        /// ×Ö·û´®Á¬½Ó·½Ê½£¨Èç CONCAT(s1, s2)£©
        /// </summary>
        Concat = 3
    }
}
