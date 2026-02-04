using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace LiteOrm.Common
{
    /// <summary>
    /// 将 Lambda 表达式转换为查询 Expr。
    /// 处理Where、OrderBy等子句中的表达式。
    /// </summary>
    public class LambdaQueryExprConverter
    {
        private readonly ParameterExpression _rootParameter; // 跟踪 Lambda 的主参数（通常是实体变量）
        private readonly LambdaExpression _expression; // 原始 Lambda 对象

        /// <summary>
        /// 转换表达式节点为 Expr 对象。
        /// </summary>
        /// <param name="node">C# 表达式节点。</param>
        /// <returns>Expr 模型。</returns>
        public Expr Convert(Expression node)
        {
            return ConvertInternal(node);
        }

        /// <summary>
        /// 构造转换器。
        /// </summary>
        /// <param name="expression">目标 Lambda 表达式。</param>
        public LambdaQueryExprConverter(LambdaExpression expression)
        {
            if (expression is null) throw new ArgumentNullException(nameof(expression));
            _expression = expression;
            _rootParameter = expression.Parameters.FirstOrDefault();
        }

        /// <summary>
        /// 执行整体转换并将根节点转为 Expr。
        /// </summary>
        public SqlSegment ToExpr()
        {
            return ConvertInternal(_expression.Body) as SqlSegment;
        }

        /// <summary>
        /// 静态便捷入口，将 Lambda 表达式转换为 Expr 模型。
        /// </summary>
        public static SqlSegment ToExpr(LambdaExpression expression)
        {
            var converter = new LambdaQueryExprConverter(expression);
            return converter.ToExpr();
        }

        private Expr ConvertInternal(Expression node)
        {
            if (node is null) return null;

            switch (node.NodeType)
            {
                case ExpressionType.Call:
                    return ConvertMethodCall((MethodCallExpression)node);
                case ExpressionType.Constant:
                    return ConvertConstant((ConstantExpression)node);
                case ExpressionType.Parameter:
                    return ConvertParameter((ParameterExpression)node);
                case ExpressionType.Lambda:
                    // 检查 Lambda 的返回类型。如果是 bool，可能是谓词；否则可能是值选择器。
                    var lambda = (LambdaExpression)node;
                    if (lambda.ReturnType == typeof(bool))
                        return LambdaExprConverter.ToExpr(lambda);
                    else
                        return LambdaExprConverter.ToValueExpr(lambda);
                case ExpressionType.Quote:
                    // 处理 Expression.Quote，通常包裹在 LINQ 方法的 Lambda 参数上
                    return ConvertInternal(((UnaryExpression)node).Operand);
                case ExpressionType.MemberAccess:
                    // 如果是成员访问，可能是实体属性或外部变量
                    return EvaluateToExpr(node);
                default:
                    // 尝试计算非结构化的表达式值
                    return EvaluateToExpr(node);
            }
        }

        private Expr EvaluateToExpr(Expression node)
        {
            // 如果包含根参数，则可能是成员访问（属性）
            if (new LambdaExprConverter.ParameterExpressionDetector().ContainsParameter(node))
            {
                return new LambdaExprConverter(_expression).Convert(node);
            }

            try
            {
                var lambda = Expression.Lambda(node);
                var compiled = lambda.Compile();
                var value = compiled.DynamicInvoke();
                if (value is Expr expr) return expr;
                return new ValueExpr(value);
            }
            catch
            {
                // 如果无法计算，退而求其次交给 LambdaExprConverter 尝试转换
                return new LambdaExprConverter(_expression).Convert(node);
            }
        }

        private Expr ConvertParameter(ParameterExpression node)
        {
            if (node == _rootParameter)
            {
                var type = node.Type;
                if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IQueryable<>) || type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                {
                    type = type.GetGenericArguments()[0];
                }

                var tableView = TableInfoProvider.Default?.GetTableView(type);
                if (tableView == null)
                {
                    // 如果 TableInfoProvider 未初始化或类型未被识别为实体，则可能需要在此添加更多防御性代码
                    throw new InvalidOperationException($"无法通过 TableInfoProvider 获取类型 {type.FullName} 的表定义。请确保 LiteOrm 已正确初始化。");
                }
                return new TableExpr(tableView);
            }
            throw new NotSupportedException($"不支持的参数引用: {node.Name}");
        }

        private Expr ConvertConstant(ConstantExpression node)
        {
            if (node.Value is Expr expr) return expr;
            if (node.Value == null) return Expr.Null;
            
            // 如果常量是 IQueryable 或类似的，我们可能需要根据其类型解析 TableExpr
            var type = node.Value.GetType();
            if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IQueryable<>) || type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                var itemType = type.GetGenericArguments()[0];
                return new TableExpr(TableInfoProvider.Default.GetTableView(itemType));
            }

            return new ValueExpr(node.Value);
        }

        private Expr ConvertMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Queryable) || node.Method.DeclaringType == typeof(Enumerable) || node.Method.Name == "Having")
            {
                switch (node.Method.Name)
                {
                    case "Where":
                        return HandleWhere(node);
                    case "OrderBy":
                    case "OrderByDescending":
                        return HandleOrderBy(node, node.Method.Name == "OrderBy");
                    case "ThenBy":
                    case "ThenByDescending":
                        return HandleThenBy(node, node.Method.Name == "ThenBy");
                    case "Skip":
                        return HandleSkip(node);
                    case "Take":
                        return HandleTake(node);
                    case "GroupBy":
                        return HandleGroupBy(node);
                    case "Having":
                        return HandleHaving(node);
                    case "Select":
                        return HandleSelect(node);
                }
            }

            // 非 LINQ 方法调用交给 LambdaExprConverter 处理
            return new LambdaExprConverter(_expression).Convert(node);
        }

        private Expr HandleWhere(MethodCallExpression node)
        {
            var source = ConvertInternal(node.Arguments[0]) as ISourceAnchor;
            var predicate = ConvertInternal(node.Arguments[1]) as LogicExpr;
            return source.Where(predicate);
        }

        private Expr HandleOrderBy(MethodCallExpression node, bool ascending)
        {
            var source = ConvertInternal(node.Arguments[0]) as IOrderByAnchor;
            var keySelector = ConvertInternal(node.Arguments[1]) as ValueTypeExpr;
            return source.OrderBy((keySelector, ascending));
        }

        private Expr HandleThenBy(MethodCallExpression node, bool ascending)
        {
            var source = ConvertInternal(node.Arguments[0]);
            var keySelector = ConvertInternal(node.Arguments[1]) as ValueTypeExpr;

            if (source is OrderByExpr orderBy)
            {
                orderBy.OrderBys.Add((keySelector, ascending));
                return orderBy;
            }
            throw new InvalidOperationException("ThenBy must follow OrderBy.");
        }

        private Expr HandleSkip(MethodCallExpression node)
        {
            var source = ConvertInternal(node.Arguments[0]) as ISectionAnchor;
            var count = (int)((ConstantExpression)node.Arguments[1]).Value;
            
            if (source is SectionExpr section)
            {
                section.Skip = count;
                return section;
            }
            return source.Section(count, 0);
        }

        private Expr HandleTake(MethodCallExpression node)
        {
            var source = ConvertInternal(node.Arguments[0]) as ISectionAnchor;
            var count = (int)((ConstantExpression)node.Arguments[1]).Value;

            if (source is SectionExpr section)
            {
                section.Take = count;
                return section;
            }
            return source.Section(0, count);
        }

        private Expr HandleGroupBy(MethodCallExpression node)
        {
            var source = ConvertInternal(node.Arguments[0]) as IGroupByAnchor;
            var keySelector = ConvertInternal(node.Arguments[1]);
            
            List<ValueTypeExpr> groupBys = new List<ValueTypeExpr>();
            if (keySelector is ValueSet vs)
                groupBys.AddRange(vs.Cast<ValueTypeExpr>());
            else if (keySelector is ValueTypeExpr vte)
                groupBys.Add(vte);
            else
                throw new NotSupportedException("Unsupported GroupBy key selector.");

            return source.GroupBy(groupBys.ToArray());
        }

        private Expr HandleHaving(MethodCallExpression node)
        {
            var source = ConvertInternal(node.Arguments[0]) as IHavingAnchor;
            var predicate = ConvertInternal(node.Arguments[1]) as LogicExpr;
            return source.Having(predicate);
        }

        private Expr HandleSelect(MethodCallExpression node)
        {
            var source = ConvertInternal(node.Arguments[0]) as ISelectAnchor;
            var selector = ConvertInternal(node.Arguments[1]);

            List<ValueTypeExpr> selects = new List<ValueTypeExpr>();
            if (selector is ValueSet vs)
                selects.AddRange(vs.Cast<ValueTypeExpr>());
            else if (selector is ValueTypeExpr vte)
                selects.Add(vte);
            else
                throw new NotSupportedException("Unsupported Select selector.");

            return source.Select(selects.ToArray());
        }
    }
}
