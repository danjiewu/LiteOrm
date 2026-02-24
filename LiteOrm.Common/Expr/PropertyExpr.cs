using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示数据库列或实体属性引用的表达式。
    /// 在生成的 SQL 中，它通常被解析为带有表限定符和定界符的列名。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class PropertyExpr : ValueTypeExpr
    {
        /// <summary>
        /// 无参构造，主要用于 JSON 反序列化。
        /// </summary>
        public PropertyExpr()
        {
        }



        /// <summary>
        /// 使用属性名称初始化 PropertyExpr。
        /// </summary>
        /// <param name="propertyName">实体对应的属性名称（通常与数据库列名映射）。</param>
        public PropertyExpr(string propertyName)
        {
            if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));
            var names = propertyName.Split('.');
            PropertyName = names[names.Length-1];
            TableAlias = names.Length > 1 ? names[0] : null;
        }

        private string _tableAlias;
        /// <summary>
        /// 获取或设置表别名（如果有）。在生成 SQL 时，如果提供了表别名，列名将以 "TableAlias.ColumnName" 的形式出现。
        /// </summary>
        public string TableAlias 
        {
            get { return _tableAlias; }
            set 
            {
                if (value != null && !LiteOrm.Common.Const.ValidNameRegex.IsMatch(value))
                {
                    throw new ArgumentException("Table alias contains illegal characters. Only letters, numbers, and underscores are allowed.", nameof(TableAlias));
                }
                _tableAlias = value;
            }
        }

        private string _propertyName;
        /// <summary>
        /// 获取或设置目标属性（列）的名称。
        /// </summary>
        public string PropertyName 
        {
            get { return _propertyName; }
            set 
            {
                if (value != null && !LiteOrm.Common.Const.ValidNameRegex.IsMatch(value))
                {
                    throw new ArgumentException("Property name contains illegal characters. Only letters, numbers, and underscores are allowed.", nameof(PropertyName));
                }
                _propertyName = value;
            }
        }

        /// <summary>
        /// 返回针对该属性的预览字符串（如 "[PropName]"）。
        /// </summary>
        public override string ToString()
        {
            if (TableAlias != null)
                return $"[{TableAlias}].[{PropertyName}]";
            else
                return $"[{PropertyName}]";
        }

        /// <summary>
        /// 比较两个 PropertyExpr 是否引用同一个属性。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is PropertyExpr p && p.TableAlias == TableAlias && p.PropertyName == PropertyName;
        }

        /// <summary>
        /// 作为默认哈希函数。
        /// </summary>
        /// <returns>当前对象的哈希代码。</returns>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), TableAlias?.GetHashCode() ?? 0, PropertyName?.GetHashCode() ?? 0);
        }
    }
}
