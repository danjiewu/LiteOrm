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
    /// 将 Lambda 表达式转换为框架通用的 Expr 模型。
    /// 处理常见的成员访问、二元/一元运算以及重写部分常用的 C# 方法调用映射。
    /// </summary>
    public class LambdaExprConverter
    {
        // 维护 C# 表达式节点类型到内部操作符的快速映射
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

        private readonly ParameterExpression _rootParameter; // 跟踪 Lambda 的主参数（通常是实体变量）
        private readonly LambdaExpression _expression; // 原始 Lambda 对象
        private readonly ParameterExpressionDetector _parameterDetector = new ParameterExpressionDetector(); // 检测表达式是否包含 Lambda 参数

        // 处理器字典：映射方法名/成员名到自定义转换逻辑
        private static readonly Dictionary<string, Func<MethodCallExpression, LambdaExprConverter, Expr>> _methodNameHandlers = new Dictionary<string, Func<MethodCallExpression, LambdaExprConverter, Expr>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<(Type type, string name), Func<MethodCallExpression, LambdaExprConverter, Expr>> _typeMethodHandlers = new Dictionary<(Type type, string name), Func<MethodCallExpression, LambdaExprConverter, Expr>>();

        private static readonly Dictionary<string, Func<MemberExpression, LambdaExprConverter, Expr>> _memberNameHandlers = new Dictionary<string, Func<MemberExpression, LambdaExprConverter, Expr>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<(Type type, string name), Func<MemberExpression, LambdaExprConverter, Expr>> _typeMemberHandlers = new Dictionary<(Type type, string name), Func<MemberExpression, LambdaExprConverter, Expr>>();


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
            return node.Expression is null ? new FunctionExpr(node.Member.Name) : new FunctionExpr(node.Member.Name, converter.Convert(node.Expression));
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
        public LambdaExprConverter(LambdaExpression expression)
        {
            if (expression is null) throw new ArgumentNullException(nameof(expression));
            _expression = expression;
            _rootParameter = expression.Parameters.FirstOrDefault();
        }

        /// <summary>
        /// 执行整体转换并将根节点转为 Expr。
        /// </summary>
        public Expr ToExpr()
        {
            var stmt = ConvertInternal(_expression.Body);
            if (stmt is null) throw new ArgumentException($"无法转换表达式: {_expression.Body}");
            return stmt;
        }

        /// <summary>
        /// 静态便捷入口，将 Lambda 表达式转换为 Expr 模型。
        /// </summary>
        public static Expr ToExpr(LambdaExpression expression)
        {
            var converter = new LambdaExprConverter(expression);
            return converter.ToExpr();
        }

        #region 表达式转换核心逻辑

        private Expr ConvertInternal(Expression node)
        {
            // 基于节点类型的递归下降转换
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
                    return new ValueExpr(constant.Value) { IsConst = constant.Type.IsPrimitive || constant.Value == null };
                case ParameterExpression param:
                    // 裸参数不直接转换（通常作为成员访问的基础）
                    throw new NotSupportedException($"参数表达式 '{param.Name}' 不能直接转换为 Expr");
                case NewArrayExpression newArray:
                    return ConvertNewArray(newArray);
                case ListInitExpression listInit:
                    return ConvertListInit(listInit);
                case MethodCallExpression methodCall:
                    return ConvertMethodCall(methodCall);
                case NewExpression newExpression:
                    // 只要不涉及 Lambda 参数，便尝试在本地执行并取结果
                    return EvaluateToExpr(newExpression);
                default:
                    throw new NotSupportedException($"不支持的表达式类型: {node.NodeType} ({node.GetType().Name})");
            }
        }

        private Expr ConvertBinary(BinaryExpression node)
        {
            // 处理 ?? 运算符，依赖参数时转为 COALESCE 函数，否则本地计算
            if (node.NodeType == ExpressionType.Coalesce)
            {
                return _parameterDetector.ContainsParameter(node.Left) || _parameterDetector.ContainsParameter(node.Right) ? new FunctionExpr("COALESCE", ConvertInternal(node.Left), ConvertInternal(node.Right)) : EvaluateToExpr(node);
            }

            var left = ConvertInternal(node.Left);
            var right = ConvertInternal(node.Right);

            // 处理逻辑 AND/OR 及加法字符串连接重载
            switch (node.NodeType)
            {
                case ExpressionType.AndAlso:
                case ExpressionType.And:
                    return left.And(right);
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return left.Or(right);
                case ExpressionType.Add:
                    // 字符串拼接映射
                    if (node.Left.Type == typeof(string) || node.Right.Type == typeof(string))
                        return new BinaryExpr(left, BinaryOperator.Concat, right);
                    else
                        return new BinaryExpr(left, BinaryOperator.Add, right);
                default:
                    if (_operatorMappings.TryGetValue(node.NodeType, out var op))
                    {
                        // 特殊处理 CompareTo 调用 (a.CompareTo(b) op 0) -> 扁平化为直接的 BinaryExpr (a op b)
                        if (node.Left is MethodCallExpression leftCallExpression && leftCallExpression.Method.Name == "CompareTo")
                        {
                            if (!(right is ValueExpr ve && Equals(ve.Value, 0))) throw new ArgumentException($"CompareTo 方法只能与 0 进行比较: {node}");
                            if (left is BinaryExpr be)
                            {
                                left = be.Left;
                                right = be.Right;
                            }
                            else if (left is FunctionExpr fe && fe.Parameters.Count == 2)
                            {
                                left = fe.Parameters[0];
                                right = fe.Parameters[1];
                            }
                        }
                        else if (node.Right is MethodCallExpression rightCallExpression && rightCallExpression.Method.Name == "CompareTo")
                        {
                            if (!(left is ValueExpr ve && Equals(ve.Value, 0))) throw new ArgumentException($"CompareTo 方法只能与 0 进行比较: {node}");
                            if (right is BinaryExpr be)
                            {
                                left = be.Right;
                                right = be.Left;
                            }
                            else if (right is FunctionExpr fe && fe.Parameters.Count == 2)
                            {
                                left = fe.Parameters[1];
                                right = fe.Parameters[0];
                            }
                        }
                        return new BinaryExpr(left, op, right);
                    }
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

        /// <summary>
        /// 计算表达式的值。如果表达式依赖于 Lambda 参数，则无法计算。
        /// </summary>
        private Expr EvaluateToExpr(Expression node)
        {
            try
            {
                // 编译并执行不含参数的子表达式
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

            // 4. 处理嵌套属性路径，例如 x => x.Address.Street -> "Address.Street"
            if (_parameterDetector.ContainsParameter(node))
            {
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
            else
                // 5. 不依赖参数的成员访问（闭包/静态量）在本地计算结果
                return EvaluateToExpr(node);
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
            Type type = node.Method.DeclaringType;

            if (type != null && _typeMethodHandlers.TryGetValue((type, node.Method.Name), out var typeMethodHandler))
            {
                var result = typeMethodHandler(node, this);
                if (result is not null) return result;
            }

            if (_methodNameHandlers.TryGetValue(node.Method.Name, out var nameHandler))
            {
                var result = nameHandler(node, this);
                if (result is not null) return result;
            }

            if (type.IsPrimitive)
                return DefaultFunctionHandler(node, this);
            else if (_parameterDetector.ContainsParameter(node))
                return ConvertInternal(node.Object);
            else
                return EvaluateToExpr(node);
        }

        #endregion

        #region 内部处理逻辑

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
    }
}
