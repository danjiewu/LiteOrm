using System;
using System.Collections.Generic;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库列定义信息。
    /// 包含列的结构信息，如是否为主键、是否自增、数据类型等。
    /// </summary>
    public class ColumnDefinition : SqlColumn
    {
        /// <summary>
        /// 初始化 <see cref="ColumnDefinition"/> 类的新实例。
        /// </summary>
        /// <param name="property">实体对应的属性信息。</param>
        public ColumnDefinition(PropertyInfo property)
            : base(property)
        {

        }

        /// <summary>
        /// 获取或设置一个值，指示该列是否为主键。
        /// </summary>
        public bool IsPrimaryKey { get; set; }

        /// <summary>
        /// 获取或设置一个值，指示该列是否为自增标识列。
        /// </summary>
        public bool IsIdentity { get; set; }

        /// <summary>
        /// 获取或设置标识列（自增）的起始值。
        /// </summary>
        public long IdentityStart { get; set; } = 1;

        /// <summary>
        /// 获取或设置标识列（自增）的增量值。
        /// </summary>
        public int IdentityIncreasement { get; set; } = 1;

        /// <summary>
        /// 获取或设置一个值，指示该列是否为时间戳列。
        /// </summary>
        public bool IsTimestamp { get; set; }

        /// <summary>
        /// 获取或设置标识列的表达式（如序列名称）。
        /// </summary>
        public string? IdentityExpression { get; set; }

        /// <summary>
        /// 获取或设置一个值，指示该列是否应创建索引。
        /// </summary>
        public bool IsIndex { get; set; }

        /// <summary>
        /// 获取或设置一个值，指示该列是否具有唯一约束。
        /// </summary>
        public bool IsUnique { get; set; }

        /// <summary>
        /// 获取或设置数据库列的长度。
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// 获取或设置数据库列的数据类型。
        /// 为 <see cref="DbValueType.Default"/> 时表示使用默认值，由 <see cref="ISqlBuilder"/> 根据属性类型推断。
        /// </summary>
        public DbValueType DbType { get; set; } = DbValueType.Default;

        /// <summary>
        /// 计算列表达式（字符串形式，非实际列）。设置后该列不生成物理列、不参与插入/更新；
        /// 查询 SELECT 与条件中以表达式返回/生成。表达式内可用 <c>{属性名}</c> 引用同一实体的其他属性。
        /// <para>
        /// 也可通过 <see cref="ExpressionExpr"/> 设置 <see cref="ValueTypeExpr"/> 形式的表达式，
        /// 两者同时设置时优先使用 <see cref="ExpressionExpr"/>。
        /// </para>
        /// </summary>
        public string? Expression { get; set; }

        /// <summary>
        /// 计算列表达式（<see cref="ValueTypeExpr"/> 形式，非实际列）。设置后该列不生成物理列、不参与插入/更新；
        /// 查询 SELECT 与条件中以表达式返回/生成。
        /// <para>
        /// 与 <see cref="Expression"/>（字符串形式）互为替代，同时设置时优先使用本属性。
        /// 使用 <c>Expr.Prop("Price") * Expr.Prop("Quantity")</c> 等 Expr 树构建， 最终替换所在列渲染为 SQL。
        /// </para>
        /// </summary>
        public ValueTypeExpr? ExpressionExpr { get; set; }


        /// <summary>
        /// 是否为计算列（非实际列）：显式声明 <see cref="ColumnMode.Computed"/> 或设置了 <see cref="Expression"/> / <see cref="ExpressionExpr"/>。
        /// </summary>
        public bool IsComputed => Mode.IsComputed() || !string.IsNullOrEmpty(Expression) || ExpressionExpr is not null;

        /// <summary>
        /// 是否设置了计算列表达式（<see cref="Expression"/> 或 <see cref="ExpressionExpr"/>）。
        /// </summary>
        public bool HasExpression => !string.IsNullOrEmpty(Expression) || ExpressionExpr is not null;

        /// <summary>
        /// 获取或设置一个值，指示该列是否允许为空。
        /// </summary>
        public bool AllowNull { get; set; }

        /// <summary>
        /// 获取或设置列的默认值，可以是一个常量值或一个数据库函数表达式。
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// 获取或设置列的固定筛选值。支持枚举和其他可转换到属性类型的常量值。
        /// </summary>
        public object? Constant { get; set; }
        /// <summary>
        /// 获取或设置列映射模式。
        /// </summary>
        public ColumnMode Mode { get; set; }

        /// <summary>
        /// 获取当前列的定义信息。
        /// </summary>
        public override ColumnDefinition Definition => this;
    }
}
