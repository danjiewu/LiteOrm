using LiteOrm.Service;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LiteOrm.Common
{
    /// <summary>
    /// 将 Lambda 表达式转换为 Expr 表达式对象的转换类。
    /// </summary>
    public class LambdaExprConverter
    {
        private static readonly Dictionary<ExpressionType, BinaryOperator> _operatorMappings = new Dictionary<ExpressionType, BinaryOperator>
        {
            { ExpressionType.Equal, BinaryOperator.Equal },
            { ExpressionType.NotEqual, BinaryOperator.NotEqual },
            { ExpressionType.GreaterThan, BinaryOperator.GreaterThan },
            { ExpressionType.GreaterThanOrEqual, BinaryOperator.GreaterThanOrEqual },
            { ExpressionType.LessThan, BinaryOperator.LessThan },
            { ExpressionType.LessThanOrEqual, BinaryOperator.LessThanOrEqual },
            { ExpressionType.Add, BinaryOperator.Add },
            { ExpressionType.AddChecked, BinaryOperator.Add },
            { ExpressionType.Subtract, BinaryOperator.Subtract },
            { ExpressionType.SubtractChecked, BinaryOperator.Subtract },
            { ExpressionType.Multiply, BinaryOperator.Multiply },
            { ExpressionType.MultiplyChecked, BinaryOperator.Multiply },
            { ExpressionType.Divide, BinaryOperator.Divide }
        };

        private readonly ParameterExpression _rootParameter;
        private readonly LambdaExpression _expression;
        private readonly ParameterExpressionDetector _parameterDetector = new ParameterExpressionDetector();

        private static readonly Dictionary<string, Func<MethodCallExpression, LambdaExprConverter, Expr>> _methodNameHandlers = new Dictionary<string, Func<MethodCallExpression, LambdaExprConverter, Expr>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<MethodInfo, Func<MethodCallExpression, LambdaExprConverter, Expr>> _methodHandlers = new Dictionary<MethodInfo, Func<MethodCallExpression, LambdaExprConverter, Expr>>();

        static LambdaExprConverter()
        {
            RegisterDefaultHandlers();
        }

        private static void RegisterDefaultHandlers()
        {
            // String methods
            RegisterMethodHandler("StartsWith", (node, converter) =>
            {
                if (node.Method.DeclaringType != typeof(string)) return null;
                var left = converter.Convert(node.Object);
                var right = converter.Convert(node.Arguments[0]);
                return new BinaryExpr(left, BinaryOperator.StartsWith, right);
            });

            RegisterMethodHandler("EndsWith", (node, converter) =>
            {
                if (node.Method.DeclaringType != typeof(string)) return null;
                var left = converter.Convert(node.Object);
                var right = converter.Convert(node.Arguments[0]);
                return new BinaryExpr(left, BinaryOperator.EndsWith, right);
            });

            RegisterMethodHandler("Contains", (node, converter) =>
            {
                if (node.Method.DeclaringType == typeof(string))
                {
                    var left = converter.Convert(node.Object);
                    var right = converter.Convert(node.Arguments[0]);
                    return new BinaryExpr(left, BinaryOperator.Contains, right);
                }
                if (node.Method.DeclaringType == typeof(Enumerable) || typeof(IEnumerable).IsAssignableFrom(node.Method.DeclaringType))
                {
                    // Enumerable.Contains(source, value) OR list.Contains(value)
                    Expr collection = null;
                    Expr value = null;
                    if (node.Method.IsStatic)
                    {
                        collection = converter.Convert(node.Arguments[0]);
                        value = converter.Convert(node.Arguments[1]);
                    }
                    else
                    {
                        collection = converter.Convert(node.Object);
                        value = converter.Convert(node.Arguments[0]);
                    }
                    return new BinaryExpr(value, BinaryOperator.In, collection);
                }
                return null;
            });

            RegisterMethodHandler("Concat", (node, converter) =>
            {
                if (node.Method.DeclaringType != typeof(string)) return null;
                Expr left = node.Object != null ? converter.Convert(node.Object) : converter.Convert(node.Arguments[0]);
                Expr right = node.Object != null ? converter.Convert(node.Arguments[0]) : converter.Convert(node.Arguments[1]);
                return new BinaryExpr(left, BinaryOperator.Concat, right);
            });

            RegisterMethodHandler("Equals", (node, converter) =>
            {
                Expr left = null;
                Expr right = null;
                if (node.Object != null)
                {
                    left = converter.Convert(node.Object);
                    right = converter.Convert(node.Arguments[0]);
                }
                else
                {
                    left = converter.Convert(node.Arguments[0]);
                    right = converter.Convert(node.Arguments[1]);
                }
                return new BinaryExpr(left, BinaryOperator.Equal, right);
            });

            RegisterMethodHandler("Compare", (node, converter) =>
            {
                var left = converter.Convert(node.Arguments[0]);
                var right = converter.Convert(node.Arguments[1]);
                return new BinaryExpr(left, BinaryOperator.Equal, right);
            });

            RegisterMethodHandler("CompareTo", (node, converter) =>
            {
                var left = node.Object != null ? converter.Convert(node.Object) : converter.Convert(node.Arguments[0]);
                var right = node.Object != null ? converter.Convert(node.Arguments[0]) : converter.Convert(node.Arguments[1]);
                return new BinaryExpr(left, BinaryOperator.Equal, right);
            });
        }

        /// <summary>
        /// 注册方法调用转换句柄。
        /// </summary>
        /// <param name="methodName">方法名称。</param>
        /// <param name="handler">处理句柄。</param>
        public static void RegisterMethodHandler(string methodName, Func<MethodCallExpression, LambdaExprConverter, Expr> handler)
        {
            _methodNameHandlers[methodName] = handler;
        }

        /// <summary>
        /// 注册方法调用转换句柄。
        /// </summary>
        /// <param name="methodInfo">方法信息。</param>
        /// <param name="handler">处理句柄。</param>
        public static void RegisterMethodHandler(MethodInfo methodInfo, Func<MethodCallExpression, LambdaExprConverter, Expr> handler)
        {
            _methodHandlers[methodInfo] = handler;
        }

        /// <summary>
        /// 注册方法调用转换句柄。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="methodExpr">方法调用表达式。</param>
        /// <param name="handler">处理句柄。</param>
        public static void RegisterMethodHandler<T>(Expression<Action<T>> methodExpr, Func<MethodCallExpression, LambdaExprConverter, Expr> handler)
        {
            if (methodExpr.Body is MethodCallExpression mce)
            {
                RegisterMethodHandler(mce.Method, handler);
            }
            else
            {
                throw new ArgumentException("表达式必须是方法调用。");
            }
        }

        /// <summary>
        /// 注册方法调用转换句柄。
        /// </summary>
        /// <param name="methodExpr">方法调用表达式。</param>
        /// <param name="handler">处理句柄。</param>
        public static void RegisterMethodHandler(Expression<Action> methodExpr, Func<MethodCallExpression, LambdaExprConverter, Expr> handler)
        {
            if (methodExpr.Body is MethodCallExpression mce)
            {
                RegisterMethodHandler(mce.Method, handler);
            }
            else
            {
                throw new ArgumentException("表达式必须是方法调用。");
            }
        }

        /// <summary>
        /// 转换表达式节点。
        /// </summary>
        /// <param name="node">表达式节点。</param>
        /// <returns>转换后的 Expr。</returns>
        public Expr Convert(Expression node)
        {
            return ConvertInternal(node);
        }

        /// <summary>
        /// 初始化 LambdaExprConverter。
        /// </summary>
        /// <param name="expression">Lambda 表达式。</param>
        public LambdaExprConverter(LambdaExpression expression)
        {
            if (expression is null) throw new ArgumentNullException(nameof(expression));
            _expression = expression;
            _rootParameter = expression.Parameters.FirstOrDefault();
        }

        /// <summary>
        /// 将 Lambda 表达式转换为 Expr。
        /// <returns>转换后的 Expr。</returns>
        /// </summary>
        public Expr ToExpr()
        {
            var stmt = ConvertInternal(_expression.Body);
            if (stmt is null) throw new ArgumentException($"无法转换表达式: {_expression.Body}");
            return stmt;
        }

        /// <summary>
        /// 将 Lambda 表达式转换为 Expr。
        /// <param name="expression">Lambda 表达式。</param>
        /// <returns>转换后的 Expr。</returns>
        /// </summary>
        public static Expr ToExpr(LambdaExpression expression)
        {
            var converter = new LambdaExprConverter(expression);
            return converter.ToExpr();
        }

        #region 表达式转换核心逻辑

        private Expr ConvertInternal(Expression node)
        {
            switch (node)
            {
                case BinaryExpression binary:
                    return ConvertBinary(binary);
                case UnaryExpression unary:
                    return ConvertUnary(unary);
                case MemberExpression member:
                    return ConvertMember(member);
                case ConstantExpression constant:
                    if (constant.Value is Expr exprValue) return exprValue;
                    return new ValueExpr(constant.Value);
                case ParameterExpression param:
                    throw new NotSupportedException($"参数表达式 '{param.Name}' 不能直接转换为 Expr");
                case NewArrayExpression newArray:
                    return ConvertNewArray(newArray);
                case ListInitExpression listInit:
                    return ConvertListInit(listInit);
                case MethodCallExpression methodCall:
                    return ConvertMethodCall(methodCall);
                case NewExpression newExpression:
                    return EvaluateToExpr(newExpression);
                default:
                    throw new NotSupportedException($"不支持的表达式类型: {node.NodeType} ({node.GetType().Name})");
            }
        }

        private Expr ConvertBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.Coalesce)
            {
                return _parameterDetector.ContainsParameter(node.Left) || _parameterDetector.ContainsParameter(node.Right) ? new FunctionExpr("COALESCE", ConvertInternal(node.Left), ConvertInternal(node.Right)) : EvaluateToExpr(node);
            }

            // 特殊处理 CompareTo 方法调用
            if (node.Left is MethodCallExpression leftCallExpression && leftCallExpression.Method.Name == "CompareTo")
            {
                var compareRight = EvaluateToExpr(node.Right);
                if (!(compareRight is ValueExpr ve && Equals(ve.Value, 0))) throw new ArgumentException($"CompareTo 方法只能与 0 进行比较: {node}");
                BinaryExpr res = ConvertMethodCall(leftCallExpression) as BinaryExpr;
                if (_operatorMappings.TryGetValue(node.NodeType, out var op))
                    res.Operator = op switch
                    {
                        BinaryOperator.Equal => BinaryOperator.Equal,
                        BinaryOperator.NotEqual => BinaryOperator.NotEqual,
                        BinaryOperator.GreaterThan => BinaryOperator.GreaterThan,
                        BinaryOperator.GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
                        BinaryOperator.LessThan => BinaryOperator.LessThan,
                        BinaryOperator.LessThanOrEqual => BinaryOperator.LessThanOrEqual,
                        _ => throw new ArgumentException($"CompareTo 方法只能使用 ==, !=, >, >=, <, <= 进行比较: {node}")
                    };
                else throw new ArgumentException($"CompareTo 方法只能使用 ==, !=, >, >=, <, <= 进行比较: {node}");
                return res;
            }
            else if (node.Right is MethodCallExpression rightCallExpression && rightCallExpression.Method.Name == "CompareTo")
            {
                var compareLeft = EvaluateToExpr(node.Left);
                if (!(compareLeft is ValueExpr ve && Equals(ve.Value, 0))) throw new ArgumentException($"CompareTo 方法只能与 0 进行比较: {node}");
                BinaryExpr res = ConvertMethodCall(rightCallExpression) as BinaryExpr;
                if (_operatorMappings.TryGetValue(node.NodeType, out var op))
                    // 反转操作符
                    res.Operator = op switch
                    {
                        BinaryOperator.Equal => BinaryOperator.Equal,
                        BinaryOperator.NotEqual => BinaryOperator.NotEqual,
                        BinaryOperator.GreaterThan => BinaryOperator.LessThan,
                        BinaryOperator.GreaterThanOrEqual => BinaryOperator.LessThanOrEqual,
                        BinaryOperator.LessThan => BinaryOperator.GreaterThan,
                        BinaryOperator.LessThanOrEqual => BinaryOperator.GreaterThanOrEqual,
                        _ => throw new ArgumentException($"CompareTo 方法只能使用 ==, !=, >, >=, <, <= 进行比较: {node}")
                    };
                else throw new ArgumentException($"CompareTo 方法只能使用 ==, !=, >, >=, <, <= 进行比较: {node}");
                return res;
            }
            var left = ConvertInternal(node.Left);
            var right = ConvertInternal(node.Right);

            // 处理逻辑运算符
            switch (node.NodeType)
            {
                case ExpressionType.AndAlso:
                case ExpressionType.And:
                    return left.And(right);
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return left.Or(right);
                case ExpressionType.Add:
                    if (node.Left.Type == typeof(string) || node.Right.Type == typeof(string))
                        return new BinaryExpr(left, BinaryOperator.Concat, right);
                    else
                        return new BinaryExpr(left, BinaryOperator.Add, right);
                default:
                    if (_operatorMappings.TryGetValue(node.NodeType, out var op))
                        return new BinaryExpr(left, op, right);
                    else
                        throw new NotSupportedException($"不支持的二元操作符: {node.NodeType}");
            }
        }

        private Expr ConvertUnary(UnaryExpression node)
        {
            var operand = ConvertInternal(node.Operand);

            if (operand is null)
            {
                throw new ArgumentException($"无法转换一元表达式: {node}");
            }

            switch (node.NodeType)
            {
                case ExpressionType.OnesComplement:
                    return new UnaryExpr(UnaryOperator.BitwiseNot, operand);
                case ExpressionType.Not:
                    return new UnaryExpr(UnaryOperator.Not, operand);
                case ExpressionType.Negate:
                    return new UnaryExpr(UnaryOperator.Nagive, operand);
                case ExpressionType.Convert:
                    // 类型转换通常不需要额外处理
                    return operand;
                default:
                    throw new NotSupportedException($"不支持的一元操作符: {node.NodeType}");
            }
        }

        private Expr EvaluateToExpr(Expression node)
        {
            try
            {
                // 尝试计算 Expression 的值
                var lambda = Expression.Lambda(node);
                var compiled = lambda.Compile();
                var value = compiled.DynamicInvoke();
                if (value is Expr expr) return expr;
                return new ValueExpr(value);
            }
            catch
            {
                throw new ArgumentException($"无法计算 Expression 的值: {node}");
            }
        }

        private Expr ConvertMember(MemberExpression node)
        {
            if (Nullable.GetUnderlyingType(node.Member.DeclaringType) is not null && node.Member.Name == "Value")
            {
                // 处理 Nullable<T>.Value  
                return ConvertInternal(node.Expression);
            }
            // 处理属性访问，如 x => x.Name
            if (node.Expression is ParameterExpression paramExpr &&
                (_rootParameter is null || paramExpr == _rootParameter))
            {
                if (node.Member is PropertyInfo propertyInfo)
                {
                    return Expr.Property(propertyInfo.Name);
                }
                else if (node.Member is FieldInfo fieldInfo)
                {
                    return Expr.Property(fieldInfo.Name);
                }
            }

            if (IsFunction(node))
            {
                // 处理字符串或数组的长属性 Length 等
                var targetExpr = node.Expression;
                if (targetExpr is null) return new FunctionExpr(node.Member.Name);
                else
                    return new FunctionExpr(node.Member.Name, ConvertInternal(targetExpr));
            }

            if (new ParameterExpressionDetector().ContainsParameter(node))
            {
                // 处理嵌套属性访问，如 x => x.Address.City
                var parts = new List<string>();
                Expression current = node;
                while (current is MemberExpression memberExpr)
                {
                    parts.Add(memberExpr.Member.Name);
                    current = memberExpr.Expression;
                }
                parts.Reverse();
                var propertyName = string.Join(".", parts);
                return Expr.Property(propertyName);
            }
            else// 处理静态成员访问，如 DateTime.Now
                return EvaluateToExpr(node);
        }

        private static bool IsFunction(MemberExpression node)
        {
            switch (node.Member.Name)
            {
                case "Length":
                    return node.Type == typeof(int) && (node.Expression.Type == typeof(string) || typeof(Array).IsAssignableFrom(node.Expression.Type));
                case "Now":
                    return node.Type == typeof(DateTime);
                case "Today":
                    return node.Type == typeof(DateTime);
                default:
                    return false;
            }
        }

        private Expr ConvertNewArray(NewArrayExpression node)
        {
            var items = new List<Expr>();
            foreach (var expression in node.Expressions)
            {
                var item = ConvertInternal(expression);
                if (item is not null)
                {
                    items.Add(item);
                }
            }

            return new ExprSet(items);
        }

        private Expr ConvertListInit(ListInitExpression node)
        {
            var items = new List<Expr>();
            foreach (var init in node.Initializers)
            {
                foreach (var arg in init.Arguments)
                {
                    var item = ConvertInternal(arg);
                    if (item is not null)
                    {
                        items.Add(item);
                    }
                }
            }

            return new ExprSet(items);
        }

        #endregion

        #region 方法调用处理

        private Expr ConvertMethodCall(MethodCallExpression node)
        {
            if (_methodHandlers.TryGetValue(node.Method, out var handler))
            {
                var result = handler(node, this);
                if (result != null) return result;
            }

            if (_methodNameHandlers.TryGetValue(node.Method.Name, out var nameHandler))
            {
                var result = nameHandler(node, this);
                if (result != null) return result;
            }

            if (!_parameterDetector.ContainsParameter(node))
            {
                return EvaluateToExpr(node);
            }

            // 默认处理逻辑
            bool useFunction = node.Method.Name != "ToString";
            return ConvertMethodCallDefault(node, useFunction);
        }

        #endregion

        #region 内部处理逻辑

        private Expr ConvertMethodCallDefault(MethodCallExpression node, bool useFunction)
        {
            if (_parameterDetector.ContainsParameter(node))
            {
                if (useFunction)
                    // 将外部方法视为函数调用
                    return CreateFunctionExpr(node);
                else
                    return ConvertInternal(node.Object);
            }
            else
                return EvaluateToExpr(node);
        }

        private Expr CreateFunctionExpr(MethodCallExpression node)
        {
            var parameters = new List<Expr>();

            // 添加对象实例（如果是非静态方法）
            if (node.Object is not null)
            {
                var obj = ConvertInternal(node.Object);
                if (obj is not null)
                {
                    parameters.Add(obj);
                }
            }

            // 添加方法参数
            foreach (var arg in node.Arguments)
            {
                var param = ConvertInternal(arg);
                if (param is not null)
                {
                    parameters.Add(param);
                }
            }

            // 方法名作为函数名
            var functionName = node.Method.Name;

            // 特殊处理一些常用类
            if (node.Method.DeclaringType == typeof(Math))
            {
                // Math 类方法通常直接使用原名或大写映射
                functionName = node.Method.Name.ToUpper();
            }

            return new FunctionExpr(functionName, parameters.ToArray());
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
    }
}
