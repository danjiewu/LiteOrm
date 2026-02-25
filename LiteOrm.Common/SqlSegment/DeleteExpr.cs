using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 删除片段，表示 DELETE 语句
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public class DeleteExpr : Expr, ISqlSegment
    {
        /// <summary>
        /// 初始化 DeleteExpr 类的新实例
        /// </summary>
        public DeleteExpr() { }

        /// <summary>
        /// 使用指定的源片段和筛选条件初始化 DeleteExpr 类的新实例
        /// </summary>
        /// <param name="source">源片段</param>
        /// <param name="where">筛选条件表达式</param>
        public DeleteExpr(FromExpr source, LogicExpr where = null)
        {
            Source = source;
            Where = where;
        }

        /// <summary>
        /// 获取或设置删除操作的源片段（From表达式）
        /// </summary>
        public ISqlSegment Source { get; set; }

        /// <summary>
        /// 获取片段类型，返回 Delete 类型标识
        /// </summary>
        public SqlSegmentType SegmentType => SqlSegmentType.Delete;

        /// <summary>
        /// 获取或设置筛选条件表达式
        /// </summary>
        public LogicExpr Where { get; set; }

        /// <summary>
        /// 判断两个 DeleteExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is DeleteExpr other && Equals(Source, other.Source) && Equals(Where, other.Where);

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(DeleteExpr).GetHashCode(), Source?.GetHashCode() ?? 0, Where?.GetHashCode() ?? 0);

        /// <summary>
        /// 返回删除片段的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString() => $"DELETE FROM {Source}{(Where != null ? $" WHERE {Where}" : "")}";
    }
}
