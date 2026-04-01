using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 删除片段，表示 DELETE 语句
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public class DeleteExpr : SqlSegment
    {
        /// <summary>
        /// 初始化 DeleteExpr 类的新实例
        /// </summary>
        public DeleteExpr() { }

        /// <summary>
        /// 使用指定的源片段和筛选条件初始化 DeleteExpr 类的新实例
        /// </summary>
        /// <param name="table">源表</param>
        /// <param name="where">筛选条件表达式</param>
        public DeleteExpr(TableExpr table, LogicExpr where = null)
        {
            Table = table;
            Where = where;
        }

        /// <summary>
        /// 获取或设置删除操作的源表
        /// </summary>
        [JsonIgnore]
        public TableExpr Table { get; set; }
        /// <summary>
        /// 使用主表表达式重写源片段属性
        /// </summary>
        public override SqlSegment Source { get => Table; set => Table = (TableExpr)value; }

        /// <summary>
        /// 获取片段类型，返回 Delete 类型标识
        /// </summary>
        public override ExprType ExprType => ExprType.Delete;

        /// <summary>
        /// 获取或设置筛选条件表达式
        /// </summary>
        public LogicExpr Where { get; set; }

        /// <summary>
        /// 判断两个 DeleteExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is DeleteExpr other && Equals(Table, other.Table) && Equals(Where, other.Where);

        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(DeleteExpr).GetHashCode(), Table?.GetHashCode() ?? 0, Where?.GetHashCode() ?? 0);

        /// <summary>
        /// 返回删除片段的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString() => $"DELETE FROM {Table}{(Where != null ? $" WHERE {Where}" : "")}";

        /// <summary>
        /// 克隆 DeleteExpr
        /// </summary>
        public override Expr Clone()
        {
            var d = new DeleteExpr();
            d.Table = (TableExpr)Table?.Clone();
            d.Where = (LogicExpr)Where?.Clone();
            return d;
        }
    }
}