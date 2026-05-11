using System;

namespace LiteOrm.Common
{
    public class CommonTableExpr : SourceExpr
    {
        // Source 为空时仍需要保留别名，以支持“只按别名引用前面已定义的 CTE”。
        private string _alias;

        /// <summary>
        /// 默认构造函数，初始化一个没有 Source 的实例。
        /// </summary>
        public CommonTableExpr()
        {
        }

        /// <summary>
        /// 创建一个新的 CommonTableExpr 实例，并将 Source 属性设置为提供的 SelectExpr 对象。Alias 属性将从 Source 的 Alias 属性继承（如果 Source 不为 null）。如果 Source 的 Alias 为空，则 Alias 将保持为 null，直到显式设置。
        /// </summary>
        /// <param name="source">SelectExpr 对象，用于初始化 Source 属性。</param>
        public CommonTableExpr(SelectExpr source)
        {
            Source = source;
        }

        /// <summary>
        /// 获取或设置 CommonTableExpr 的数据源，必须是一个 SelectExpr 对象，表示 CTE 定义的查询部分。设置 Source 时，如果提供的 SelectExpr 对象不为 null，则会将其 Alias 属性与 CommonTableExpr 的 Alias 属性保持一致，以确保在重新挂回完整定义时继续沿用之前的别名。
        /// </summary>
        public new SelectExpr Source
        {
            get => (SelectExpr)base.Source;
            set
            {
                base.Source = value;
                if (value != null)
                {
                    // 让 alias-only 引用在重新挂回完整定义时继续沿用之前的别名。
                    if (!string.IsNullOrEmpty(_alias) && string.IsNullOrEmpty(value.Alias))
                    {
                        value.Alias = _alias;
                    }
                    _alias = value.Alias;
                }
            }
        }

        /// <summary>
        /// CTE 的别名。对于“只按别名引用前面已定义的 CTE”，Source 可能为 null，此时仍需保留别名以供引用。
        /// </summary>
        public override string Alias
        {
            get => Source?.Alias ?? _alias;
            set
            {
                ThrowIfInvalidSqlName(nameof(Alias), value);
                _alias = value;
                if (Source != null)
                {
                    Source.Alias = value;
                }
            }
        }

        /// <summary>
        /// 获取表达式类型。对于 CommonTableExpr，表达式类型为 ExprType.CommonTable。
        /// </summary>
        public override ExprType ExprType => ExprType.CommonTable;

        /// <summary>
        /// 创建当前表达式的一个深复制。对于 CommonTableExpr，会复制 Source 属性（如果不为 null）并保留 Alias 属性。
        /// </summary>
        /// <returns>深复制的 CommonTableExpr 实例。</returns>
        public override Expr Clone()
        {
            return new CommonTableExpr((SelectExpr)Source?.Clone()) { Alias = Alias };
        }

        /// <summary>
        /// 判断当前实例与另一个对象是否相等。对于 CommonTableExpr，两个实例被认为相等当且仅当它们的 Source 属性相等（使用 Source 的 Equals 方法进行比较）且 Alias 属性相等。
        /// </summary>
        /// <param name="obj">要比较的对象。</param>
        /// <returns>如果两个对象相等，则返回 true；否则返回 false。</returns>
        public override bool Equals(object obj)
        {
            return obj is CommonTableExpr other
                && Alias == other.Alias
                && Equals(Source, other.Source);
        }

        /// <summary>
        /// 当前实例的哈希值。对于 CommonTableExpr，哈希代码基于 Source 属性和 Alias 属性计算，以确保与 Equals 方法的一致性。
        /// </summary>
        /// <returns>当前实例的哈希值。</returns>
        public override int GetHashCode()
        {
            return OrderedHashCodes(typeof(CommonTableExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Alias?.GetHashCode() ?? 0);
        }

        /// <summary>
        /// 返回当前实例的字符串表示形式。对于 CommonTableExpr，如果 Source 为 null，则返回 Alias；否则返回格式为 "Alias AS (Source)" 的字符串，其中 Source 的字符串表示形式由其 ToString 方法提供。
        /// </summary>
        /// <returns>当前实例的字符串表示形式。</returns>
        public override string ToString()
        {
            return Source == null ? Alias : $"{Alias} AS ({Source})";
        }
    }
}
