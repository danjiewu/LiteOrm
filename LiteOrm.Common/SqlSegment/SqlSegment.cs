using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// Sql 片段基类，表示 SQL 查询中的一个部分，支持链式调用和表达式树构建
    /// </summary>
    [JsonConverter(typeof(ExprJsonConverterFactory))]
    public abstract class SqlSegment : Expr
    {
        /// <summary>
        /// 获取当前片段的源片段，表示当前片段依赖的上一个片段
        /// </summary>
        public virtual SqlSegment Source { get; set; }
    }
    /// <summary>
    /// 选择锚点接口，支持 Select 操作
    /// </summary>
    public interface ISelectAnchor { }

    /// <summary>
    /// 分页锚点接口，支持 Skip/Take 操作
    /// </summary>
    public interface ISectionAnchor : ISelectAnchor { }

    /// <summary>
    /// 排序锚点接口，支持 OrderBy/ThenBy 操作
    /// </summary>
    public interface IOrderByAnchor : ISectionAnchor { }

    /// <summary>
    /// Having 锚点接口，支持 Having 操作
    /// </summary>
    public interface IHavingAnchor : IOrderByAnchor { }

    /// <summary>
    /// 分组锚点接口，支持 GroupBy 操作
    /// </summary>
    public interface IGroupByAnchor : IHavingAnchor { }

    /// <summary>
    /// 源锚点接口，支持 Where 和表操作
    /// </summary>
    public interface ISourceAnchor : IGroupByAnchor { }
}
