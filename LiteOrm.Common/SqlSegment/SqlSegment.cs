using System;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 锚点接口系列，用于精确控制流式 API 的构建过程
    /// 每个接口代表查询构建过程中的一个阶段
    /// </summary>
    public interface ISqlSegment {
        /// <summary>
        /// 获取或设置此片段的源片段
        /// </summary>
        ISqlSegment Source { get; internal set;}
    }

    /// <summary>
    /// 分页锚点接口，支持 Skip/Take 操作
    /// </summary>
    public interface ISectionAnchor : ISelectAnchor { }

    /// <summary>
    /// 排序锚点接口，支持 OrderBy/ThenBy 操作
    /// </summary>
    public interface IOrderByAnchor : ISectionAnchor { }

    /// <summary>
    /// 选择锚点接口，支持 Select 操作
    /// </summary>
    public interface ISelectAnchor : ISqlSegment { }

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
