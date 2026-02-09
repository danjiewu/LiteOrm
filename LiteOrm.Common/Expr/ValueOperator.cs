using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// Ö§³ÖµÄÖµ¶þÔª²Ù×÷·û¡£
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ValueOperator
    {
        /// <summary>
        /// ¼Ó·¨
        /// </summary>
        Add = 0,
        /// <summary>
        /// ¼õ·¨
        /// </summary>
        Subtract = 1,
        /// <summary>
        /// ³Ë·¨
        /// </summary>
        Multiply = 2,
        /// <summary>
        /// ³ý·¨
        /// </summary>
        Divide = 3,
        /// <summary>
        /// ×Ö·û´®Á¬½Ó
        /// </summary>
        Concat = 4
    }
}
