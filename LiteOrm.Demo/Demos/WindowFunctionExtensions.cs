using System.Linq.Expressions;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 窗口函数排序项，指定排序字段和方向。用于替代 tuple，避免表达式树不支持 tuple 字面量的限制。
    /// </summary>
    public class SumOverOrderBy<T>
    {
        public SumOverOrderBy(Expression<Func<T, object>> field, bool ascending = true)
        {
            Field = field;
            Ascending = ascending;
        }

        public Expression<Func<T, object>> Field { get; }
        public bool Ascending { get; }
    }

    /// <summary>
    /// 窗口函数扩展方法，供表达式树 Lambda 使用。
    /// 本地执行时直接返回原值；实际 SQL 转换由注册的处理器完成。
    /// </summary>
    public static class WindowFunctionExtensions
    {
        /// <summary>仅分区字段的 SumOver 重载（params，可逐一传入）</summary>
        public static int SumOver<T>(this int amount,
            params Expression<Func<T, object>>[] partitionBy) => amount;

        /// <summary>分区字段 + 排序字段的 SumOver 重载</summary>
        public static int SumOver<T>(this int amount,
            Expression<Func<T, object>>[] partitionBy,
            SumOverOrderBy<T>[] orderBy) => amount;

        /// <summary>仅分区字段的 SumOver 重载（decimal 版本）</summary>
        public static decimal SumOver<T>(this decimal amount,
            params Expression<Func<T, object>>[] partitionBy) => amount;

        /// <summary>分区字段 + 排序字段的 SumOver 重载（decimal 版本）</summary>
        public static decimal SumOver<T>(this decimal amount,
            Expression<Func<T, object>>[] partitionBy,
            SumOverOrderBy<T>[] orderBy) => amount;
    }
}
