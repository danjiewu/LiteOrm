using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 表示实体属性（列）的表达式，例如用于生成 "Table.Column" 或列的表达式。
    /// </summary>
    public sealed class PropertyExpr : Expr
    {
        /// <summary>
        /// 用于序列化/反序列化 的无参构造。
        /// </summary>
        public PropertyExpr()
        {
        }

        /// <summary>
        /// 使用属性名构造一个属性表达式。
        /// </summary>
        /// <param name="propertyName">属性（列）名称</param>
        public PropertyExpr(string propertyName)
        {
            PropertyName = propertyName;
        }

        /// <summary>
        /// 属性（列）名称
        /// </summary>
        public string PropertyName { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// 会根据上下文（是否单表查询、是否存在表别名）选择使用列的格式化名称或表达式。
        /// 如果属性不存在则抛出异常。
        /// </remarks>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            SqlColumn column = context.Table.GetColumn(PropertyName);
            if (column == null) throw new Exception($"Property \"{PropertyName}\" does not exist in type \"{context.Table.DefinitionType.FullName}\". ");
            string tableAlias = context.TableAliasName;
            return tableAlias == null ? (context.SingleTable ? column.FormattedName(sqlBuilder) : column.FormattedExpression(sqlBuilder)) : $"[{tableAlias}].[{column.Name}]";
        }

        /// <summary>
        /// 返回表示当前属性的字符串。
        /// </summary>
        /// <returns>表示当前属性的字符串。</returns>
        public override string ToString()
        {
            return $"[{PropertyName}]";
        }

        /// <summary>
        /// 确定指定的对象是否等于当前对象。
        /// </summary>
        /// <param name="obj">要与当前对象进行比较的对象。</param>
        /// <returns>如果指定的对象等于当前对象，则为 true；否则为 false。</returns>
        public override bool Equals(object obj)
        {
            return obj is PropertyExpr p && p.PropertyName == PropertyName;
        }

        /// <summary>
        /// 作为默认哈希函数。
        /// </summary>
        /// <returns>当前对象的哈希代码。</returns>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), PropertyName?.GetHashCode() ?? 0);
        }
    }
}
