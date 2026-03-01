using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LiteOrm.Common
{
    /// <summary>
    /// 将 Lambda 表达式转换为框架通用的 Expr 模型。
    /// 处理常见的成员访问、二元/一元运算以及重写部分常用的 C# 方法调用映射。
    /// </summary>
    public class LambdaExprConverter
    {
        #region 静态成员
        private static readonly Dictionary<ExpressionType, object> _operatorMappings = new()
        {
            { ExpressionType.Equal, LogicOperator.Equal },
            { ExpressionType.NotEqual, LogicOperator.NotEqual },
            { ExpressionType.GreaterThan, LogicOperator.GreaterThan },
            { ExpressionType.GreaterThanOrEqual, LogicOperator.GreaterThanOrEqual },
            { ExpressionType.LessThan, LogicOperator.LessThan },
            { ExpressionType.LessThanOrEqual, LogicOperator.LessThanOrEqual },
            { ExpressionType.Add, ValueOperator.Add },
            { ExpressionType.AddChecked, ValueOperator.Add },
            { ExpressionType.Subtract, ValueOperator.Subtract },
            { ExpressionType.SubtractChecked, ValueOperator.Subtract },
            { ExpressionType.Multiply, ValueOperator.Multiply },
            { ExpressionType.MultiplyChecked, ValueOperator.Multiply },
            { ExpressionType.Divide, ValueOperator.Divide }
        };

        private static readonly ConcurrentDictionary<string, Func<MethodCallExpression, LambdaExprConverter, Expr>> _methodNameHandlers = new ConcurrentDictionary<string, Func<MethodCallExpression, LambdaExprConverter, Expr>>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<(Type type, string name), Func<MethodCallExpression, LambdaExprConverter, Expr>> _typeMethodHandlers = new ConcurrentDictionary<(Type type, string name), Func<MethodCallExpression, LambdaExprConverter, Expr>>();
        private static readonly ConcurrentDictionary<string, Func<MemberExpression, LambdaExprConverter, Expr>> _memberNameHandlers = new ConcurrentDictionary<string, Func<MemberExpression, LambdaExprConverter, Expr>>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<(Type type, string name), Func<MemberExpression, LambdaExprConverter, Expr>> _typeMemberHandlers = new ConcurrentDictionary<(Type type, string name), Func<MemberExpression, LambdaExprConverter, Expr>>();


        /// <summary>
        /// 默认的方法处理器：将方法名作为 SQL 函数名生成 FunctionExpr。
        /// </summary>
        public static Func<MethodCallExpression, LambdaExprConverter, Expr> DefaultFunctionHandler => (node, converter) =>
        {
            return converter.CreateFunctionExpr(node);
        };

        /// <summary>
        /// 默认的成员处理器：将成员（属性/字段）名映射为 FunctionExpr 或 PropertyExpr。
        /// </summary>
        public static Func<MemberExpression, LambdaExprConverter, Expr> DefaultMemberHandler => (node, converter) =>
        {
            return node.Expression is null ? new FunctionExpr(node.Member.Name) : new FunctionExpr(node.Member.Name, converter.AsValue(converter.Convert(node.Expression)));
        };

        /// <summary>
        /// 注册全局的方法转换逻辑。
        /// </summary>
        /// <param name="methodName">待拦截的方法名称。</param>
        /// <param name="handler">处理逻辑，若为 null 则使用默认处理器。</param>
        public static void RegisterMethodHandler(string methodName, Func<MethodCallExpression, LambdaExprConverter, Expr> handler = null)
        {
            _methodNameHandlers[methodName] = handler ?? DefaultFunctionHandler;
        }

        /// <summary>
        /// 注册特定类型的方法转换逻辑。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <param name="methodName">方法名称。若不指定，则扫描并注册所有公开方法。</param>
        /// <param name="handler">处理逻辑。</param>
        public static void RegisterMethodHandler(Type type, string methodName = null, Func<MethodCallExpression, LambdaExprConverter, Expr> handler = null)
        {
            handler ??= DefaultFunctionHandler;
            if (methodName == null)
            {
                // 批量注册该类型的所有实例或静态公开方法
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
                {
                    if (method.Name != "ToString" && method.Name != "Equals" && method.Name != "GetHashCode")
                        _typeMethodHandlers[(type, method.Name)] = handler;
                }
            }
            else
                _typeMethodHandlers[(type, methodName)] = handler;
        }

        /// <summary>
        /// 注册成员（属性/字段）的转换逻辑。
        /// </summary>
        public static void RegisterMemberHandler(string memberName, Func<MemberExpression, LambdaExprConverter, Expr> handler = null)
        {
            _memberNameHandlers[memberName] = handler ?? DefaultMemberHandler;
        }

        /// <summary>
        /// 注册特定类型的成员转换逻辑。
        /// </summary>
        public static void RegisterMemberHandler(Type type, string memberName, Func<MemberExpression, LambdaExprConverter, Expr> handler = null)
        {
            if (String.IsNullOrEmpty(memberName)) throw new ArgumentNullException(nameof(memberName));
            _typeMemberHandlers[(type, memberName)] = handler ?? DefaultMemberHandler;
        }

        #endregion 静态成员

        /// <summary>
        /// 初始化 LambdaExprConverter 类的新实例。
        /// </summary>
        /// <param name="expression">要转换的 Lambda 表达式。</param>
        public LambdaExprConverter(LambdaExpression expression)
        {
            if (expression is null) throw new ArgumentNullException(nameof(expression));
            _expression = expression;
            _rootParameter = expression.Parameters.FirstOrDefault();
            Type objectType = _rootParameter.Type;
            if (objectType.IsGenericType && (objectType.GetGenericTypeDefinition() == typeof(IQueryable<>) || objectType.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                objectType = objectType.GetGenericArguments()[0];
            if (objectType.Name.StartsWith("<>f__AnonymousType") && objectType.IsDefined(typeof(CompilerGeneratedAttribute), false) && objectType.IsGenericType && objectType.IsSealed)//匿名类不作为参数别名
                return;
            if (_rootParameter != null)
            {
                _parameterAliases[_rootParameter] = objectType.Name;
            }
        }

        /// <summary>
        /// 跟踪 Lambda 的主参数（通常是实体变量）
        /// </summary>
        protected readonly ParameterExpression _rootParameter;
        /// <summary>
        /// 原始 Lambda 对象
        /// </summary>
        protected readonly LambdaExpression _expression;
        /// <summary>
        /// 参数表达式到别名的映射
        /// </summary>
        protected readonly Dictionary<ParameterExpression, string> _parameterAliases = new Dictionary<ParameterExpression, string>();
        /// <summary>
        /// 检测表达式是否包含 Lambda 参数
        /// </summary>
        protected readonly ParameterExpressionDetector _parameterDetector = new ParameterExpressionDetector();

        /// <summary>
        /// 转换表达式节点为 Expr 对象。
        /// </summary>
        public virtual Expr Convert(Expression node) => ConvertInternal(node);

        /// <summary>
        /// 执行整体转换并将根节点转为 LogicExpr。
        /// </summary>
        public LogicExpr ToLogicExpr() => AsLogic(ConvertInternal(_expression.Body));

        /// <summary>
        /// 执行整体转换并将根节点转为 ValueTypeExpr。
        /// </summary>
        public ValueTypeExpr ToValueExpr() => AsValue(ConvertInternal(_expression.Body));

        /// <summary>
        /// 静态便捷入口，将 Lambda 表达式转换为 ValueTypeExpr 模型。
        /// </summary>
        public static ValueTypeExpr ToValueExpr(LambdaExpression expression) => new LambdaExprConverter(expression).ToValueExpr();

        /// <summary>
        /// 静态便捷入口，将 Lambda 表达式转换为 LogicExpr 模型。
        /// </summary>
        public static LogicExpr ToLogicExpr(LambdaExpression expression) => new LambdaExprConverter(expression).ToLogicExpr();

        /// <summary>
        /// 执行表达式转换并将结果转换为 SqlSegment
        /// </summary>
        public Expr ToSqlSegment()
        {
            return ConvertInternal(_expression.Body);
        }

        /// <summary>
        /// 静态方法：将 Lambda 表达式转换为 SqlSegment 模型
        /// </summary>
        /// <param name="expression">要转换的 Lambda 表达式</param>
        /// <returns>转换后的表达式对象</returns>
        public static Expr ToSqlSegment(LambdaExpression expression) => new LambdaExprConverter(expression).ToSqlSegment();


        #region 表达式转换核心逻辑

        /// <summary>
        /// 执行内部表达式转换，将表达式节点转换为 Expr 对象
        /// </summary>
        /// <param name="node">要转换的表达式节点</param>
        /// <returns>转换后的 Expr 对象</returns>
        protected virtual Expr ConvertInternal(Expression node)
        {
            if (node is null) return null;
            return node switch
            {
                BinaryExpression binary => ConvertBinary(binary),
                UnaryExpression unary => ConvertUnary(unary),
                MemberExpression member => ConvertMember(member),
                ConstantExpression constant => ConvertConstant(constant),
                ParameterExpression param => ConvertParameter(param),
                NewArrayExpression newArray => ConvertNewArray(newArray),
                ListInitExpression listInit => ConvertListInit(listInit),
                NewExpression newExpression => ConvertNew(newExpression),
                MethodCallExpression methodCall => ConvertMethodCall(methodCall),
                LambdaExpression lambda => ConvertLambda(lambda),
                _ => throw new NotSupportedException($"Unsupported expression type: {node.NodeType} ({node.GetType().Name})"),
            };
        }

        /// <summary>
        /// 将 Lambda 表达式转换为 Expr 对象
        /// </summary>
        /// <param name="lambda">要转换的 Lambda 表达式</param>
        /// <returns>转换后的 Expr 对象</returns>
        protected virtual Expr ConvertLambda(LambdaExpression lambda)
        {
            return lambda.ReturnType == typeof(bool) ? AsLogic(ConvertInternal(lambda.Body)) : AsValue(ConvertInternal(lambda.Body));
        }

        private Expr ConvertNew(NewExpression node)
        {
            if (_parameterDetector.ContainsParameter(node))
            {
                var items = new List<ValueTypeExpr>();
                foreach (var arg in node.Arguments)
                {
                    var item = ConvertInternal(arg) as ValueTypeExpr;
                    if (item is not null) items.Add(item);
                }
                return new ValueSet(ValueJoinType.List, items.ToArray());
            }
            return EvaluateToExpr(node);
        }

        /// <summary>
        /// 将 Expr 表达式转换为 LogicExpr 逻辑表达式
        /// </summary>
        /// <param name="expr">要转换的表达式</param>
        /// <returns>转换后的 LogicExpr 对象</returns>
        /// <exception cref="NotSupportedException">当表达式无法转换为 LogicExpr 时抛出</exception>
        protected LogicExpr AsLogic(Expr expr)
        {
            if (expr is LogicExpr logicExpr) return logicExpr;
            if (expr is ValueTypeExpr vte) return new LogicBinaryExpr(vte, LogicOperator.Equal, new ValueExpr(true));
            throw new NotSupportedException($"Expression {expr} of type {expr?.GetType().Name} cannot be converted to LogicExpr.");
        }

        /// <summary>
        /// 将 Expr 表达式转换为 ValueTypeExpr 值类型表达式
        /// </summary>
        /// <param name="expr">要转换的表达式</param>
        /// <returns>转换后的 ValueTypeExpr 对象</returns>
        /// <exception cref="NotSupportedException">当表达式无法转换为 ValueTypeExpr 时抛出</exception>
        protected ValueTypeExpr AsValue(Expr expr)
        {
            if (expr is ValueTypeExpr valueExpr) return valueExpr;
            else throw new NotSupportedException($"Expression {expr} of type {expr?.GetType().Name} cannot be converted to ValueTypeExpr.");
        }

        /// <summary>
        /// 将二元表达式（如 a + b、a == b）转换为对应的 Expr 对象
        /// </summary>
        /// <param name="node">要转换的二元表达式节点</param>
        /// <returns>转换后的 Expr 对象（可能是 LogicBinaryExpr 或 ValueBinaryExpr）</returns>
        protected Expr ConvertBinary(BinaryExpression node)
        {
            // 处理 ?? 运算符，依赖参数时转为 COALESCE 函数，否则本地计算
            if (node.NodeType == ExpressionType.Coalesce)
            {
                return _parameterDetector.ContainsParameter(node.Left) || _parameterDetector.ContainsParameter(node.Right) ? new FunctionExpr("COALESCE", ConvertInternal(node.Left) as ValueTypeExpr, ConvertInternal(node.Right) as ValueTypeExpr) : EvaluateToExpr(node);
            }

            var left = ConvertInternal(node.Left);
            var right = ConvertInternal(node.Right);

            // 处理逻辑 AND/OR 及加法字符串连接重载
            switch (node.NodeType)
            {
                case ExpressionType.AndAlso:
                case ExpressionType.And:
                    return AsLogic(left).And(AsLogic(right));
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return AsLogic(left).Or(AsLogic(right));
                case ExpressionType.Add:
                    // 字符串拼接映射
                    if (node.Left.Type == typeof(string) || node.Right.Type == typeof(string))
                        return new ValueBinaryExpr(AsValue(left), ValueOperator.Concat, AsValue(right));
                    else
                        return new ValueBinaryExpr(AsValue(left), ValueOperator.Add, AsValue(right));
                default:
                    if (_operatorMappings.TryGetValue(node.NodeType, out var op))
                    {
                        // 特殊处理 CompareTo 调用 (a.CompareTo(b) op 0) -> 扁平化为直接的 BinaryExpr (a op b)
                        if (node.Left is MethodCallExpression leftCallExpression && leftCallExpression.Method.Name == "CompareTo")
                        {
                            var vRight = AsValue(right);
                            if (!(vRight is ValueExpr ve && Equals(ve.Value, 0))) throw new ArgumentException($"CompareTo method can only be compared with 0: {node}");
                            if (left is FunctionExpr fe && fe.Parameters.Count == 2)
                            {
                                left = fe.Parameters[0];
                                right = fe.Parameters[1];
                            }
                        }
                        else if (node.Right is MethodCallExpression rightCallExpression && rightCallExpression.Method.Name == "CompareTo")
                        {
                            var vLeft = AsValue(left);
                            if (!(vLeft is ValueExpr ve && Equals(ve.Value, 0))) throw new ArgumentException($"CompareTo method can only be compared with 0: {node}");
                            if (right is FunctionExpr fe && fe.Parameters.Count == 2)
                            {
                                left = fe.Parameters[1];// 交换参数位置
                                right = fe.Parameters[0];
                            }
                        }

                        if (op is ValueOperator vop)
                            return new ValueBinaryExpr(left as ValueTypeExpr, vop, AsValue(right));
                        else
                            return new LogicBinaryExpr(left as ValueTypeExpr, (LogicOperator)op, AsValue(right));
                    }
                    else
                        throw new NotSupportedException($"Unsupported binary operator: {node.NodeType}");
            }
        }

        private Expr ConvertUnary(UnaryExpression node)
        {
            var operand = ConvertInternal(node.Operand);

            if (operand is null)
                throw new ArgumentException($"Unable to convert unary expression: {node}");

            return node.NodeType switch
            {
                ExpressionType.OnesComplement => new UnaryExpr(UnaryOperator.BitwiseNot, operand as ValueTypeExpr),
                ExpressionType.Not => new NotExpr(AsLogic(operand)),
                ExpressionType.Negate => new UnaryExpr(UnaryOperator.Nagive, operand as ValueTypeExpr),
                ExpressionType.Convert or ExpressionType.Quote => operand,// 不需要额外处理
                _ => throw new NotSupportedException($"Unsupported unary operator: {node.NodeType}"),
            };
        }

        /// <summary>
        /// 计算表达式的值。如果表达式依赖于 Lambda 参数，则无法计算。
        /// </summary>
        private Expr EvaluateToExpr(Expression node)
        {
            var value = Evaluate(node);
            if (value is Expr expr) return expr;
            return new ValueExpr(value);
        }

        /// <summary>
        /// 尝试从任意表达式中解析值：
        /// - 直接读取 ConstantExpression
        /// - 处理 UnaryExpression(转换) 包含的常量
        /// - 最后尝试编译并执行表达式以求值（用于闭包变量等）
        /// 若无法求值则抛出异常。
        /// </summary>
        protected object Evaluate(Expression expr)
        {
            if (expr is null) throw new ArgumentNullException(nameof(expr));

            // 常量直接读取
            if (expr is ConstantExpression ce)
            {
                return ce.Value;
            }

            // 处理常见的转换包装 (例如 Convert(constant))
            if (expr is UnaryExpression ue && ue.Operand is ConstantExpression ce2)
            {
                return ce2.Value;
            }

            // 尝试编译并执行表达式（支持闭包、字段、属性等）
            try
            {
                var lambda = Expression.Lambda(expr);
                var compiled = lambda.Compile();
                return compiled.DynamicInvoke();
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Unable to evaluate the value from expression: {expr}", ex);
            }
        }

        /// <summary>
        /// 将成员访问表达式（如 x.Name）转换为对应的 Expr 对象
        /// </summary>
        /// <param name="node">要转换的成员访问表达式节点</param>
        /// <returns>转换后的 Expr 对象</returns>
        protected Expr ConvertMember(MemberExpression node)
        {
            // Nullable<T>.Value 自动降级处理
            if (Nullable.GetUnderlyingType(node.Member.DeclaringType) is not null && node.Member.Name == "Value")
            {
                return ConvertInternal(node.Expression);
            }

            // 1. 优先匹配已注册的类型成员处理器 (如 DateTime.Now)
            if (node.Member.DeclaringType != null && _typeMemberHandlers.TryGetValue((node.Member.DeclaringType, node.Member.Name), out var typeMemberHandler))
            {
                var result = typeMemberHandler(node, this);
                if (result is not null) return result;
            }

            // 2. 匹配已注册的通用名称处理器 (如 Length)
            if (_memberNameHandlers.TryGetValue(node.Member.Name, out var nameMemberHandler))
            {
                var result = nameMemberHandler(node, this);
                if (result is not null) return result;
            }

            // 3. 处理直接的实体参数访问 (映射为数据库列)
            if (node.Expression is ParameterExpression paramExpr)
            {
                if (_parameterAliases.TryGetValue(paramExpr, out var paramAlias))
                {
                    if (node.Member is PropertyInfo propertyInfo)
                    {
                        return new PropertyExpr(propertyInfo.Name) { TableAlias = paramAlias };
                    }
                    else if (node.Member is FieldInfo fieldInfo)
                    {
                        return new PropertyExpr(fieldInfo.Name) { TableAlias = paramAlias };
                    }
                }
                else
                {
                    if (node.Member is PropertyInfo propertyInfo)
                    {
                        return Expr.Prop(propertyInfo.Name);
                    }
                    else if (node.Member is FieldInfo fieldInfo)
                    {
                        return Expr.Prop(fieldInfo.Name);
                    }
                }
            }

            // 4. 处理外部变量引用（参数不在字典中的其他情况）
            var (externalAlias, propertyName) = GetExternalPropertyInfo(node);
            if (externalAlias != null)
            {
                return new PropertyExpr(propertyName) { TableAlias = externalAlias };
            }

            // 5. 不依赖参数的成员访问（闭包/静态量）在本地计算结果
            return EvaluateToExpr(node);
        }

        private Expr ConvertNewArray(NewArrayExpression node)
        {
            var items = new List<ValueTypeExpr>();
            foreach (var expression in node.Expressions)
            {
                var item = ConvertInternal(expression) as ValueTypeExpr;
                if (item is not null)
                {
                    items.Add(item);
                }
            }

            return new ValueSet(items);
        }

        private Expr ExprSet(List<ValueTypeExpr> items)
        {
            return new ValueSet(items);
        }

        private Expr ConvertListInit(ListInitExpression node)
        {
            var items = new List<ValueTypeExpr>();
            foreach (var init in node.Initializers)
            {
                foreach (var arg in init.Arguments)
                {
                    var item = ConvertInternal(arg) as ValueTypeExpr;
                    if (item is not null)
                    {
                        items.Add(item);
                    }
                }
            }

            return new ValueSet(items);
        }

        #endregion

        #region 方法调用处理

        /// <summary>
        /// 将方法调用表达式转换为对应的 Expr 对象
        /// </summary>
        /// <param name="node">要转换的方法调用表达式节点</param>
        /// <returns>转换后的 Expr 对象</returns>
        protected virtual Expr ConvertMethodCall(MethodCallExpression node)
        {
            var type = node.Method.DeclaringType;

            // 处理 LINQ 扩展方法（Queryable、Enumerable）
            if (type == typeof(Queryable) || type == typeof(Enumerable) || type != null && type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IQueryable<>) || type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                // 处理 IQueryable/Enumerable 上的扩展方法
                switch (node.Method.Name)
                {
                    case "Where": return HandleWhere(node);
                    case "OrderBy" or "OrderByDescending": return HandleOrderBy(node, node.Method.Name == "OrderBy");
                    case "ThenBy" or "ThenByDescending": return HandleThenBy(node, node.Method.Name == "ThenBy");
                    case "Skip": return HandleSkip(node);
                    case "Take": return HandleTake(node);
                    case "GroupBy": return HandleGroupBy(node);
                    case "Select": return HandleSelect(node);
                }
            }
            if (type == typeof(Expr) && node.Method.Name == "Exists")
            {
                return HandleExists(node);
            }

            // 处理类型成员处理器
            if (type != null && _typeMethodHandlers.TryGetValue((type, node.Method.Name), out var typeMethodHandler))
            {
                var result = typeMethodHandler(node, this);
                if (result is not null) return result;
            }

            // 处理方法名处理器
            if (_methodNameHandlers.TryGetValue(node.Method.Name, out var nameHandler))
            {
                var result = nameHandler(node, this);
                if (result is not null) return result;
            }

            if (type.IsPrimitive)
                return DefaultFunctionHandler(node, this);
            else if (_parameterDetector.ContainsParameter(node))
            {
                // 如果是实例方法且包含参数依赖
                if (node.Object != null) return ConvertInternal(node.Object);
                return DefaultFunctionHandler(node, this);
            }
            else
                return EvaluateToExpr(node);
        }

        /// <summary>
        /// 将常量表达式转换为对应的 Expr 对象
        /// </summary>
        /// <param name="node">要转换的常量表达式节点</param>
        /// <returns>转换后的 Expr 对象</returns>
        protected Expr ConvertConstant(ConstantExpression node)
        {
            if (node.Value is Expr expr) return expr;
            if (node.Value == null) return Expr.Null;
            bool isConst = node.Type.IsPrimitive;
            return new ValueExpr(node.Value) { IsConst = isConst };
        }

        #endregion

        #region 内部处理逻辑

        private FunctionExpr CreateFunctionExpr(MethodCallExpression node)
        {
            var parameters = new List<ValueTypeExpr>();

            // 添加对象实例（如果是非静态方法）
            if (node.Object is not null)
            {
                var obj = ConvertInternal(node.Object) as ValueTypeExpr;
                if (obj is not null)
                {
                    parameters.Add(obj);
                }
            }

            // 添加方法参数
            foreach (var arg in node.Arguments)
            {
                var param = ConvertInternal(arg) as ValueTypeExpr;
                if (param is not null)
                {
                    parameters.Add(param);
                }
            }

            return new FunctionExpr(node.Method.Name, parameters.ToArray());
        }

        #endregion

        /// <summary>
        /// 表达式参数检测器
        /// </summary>
        public class ParameterExpressionDetector : ExpressionVisitor
        {
            private bool _hasParameter = false;
            /// <summary>
            /// 检查表达式中是否包含参数引用
            /// </summary>
            /// <param name="expression">要在检查的表达式。</param>
            /// <returns>如果包含参数引用则返回 true，否则返回 false。</returns>
            public bool ContainsParameter(Expression expression)
            {
                _hasParameter = false;
                Visit(expression);
                return _hasParameter;
            }

            /// <summary>
            /// 访问参数表达式。
            /// </summary>
            /// <param name="node">参数表达式节点。</param>
            /// <returns>返回处理后的表达式节点。</returns>
            protected override Expression VisitParameter(ParameterExpression node)
            {
                _hasParameter = true;
                return base.VisitParameter(node);
            }
        }

        /// <summary>
        /// 将参数表达式转换为 From 表达式。
        /// 当参数对应一个 IQueryable 或 IEnumerable 类型时，会取其泛型参数类型作为表实体类型。
        /// </summary>
        /// <param name="node">参数表达式</param>
        /// <returns>对应的 FromExpr</returns>
        private Expr ConvertParameter(ParameterExpression node)
        {
            var type = node.Type;
            if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IQueryable<>) || type.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                type = type.GetGenericArguments()[0];

            if (!_parameterAliases.TryGetValue(node, out var alias))
            {
                alias = "T" + _parameterAliases.Count;
                _parameterAliases[node] = alias;
            }

            return new FromExpr(type) { Alias = alias };
        }

        /// <summary>
        /// 处理子 Lambda 表达式，返回 LogicExpr 或 ValueTypeExpr 取决于 Lambda 的返回类型。
        /// </summary>
        /// <param name="lambda">要处理的 Lambda 表达式</param>
        /// <returns>对应的 Expr（LogicExpr 或 ValueTypeExpr）</returns>
        private Expr HandleSubLambda(LambdaExpression lambda) => lambda.ReturnType == typeof(bool) ? ToLogicExpr(lambda) : ToValueExpr(lambda);

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

            // 将 Lambda 条件转换为 LogicExpr
            var newCondition = ToLogicExpr(lambda);

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
                var op = binary.NodeType switch
                {
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
        /// Exists 方法处理器：将 Expr.Exists{T}(lambda) 转换为 ForeignExpr。
        /// </summary>
        private Expr HandleExists(MethodCallExpression node)
        {
            if (node.Method.Name == "Exists" && node.Arguments.Count == 1)
            {
                Expression lambdaArg = node.Arguments[0];
                if (lambdaArg is UnaryExpression unaryExpr && unaryExpr.NodeType == ExpressionType.Quote)
                {
                    lambdaArg = unaryExpr.Operand;
                }
                var lambda = lambdaArg as LambdaExpression;
                if (lambda != null)
                {
                    ParameterExpression parameter = lambda.Parameters.FirstOrDefault();
                    if (parameter != null && !_parameterAliases.ContainsKey(parameter))
                    {
                        _parameterAliases[parameter] = "T" + _parameterAliases.Count;
                    }
                    return new ForeignExpr(parameter.Type, _parameterAliases[parameter], AsLogic(ConvertInternal(lambda.Body)));
                }
            }
            return null;
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

        /// <summary>
        /// 获取外部变量引用的属性信息。
        /// 例如：t.DeptId 返回 (t, "DeptId")
        /// </summary>
        /// <param name="node">成员访问表达式节点</param>
        /// <returns>元组 (别名, 属性名) 或 (null, null)</returns>
        private (string alias, string propertyName) GetExternalPropertyInfo(MemberExpression node)
        {
            if (node.Expression is ParameterExpression paramExpr)
            {
                if (_parameterAliases.TryGetValue(paramExpr, out var alias))
                {
                    return (alias, node.Member.Name);
                }
                return (paramExpr.Name, node.Member.Name);
            }
            return (null, null);
        }
    }
}
