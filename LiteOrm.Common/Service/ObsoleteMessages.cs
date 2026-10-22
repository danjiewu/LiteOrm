namespace LiteOrm.Common
{
    /// <summary>
    /// 过期提示用的常量文案。取值是编译期常量，可在 <c>ObsoleteAttribute</c> 中直接引用，
    /// <c>LiteOrm.Common</c> 与 <c>LiteOrm</c> 共用同一份措辞。
    /// </summary>
    public static class ObsoleteMessages
    {
        /// <summary>
        /// 接受 IQueryable Lambda 参数的查询方法统一过期提示：改用 IQueryable 参数的重载。
        /// </summary>
        public const string PreferIQueryableOverload =
            "Prefer the IQueryable<T> overload: compose the query with Expr.From<T>() and pass the result here. " +
            "The lambda form is kept only for shapes that must be parsed inside a lambda (Join, root-level aggregates) " +
            "and will be removed in a future release.";
    }
}
