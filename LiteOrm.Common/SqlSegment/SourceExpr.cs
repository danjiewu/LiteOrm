using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public abstract class SourceExpr : SqlSegment, ISourceAnchor
    {
        /// <summary>
        /// 别名
        /// </summary>
        private string _alias;
        /// <summary>
        /// 别名
        /// </summary>
        public string Alias
        {
            get => _alias;
            set
            {
                ThrowIfInvalidSqlName(nameof(Alias), value);
                _alias = value;
            }
        }
    }
}
