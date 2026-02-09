using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// Ö§³ÖµÄÒ»Ôª²Ù×÷·ûÃ¶¾Ù¡£
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UnaryOperator
    {
        /// <summary>
        /// ËãÊõ¸ººÅ (-)
        /// </summary>
        Nagive = 0,
        /// <summary>
        /// °´Î»È¡·´ (~)
        /// </summary>
        BitwiseNot = 1,
    }
}
