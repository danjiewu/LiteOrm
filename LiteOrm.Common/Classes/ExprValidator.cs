using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表达式验证器基类，访问表达式树进行验证
    /// </summary>
    public abstract class ExprValidator : IExprNodeVisitor
    {
        /// <summary>
        /// 验证指定表达式节点
        /// </summary>
        /// <param name="node">要验证的表达式节点</param>
        /// <returns>验证通过返回 true，否则返回 false</returns>
        public abstract bool Validate(Expr node);

        /// <summary>
        /// 获取验证失败的表达式节点
        /// </summary>
        public Expr FailedExpr { get; private set; }

        /// <summary>
        /// 访问并验证表达式节点，验证失败时记录节点引用
        /// </summary>
        /// <param name="node">要访问的表达式节点</param>
        /// <returns>验证通过返回 true，失败返回 false</returns>
        bool IExprNodeVisitor.Visit(Expr node)
        {
            if (Validate(node)) return true;
            FailedExpr = node;
            return false;
        }

        /// <summary>
        /// 创建一个最小验证器实例，允许基本值类型、一元表达式、集合类型、逻辑类型及基础 SQL 片段
        /// </summary>
        /// <returns></returns>
        public static ExprTypeValidator CreateMinimum() => new ExprTypeValidator(ExprTypeValidator.Minimum);
        /// <summary>
        /// 创建一个查询验证器实例，允许完整的 SELECT 查询相关表达式类型
        /// </summary>
        /// <returns></returns>
        public static ExprTypeValidator CreateQueryOnly() => new ExprTypeValidator(ExprTypeValidator.QueryOnly);
    }

    /// <summary>
    /// 表达式验证器组，支持多个验证器组合使用
    /// </summary>
    public class ExprValidatorGroup : ExprValidator
    {
        private readonly List<IExprNodeVisitor> _visitors = new List<IExprNodeVisitor>();

        /// <summary>
        /// 获取验证失败的访问器
        /// </summary>
        public IExprNodeVisitor FaildedVisitor { get; private set; }

        /// <summary>
        /// 初始化验证器组
        /// </summary>
        /// <param name="visitors">要组合的验证器数组</param>
        public ExprValidatorGroup(params IExprNodeVisitor[] visitors)
        {
            _visitors.AddRange(visitors);
        }

        /// <summary>
        /// 验证表达式节点，所有子验证器都通过才视为成功
        /// </summary>
        /// <param name="node">要验证的表达式节点</param>
        /// <returns>所有验证器通过返回 true，任一验证器失败返回 false</returns>
        public override bool Validate(Expr node)
        {
            if (node == null) return true;

            foreach (var visitor in _visitors)
            {
                if (!visitor.Visit(node))
                {
                    FaildedVisitor = visitor;
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// 基于表达式类型的验证器，只允许指定的表达式类型通过
    /// </summary>
    public class ExprTypeValidator : ExprValidator
    {
        /// <summary>
        /// 最小验证器集合，允许基本值类型、一元表达式、集合类型、逻辑类型及基础 SQL 片段
        /// </summary>
        public static readonly IReadOnlyCollection<ExprType> Minimum = new HashSet<ExprType> {
            ExprType.Value,
            ExprType.Property,
            ExprType.Unary,
            ExprType.ValueSet,
            ExprType.LogicBinary,
            ExprType.And,
            ExprType.Or,
            ExprType.Not,
            ExprType.Where,
            ExprType.OrderBy,
            ExprType.OrderByItem,
            ExprType.Section};

        /// <summary>
        /// 查询验证器集合，允许完整的 SELECT 查询相关表达式类型
        /// </summary>
        public static readonly IReadOnlyCollection<ExprType> QueryOnly = new HashSet<ExprType>
        {
            ExprType.Value,
            ExprType.Property,
            ExprType.Unary,
            ExprType.ValueSet,
            ExprType.LogicBinary,
            ExprType.And,
            ExprType.Or,
            ExprType.Not,
            ExprType.From,
            ExprType.Where,
            ExprType.GroupBy,
            ExprType.OrderBy,
            ExprType.OrderByItem,
            ExprType.Section,
            ExprType.Select,
            ExprType.SelectItem,
            ExprType.GenericSql,
            ExprType.Function,
            ExprType.Table,
            ExprType.TableJoin };

        private readonly HashSet<ExprType> _allowedTypes = new HashSet<ExprType>();

        /// <summary>
        /// 初始化类型验证器
        /// </summary>
        /// <param name="allowedTypes">允许的表达式类型数组</param>
        public ExprTypeValidator(params ExprType[] allowedTypes)
        {
            _allowedTypes.UnionWith(allowedTypes);
        }

        /// <summary>
        /// 初始化类型验证器
        /// </summary>
        /// <param name="allowedTypes">允许的表达式类型数组</param>
        public ExprTypeValidator(IEnumerable<ExprType> allowedTypes)
        {
            _allowedTypes.UnionWith(allowedTypes);
        }

        /// <summary>
        /// 获取允许的表达式类型集合
        /// </summary>
        public IReadOnlyCollection<ExprType> AllowedTypes => _allowedTypes;

        /// <summary>
        /// 验证表达式类型是否在允许集合中
        /// </summary>
        /// <param name="node">要验证的表达式节点</param>
        /// <returns>类型在允许集合中返回 true，否则返回 false</returns>
        public override bool Validate(Expr node)
        {
            if (node == null) return true;
            if (!_allowedTypes.Contains(node.ExprType))
                return false;
            return true;
        }       
    }
}