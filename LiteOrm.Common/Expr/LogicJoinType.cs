using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// Âß¼­±í´ïÊ½¼¯ºÏµÄÁ¬½Ó·½Ê½¡£
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LogicJoinType
    {
        /// <summary>
        /// Âß¼­ AND Á¬½Ó
        /// </summary>
        And = 1,
        /// <summary>
        /// Âß¼­ OR Á¬½Ó
        /// </summary>
        Or = 2
    }
}
