using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// SQL 片段基类，表示查询语句的一个组成部分（如表、选择、筛选、排序等）
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public abstract class SqlSegment : Expr
    {
        /// <summary>
        /// 获取或设置此片段的源片段
        /// </summary>
        public SqlSegment Source;

        /// <summary>
        /// 获取片段类型，用于标识当前片段的种类
        /// </summary>
        public abstract SqlSegmentType SegmentType { get; }
    }

    /// <summary>
    /// SQL 片段类型枚举，标识不同种类的 SQL 片段
    /// </summary>
    public enum SqlSegmentType
    {
        /// <summary>表片段，表示数据源表</summary>
        Table,
        /// <summary>选择片段，表示 SELECT 查询</summary>
        Select,
        /// <summary>更新片段，表示 UPDATE 语句</summary>
        Update,
        /// <summary>删除片段，表示 DELETE 语句</summary>
        Delete,
        /// <summary>筛选片段，表示 WHERE 条件</summary>
        Where,
        /// <summary>分组片段，表示 GROUP BY 子句</summary>
        GroupBy,
        /// <summary>排序片段，表示 ORDER BY 子句</summary>
        OrderBy,
        /// <summary>Having 片段，表示 HAVING 条件</summary>
        Having,
        /// <summary>分页片段，表示 LIMIT/OFFSET 子句</summary>
        Section
    }

    /// <summary>
    /// 锚点接口系列，用于精确控制流式 API 的构建过程
    /// 每个接口代表查询构建过程中的一个阶段
    /// </summary>
    public interface ISqlAnchor { }

    /// <summary>
    /// 分页锚点接口，支持 Skip/Take 操作
    /// </summary>
    public interface ISectionAnchor : ISqlAnchor { }

    /// <summary>
    /// 排序锚点接口，支持 OrderBy/ThenBy 操作
    /// </summary>
    public interface IOrderByAnchor : ISectionAnchor { }

    /// <summary>
    /// 选择锚点接口，支持 Select 操作
    /// </summary>
    public interface ISelectAnchor : IOrderByAnchor { }

    /// <summary>
    /// Having 锚点接口，支持 Having 操作
    /// </summary>
    public interface IHavingAnchor : ISelectAnchor { }

    /// <summary>
    /// 分组锚点接口，支持 GroupBy 操作
    /// </summary>
    public interface IGroupByAnchor : IHavingAnchor { }

    /// <summary>
    /// 源锚点接口，支持 Where 和表操作
    /// </summary>
    public interface ISourceAnchor : IGroupByAnchor { }
}
