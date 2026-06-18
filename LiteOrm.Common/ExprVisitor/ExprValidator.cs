using System;
using System.Collections.Generic;

namespace LiteOrm.Common
{
    /// <summary>
    /// 表达式验证器基类，通过 <see cref="ExprVisitor.Validate(ExprValidator, Expr, ExprVisitOrder, System.Threading.CancellationToken)"/> 驱动遍历验证。
    /// 验证失败时自动记录失败节点到 <see cref="FailedExpr"/>。
    /// </summary>
    public abstract class ExprValidator
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
        public Expr FailedExpr { get; internal set; }

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
    /// 表达式验证器组，支持多个验证器组合使用，任一验证器失败即短路停止
    /// </summary>
    public class ExprValidatorGroup : ExprValidator
    {
        private readonly List<ExprValidator> _validators = new List<ExprValidator>();

        /// <summary>
        /// 获取验证失败的验证器
        /// </summary>
        public ExprValidator FailedValidator { get; private set; }

        /// <summary>
        /// 初始化验证器组
        /// </summary>
        /// <param name="validators">要组合的验证器数组</param>
        public ExprValidatorGroup(params ExprValidator[] validators)
        {
            if (validators == null) throw new ArgumentNullException(nameof(validators));
            _validators.AddRange(validators);
        }

        /// <summary>
        /// 验证表达式节点，所有子验证器都通过才视为成功
        /// </summary>
        /// <param name="node">要验证的表达式节点</param>
        /// <returns>所有验证器通过返回 true，任一验证器失败返回 false</returns>
        public override bool Validate(Expr node)
        {
            if (node == null) return true;

            foreach (var validator in _validators)
            {
                if (!validator.Validate(node))
                {
                    FailedValidator = validator;
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
            ExprType.TableJoin,
            ExprType.CommonTable };

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
