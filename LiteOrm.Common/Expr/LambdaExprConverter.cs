﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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
        /// 检测表达式是否包含 Lambda 参数
        /// </summary>
        protected readonly ParameterExpressionDetector _parameterDetector = new ParameterExpressionDetector();

        /// <summary>
        /// 转换表达式节点为 Expr 对象。
        /// </summary>
        public virtual Expr Convert(Expression node)
        {
            return ConvertInternal(node);
        }

        /// <summary>
        /// 执行整体转换并将根节点转为 LogicExpr。
        /// </summary>
        public LogicExpr ToExpr()
        {
            var body = ConvertInternal(_expression.Body);
            return AsLogic(body);
        }

        /// <summary>
        /// 执行整体转换并将根节点转为 ValueTypeExpr。
        /// </summary>
        public ValueTypeExpr ToValueExpr()
        {
            var body = ConvertInternal(_expression.Body);
            return AsValue(body);
        }

        /// <summary>
        /// 静态便捷入口，将 Lambda 表达式转换为 ValueTypeExpr 模型。
        /// </summary>
        public static ValueTypeExpr ToValueExpr(LambdaExpression expression)
        {
            var converter = new LambdaExprConverter(expression);
            return converter.ToValueExpr();
        }

        /// <summary>
        /// 静态便捷入口，将 Lambda 表达式转换为 LogicExpr 模型。
        /// </summary>
        public static LogicExpr ToExpr(LambdaExpression expression)
        {
            var converter = new LambdaExprConverter(expression);
            return converter.ToExpr();
        }

        #region 表达式转换核心逻辑

        /// <summary>
        /// 执行内部表达式转换，将表达式节点转换为 Expr 对象
        /// </summary>
        /// <param name="node">要转换的表达式节点</param>
        /// <returns>转换后的 Expr 对象</returns>
        protected virtual Expr ConvertInternal(Expression node)
        {
            if (node is null) return null;

            return node.NodeType switch
            {
                ExpressionType.Call => ConvertMethodCall((MethodCallExpression)node),
                ExpressionType.Constant => ConvertConstant((ConstantExpression)node),
                ExpressionType.Lambda => (((LambdaExpression)node).ReturnType == typeof(bool))
                        ? ToExpr((LambdaExpression)node)
                        : ToValueExpr((LambdaExpression)node),// 检查 Lambda 的返回类型。如果是 bool，可能是谓词；否则可能是值选择器。
                ExpressionType.MemberAccess => ConvertMember((MemberExpression)node),  // 如果是成员访问，可能是实体属性或外部变量
                ExpressionType.Quote => ConvertInternal(((UnaryExpression)node).Operand),
                _ => ConvertOriginal(node)
            };
        }

        /// <summary>
        /// 将原始表达式节点（不支持直接转换的类型）转换为 Expr 对象
        /// </summary>
        /// <param name="node">要转换的表达式节点</param>
        /// <returns>转换后的 Expr 对象</returns>
        protected Expr ConvertOriginal(Expression node)
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
                    throw new NotSupportedException($"Parameter expression '{param.Name}' cannot be converted directly to Expr");
                case NewArrayExpression newArray:
                    return ConvertNewArray(newArray);
                case ListInitExpression listInit:
                    return ConvertListInit(listInit);
                case NewExpression newExpression:
                    return ConvertNew(newExpression);
                default:
                    throw new NotSupportedException($"Unsupported expression type: {node.NodeType} ({node.GetType().Name})");
            }
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
                            if (left is LogicBinaryExpr lbe)
                            {
                                left = lbe.Left;
                                right = lbe.Right;
                            }
                            else if (left is ValueBinaryExpr vbe)
                            {
                                left = vbe.Left;
                                right = vbe.Right;
                            }
                            else if (left is FunctionExpr fe && fe.Parameters.Count == 2)
                            {
                                left = fe.Parameters[0];
                                right = fe.Parameters[1];
                            }
                        }
                        else if (node.Right is MethodCallExpression rightCallExpression && rightCallExpression.Method.Name == "CompareTo")
                        {
                            var vLeft = AsValue(left);
                            if (!(vLeft is ValueExpr ve && Equals(ve.Value, 0))) throw new ArgumentException($"CompareTo method can only be compared with 0: {node}");
                            if (right is LogicBinaryExpr lbe)
                            {
                                left = lbe.Right;
                                right = lbe.Left;
                            }
                            else if (right is ValueBinaryExpr vbe)
                            {
                                left = vbe.Right;
                                right = vbe.Left;
                            }
                            else if (right is FunctionExpr fe && fe.Parameters.Count == 2)
                            {
                                left = fe.Parameters[1];
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
            {
                throw new ArgumentException($"Unable to convert unary expression: {node}");
            }

            switch (node.NodeType)
            {
                case ExpressionType.OnesComplement:
                    return new UnaryExpr(UnaryOperator.BitwiseNot, operand as ValueTypeExpr);
                case ExpressionType.Not:
                    return new NotExpr(AsLogic(operand));
                case ExpressionType.Negate:
                    return new UnaryExpr(UnaryOperator.Nagive, operand as ValueTypeExpr);
                case ExpressionType.Convert:
                    // 类型转换通常不需要额外处理
                    return operand;
                default:
                    throw new NotSupportedException($"Unsupported unary operator: {node.NodeType}");
            }
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
            if (node.Expression is ParameterExpression paramExpr &&
                (_rootParameter is null || paramExpr == _rootParameter))
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
                return Expr.Prop(propertyName);
            }
            else
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
            if (type == typeof(Queryable) || type == typeof(Enumerable))
            {
                return ConvertQueryableMethodCall(node);
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
        /// 转换Queryable/Enumerable扩展方法调用（基类默认抛异常，子类可重写）
        /// </summary>
        protected virtual Expr ConvertQueryableMethodCall(MethodCallExpression node)
        {
            return ConvertOriginal(node);
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
    }
}
