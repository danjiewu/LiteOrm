using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace LiteOrm.Common
{
    /// <summary>
    /// 将 LINQ 表达式转换为 SQL 片段（Select、Where、OrderBy 等）
    /// </summary>
    public class LambdaSqlSegmentConverter : LambdaExprConverter
    {
        /// <summary>
        /// 使用指定的 Lambda 表达式初始化 LambdaSqlSegmentConverter 类的新实例
        /// </summary>
        /// <param name="expression">要转换的 Lambda 表达式</param>
        public LambdaSqlSegmentConverter(LambdaExpression expression) : base(expression) { }

        /// <summary>
        /// 执行表达式转换并将结果转换为 SqlSegment
        /// </summary>
        public SqlSegment ToSqlSegment() {
            var sqlSegment = ConvertInternal(_expression.Body) as SqlSegment;
            return sqlSegment;
        }

        /// <summary>
        /// 静态方法：将 Lambda 表达式转换为 SqlSegment 模型
        /// </summary>
        public static SqlSegment ToSqlSegment(LambdaExpression expression) => new LambdaSqlSegmentConverter(expression).ToSqlSegment();

        /// <summary>
        /// 执行内部表达式转换，将表达式节点转换为 SqlSegment 对象
        /// </summary>
        /// <param name="node">要转换的表达式节点</param>
        /// <returns>转换后的 Expr 对象</returns>
        protected override Expr ConvertInternal(Expression node)
        {
            if (node is null) return null;

            return node.NodeType switch
            {
                ExpressionType.Call => ConvertMethodCall((MethodCallExpression)node),
                ExpressionType.Parameter => ConvertParameter((ParameterExpression)node),
                ExpressionType.Lambda => HandleSubLambda((LambdaExpression)node),                
                _ => base.ConvertInternal(node)
            };
        }

        /// <summary>
        /// 将方法调用表达式转换为对应的 SqlSegment 对象
        /// </summary>
        /// <param name="node">要转换的方法调用表达式节点</param>
        /// <returns>转换后的 Expr 对象</returns>
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

        // LINQ 扩展方法处理器
        private Expr HandleWhere(MethodCallExpression node)
        {
            var source = ConvertInternal(node.Arguments[0]) as ISourceAnchor;
            var newCondition = AsLogic(ConvertInternal(node.Arguments[1]));
            
            // 如果源已经是 WhereExpr，将新条件与现有条件用 AND 合并
            if (source is WhereExpr existingWhere)
            {
                var combinedCondition = new LogicSet(LogicJoinType.And, existingWhere.Where, newCondition);
                existingWhere.Where = combinedCondition;
                return existingWhere;
            }
            
            // 否则创建新的 WhereExpr
            return source.Where(newCondition);
        }
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