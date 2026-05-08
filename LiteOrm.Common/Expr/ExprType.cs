using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm.Common
{
    /// <summary>
    /// Expr 类型枚举，标识不同种类的 Expr
    /// </summary>
    public enum ExprType
    {
        /// <summary>表片段，表示单表或子查询引用</summary>
        Table,
        /// <summary>
        /// 公共表表达式（CTE）片段，表示 WITH 子句中的 CTE 定义
        /// </summary>
        CommonTable,
        /// <summary>表连接片段，表示 JOIN 子句</summary>
        TableJoin,
        /// <summary>From 片段，表示数据源（表或视图）</summary>
        From,
        /// <summary>选择片段，表示 SELECT 查询</summary>
        Select,
        /// <summary>Select项，用于 SELECT 列定义</summary>
        SelectItem,
        /// <summary>SELECT 中的字段排序项</summary>
        OrderByItem,
        /// <summary>函数调用表达式</summary>
        Function,
        /// <summary>外键 EXISTS 表达式</summary>
        Foreign,
        /// <summary>Lambda 包装表达式（仅用于解析）</summary>
        Lambda,
        /// <summary>逻辑二元表达式（比较运算）</summary>
        LogicBinary,
        /// <summary>逻辑 AND 表达式组合</summary>
        And,
        /// <summary>逻辑 OR 表达式组合</summary>
        Or,
        /// <summary>逻辑 NOT 表达式</summary>
        Not,
        /// <summary>值二元表达式（算术或串联）</summary>
        ValueBinary,
        /// <summary>值集合表达式（用于 IN 或 CONCAT）</summary>
        ValueSet,
        /// <summary>一元表达式（如 DISTINCT, -a 等）</summary>
        Unary,
        /// <summary>属性（列）引用表达式</summary>
        Property,
        /// <summary>常量值表达式</summary>
        Value,
        /// <summary>通过委托或注册生成的 SQL 片段</summary>
        GenericSql,
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
}
