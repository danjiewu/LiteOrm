using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表示 ORDER BY 子句中的单个排序项，封装排序字段表达式与排序方向。
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public sealed class OrderByItemExpr : ValueTypeExpr
    {
        /// <summary>
        /// 初始化 OrderByItemExpr 类的新实例。
        /// </summary>
        public OrderByItemExpr() { }

        /// <summary>
        /// 使用指定的字段表达式和排序方向初始化 OrderByItemExpr 类的新实例。
        /// </summary>
        /// <param name="field">排序字段表达式。</param>
        /// <param name="ascending">true 表示升序（ASC），false 表示降序（DESC）。</param>
        public OrderByItemExpr(ValueTypeExpr field, bool ascending = true)
        {
            Field = field;
            Ascending = ascending;
        }

        /// <summary>
        /// 获取或设置排序字段表达式。
        /// </summary>
        public ValueTypeExpr Field { get; set; }

        /// <summary>
        /// 获取或设置排序方向，true 表示升序（ASC），false 表示降序（DESC）。
        /// </summary>
        public bool Ascending { get; set; } = true;

        /// <summary>
        /// 将 <see cref="ValueTypeExpr"/> 与 <see cref="bool"/> 的元组隐式转换为 <see cref="OrderByItemExpr"/>。
        /// </summary>
        public static implicit operator OrderByItemExpr((ValueTypeExpr field, bool ascending) tuple)
            => new OrderByItemExpr(tuple.field, tuple.ascending);

        /// <summary>
        /// 将 <see cref="OrderByItemExpr"/> 隐式转换为 <see cref="ValueTypeExpr"/> 与 <see cref="bool"/> 的元组。
        /// </summary>
        public static implicit operator (ValueTypeExpr, bool)(OrderByItemExpr item)
            => (item.Field, item.Ascending);

        /// <inheritdoc/>
        public override bool Equals(object obj)
            => obj is OrderByItemExpr other && Equals(Field, other.Field) && Ascending == other.Ascending;

        /// <inheritdoc/>
        public override int GetHashCode()
            => OrderedHashCodes(typeof(OrderByItemExpr).GetHashCode(), Field?.GetHashCode() ?? 0, Ascending.GetHashCode());

        /// <inheritdoc/>
        public override string ToString() => $"{Field}{(Ascending ? "" : " DESC")}";
    }
}
