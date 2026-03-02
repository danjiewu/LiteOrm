using System;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表达式字符串构建上下文接口
    /// </summary>
    public interface IExprStringBuildContext
    {
        /// <summary>
        /// 获取SQL构建器
        /// </summary>
        ISqlBuilder SqlBuilder { get; }

        /// <summary>
        /// 创建SQL构建上下文
        /// </summary>
        /// <param name="initTable">是否初始化表信息</param>
        /// <returns>SQL构建上下文</returns>
        SqlBuildContext CreateSqlBuildContext(bool initTable = false);
    }
}
