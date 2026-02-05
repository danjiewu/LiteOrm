using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// 处理 LINQ 风格 SQL 片段（Select, Where, OrderBy 等）的转换器。
    /// </summary>
    public class LambdaSqlSegmentConverter : LambdaExprConverter
    {
        public LambdaSqlSegmentConverter(LambdaExpression expression) : base(expression) { }

        /// <summary>
        /// 执行整体转换并将根节点转为 SqlSegment。
        /// </summary>
        public SqlSegment ToSqlSegment() => ConvertInternal(_expression.Body) as SqlSegment;

        /// <summary>
        /// 静态便捷入口，将 Lambda 表达式转换为 SqlSegment 模型。
        /// </summary>
        public static SqlSegment ToSqlSegment(LambdaExpression expression) => new LambdaSqlSegmentConverter(expression).ToSqlSegment();

        protected override Expr ConvertInternal(Expression node)
        {
            if (node is null) return null;

            return node.NodeType switch
            {
                ExpressionType.Call => ConvertMethodCall((MethodCallExpression)node),
                ExpressionType.Parameter => ConvertParameter((ParameterExpression)node),
                ExpressionType.Lambda => HandleSubLambda((LambdaExpression)node),
                ExpressionType.Quote => ConvertInternal(((UnaryExpression)node).Operand),
                _ => base.ConvertInternal(node)
            };
        }

        protected override Expr ConvertMethodCall(MethodCallExpression node)
        {
            var type = node.Method.DeclaringType;
            if (type == typeof(Queryable) || type == typeof(Enumerable) || node.Method.Name == "Having")
            {
                return node.Method.Name switch
                {
                    "Where" => HandleWhere(node),
                    "OrderBy" or "OrderByDescending" => HandleOrderBy(node, node.Method.Name == "OrderBy"),
                    "ThenBy" or "ThenByDescending" => HandleThenBy(node, node.Method.Name == "ThenBy"),
                    "Skip" => HandleSkip(node),
                    "Take" => HandleTake(node),
                    "GroupBy" => HandleGroupBy(node),
                    "Having" => HandleHaving(node),
                    "Select" => HandleSelect(node),
                    _ => base.ConvertMethodCall(node)
                };
            }
            return base.ConvertMethodCall(node);
        }

        private Expr ConvertParameter(ParameterExpression node)
        {
            if (node != _rootParameter) throw new NotSupportedException($"Unsupported parameter: {node.Name}");
            var type = node.Type;
            if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IQueryable<>) || type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                type = type.GetGenericArguments()[0];

            return new TableExpr(TableInfoProvider.Default?.GetTableView(type) ?? throw new InvalidOperationException($"Table info not found for {type}"));
        }

        private Expr HandleSubLambda(LambdaExpression lambda) => lambda.ReturnType == typeof(bool) ? ToExpr(lambda) : ToValueExpr(lambda);

        // LINQ 算子处理逻辑
        private Expr HandleWhere(MethodCallExpression node) => (ConvertInternal(node.Arguments[0]) as ISourceAnchor).Where(AsLogic(ConvertInternal(node.Arguments[1])));
        private Expr HandleOrderBy(MethodCallExpression node, bool asc) => (ConvertInternal(node.Arguments[0]) as IOrderByAnchor).OrderBy((AsValue(ConvertInternal(node.Arguments[1])), asc));
        private Expr HandleThenBy(MethodCallExpression node, bool asc)
        {
            if (ConvertInternal(node.Arguments[0]) is OrderByExpr ob) { ob.OrderBys.Add((AsValue(ConvertInternal(node.Arguments[1])), asc)); return ob; }
            throw new InvalidOperationException("ThenBy must follow OrderBy.");
        }
        private Expr HandleSkip(MethodCallExpression node)
        {
            var s = ConvertInternal(node.Arguments[0]) as ISectionAnchor;
            var v = (int)((ConstantExpression)node.Arguments[1]).Value;
            if (s is SectionExpr se) { se.Skip = v; return se; }
            return s.Section(v, 0);
        }
        private Expr HandleTake(MethodCallExpression node)
        {
            var s = ConvertInternal(node.Arguments[0]) as ISectionAnchor;
            var v = (int)((ConstantExpression)node.Arguments[1]).Value;
            if (s is SectionExpr se) { se.Take = v; return se; }
            return s.Section(0, v);
        }
        private Expr HandleGroupBy(MethodCallExpression node)
        {
            var s = ConvertInternal(node.Arguments[0]) as IGroupByAnchor;
            var k = ConvertInternal(node.Arguments[1]);
            return s.GroupBy(k is ValueSet vs ? vs.Cast<ValueTypeExpr>().ToArray() : new[] { AsValue(k) });
        }
        private Expr HandleHaving(MethodCallExpression node) => (ConvertInternal(node.Arguments[0]) as IHavingAnchor).Having(AsLogic(ConvertInternal(node.Arguments[1])));
        private Expr HandleSelect(MethodCallExpression node)
        {
            var s = ConvertInternal(node.Arguments[0]) as ISelectAnchor;
            var k = ConvertInternal(node.Arguments[1]);
            return s.Select(k is ValueSet vs ? vs.Cast<ValueTypeExpr>().ToArray() : new[] { AsValue(k) });
        }
    }
}