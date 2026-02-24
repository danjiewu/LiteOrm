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
        public Expr ToSqlSegment() {
            return ConvertInternal(_expression.Body);
        }

        /// <summary>
        /// 静态方法：将 Lambda 表达式转换为 SqlSegment 模型
        /// </summary>
        /// <param name="expression">要转换的 Lambda 表达式</param>
        /// <returns>转换后的表达式对象</returns>
        public static Expr ToSqlSegment(LambdaExpression expression) => new LambdaSqlSegmentConverter(expression).ToSqlSegment();

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
            if (type == typeof(Queryable) || type == typeof(Enumerable))
            {
                return node.Method.Name switch
                {
                    "Where" => HandleWhere(node),
                    "OrderBy" or "OrderByDescending" => HandleOrderBy(node, node.Method.Name == "OrderBy"),
                    "ThenBy" or "ThenByDescending" => HandleThenBy(node, node.Method.Name == "ThenBy"),
                    "Skip" => HandleSkip(node),
                    "Take" => HandleTake(node),
                    "GroupBy" => HandleGroupBy(node),
                    "Select" => HandleSelect(node),
                    _ => base.ConvertMethodCall(node)
                };
            }
            return base.ConvertMethodCall(node);
        }

        /// <summary>
        /// 将参数表达式转换为 From 表达式。
        /// 当参数对应一个 IQueryable 或 IEnumerable 类型时，会取其泛型参数类型作为表实体类型。
        /// </summary>
        /// <param name="node">参数表达式</param>
        /// <returns>对应的 FromExpr</returns>
        private Expr ConvertParameter(ParameterExpression node)
        {
            if (node != _rootParameter) throw new NotSupportedException($"Unsupported parameter: {node.Name}");
            var type = node.Type;
            if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IQueryable<>) || type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                type = type.GetGenericArguments()[0];

            return new FromExpr(type);
        }

        /// <summary>
        /// 处理子 Lambda 表达式，返回 LogicExpr 或 ValueTypeExpr 取决于 Lambda 的返回类型。
        /// </summary>
        /// <param name="lambda">要处理的 Lambda 表达式</param>
        /// <returns>对应的 Expr（LogicExpr 或 ValueTypeExpr）</returns>
        private Expr HandleSubLambda(LambdaExpression lambda) => lambda.ReturnType == typeof(bool) ? ToExpr(lambda) : ToValueExpr(lambda);

        // LINQ 扩展方法处理器
        /// <summary>
        /// 处理 Where 调用，将条件转换并合并到已有的 WhereExpr 或创建新的 WhereExpr。
        /// </summary>
        /// <param name="node">方法调用表达式节点</param>
        /// <returns>处理后的 Expr</returns>
        private Expr HandleWhere(MethodCallExpression node)
        {
            var src = ConvertInternal(node.Arguments[0]);
            var lambda = (LambdaExpression)((UnaryExpression)node.Arguments[1]).Operand;

            // 如果源是 GroupByExpr 或 HavingExpr，则这是一个 Having 筛选，否则是普通的 Where 筛选
            if (src is GroupByExpr or HavingExpr)
            {
                var havingAnchor = (IHavingAnchor)src;
                var groupBySource = src as GroupByExpr ?? (src as HavingExpr)?.Source as GroupByExpr;
                var havingLogic = ConvertHavingLambda(lambda, groupBySource?.GroupBys.ToArray());

                if (src is HavingExpr existingHaving)
                {
                    existingHaving.Having = new LogicSet(LogicJoinType.And, existingHaving.Having, havingLogic);
                    return existingHaving;
                }

                return havingAnchor.Having(havingLogic);
            }

            ISqlSegment source = src as ISourceAnchor;
            if (source == null && src is FromExpr tve)
            {
                source = tve;
            }
            var newCondition = AsLogic(ConvertInternal(node.Arguments[1]));
            
            // 如果源已经是 WhereExpr，将新条件与现有条件用 AND 合并
            if (source is WhereExpr existingWhere)
            {
                var combinedCondition = new LogicSet(LogicJoinType.And, existingWhere.Where, newCondition);
                existingWhere.Where = combinedCondition;
                return existingWhere;
            }
            
            // 否则创建新的 WhereExpr，需要转换为 ISourceAnchor
            return ((ISourceAnchor)source).Where(newCondition);
        }

        /// <summary>
        /// 处理 OrderBy/OrderByDescending 并返回 OrderByExpr 或更新现有表达式。
        /// </summary>
        private Expr HandleOrderBy(MethodCallExpression node, bool asc) => (ConvertInternal(node.Arguments[0]) as IOrderByAnchor).OrderBy((AsValue(ConvertInternal(node.Arguments[1])), asc));

        /// <summary>
        /// 处理 ThenBy/ThenByDescending 并更新现有的 OrderByExpr。
        /// </summary>
        /// <param name="node">方法调用表达式节点</param>
        /// <param name="asc">是否升序</param>
        /// <returns>更新后的 OrderByExpr</returns>
        private Expr HandleThenBy(MethodCallExpression node, bool asc)
        {
            if (ConvertInternal(node.Arguments[0]) is OrderByExpr ob) { ob.OrderBys.Add((AsValue(ConvertInternal(node.Arguments[1])), asc)); return ob; }
            throw new InvalidOperationException("ThenBy must follow OrderBy.");
        }

        /// <summary>
        /// 处理 Skip 调用，转换为 SectionExpr 的 Skip 部分或 Section 方法。
        /// </summary>
        /// <param name="node">方法调用表达式节点</param>
        /// <returns>SectionExpr 或 ISectionAnchor</returns>
        private Expr HandleSkip(MethodCallExpression node)
        {
            var s = ConvertInternal(node.Arguments[0]) as ISectionAnchor;
            var v = System.Convert.ToInt32(Evaluate(node.Arguments[1]));
            if (s is SectionExpr se) { se.Skip = v; return se; }
            return s.Section(v, 0);
        }

        /// <summary>
        /// 处理 Take 调用，转换为 SectionExpr 的 Take 部分或 Section 方法。
        /// </summary>
        /// <param name="node">方法调用表达式节点</param>
        /// <returns>SectionExpr 或 ISectionAnchor</returns>
        private Expr HandleTake(MethodCallExpression node)
        {
            var s = ConvertInternal(node.Arguments[0]) as ISectionAnchor;
            var v = System.Convert.ToInt32(Evaluate(node.Arguments[1]));
            if (s is SectionExpr se) { se.Take = v; return se; }
            return s.Section(0, v);
        }

        /// <summary>
        /// 处理 GroupBy 调用，将 key 表达式转换为 ValueSet 或单独的 ValueTypeExpr。
        /// </summary>
        /// <param name="node">方法调用表达式节点</param>
        /// <returns>GroupByExpr</returns>
        private Expr HandleGroupBy(MethodCallExpression node)
        {
            var s = ConvertInternal(node.Arguments[0]) as IGroupByAnchor;
            var k = ConvertInternal(node.Arguments[1]);
            return s.GroupBy(k is ValueSet vs ? vs.Cast<ValueTypeExpr>().ToArray() : new[] { AsValue(k) });
        }

        /// <summary>
        /// 将针对 GroupBy 的 Lambda 表达式转换为 Having 子句逻辑表达式。
        /// </summary>
        /// <param name="lambda">要转换的 Lambda 表达式</param>
        /// <param name="groupKeys">分组键集合</param>
        /// <returns>转换后的 LogicExpr</returns>
        private LogicExpr ConvertHavingLambda(LambdaExpression lambda, ValueTypeExpr[] groupKeys)
        {
            var expr = ConvertGroupedExpr(lambda.Body, lambda.Parameters[0], groupKeys);
            return AsLogic(expr);
        }

        /// <summary>
        /// 处理 Select 调用，支持 GroupBy 后的 Select 情形。
        /// </summary>
        /// <param name="node">方法调用表达式节点</param>
        /// <returns>SelectExpr</returns>
        private Expr HandleSelect(MethodCallExpression node)
        {
            var source = ConvertInternal(node.Arguments[0]) as ISelectAnchor;
            var lambda = (LambdaExpression)((UnaryExpression)node.Arguments[1]).Operand;
            
            // 检查是否是 GroupBy 后的 Select
            var groupBySource = source as GroupByExpr;
            
            // 转换 Select 的选择表达式
            var selectItems = ConvertSelectLambda(lambda, groupBySource?.GroupBys.ToArray());
            
            return source.Select(selectItems);
        }

        /// <summary>
        /// 转换 Select Lambda 表达式（支持 GroupBy 后的 Select）
        /// </summary>
        /// <param name="lambda">要转换的 Lambda 表达式</param>
        /// <param name="groupKeys">用于分组时的键集合</param>
        /// <returns>SelectItemExpr 数组</returns>
        private SelectItemExpr[] ConvertSelectLambda(LambdaExpression lambda, ValueTypeExpr[] groupKeys)
        {
            var body = lambda.Body;
            var lambdaParam = lambda.Parameters[0];

            // 处理 NewExpression (匿名对象)
            if (body is NewExpression newExpr)
            {
                var items = new List<SelectItemExpr>();
                for (int i = 0; i < newExpr.Arguments.Count; i++)
                {
                    var arg = newExpr.Arguments[i];
                    var item = ConvertGroupedExpr(arg, lambdaParam, groupKeys);
                    if (item is not null)
                    {
                        var selectItem = new SelectItemExpr(AsValue(item));
                        if (newExpr.Members != null && i < newExpr.Members.Count)
                        {
                            selectItem.Name = newExpr.Members[i].Name;
                        }
                        items.Add(selectItem);
                    }
                }
                return items.ToArray();
            }

            // 处理单个选择
            var singleItem = ConvertGroupedExpr(body, lambdaParam, groupKeys);
            return singleItem is not null ? new[] { new SelectItemExpr(AsValue(singleItem)) } : Array.Empty<SelectItemExpr>();
        }

        /// <summary>
        /// 转换分组后的表达式（支持 g.Key 和 g.Count() 等分组特有的访问，也支持二元运算如 g.Count() > 1）
        /// </summary>
        /// <param name="arg">要转换的表达式节点</param>
        /// <param name="lambdaParam">Lambda 表达式的参数</param>
        /// <param name="groupKeys">分组键集合</param>
        /// <returns>转换后的 Expr</returns>
        private Expr ConvertGroupedExpr(Expression arg, ParameterExpression lambdaParam, ValueTypeExpr[] groupKeys)
        {
            // 如果是 MemberAccess (如 g.Key) 
            if (arg is MemberExpression memberExpr)
            {
                // 检查是否是 lambda 参数的成员访问
                if (IsParameterAccess(memberExpr.Expression, lambdaParam))
                {
                    var memberName = memberExpr.Member.Name;
                    
                    // 检查是否是 Key 访问
                    if (memberName == "Key" && groupKeys?.Length > 0)
                    {
                        return groupKeys[0];
                    }
                    
                    // 其他成员访问
                    return Expr.Prop(memberName);
                }
            }
            
            // 如果是 MethodCallExpression (如 g.Count() 或 Enumerable.Count(g))
            if (arg is MethodCallExpression methodCall)
            {
                // 聚合函数名映射
                var aggregateName = GetAggregateName(methodCall.Method.Name);
                if (aggregateName != null)
                {
                    Expression aggregateTarget = null;
                    Expression fieldExpr = null;

                    if (methodCall.Object != null && IsParameterAccess(methodCall.Object, lambdaParam))
                    {
                        aggregateTarget = methodCall.Object;
                        if (methodCall.Arguments.Count > 0) fieldExpr = methodCall.Arguments[0];
                    }
                    else if (methodCall.Arguments.Count > 0 && IsParameterAccess(methodCall.Arguments[0], lambdaParam))
                    {
                        aggregateTarget = methodCall.Arguments[0];
                        if (methodCall.Arguments.Count > 1) fieldExpr = methodCall.Arguments[1];
                        else fieldExpr = methodCall.Arguments[0];
                    }

                    if (aggregateTarget != null)
                    {
                        // 获取聚合字段
                        ValueTypeExpr aggregateArg = Expr.Const(1);
                        
                        // 如果有第二个参数 (字段选择器)
                        if (fieldExpr != null && fieldExpr != aggregateTarget)
                        {
                            if (fieldExpr is LambdaExpression fieldLambda)
                            {
                                if (fieldLambda.Body is MemberExpression me) aggregateArg = Expr.Prop(me.Member.Name);
                            }
                            else if (fieldExpr is MemberExpression me2)
                            {
                                aggregateArg = Expr.Prop(me2.Member.Name);
                            }
                        }
                        
                        return new AggregateFunctionExpr(aggregateName, aggregateArg);
                    }
                }
            }

            // 处理二元运算 (如 g.Count() > 1)
            if (arg is BinaryExpression binary)
            {
                var left = ConvertGroupedExpr(binary.Left, lambdaParam, groupKeys);
                var right = ConvertGroupedExpr(binary.Right, lambdaParam, groupKeys);
                
                // Logic operation
                if (binary.NodeType == ExpressionType.AndAlso || binary.NodeType == ExpressionType.And)
                    return AsLogic(left).And(AsLogic(right));
                if (binary.NodeType == ExpressionType.OrElse || binary.NodeType == ExpressionType.Or)
                    return AsLogic(left).Or(AsLogic(right));
                
                // Other binary operators
                var op = binary.NodeType switch {
                    ExpressionType.Equal => LogicOperator.Equal,
                    ExpressionType.NotEqual => LogicOperator.NotEqual,
                    ExpressionType.GreaterThan => LogicOperator.GreaterThan,
                    ExpressionType.GreaterThanOrEqual => LogicOperator.GreaterThanOrEqual,
                    ExpressionType.LessThan => LogicOperator.LessThan,
                    ExpressionType.LessThanOrEqual => LogicOperator.LessThanOrEqual,
                    _ => (object)null
                };

                if (op is LogicOperator lo) return new LogicBinaryExpr(AsValue(left), lo, AsValue(right));
            }

            // 避免对 naked parameter 调用 ConvertInternal，这会触发 NotSupportedException
            if (arg is ParameterExpression pe && ReferenceEquals(pe, lambdaParam)) return null;
            
            // 回退到普通转换
            return ConvertInternal(arg);
        }

        /// <summary>
        /// 根据方法名获取对应的聚合函数名称（用于将 LINQ 聚合函数映射到 SQL 聚合函数）。
        /// </summary>
        /// <param name="name">方法名</param>
        /// <returns>聚合函数名称或 null</returns>
        private static string GetAggregateName(string name) => name switch
        {
            "Count" or "LongCount" => "Count",
            "Sum" => "Sum",
            "Average" => "Avg",
            "Max" => "Max",
            "Min" => "Min",
            _ => null
        };

        /// <summary>
        /// 判断给定表达式节点是否引用了指定的 Lambda 参数（即是否为参数访问）。
        /// </summary>
        /// <param name="node">要检查的表达式节点</param>
        /// <param name="lambdaParam">Lambda 的参数表达式</param>
        /// <returns>如果是对参数的访问则返回 true，否则返回 false</returns>
        private bool IsParameterAccess(Expression node, ParameterExpression lambdaParam)
        {
            if (node is null) return false;
            if (node is ParameterExpression pe) return ReferenceEquals(pe, lambdaParam);
            return false;
        }     
    }
}
