using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        /// <summary>
        /// 初始化 Lambda 表达式转换器，注册默认的处理器。
        /// </summary>
        static LambdaExprConverter()
        {
            LiteOrmLambdaHandlerInitializer.InitialRegister();
        }

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
            { ExpressionType.Divide, ValueOperator.Divide },
            { ExpressionType.Modulo, ValueOperator.Modulo }
        };


        private static readonly ConcurrentDictionary<string, Func<MethodCallExpression, LambdaExprConverter, Expr>> _methodNameHandlers = new ConcurrentDictionary<string, Func<MethodCallExpression, LambdaExprConverter, Expr>>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<(Type type, string name), Func<MethodCallExpression, LambdaExprConverter, Expr>> _typeMethodHandlers = new ConcurrentDictionary<(Type type, string name), Func<MethodCallExpression, LambdaExprConverter, Expr>>();
        private static readonly ConcurrentDictionary<string, Func<MemberExpression, LambdaExprConverter, Expr>> _memberNameHandlers = new ConcurrentDictionary<string, Func<MemberExpression, LambdaExprConverter, Expr>>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<(Type type, string name), Func<MemberExpression, LambdaExprConverter, Expr>> _typeMemberHandlers = new ConcurrentDictionary<(Type type, string name), Func<MemberExpression, LambdaExprConverter, Expr>>();
        private static readonly ConcurrentDictionary<Type, Func<MemberExpression, LambdaExprConverter, Expr>> _typeFallbackMemberHandlers = new ConcurrentDictionary<Type, Func<MemberExpression, LambdaExprConverter, Expr>>();


        /// <summary>
        /// 默认的方法处理器：将方法名作为 SQL 函数名生成 FunctionExpr。
        /// </summary>
        public static Func<MethodCallExpression, LambdaExprConverter, Expr> DefaultFunctionHandler => static (node, converter) =>
        {
            return converter.CreateFunctionExpr(node);
        };

        /// <summary>
        /// 默认的成员处理器：将成员（属性/字段）名映射为 FunctionExpr 或 PropertyExpr。
        /// </summary>
        public static Func<MemberExpression, LambdaExprConverter, Expr> DefaultMemberHandler => static (node, converter) =>
        {
            return node.Expression is null ? new FunctionExpr(node.Member.Name) : new FunctionExpr(node.Member.Name, converter.Convert(node.Expression).AsValue());
        };

        /// <summary>
        /// 注册全局的方法转换逻辑。
        /// </summary>
        /// <param name="methodName">待拦截的方法名称。</param>
        /// <param name="handler">处理逻辑，若为 null 则使用默认处理器。</param>
        public static void RegisterMethodHandler(string methodName, Func<MethodCallExpression, LambdaExprConverter, Expr>? handler = null)
        {
            _methodNameHandlers[methodName] = handler ?? DefaultFunctionHandler;
        }

        /// <summary>
        /// 注册特定类型的方法转换逻辑。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <param name="methodName">方法名称。若不指定，则扫描并注册所有公开方法。</param>
        /// <param name="handler">处理逻辑，若为 null 则使用默认处理器。</param>
        public static void RegisterMethodHandler([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type type, string? methodName = null, Func<MethodCallExpression, LambdaExprConverter, Expr>? handler = null)
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
        /// 按「类型 + 方法名」查找已注册的方法处理器。
        /// 优先按特化类型（如 <c>QuerySource&lt;User&gt;</c>）匹配；未命中且该类型是构造泛型时，
        /// 再按泛型定义（如 <c>typeof(QuerySource&lt;&gt;)</c>）匹配，从而支持注册在开放泛型上的处理器。
        /// </summary>
        /// <param name="type">方法声明类型，可为 null。</param>
        /// <param name="methodName">方法名称。</param>
        /// <param name="handler">命中的处理器，未命中时为 null。</param>
        /// <returns>命中时为 true。</returns>
        private static bool TryGetTypeMethodHandler(
            Type? type,
            string methodName,
            out Func<MethodCallExpression, LambdaExprConverter, Expr>? handler)
        {
            handler = null;
            if (type is null) return false;

            // 1. 特化类型优先：注册在具体类型上的处理器可以覆盖泛型定义的默认行为
            if (_typeMethodHandlers.TryGetValue((type, methodName), out handler) && handler is not null) return true;

            // 2. 回退到泛型定义。开放泛型自身的 GetGenericTypeDefinition 返回自身，跳过避免重复查找
            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                if (definition != type
                    && _typeMethodHandlers.TryGetValue((definition, methodName), out handler)
                    && handler is not null)
                    return true;
            }

            handler = null;
            return false;
        }

        /// <summary>
        /// 注册成员（属性/字段）的转换逻辑。
        /// </summary>
        public static void RegisterMemberHandler(string memberName, Func<MemberExpression, LambdaExprConverter, Expr>? handler = null)
        {
            if (String.IsNullOrEmpty(memberName)) throw new ArgumentNullException(nameof(memberName));
            _memberNameHandlers[memberName] = handler ?? DefaultMemberHandler;
        }

        /// <summary>
        /// 注册特定类型的成员（属性/字段）转换逻辑。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <param name="memberName">成员名称。若不指定，则注册为该类型的成员兜底处理器，在解析时对该类型的所有成员生效。</param>
        /// <param name="handler">处理逻辑，若为 null 则使用默认处理器。</param>
        /// <remarks>
        /// 不指定 <paramref name="memberName"/> 时只登记一条类型级兜底，不会在注册时枚举该类型的属性与字段；
        /// 解析成员访问时，显式指定的成员注册（按类型或按成员名）优先于类型级兜底。
        /// </remarks>
        public static void RegisterMemberHandler([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields)] Type type, string? memberName = null, Func<MemberExpression, LambdaExprConverter, Expr>? handler = null)
        {
            if (memberName != null && memberName.Length == 0) throw new ArgumentNullException(nameof(memberName));
            handler ??= DefaultMemberHandler;
            if (memberName == null)
                _typeFallbackMemberHandlers[type] = handler;
            else
                _typeMemberHandlers[(type, memberName)] = handler;
        }

        /// <summary>
        /// 查询按方法名注册的方法处理器；未找到返回 <c>null</c>。
        /// </summary>
        /// <param name="methodName">方法名称。</param>
        /// <returns>已注册的方法处理器，若未注册则返回 <c>null</c>。</returns>
        public static Func<MethodCallExpression, LambdaExprConverter, Expr>? FindMethodHandler(string methodName)
        {
            if (String.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            return _methodNameHandlers.TryGetValue(methodName, out var handler) ? handler : null;
        }

        /// <summary>
        /// 查询按 类型+方法名 注册的方法处理器；未找到返回 <c>null</c>。
        /// 与 <see cref="ConvertMethodCall"/> 的类型级解析顺序一致，便于在自定义处理器中回退到原处理器。
        /// </summary>
        /// <param name="type">目标类型。</param>
        /// <param name="methodName">方法名称。</param>
        /// <returns>已注册的方法处理器，若未注册则返回 <c>null</c>。</returns>
        public static Func<MethodCallExpression, LambdaExprConverter, Expr>? FindMethodHandler(Type type, string methodName)
        {
            if (String.IsNullOrEmpty(methodName)) throw new ArgumentNullException(nameof(methodName));
            if (type is null || !_typeMethodHandlers.TryGetValue((type, methodName), out var handler)) return null;
            return handler;
        }

        /// <summary>
        /// 查询指定方法调用当前会命中的方法处理器（类型处理器优先，其次方法名处理器）；
        /// 与 <see cref="ConvertMethodCall"/> 的解析顺序一致，未找到返回 <c>null</c>。
        /// </summary>
        /// <param name="node">方法调用表达式。</param>
        /// <returns>命中的方法处理器，若未命中则返回 <c>null</c>。</returns>
        public static Func<MethodCallExpression, LambdaExprConverter, Expr>? FindMethodHandler(MethodCallExpression node)
        {
            if (node is null) throw new ArgumentNullException(nameof(node));
            var type = node.Method.DeclaringType;
            if (type != null && _typeMethodHandlers.TryGetValue((type, node.Method.Name), out var typeHandler) && typeHandler != null)
                return typeHandler;
            if (node.Method.Name.Length > 0 && _methodNameHandlers.TryGetValue(node.Method.Name, out var nameHandler) && nameHandler != null)
                return nameHandler;
            return null;
        }

        #endregion 静态成员

        /// <summary>
        /// 初始化 LambdaExprConverter 类的新实例。
        /// </summary>
        /// <param name="expression">要转换的 Lambda 表达式。</param>
        public LambdaExprConverter(LambdaExpression expression) : this(expression, null)
        {
        }

        /// <summary>
        /// 初始化 LambdaExprConverter 类的新实例，支持嵌套 Lambda 表达式的外部参数上下文。
        /// </summary>
        /// <param name="expression">要转换的 Lambda 表达式。</param>
        /// <param name="parent">父转换器。为 null 时按独立作用域转换；非 null 时继承父转换器的参数映射（外层参数可见），
        /// 自身的参数按新作用域注册，并与父转换器共享别名编号。</param>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "LambdaExpression.Type returns a runtime-known Type; properties are preserved via [Table] entities through the source generator; under AOT, ensure entity types are registered.")]
        [UnconditionalSuppressMessage("Trimming", "IL2062", Justification = "LambdaExpression.Type is a runtime value; under AOT, users must annotate entity types with [Table] to ensure properties are preserved.")]
#endif
        public LambdaExprConverter(LambdaExpression expression, LambdaExprConverter? parent = null)
        {
            if (expression is null) throw new ArgumentNullException(nameof(expression));
            _expression = expression;
            _body = expression.Body;
            var parameters = expression.Parameters;
            if (parent != null)
            {
                // 继承外层参数映射，使内层 Lambda 可以引用外层参数
                foreach (var item in parent._parameterAliases)
                    _parameterAliases[item.Key] = item.Value;
                foreach (var item in parent._parameterExprs)
                    _parameterExprs[item.Key] = item.Value;
                // 父子共享别名编号，避免嵌套作用域分配到重复别名
                _aliasCounter = parent._aliasCounter;
                // 无自有参数时沿用外层当前别名
                _currentAlias = parent._currentAlias;
            }
            else
            {
                if (parameters.Count == 0)
                    throw new ArgumentException("Lambda expression must have at least one parameter.", nameof(expression));
                _aliasCounter = new AliasSequence(1);
            }
            for (int i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                var tableExpr = Expr.From(UnwrapSourceType(parameter.Type))
                    .As(i == 0 && parent == null ? Constants.DefaultTableAlias : _aliasCounter.NextAlias());
                _parameterAliases[parameter] = tableExpr.Alias!;
                _parameterExprs[parameter] = tableExpr;
                if (i == 0)
                {
                    _tableExpr = tableExpr;
                    _currentAlias = tableExpr.Alias!;
                }
            }
        }

        /// <summary>
        /// 直接从表达式主体与参数构造转换器，不要求先合成 <see cref="LambdaExpression"/>。
        /// 用于把 IQueryable 查询树转成 Expr：AOT 下 <see cref="Expression.Lambda(Expression, ParameterExpression[])"/>
        /// 需要动态生成委托类型，此入口可以绕开。
        /// </summary>
        /// <param name="body">表达式主体。</param>
        /// <param name="parameter">查询根参数，作为第一个参数分配默认别名。</param>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "The parameter type comes from the query expression root; properties are preserved via [Table] entities through the source generator.")]
        [UnconditionalSuppressMessage("Trimming", "IL2062", Justification = "The parameter type is a runtime value; under AOT, users must annotate entity types with [Table] to ensure properties are preserved.")]
#endif
        internal LambdaExprConverter(Expression body, ParameterExpression parameter)
        {
            if (body is null) throw new ArgumentNullException(nameof(body));
            if (parameter is null) throw new ArgumentNullException(nameof(parameter));

            _body = body;
            _expression = null!;
            _aliasCounter = new AliasSequence(1);

            var tableExpr = Expr.From(UnwrapSourceType(parameter.Type)).As(Constants.DefaultTableAlias);
            _parameterAliases[parameter] = tableExpr.Alias!;
            _parameterExprs[parameter] = tableExpr;
            _tableExpr = tableExpr;
            _currentAlias = tableExpr.Alias!;
        }

        private TableExpr? _tableExpr;
        /// <summary>
        /// 获取解析后的 TableExpr 对象，代表 Lambda 表达式中根参数对应的表信息。
        /// 传入父转换器且 Lambda 无参数时为 null。
        /// </summary>
        public TableExpr? Table => _tableExpr;
        /// <summary>
        /// 当前别名，用于生成 SQL 时的表别名。对于嵌套 Lambda 表达式会动态更新以支持多层别名映射。
        /// </summary>
        protected string _currentAlias = Constants.DefaultTableAlias;
        /// <summary>
        /// 原始 Lambda 对象
        /// </summary>
        protected readonly LambdaExpression _expression;
        /// <summary>
        /// 要转换的表达式主体。由 Lambda 构造时取 <see cref="LambdaExpression.Body"/>；
        /// 由 <see cref="LambdaExprConverter(Expression, ParameterExpression)"/> 构造时直接使用传入的主体。
        /// </summary>
        protected readonly Expression _body;
        /// <summary>
        /// 参数表达式到别名的映射。按 <see cref="ParameterExpression"/> 引用键控，
        /// 内层 Lambda 参数与外层同名时不会互相覆盖或删除。
        /// </summary>
        protected readonly Dictionary<ParameterExpression, string> _parameterAliases = new Dictionary<ParameterExpression, string>();
        /// <summary>
        /// 参数表达式到 FromExpr 或 ForeignExpr 对象的映射，用于处理分表参数。
        /// 按 <see cref="ParameterExpression"/> 引用键控。
        /// </summary>
        protected readonly Dictionary<ParameterExpression, Expr> _parameterExprs = new Dictionary<ParameterExpression, Expr>();
        /// <summary>
        /// 元组 Lambda 参数到其元素树的映射，用于解析 <c>x.Item1</c> / <c>x.Item1.Item2</c> 这类元素访问。
        /// 按 <see cref="ParameterExpression"/> 引用键控，树结构与元组的 Item1..ItemN 嵌套一致。
        /// </summary>
        protected readonly Dictionary<ParameterExpression, TupleNode> _tupleTrees = new Dictionary<ParameterExpression, TupleNode>();
        /// <summary>
        /// 别名编号序列，父子转换器共享同一实例以避免嵌套作用域分配到重复别名
        /// </summary>
        protected readonly AliasSequence _aliasCounter;
        /// <summary>
        /// 检测表达式是否包含 Lambda 参数
        /// </summary>
        protected readonly ExpressionDetector _expressionDetector = new ExpressionDetector();

        /// <summary>
        /// 解包 IQueryable&lt;T&gt; / IEnumerable&lt;T&gt; / QuerySource&lt;T&gt; 形式的源参数，取其元素类型。
        /// </summary>
        private static Type UnwrapSourceType(Type type)
        {
            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                if (definition == typeof(IQueryable<>) || definition == typeof(IEnumerable<>))
                    return type.GetGenericArguments()[0];
            }
            // QuerySource<T1> / QuerySource<T1,T2> ... 的元素类型由基类 QuerySource<TElement> 的类型实参给出
            for (Type? current = type; current is not null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(QuerySource<>))
                    return current.GetGenericArguments()[0];
            }
            return type;
        }

        /// <summary>
        /// 转换表达式节点为 Expr 对象。
        /// </summary>
        public virtual Expr Convert(Expression node) => ConvertInternal(node);

        /// <summary>
        /// 执行整体转换并将根节点转为 LogicExpr。
        /// </summary>
        public LogicExpr? ToLogicExpr() => ConvertInternal(_body).AsLogic();

        /// <summary>
        /// 执行整体转换并将根节点转为 ValueTypeExpr。
        /// </summary>
        public ValueTypeExpr ToValueExpr() => ConvertInternal(_body).AsValue();

        /// <summary>
        /// 静态便捷入口，将 Lambda 表达式转换为 ValueTypeExpr 模型。
        /// </summary>
        public static ValueTypeExpr ToValueExpr(LambdaExpression expression) => new LambdaExprConverter(expression).ToValueExpr();

        /// <summary>
        /// 静态便捷入口，将 Lambda 表达式转换为 LogicExpr 模型。
        /// </summary>
        public static LogicExpr? ToLogicExpr(LambdaExpression expression) => new LambdaExprConverter(expression).ToLogicExpr();

        /// <summary>
        /// 执行表达式转换并将结果转换为 SqlSegment
        /// </summary>
        public Expr ToSqlSegment()
        {
            Expr expr = ConvertInternal(_body);
            if (expr is not SqlSegment) throw new InvalidOperationException("Lambda expression cannot convert to SqlSegment.");
            return expr;
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
            if (node is null) return null!;
            if (node is not ConstantExpression && _expressionDetector.CanEvaluate(node))
                return EvaluateToExpr(node);
            return node switch
            {
                BinaryExpression binary => ConvertBinary(binary),
                UnaryExpression unary => ConvertUnary(unary),
                ConditionalExpression conditional => ConvertConditional(conditional),
                MemberExpression member => ConvertMember(member),
                NewArrayExpression newArray => ConvertNewArray(newArray),
                ConstantExpression constant => ConvertConstant(constant),
                ListInitExpression listInit => ConvertListInit(listInit),
                NewExpression newExpression => ConvertNew(newExpression),
                MethodCallExpression methodCall => ConvertMethodCall(methodCall),
                LambdaExpression lambda => ConvertLambda(lambda),
                ParameterExpression parameter => ConvertParameter(parameter),
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
            string lastAlias = _currentAlias;
            // 登记全部参数（按引用键控，内层同名参数不会波及外层映射）
            foreach (var parameter in lambda.Parameters)
                _parameterAliases[parameter] = _currentAlias;
            var result = ConvertInternal(lambda.Body);
            result = lambda.ReturnType == typeof(bool) ? result.AsLogic() ?? null! : result.AsValue();
            foreach (var parameter in lambda.Parameters)
                _parameterAliases.Remove(parameter);
            _currentAlias = lastAlias;
            return result;
        }

        /// <summary>
        /// 将参数表达式转换为 Expr 对象。通常情况下，参数表达式会映射为 FromExpr 或 ForeignExpr 对象，代表查询中的表或关联表。
        /// </summary>
        /// <param name="parameter">参数表达式</param>
        /// <returns>转换后的 Expr 对象</returns>
        protected virtual Expr ConvertParameter(ParameterExpression parameter)
        {
            _parameterExprs.TryGetValue(parameter, out var expr);
            return expr ?? null!;
        }

        private Expr ConvertNew(NewExpression node)
        {
            var items = new List<ValueTypeExpr>();
            foreach (var arg in node.Arguments)
            {
                var item = ConvertInternal(arg).AsValue();
                if (item is not null) items.Add(item);
            }
            return new ValueSet(ValueJoinType.List, items.ToArray());
        }


        /// <summary>
        /// 将二元表达式（如 a + b、a == b）转换为对应的 Expr 对象
        /// </summary>
        /// <param name="node">要转换的二元表达式节点</param>
        /// <returns>转换后的 Expr 对象（可能是 LogicBinaryExpr 或 ValueBinaryExpr）</returns>
        protected Expr ConvertBinary(BinaryExpression node)
        {
            // 特殊处理 TableArgs 赋值：u => ((IArged)u).TableArgs == ["202401"]
            // Join 场景可指定到具体表：x => ((IArged)x.Item1).TableArgs == ["202401"]
            if (node.NodeType == ExpressionType.Equal)
            {
                // 检查左边或右边是否是 TableArgs 属性访问
                MemberExpression? tableArgsMember = null;
                Expression? valueExpr = null;

                if (node.Left is MemberExpression leftMember && typeof(IArged).IsAssignableFrom(leftMember.Member.DeclaringType) && leftMember.Member.Name == nameof(IArged.TableArgs))
                {
                    tableArgsMember = leftMember;
                    valueExpr = node.Right;
                }
                else if (node.Right is MemberExpression rightMember && typeof(IArged).IsAssignableFrom(rightMember.Member.DeclaringType) && rightMember.Member.Name == nameof(IArged.TableArgs))
                {
                    tableArgsMember = rightMember;
                    valueExpr = node.Left;
                }

                if (tableArgsMember != null && valueExpr != null)
                {
                    // 提取赋值目标（可能有类型转换）
                    var targetExpr = tableArgsMember.Expression;
                    while (targetExpr is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.TypeAs))
                    {
                        targetExpr = unary.Operand;
                    }

                    // 赋值目标可以是普通参数（单表），也可以是 Join 元组元素（x.Item1 / x.Item1.Item2）
                    TableExpr? tableExpr = null;
                    ForeignExpr? foreignExpr = null;
                    bool isTableArgsTarget = false;

                    if (targetExpr is ParameterExpression paramExpr && _parameterAliases.ContainsKey(paramExpr))
                    {
                        isTableArgsTarget = true;
                        if (_parameterExprs.TryGetValue(paramExpr, out var paramArg))
                        {
                            if (paramArg is TableExpr te) tableExpr = te;
                            else if (paramArg is ForeignExpr fe) foreignExpr = fe;
                        }
                    }
                    else if (targetExpr is MemberExpression tupleMember
                        && ResolveTupleNode(tupleMember)?.Table is TableExpr tupleTable)
                    {
                        isTableArgsTarget = true;
                        tableExpr = tupleTable;
                    }

                    if (isTableArgsTarget)
                    {
                        // 解析赋值的值（数组或集合）
                        var tableArgs = EvaluateTableArgs(valueExpr);
                        if (tableExpr is not null) tableExpr.TableArgs = tableArgs;
                        else if (foreignExpr is not null) foreignExpr.TableArgs = tableArgs;

                        // 返回 null 表示这个赋值条件不产生额外的 SQL 条件
                        return null!;
                    }
                }
            }

            // 处理 ?? 运算符，依赖参数时转为 COALESCE 函数，否则本地计算
            if (node.NodeType == ExpressionType.Coalesce)
            {
                return _expressionDetector.CanEvaluate(node) ? EvaluateToExpr(node) : ConvertInternal(node.Left).AsValue().IfNull(ConvertInternal(node.Right).AsValue());
            }

            var left = ConvertInternal(node.Left);
            var right = ConvertInternal(node.Right);

            // 处理逻辑 AND/OR 及加法字符串连接重载
            switch (node.NodeType)
            {
                case ExpressionType.AndAlso:
                case ExpressionType.And:
                    {
                        var andLeft = left.AsLogic();
                        var andRight = right.AsLogic();
                        if (andLeft is null) return andRight!;
                        if (andRight is null) return andLeft!;
                        return andLeft.And(andRight);
                    }
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    {
                        var orLeft = left.AsLogic();
                        var orRight = right.AsLogic();
                        if (orLeft is null) return orRight!;
                        if (orRight is null) return orLeft!;
                        return orLeft.Or(orRight);
                    }
                case ExpressionType.Add:
                    // 字符串拼接映射
                    if (node.Left.Type == typeof(string) || node.Right.Type == typeof(string))
                        return new ValueBinaryExpr(left.AsValue(), ValueOperator.Concat, right.AsValue());
                    else
                        return new ValueBinaryExpr(left.AsValue(), ValueOperator.Add, right.AsValue());
                default:
                    if (_operatorMappings.TryGetValue(node.NodeType, out var op))
                    {
                        // 特殊处理 CompareTo 调用 (a.CompareTo(b) op 0) -> 扁平化为直接的 BinaryExpr (a op b)
                        if (node.Left is MethodCallExpression leftCallExpression && leftCallExpression.Method.Name == "CompareTo")
                        {
                            var vRight = right.AsValue();
                            if (!(vRight is ValueExpr ve && Equals(ve.Value, 0))) throw new ArgumentException($"CompareTo method can only be compared with 0: {node}");
                            if (left is FunctionExpr fe && fe.Args.Count == 2)
                            {
                                left = fe.Args[0];
                                right = fe.Args[1];
                            }
                        }
                        else if (node.Right is MethodCallExpression rightCallExpression && rightCallExpression.Method.Name == "CompareTo")
                        {
                            var vLeft = left.AsValue();
                            if (!(vLeft is ValueExpr ve && Equals(ve.Value, 0))) throw new ArgumentException($"CompareTo method can only be compared with 0: {node}");
                            if (right is FunctionExpr fe && fe.Args.Count == 2)
                            {
                                left = fe.Args[1];// 交换参数位置
                                right = fe.Args[0];
                            }
                        }

                        if (op is ValueOperator vop)
                        {
                            return new ValueBinaryExpr(left.AsValue(), vop, right.AsValue());
                        }
                        else
                            return new LogicBinaryExpr(left.AsValue(), (LogicOperator)op, right.AsValue());
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
                ExpressionType.OnesComplement =>
                    operand is ValueTypeExpr vte
                        ? new UnaryExpr(UnaryOperator.BitwiseNot, vte)
                        : throw new NotSupportedException($"Bitwise NOT operator requires a value expression, but got {operand?.GetType().Name}"),
                ExpressionType.Not =>
                    // ExpressionType.Not 可能表示逻辑非 (!) 或按位非 (~)
                    // 逻辑非应用于 bool，按位非应用于整数
                    node.Operand.Type == typeof(bool)
                        ? new NotExpr(operand.AsLogic())
                        : operand is ValueTypeExpr vte2
                            ? new UnaryExpr(UnaryOperator.BitwiseNot, vte2)
                            : throw new NotSupportedException($"Bitwise NOT operator requires a value expression, but got {operand?.GetType().Name}"),
                ExpressionType.Negate =>
                    operand is ValueTypeExpr vte3
                        ? new UnaryExpr(UnaryOperator.Nagive, vte3)
                        : throw new NotSupportedException($"Negate operator requires a value expression, but got {operand?.GetType().Name}"),
                ExpressionType.Convert or ExpressionType.Quote => operand,
                _ => throw new NotSupportedException($"Unsupported unary operator: {node.NodeType}"),
            };
        }

        /// <summary>
        /// 将条件表达式（如 a ? b : c）转换为对应的 Expr 对象，映射为 SQL 的 CASE WHEN 表达式。
        /// </summary>
        /// <param name="node">要转换的条件表达式节点</param>
        /// <returns>转换后的 Expr 对象</returns>
        private Expr ConvertConditional(ConditionalExpression node)
        {
            var test = ConvertInternal(node.Test).AsLogic();
            var ifTrue = ConvertInternal(node.IfTrue).AsValue();
            var ifFalse = ConvertInternal(node.IfFalse).AsValue();
            return Expr.If(test!, ifTrue, ifFalse);
        }

        /// <summary>
        /// 计算表达式的值。如果表达式依赖于 Lambda 参数，则无法计算。
        /// </summary>
        private Expr EvaluateToExpr(Expression node)
        {
            var value = Evaluate(node);
            if (typeof(Expr).IsAssignableFrom(node.Type) || value is Expr) return (Expr)value!;
            return new ValueExpr(value);
        }

        /// <summary>
        /// 对不依赖 Lambda 参数的子表达式求值，返回其运行时值。
        /// - 直接读取 ConstantExpression
        /// - 处理 UnaryExpression(转换) 包含的常量
        /// - 最后尝试编译并执行表达式以求值（用于闭包变量等）
        /// 若无法求值则抛出异常。
        /// </summary>
        internal protected object? Evaluate(Expression expr)
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

            // 处理隐式/显式转换运算符，直接返回参数表达式的转换结果（如 string s = (string)obj;）
            if (expr is MethodCallExpression call && call.Method.IsSpecialName && call.Method.IsStatic && (call.Method.Name == "op_Implicit" || call.Method.Name == "op_Explicit"))
                return Evaluate(call.Arguments[0]);

            // 尝试编译并执行表达式（支持闭包、字段、属性等）
            try
            {
                if (!RuntimeFeature.IsDynamicCodeSupported)
                {
                    return ExpressionEvaluator.Evaluate(expr);
                }
                else
                {
                    var lambda = Expression.Lambda(expr);
                    var compiled = lambda.Compile();
                    return compiled.DynamicInvoke();
                }
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
            if (Nullable.GetUnderlyingType(node.Member.DeclaringType!) is not null && node.Member.Name == "Value")
            {
                return ConvertInternal(node.Expression!);
            }

            // 1. 处理元组元素成员访问 (如 x.Item2.Name)，需在普通参数访问之前判定，
            //    否则 x.Item1 会被误当作名为 Item1 的数据库列
            if (TryResolveTupleElement(node, out TableExpr? tupleElement))
            {
                string? elementAlias = tupleElement!.Alias;
                if (elementAlias == _currentAlias) elementAlias = null;
                if (node.Member is PropertyInfo tupleProperty)
                {
                    return new PropertyExpr(tupleProperty.Name) { TableAlias = elementAlias };
                }
                if (node.Member is FieldInfo tupleField)
                {
                    return new PropertyExpr(tupleField.Name) { TableAlias = elementAlias };
                }
            }

            // 1.1 指向嵌套元组分支（如 ((A, B), C) 上的 x.Item1）无法映射到列，明确报错而不是渲染成 [Item1]
            if (IsNestedTupleAccess(node))
                throw new NotSupportedException(
                    $"'{node}' refers to a nested tuple element and cannot be used as a column. " +
                    $"Access a concrete table element instead, for example x.Item1.Item2.Column.");

            // 2. 处理直接的实体参数访问 (映射为数据库列)
            if (node.Expression is ParameterExpression paramExpr)
            {
                _parameterAliases.TryGetValue(paramExpr, out var paramAlias);
                if (paramAlias == _currentAlias) paramAlias = null;
                if (node.Member is PropertyInfo propertyInfo)
                {
                    return new PropertyExpr(propertyInfo.Name) { TableAlias = paramAlias };
                }
                else if (node.Member is FieldInfo fieldInfo)
                {
                    return new PropertyExpr(fieldInfo.Name) { TableAlias = paramAlias };
                }
            }

            // 2. 优先匹配已注册的类型成员处理器 (如 DateTime.Now)
            if (node.Member.DeclaringType != null && _typeMemberHandlers.TryGetValue((node.Member.DeclaringType, node.Member.Name), out var typeMemberHandler))
                if (typeMemberHandler != null) return typeMemberHandler(node, this);

            // 3. 匹配已注册的通用名称处理器 (如 Length)
            if (_memberNameHandlers.TryGetValue(node.Member.Name, out var nameMemberHandler))
                if (nameMemberHandler != null) return nameMemberHandler(node, this);

            // 4. 匹配类型级兜底处理器，对未单独注册的成员生效
            if (node.Member.DeclaringType != null && _typeFallbackMemberHandlers.TryGetValue(node.Member.DeclaringType, out var fallbackMemberHandler))
                if (fallbackMemberHandler != null) return fallbackMemberHandler(node, this);

            // 5. 兜底使用默认成员处理器，将成员访问转换为 FunctionExpr
            return DefaultMemberHandler(node, this);
        }

        /// <summary>
        /// 判断成员访问的宿主是否为某个元组参数的元素（如 <c>x.Item2.Name</c> 中的 <c>x.Item2</c>），
        /// 是则通过 <paramref name="element"/> 返回该元素对应的表。支持 <c>x.Item1.Item2.Column</c> 这类多级元素路径。
        /// </summary>
        /// <param name="node">成员访问表达式</param>
        /// <param name="element">解析出的元素表</param>
        /// <returns>命中元组元素且落在具体表上时为 true</returns>
        private bool TryResolveTupleElement(MemberExpression node, out TableExpr? element)
        {
            element = null;
            if (_tupleTrees.Count == 0) return false;
            if (node.Expression is not MemberExpression host) return false;
            if (!TryGetTupleIndex(host.Member.Name, out int index)) return false;

            TupleNode? parent = ResolveTupleNode(host.Expression);
            TupleNode? target = parent?.GetChild(index);
            // 落到嵌套元组而不是具体表时不算命中，交由调用方决定如何处理
            if (target?.Table is null) return false;

            element = target.Table;
            return true;
        }

        /// <summary>
        /// 判断成员访问是否指向嵌套元组的中间层级（如 <c>((A, B), C)</c> 上的 <c>x.Item1</c>），
        /// 这类访问不对应任何表，不能当作列使用。
        /// </summary>
        /// <param name="node">成员访问表达式</param>
        /// <returns>指向嵌套元组分支时为 true</returns>
        private bool IsNestedTupleAccess(MemberExpression node)
        {
            if (_tupleTrees.Count == 0) return false;
            if (!TryGetTupleIndex(node.Member.Name, out _)) return false;
            // 宿主可以是参数本身（x.Item1）或上一级元素（x.Item1.Item2）
            if (node.Expression is not (ParameterExpression or MemberExpression)) return false;

            TupleNode? target = ResolveTupleNode(node);
            return target is not null && target.Table is null;
        }

        /// <summary>
        /// 沿 <c>Item1</c> / <c>Item2</c> 链逐级下沉，取得表达式对应的元组节点。
        /// </summary>
        /// <param name="expression">参数本身或形如 <c>x.Item1</c> 的成员访问</param>
        /// <returns>对应的元组节点，无法解析时返回 null</returns>
        private TupleNode? ResolveTupleNode(Expression? expression)
        {
            if (expression is ParameterExpression parameter)
                return _tupleTrees.TryGetValue(parameter, out TupleNode? root) ? root : null;

            if (expression is MemberExpression member && TryGetTupleIndex(member.Member.Name, out int index))
            {
                TupleNode? parent = ResolveTupleNode(member.Expression);
                return parent?.GetChild(index);
            }
            return null;
        }

        /// <summary>
        /// 从 <c>Item1</c> / <c>Item2</c> 这类成员名中解析出从 0 开始的元素序号。
        /// </summary>
        private static bool TryGetTupleIndex(string memberName, out int index)
        {
            index = -1;
            if (memberName.Length <= 4 || !memberName.StartsWith("Item", StringComparison.Ordinal)) return false;
            if (!int.TryParse(memberName.Substring(4), out int itemNumber) || itemNumber < 1) return false;
            index = itemNumber - 1;
            return true;
        }

        /// <summary>
        /// 为元组类型的 Lambda 参数登记元素表，使其后的 <c>Where</c> / <c>Select</c> 能解析 <c>x.Item1</c> 这类访问。
        /// 元素表按 <see cref="FromExpr"/> 的主表在前、各连接表在后的顺序排列。
        /// </summary>
        /// <param name="lambda">要转换的 Lambda 表达式</param>
        /// <param name="source">Join 产生的查询源</param>
        /// <returns>登记成功时返回元组参数，否则返回 null。</returns>
        private ParameterExpression? RegisterTupleElements(LambdaExpression lambda, Expr? source)
        {
            if (lambda.Parameters.Count != 1) return null;
            ParameterExpression parameter = lambda.Parameters[0];
            if (!IsTupleType(parameter.Type)) return null;

            // Join 之后若先接 Where/Having/OrderBy/Section，源已变成对应包装表达式，
            // 需要沿 Source 逐层回溯到 FromExpr 才能还原元组元素与表的对应关系。
            Expr? current = source;
            while (true)
            {
                switch (current)
                {
                    case WhereExpr whereExpr:
                        current = whereExpr.Source;
                        break;
                    case HavingExpr havingExpr:
                        current = havingExpr.Source;
                        break;
                    case OrderByExpr orderByExpr:
                        current = orderByExpr.Source;
                        break;
                    case SectionExpr sectionExpr:
                        current = sectionExpr.Source;
                        break;
                    default:
                        goto unwrapped;
                }
            }
        unwrapped:
            if (current is not FromExpr from) return null;

            var tree = BuildTupleTree(parameter.Type, from);
            if (tree is null) return null;

            _tupleTrees[parameter] = tree;
            return parameter;
        }

        /// <summary>
        /// 按主表在前、各连接表在后的顺序，并结合元组类型的嵌套结构构建元组元素树。
        /// 二元链式 Join 会得到 <c>((A, B), C)</c> 这样的嵌套树；一次连接多表得到的平坦元组
        /// <c>(A, B, C)</c> 则按三个并列元素建树。
        /// </summary>
        /// <param name="tupleType">Lambda 参数声明的元组类型</param>
        /// <param name="from">连接产生的 <see cref="FromExpr"/></param>
        /// <returns>元组元素树，类型与表数量不匹配时返回 null</returns>
        private static TupleNode? BuildTupleTree(Type tupleType, FromExpr from)
        {
            var tables = new List<TableExpr>();
            if (from.Source is TableExpr mainTable) tables.Add(mainTable);
            foreach (TableJoinExpr join in from.Joins)
            {
                if (join.Source is TableExpr joinTable) tables.Add(joinTable);
            }
            if (tables.Count == 0) return null;

            int index = 0;
            TupleNode? root = BuildTupleNode(tupleType, tables, ref index);
            return index == tables.Count ? root : null;
        }

        /// <summary>
        /// 递归构建元组元素树：元组类型按泛型参数逐层展开，实体类型消费一张表。
        /// </summary>
        private static TupleNode? BuildTupleNode(Type type, IReadOnlyList<TableExpr> tables, ref int index)
        {
            if (IsTupleType(type))
            {
                Type[] elementTypes = type.GetGenericArguments();
                var children = new TupleNode[elementTypes.Length];
                for (int i = 0; i < elementTypes.Length; i++)
                {
                    TupleNode? child = BuildTupleNode(elementTypes[i], tables, ref index);
                    if (child is null) return null;
                    children[i] = child;
                }
                return TupleNode.FromBranch(children);
            }

            if (index >= tables.Count) return null;
            return TupleNode.FromTable(tables[index++]);
        }

        /// <summary>
        /// 元组元素树节点：叶子对应实体表，分支对应元组及其 <c>Item1..ItemN</c> 子元素。
        /// </summary>
        protected sealed class TupleNode
        {
            private readonly TupleNode[] _children;

            private TupleNode(TableExpr? table, TupleNode[]? children)
            {
                Table = table;
                _children = children ?? Array.Empty<TupleNode>();
            }

            /// <summary>叶子节点对应的表；分支节点为 null。</summary>
            public TableExpr? Table { get; }

            /// <summary>按 <c>Item1..ItemN</c> 顺序排列的子节点。</summary>
            public IReadOnlyList<TupleNode> Children => _children;

            /// <summary>取得指定序号的子节点，序号越界时返回 null。</summary>
            public TupleNode? GetChild(int index)
                => index >= 0 && index < _children.Length ? _children[index] : null;

            /// <summary>创建表叶子节点。</summary>
            public static TupleNode FromTable(TableExpr table) => new TupleNode(table, null);

            /// <summary>创建元组分支节点。</summary>
            public static TupleNode FromBranch(params TupleNode[] children) => new TupleNode(null, children);
        }

        /// <summary>
        /// 判断类型是否为二元到七元的元组类型（<see cref="ValueTuple"/> 或 <see cref="Tuple"/>）。
        /// </summary>
        private static bool IsTupleType(Type type)
        {
            if (!type.IsGenericType) return false;
            Type definition = type.GetGenericTypeDefinition();
            if (definition.Namespace != "System") return false;
            string name = definition.Name;
            if (!name.StartsWith("ValueTuple`", StringComparison.Ordinal)
                && !name.StartsWith("Tuple`", StringComparison.Ordinal))
            {
                return false;
            }

            int arity = definition.GetGenericArguments().Length;
            return arity >= 2 && arity <= 7;
        }

        /// <summary>
        /// 将数组初始化表达式（如 new[] { 1, 2, 3 }）转换为对应的 ValueSet 对象
        /// </summary>
        /// <param name="node">要转换的数组初始化表达式节点</param>
        /// <returns>转换后的 Expr 对象</returns>
        protected Expr ConvertNewArray(NewArrayExpression node)
        {
            var items = new List<ValueTypeExpr>();
            foreach (var expression in node.Expressions)
            {
                var item = ConvertInternal(expression).AsValue();
                if (item is not null)
                {
                    items.Add(item);
                }
            }

            return new ValueSet(items);
        }

        /// <summary>
        /// 将集合初始化表达式（如 new List&lt;int&gt; { 1, 2, 3 }）转换为对应的 ValueSet 对象
        /// </summary>
        /// <param name="node">要转换的集合初始化表达式节点</param>
        /// <returns>转换后的 Expr 对象</returns>

        protected Expr ConvertListInit(ListInitExpression node)
        {
            var items = new List<ValueTypeExpr>();
            foreach (var init in node.Initializers)
            {
                foreach (var arg in init.Arguments)
                {
                    var item = ConvertInternal(arg).AsValue();
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
            var method = node.Method;
            var type = method.DeclaringType;

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
                // 未匹配的 LINQ 方法中，聚合函数单独处理，避免落到默认函数处理器生成非法 SQL
                if (IsAggregateMethod(node)) return HandleAggregate(node);

                // Queryable 上未实现的算子必须显式报错，不能落到 DefaultFunctionHandler 生成伪函数 SQL。
                // 只对 Queryable 生效：Enumerable 上的 Contains 等已有按名注册的处理器，提前抛错会破坏现有功能。
                if (type == typeof(Queryable))
                    throw new NotSupportedException(
                        $"LINQ operator '{node.Method.Name}' is not supported in LiteOrm expressions. " +
                        "Supported: Where/Select/OrderBy/ThenBy/Skip/Take/GroupBy and Count/Sum/Average/Max/Min.");
            }
            else if (type == typeof(Expr) && (method.Name == nameof(Expr.Exists) || method.Name == nameof(Expr.ExistsRelated)))
                return HandleExists(node);
            else if (type == typeof(Expr) && (method.Name == nameof(Expr.From) || method.Name == nameof(Expr.FromRelated)))
                return HandleFrom(node);
            else if (type != null && typeof(IQuerySource).IsAssignableFrom(type)
                && (method.Name == nameof(QuerySource<object>.Join) || method.Name == nameof(QuerySource<object>.JoinRelated)))
                return HandleJoin(node);

            // 处理类型成员处理器：先按特化类型查，未命中再按泛型定义查
            if (TryGetTypeMethodHandler(type, method.Name, out var typeMethodHandler))
                if (typeMethodHandler != null) return typeMethodHandler(node, this);

            // 处理方法名处理器
            if (_methodNameHandlers.TryGetValue(method.Name, out var nameHandler))
                if (nameHandler != null) return nameHandler(node, this);

            // 兜底使用默认函数表达式
            return DefaultFunctionHandler(node, this);
        }

        /// <summary>
        /// 将常量表达式转换为对应的 Expr 对象
        /// </summary>
        /// <param name="node">要转换的常量表达式节点</param>
        /// <returns>转换后的 Expr 对象</returns>
        protected Expr ConvertConstant(ConstantExpression node)
        {
            if (node.Value is Expr expr) return expr;
            if (node.Value is IQueryable)
                throw new NotSupportedException(
                    "An IQueryable constant cannot be converted to Expr directly. " +
                    "Use QuerySourceExprConverter.ToExpr or ObjectViewDAO.SearchAs(IQueryable<T>) so the query root is rewritten to a table source.");
            if (node.Value == null) return Expr.Null;
            bool isConst = node.Type.IsPrimitive || node.Type == typeof(string);
            return new ValueExpr(node.Value) { IsConst = isConst };
        }

        #endregion

        #region 内部处理逻辑

        /// <summary>
        /// 解析 TableArgs 赋值表达式的值，支持数组初始化、集合初始化和变量引用
        /// </summary>
        /// <param name="expr">赋值表达式的右侧</param>
        /// <returns>TableArgs 数组</returns>
        private string[] EvaluateTableArgs(Expression expr)
        {
            if (expr is NewArrayExpression newArrayExpr)
            {
                var args = new List<string>();
                foreach (var element in newArrayExpr.Expressions)
                {
                    var value = Evaluate(element);
                    if (value != null)
                    {
                        args.Add(value.ToString() ?? string.Empty);
                    }
                }
                return args.ToArray();
            }
            if (expr is ListInitExpression listInitExpr)
            {
                var args = new List<string>();
                foreach (var initializer in listInitExpr.Initializers)
                {
                    foreach (var arg in initializer.Arguments)
                    {
                        var value = Evaluate(arg);
                        if (value != null)
                        {
                            args.Add(value.ToString() ?? string.Empty);
                        }
                    }
                }
                return args.ToArray();
            }

            // 处理变量引用或常量
            var evaluatedValue = Evaluate(expr);
            if (evaluatedValue is string[] stringArray)
            {
                return stringArray;
            }
            else if (evaluatedValue is System.Collections.IEnumerable enumerable)
            {
                var args = new List<string>();
                foreach (var item in enumerable)
                {
                    if (item != null)
                    {
                        args.Add(item.ToString() ?? string.Empty);
                    }
                }
                return args.ToArray();
            }
            else if (evaluatedValue != null)
            {
                return new[] { evaluatedValue.ToString() ?? string.Empty };
            }

            return Array.Empty<string>();
        }

        private FunctionExpr CreateFunctionExpr(MethodCallExpression node)
        {
            var parameters = new List<ValueTypeExpr>();

            // 添加对象实例（如果是非静态方法）
            if (node.Object is not null)
            {
                var obj = ConvertInternal(node.Object).AsValue();
                if (obj is not null)
                {
                    parameters.Add(obj);
                }
            }

            // 添加方法参数
            foreach (var arg in node.Arguments)
            {
                var param = ConvertInternal(arg).AsValue();
                if (param is not null)
                {
                    parameters.Add(param);
                }
            }

            return new FunctionExpr(node.Method.Name, parameters.ToArray());
        }

        #endregion

        #region LINQ 方法处理器

        // LINQ 扩展方法处理器
        /// <summary>
        /// 处理 Where 调用，将条件转换并合并到已有的 WhereExpr 或创建新的 WhereExpr。
        /// </summary>
        /// <param name="node">方法调用表达式节点</param>
        /// <returns>处理后的 Expr</returns>
        private Expr HandleWhere(MethodCallExpression node)
        {
            var src = ConvertInternal(node.Arguments[0]);
            var lambda = UnwrapLambda(node.Arguments[1]);

            // 如果源是 GroupByExpr 或 HavingExpr，则这是一个 Having 筛选，否则是普通的 Where 筛选
            if (src is GroupByExpr or HavingExpr)
            {
                var havingAnchor = (IHavingAnchor)src;
                var groupBySource = src as GroupByExpr ?? (src as HavingExpr)?.Source as GroupByExpr;
                var havingLogic = ConvertHavingLambda(lambda, groupBySource?.GroupBys.ToArray());

                if (src is HavingExpr existingHaving)
                {
                    existingHaving.Having &= havingLogic;
                    return existingHaving;
                }

                return havingAnchor.Having(havingLogic);
            }

            SqlSegment? source = src as SqlSegment;

            ParameterExpression parameter = lambda.Parameters[0];
            string lastAlias = _currentAlias;
            _parameterAliases[parameter] = _currentAlias;
            // Where 的谓词参数没有独立的 FromExpr，这里把它映射到源链上的单表，
            // 使 ((IArged)x).TableArgs == [...] 这类分表条件能落到正确的表。
            bool registeredParamExpr = false;
            if (!_parameterExprs.ContainsKey(parameter))
            {
                TableExpr? predicateTable = GetSingleTable(source);
                if (predicateTable is not null && predicateTable.Alias == _currentAlias)
                {
                    _parameterExprs[parameter] = predicateTable;
                    registeredParamExpr = true;
                }
            }
            // Join 产生的元组参数需要按 Item1..ItemN 还原到各表，才能解析 x.Item2.Name
            ParameterExpression? tupleParam = RegisterTupleElements(lambda, src);

            // 将 Lambda 条件转换为 LogicExpr
            var newCondition = ConvertInternal(lambda.Body).AsLogic();

            if (tupleParam is not null) _tupleTrees.Remove(tupleParam);
            _parameterAliases.Remove(parameter);
            if (registeredParamExpr) _parameterExprs.Remove(parameter);
            _currentAlias = lastAlias;

            // 条件为 null 时（如纯 TableArgs 赋值），直接返回源不添加 WHERE
            if (newCondition is null)
                return src;

            // 如果源已经是 WhereExpr，将新条件与现有条件用 AND 合并
            if (source is WhereExpr existingWhere)
            {
                existingWhere.Where &= newCondition;
                return existingWhere;
            }

            // 否则创建新的 WhereExpr，需要转换为 ISourceAnchor
            return (source as ISourceAnchor).Where(newCondition);
        }

        /// <summary>
        /// 从查询源链中取出唯一的表表达式（跳过 Where / OrderBy 等包装）。
        /// 用于把 Where 谓词参数映射到对应的表，从而支持 TableArgs 分表条件。
        /// </summary>
        /// <param name="source">查询源。</param>
        /// <returns>源是单表查询时返回该表，否则返回 null。</returns>
        private static TableExpr? GetSingleTable(SqlSegment? source)
        {
            SqlSegment? current = source;
            while (current is not null)
            {
                switch (current)
                {
                    case TableExpr tableExpr:
                        return tableExpr;
                    case WhereExpr whereExpr:
                        current = whereExpr.Source;
                        break;
                    case OrderByExpr orderByExpr:
                        current = orderByExpr.Source;
                        break;
                    default:
                        return null;
                }
            }
            return null;
        }

        /// <summary>
        /// 处理 OrderBy/OrderByDescending 并返回 OrderByExpr 或更新现有表达式。
        /// </summary>
        private Expr HandleOrderBy(MethodCallExpression node, bool asc)
        {
            Expr source = ConvertInternal(node.Arguments[0]);
            LambdaExpression lambda = UnwrapLambda(node.Arguments[1]);
            // Join 产生的元组参数需要按 Item1..ItemN 还原到各表，才能解析 x.Item2.Name
            ParameterExpression? tupleParam = RegisterTupleElements(lambda, source);
            Expr key = ConvertInternal(node.Arguments[1]);
            if (tupleParam is not null) _tupleTrees.Remove(tupleParam);
            return (source as IOrderByAnchor).OrderBy((key.AsValue(), asc));
        }

        /// <summary>
        /// 处理 ThenBy/ThenByDescending 并更新现有的 OrderByExpr。
        /// </summary>
        /// <param name="node">方法调用表达式节点</param>
        /// <param name="asc">是否升序</param>
        /// <returns>更新后的 OrderByExpr</returns>
        private Expr HandleThenBy(MethodCallExpression node, bool asc)
        {
            if (ConvertInternal(node.Arguments[0]) is OrderByExpr ob)
            {
                LambdaExpression lambda = UnwrapLambda(node.Arguments[1]);
                ParameterExpression? tupleParam = RegisterTupleElements(lambda, ob);
                Expr key = ConvertInternal(node.Arguments[1]);
                if (tupleParam is not null) _tupleTrees.Remove(tupleParam);
                ob.OrderBys.Add((key.AsValue(), asc));
                return ob;
            }
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
            Expr source = ConvertInternal(node.Arguments[0]);
            var s = source as IGroupByAnchor;
            LambdaExpression lambda = UnwrapLambda(node.Arguments[1]);
            // Join 产生的元组参数需要按 Item1..ItemN 还原到各表，才能解析 x.Item2.Name
            ParameterExpression? tupleParam = RegisterTupleElements(lambda, source);
            Expr k = ConvertInternal(node.Arguments[1]);
            if (tupleParam is not null) _tupleTrees.Remove(tupleParam);
            return s.GroupBy(k is ValueSet vs ? vs.Cast<ValueTypeExpr>().ToArray() : new[] { k.AsValue() });
        }

        /// <summary>
        /// 将针对 GroupBy 的 Lambda 表达式转换为 Having 子句逻辑表达式。
        /// </summary>
        /// <param name="lambda">要转换的 Lambda 表达式</param>
        /// <param name="groupKeys">分组键集合</param>
        /// <returns>转换后的 LogicExpr</returns>
        private LogicExpr ConvertHavingLambda(LambdaExpression lambda, ValueTypeExpr[]? groupKeys)
        {
            var expr = ConvertGroupedExpr(lambda.Body, lambda.Parameters[0], groupKeys);
            return expr.AsLogic()!;
        }

        /// <summary>
        /// 处理 Select 调用，支持 GroupBy 后的 Select 情形。
        /// </summary>
        /// <param name="node">方法调用表达式节点</param>
        /// <returns>SelectExpr</returns>
        private Expr HandleSelect(MethodCallExpression node)
        {
            Expr rawSource = ConvertInternal(node.Arguments[0]);
            var source = rawSource as ISelectAnchor;
            var lambda = UnwrapLambda(node.Arguments[1]);

            // 检查是否是 GroupBy 后的 Select
            var groupBySource = source as GroupByExpr;

            // 转换 Select 的选择表达式；Join 产生的元组参数需按 Item1..ItemN 还原到各表
            ParameterExpression? tupleParam = RegisterTupleElements(lambda, rawSource);
            var selectItems = ConvertSelectLambda(lambda, groupBySource?.GroupBys.ToArray());
            if (tupleParam is not null) _tupleTrees.Remove(tupleParam);

            var selectExpr = (SelectExpr)source.Select(selectItems);

            // 为 SelectExpr 生成别名
            _currentAlias = selectExpr.Alias = _aliasCounter.NextAlias();

            return selectExpr;
        }

        /// <summary>
        /// 转换 Select Lambda 表达式（支持 GroupBy 后的 Select）
        /// </summary>
        /// <param name="lambda">要转换的 Lambda 表达式</param>
        /// <param name="groupKeys">用于分组时的键集合</param>
        /// <returns>SelectItemExpr 数组</returns>
        private SelectItemExpr[] ConvertSelectLambda(LambdaExpression lambda, ValueTypeExpr[]? groupKeys)
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
                        var selectItem = new SelectItemExpr(item.AsValue());
                        if (newExpr.Members != null && i < newExpr.Members.Count)
                        {
                            selectItem.Alias = newExpr.Members[i].Name;
                        }
                        items.Add(selectItem);
                    }
                }
                return items.ToArray();
            }

            // 处理 MemberInitExpression（命名类型初始化，如 new MyView { Prop = expr, ... }）
            if (body is MemberInitExpression memberInit)
            {
                var items = new List<SelectItemExpr>();
                foreach (var binding in memberInit.Bindings)
                {
                    if (binding is MemberAssignment assignment)
                    {
                        var item = ConvertGroupedExpr(assignment.Expression, lambdaParam, groupKeys);
                        if (item is not null)
                            items.Add(new SelectItemExpr(item.AsValue(), assignment.Member.Name));
                    }
                }
                return items.ToArray();
            }

            // 处理单个选择
            var singleItem = ConvertGroupedExpr(body, lambdaParam, groupKeys);
            return singleItem is not null ? new[] { new SelectItemExpr(singleItem.AsValue()) } : Array.Empty<SelectItemExpr>();
        }

        /// <summary>
        /// 转换分组后的表达式（支持 g.Key 和 g.Count() 等分组特有的访问，也支持二元运算如 g.Count() > 1）
        /// </summary>
        /// <param name="arg">要转换的表达式节点</param>
        /// <param name="lambdaParam">Lambda 表达式的参数</param>
        /// <param name="groupKeys">分组键集合</param>
        /// <returns>转换后的 Expr</returns>
        private Expr ConvertGroupedExpr(Expression arg, ParameterExpression lambdaParam, ValueTypeExpr[]? groupKeys)
        {
            // 如果是 MemberAccess (如 g.Key) 
            if (arg is MemberExpression memberExpr)
            {
                // 元组元素访问（x.Item2.Name）需要还原到对应表别名，优先于普通列处理
                if (TryResolveTupleElement(memberExpr, out TableExpr? tupleElement))
                {
                    string? elementAlias = tupleElement!.Alias;
                    if (elementAlias == _currentAlias) elementAlias = null;
                    if (memberExpr.Member is PropertyInfo tupleProperty)
                        return new PropertyExpr(tupleProperty.Name) { TableAlias = elementAlias };
                    if (memberExpr.Member is FieldInfo tupleField)
                        return new PropertyExpr(tupleField.Name) { TableAlias = elementAlias };
                }

                // 指向嵌套元组分支时无法映射到列，明确报错而不是渲染成 [Item1]
                if (IsNestedTupleAccess(memberExpr))
                    throw new NotSupportedException(
                        $"'{memberExpr}' refers to a nested tuple element and cannot be used as a column. " +
                        $"Access a concrete table element instead, for example x.Item1.Item2.Column.");

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
                    Expression? aggregateTarget = null;
                    Expression? fieldExpr = null;

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

                        return new FunctionExpr(aggregateName, aggregateArg) { IsAggregate = true };
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
                    return left.AsLogic()!.And(right.AsLogic()!);
                if (binary.NodeType == ExpressionType.OrElse || binary.NodeType == ExpressionType.Or)
                    return left.AsLogic()!.Or(right.AsLogic()!);

                // Other binary operators
                var op = binary.NodeType switch
                {
                    ExpressionType.Equal => LogicOperator.Equal,
                    ExpressionType.NotEqual => LogicOperator.NotEqual,
                    ExpressionType.GreaterThan => LogicOperator.GreaterThan,
                    ExpressionType.GreaterThanOrEqual => LogicOperator.GreaterThanOrEqual,
                    ExpressionType.LessThan => LogicOperator.LessThan,
                    ExpressionType.LessThanOrEqual => LogicOperator.LessThanOrEqual,
                    _ => (object?)null
                };

                if (op is LogicOperator lo) return new LogicBinaryExpr(left.AsValue(), lo, right.AsValue());
            }

            // 避免对 naked parameter 调用 ConvertInternal，这会触发 NotSupportedException
            if (IsParameterAccess(arg, lambdaParam)) return null!;

            // 回退到普通转换
            return ConvertInternal(arg);
        }

        /// <summary>
        /// Exists 方法处理器：将 Expr.Exists{T}(lambda) 转换为 ForeignExpr。
        /// </summary>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "ParameterExpression.Type returns a runtime-known Type; properties are preserved via [Table] entities through the source generator; under AOT, ensure entity types are registered.")]
#endif
        private Expr HandleExists(MethodCallExpression node)
        {
            if (node.Arguments.Count == 1)
            {
                Expression lambdaArg = node.Arguments[0];
                if (lambdaArg is UnaryExpression unaryExpr && unaryExpr.NodeType == ExpressionType.Quote)
                {
                    lambdaArg = unaryExpr.Operand;
                }
                var lambda = lambdaArg as LambdaExpression;
                if (lambda != null)
                {
                    ParameterExpression parameter = lambda.Parameters[0];
                    var lastAlias = _currentAlias;
                    // 为 Lambda 参数生成一个新的别名并缓存
                    _currentAlias = _parameterAliases[parameter] = _aliasCounter.NextAlias();
                    // 创建 ForeignExpr 并缓存到 _parameterArgs
                    var foreignExpr = new ForeignExpr(parameter.Type) { Alias = _currentAlias };
                    // 将参数与 ForeignExpr 关联，以便在转换 Lambda.Body 时正确解析参数访问
                    _parameterExprs[parameter] = foreignExpr;

                    // 转换 Lambda.Body，得到内部的 LogicExpr
                    foreignExpr.InnerExpr = ConvertInternal(lambda.Body).AsLogic();

                    // 转换完成后清理缓存，恢复之前的别名
                    _parameterExprs.Remove(parameter);
                    _parameterAliases.Remove(parameter);
                    _currentAlias = lastAlias;
                    if (node.Method.Name == nameof(Expr.ExistsRelated)) foreignExpr.AutoRelated = true;
                    return foreignExpr;
                }
            }
            return null!;
        }

        /// <summary>
        /// From / FromRelated 方法处理器：将 <c>Expr.From&lt;T&gt;()</c> 转换为 <see cref="TableExpr"/>，
        /// <c>Expr.FromRelated&lt;T&gt;()</c> 在此基础上按关联元数据推断关联条件并包一层 <see cref="WhereExpr"/>。
        /// </summary>
        /// <param name="node">方法调用表达式节点</param>
        /// <returns>表表达式，或带关联条件的筛选表达式</returns>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Generic argument type comes from the lambda itself; properties are preserved via [Table] entities through the source generator.")]
        [UnconditionalSuppressMessage("Trimming", "IL2062", Justification = "The generic argument type of Expr.From<T> is resolved from the lambda's method call; entity types must be registered via [Table] under AOT.")]
#endif
        private Expr HandleFrom(MethodCallExpression node)
        {
            Type[] genericArgs = node.Method.GetGenericArguments();
            if (genericArgs.Length != 1)
                throw new ArgumentException($"Method {node.Method.Name} must have exactly one generic argument.", nameof(node));

            Type entityType = genericArgs[0];
            string alias = _aliasCounter.NextAlias();
            var tableExpr = new TableExpr(entityType) { Alias = alias };
            if (node.Arguments.Count > 0)
                tableExpr.TableArgs = EvaluateTableArgs(node.Arguments[0]);

            if (node.Method.Name != nameof(Expr.FromRelated)) return tableExpr;

            // FromRelated 以当前根参数的表作为关联源，推断不到关联关系时退化为普通 From
            Type? rootType = _tableExpr?.Type;
            string? rootAlias = _tableExpr?.Alias;
            if (rootType == null || rootAlias == null) return tableExpr;

            LogicExpr? related = Expr.BuildRelatedLogic(rootType, rootAlias, entityType, alias);
            return related is null ? tableExpr : new WhereExpr(tableExpr, related);
        }

        /// <summary>
        /// Join / JoinRelated 方法处理器：把右表以 <see cref="TableJoinExpr"/> 形式追加到 <see cref="FromExpr"/> 的 Joins 中。
        /// 左源是 <see cref="TableExpr"/> 时新建 <see cref="FromExpr"/>，已是 <see cref="FromExpr"/> 时在其上追加，从而支持多级连接。
        /// </summary>
        /// <param name="node">方法调用表达式节点</param>
        /// <returns>包含连接信息的 <see cref="FromExpr"/></returns>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Generic argument types come from the join call itself; properties are preserved via [Table] entities through the source generator.")]
        [UnconditionalSuppressMessage("Trimming", "IL2062", Justification = "The right-hand entity types are resolved from the generic arguments of Join; entity types must be registered via [Table] under AOT.")]
#endif
        private Expr HandleJoin(MethodCallExpression node)
        {
            // 每次连接只追加一张表，右表类型即连接方法的泛型实参
            IReadOnlyList<Type> rightTypes = ResolveJoinRightTypes(node);
            // 实例方法的接收者在 Object 上；扩展方法的接收者是第一个参数
            Expression sourceExpression = node.Object
                ?? (node.Arguments.Count > 0 ? node.Arguments[0] : throw new ArgumentException($"Join source is missing on {node.Method.Name}.", nameof(node)));
            Expr left = ConvertInternal(sourceExpression);

            // Where 先于 Join 时源已变成 WhereExpr，沿 Source 回溯到表/连接链，连接完成后再把筛选包回外层
            var filters = new List<WhereExpr>();
            while (left is WhereExpr { Source: not null } where)
            {
                filters.Add(where);
                left = where.Source;
            }

            // 左源统一收敛到 FromExpr：TableExpr 作为主表新建，FromExpr 直接复用以便追加后续连接
            FromExpr from;
            if (left is FromExpr existingFrom) from = existingFrom;
            else if (left is TableExpr tableExpr) from = new FromExpr(tableExpr);
            else
                throw new ArgumentException($"Join source must be a table or an existing join chain, but got {left?.GetType().Name}.", nameof(node));

            var joinTables = new List<TableExpr>(rightTypes.Count);
            foreach (Type rightType in rightTypes)
            {
                joinTables.Add(new TableExpr(rightType) { Alias = _aliasCounter.NextAlias() });
            }

            // 先构造右表别名并计算 ON 条件，再统一追加到连接链
            LambdaExpression? on = GetJoinLambda(node);
            LogicExpr?[] conditions = BuildJoinConditions(node, from, joinTables, on, out TableExpr?[] relatedLeftTables);
            TableJoinType? explicitJoinType = GetJoinTypeArgument(node);
            for (int i = 0; i < joinTables.Count; i++)
            {
                TableExpr joinTable = joinTables[i];
                LogicExpr? condition = conditions[i];
                var joinExpr = new TableJoinExpr(joinTable, condition)
                {
                    // 没有条件的连接段使用 CROSS JOIN，避免生成缺少 ON 的 LEFT JOIN
                    JoinType = condition is null
                        ? TableJoinType.Cross
                        : ResolveJoinType(node.Method.Name, explicitJoinType, relatedLeftTables[i] ?? from.Source as TableExpr, joinTable.Type!)
                };
                from.Joins.Add(joinExpr);
            }

            // 把 Join 之前累积的筛选重新包回连接结果之上
            Expr result = from;
            for (int i = filters.Count - 1; i >= 0; i--)
            {
                filters[i].Source = (SqlSegment)result;
                result = filters[i];
            }
            return result;
        }

        /// <summary>
        /// 从 Join / JoinRelated 的方法泛型实参解析右表类型。
        /// </summary>
        private static IReadOnlyList<Type> ResolveJoinRightTypes(MethodCallExpression node)
        {
            // 连接方法只追加一张表，右表类型即方法自身的泛型实参
            Type[] typeArguments = node.Method.GetGenericArguments();
            if (typeArguments.Length == 0)
                throw new ArgumentException(
                    $"Cannot resolve the joined table type from {node.Method.Name}: the join method has no generic argument.",
                    nameof(node));
            return typeArguments;
        }

        /// <summary>
        /// 构造 Join 的 ON 条件数组。Join 使用显式条件，并按条件中引用到的右表拆分到对应的 JOIN 段；
        /// JoinRelated 使用自动推断的关联条件，若同时提供了显式条件则两者按 AND 合并。
        /// </summary>
        /// <param name="node">Join / JoinRelated 方法调用。</param>
        /// <param name="from">当前连接链。</param>
        /// <param name="joinTables">本次要追加的右表。</param>
        /// <param name="on">显式连接条件，可能为 null。</param>
        /// <param name="relatedLeftTables">JoinRelated 推断出关联关系的左表；Join 时为 null。</param>
        /// <returns>每个右表对应的 ON 条件。</returns>
        private LogicExpr?[] BuildJoinConditions(MethodCallExpression node, FromExpr from, IReadOnlyList<TableExpr> joinTables, LambdaExpression? on, out TableExpr?[] relatedLeftTables)
        {
            bool isRelated = node.Method.Name == nameof(QuerySource<object>.JoinRelated);
            bool hasExplicitOn = on is not null;
            var conditions = new LogicExpr?[joinTables.Count];
            relatedLeftTables = new TableExpr?[joinTables.Count];

            if (isRelated)
            {
                // 按关联顺序从后向前匹配：优先用最近加入的连接表推断，最后才回退到主表
                var leftCandidates = new List<TableExpr>(from.Joins.Count + 1);
                for (int i = from.Joins.Count - 1; i >= 0; i--)
                {
                    if (from.Joins[i].Source is TableExpr joinedTable)
                        leftCandidates.Add(joinedTable);
                }
                if (from.Source is TableExpr rootTable)
                    leftCandidates.Add(rootTable);

                if (leftCandidates.Count == 0)
                    throw new InvalidOperationException($"{node.Method.Name} requires a table as the join source.");

                for (int i = 0; i < joinTables.Count; i++)
                {
                    TableExpr joinTable = joinTables[i];
                    LogicExpr? autoRelated = null;
                    TableExpr? matchedLeftTable = null;
                    foreach (TableExpr candidate in leftCandidates)
                    {
                        if (candidate.Type is null || candidate.Alias is null) continue;
                        autoRelated = Expr.BuildRelatedLogic(candidate.Type, candidate.Alias, joinTable.Type!, joinTable.Alias!);
                        if (autoRelated is not null)
                        {
                            matchedLeftTable = candidate;
                            break;
                        }
                    }
                    if (autoRelated is null && !hasExplicitOn)
                        throw new InvalidOperationException(
                            $"No relation between the joined tables and {joinTable.Type!.Name} is found in metadata. " +
                            $"Annotate the association with [ForeignType] / [TableJoin], or use Join with an explicit condition.");
                    conditions[i] = autoRelated;
                    relatedLeftTables[i] = matchedLeftTable;
                }
            }

            if (on is null) return conditions;

            // 连接条件的参数按顺序一表一个：已连接的每张表（主表 + 各 JOIN 表）在前，新右表在后
            var leftTables = new List<TableExpr>(from.Joins.Count + 1);
            if (from.Source is not TableExpr mainTable)
                throw new InvalidOperationException($"{node.Method.Name} requires a table as the join source.");
            leftTables.Add(mainTable);
            foreach (TableJoinExpr join in from.Joins)
            {
                if (join.Source is not TableExpr joinedTable)
                    throw new InvalidOperationException($"{node.Method.Name} requires a table as the join source.");
                leftTables.Add(joinedTable);
            }

            int expectedParameters = leftTables.Count + joinTables.Count;
            if (on.Parameters.Count != expectedParameters)
                throw new ArgumentException(
                    $"The join condition for {node.Method.Name} expects {expectedParameters} parameters (one per joined table in order), " +
                    $"but got {on.Parameters.Count}.",
                    nameof(node));

            ParameterExpression leftParam = on.Parameters[0];
            var leftParams = new ParameterExpression[leftTables.Count];
            for (int i = 0; i < leftParams.Length; i++) leftParams[i] = on.Parameters[i];
            var rightParams = new ParameterExpression[joinTables.Count];
            for (int i = 0; i < rightParams.Length; i++) rightParams[i] = on.Parameters[leftParams.Length + i];

            string? lastAlias = _currentAlias;
            string leftAliasName = mainTable.Alias ?? _currentAlias
                ?? throw new InvalidOperationException($"{node.Method.Name} requires an alias on the join source.");

            for (int i = 0; i < leftTables.Count; i++)
            {
                _parameterAliases[leftParams[i]] = leftTables[i].Alias ?? leftAliasName;
                _parameterExprs[leftParams[i]] = leftTables[i];
            }
            for (int i = 0; i < joinTables.Count; i++)
            {
                _parameterAliases[rightParams[i]] = joinTables[i].Alias!;
                _parameterExprs[rightParams[i]] = joinTables[i];
            }
            _currentAlias = leftAliasName;

            try
            {
                var explicitParts = new List<LogicExpr>[joinTables.Count];
                for (int i = 0; i < explicitParts.Length; i++) explicitParts[i] = new List<LogicExpr>();

                // 按 AND 拆分条件，并把每个分支放到引用到的最右侧 JOIN 段：
                // (o.CustomerId == c.Id) && (c.RegionId == r.Id) 会分别成为 C 和 R 的 ON 条件。
                foreach (Expression conjunct in SplitConjuncts(on.Body))
                {
                    int targetIndex = DetermineJoinIndex(conjunct, leftParam, rightParams);
                    if (targetIndex < 0) targetIndex = 0;
                    LogicExpr? explicitPart = ConvertInternal(conjunct).AsLogic();
                    if (explicitPart is not null) explicitParts[targetIndex].Add(explicitPart);
                }

                for (int i = 0; i < joinTables.Count; i++)
                {
                    if (explicitParts[i].Count == 0) continue;
                    LogicExpr explicitOn = explicitParts[i].Count == 1
                        ? explicitParts[i][0]
                        : new AndExpr(explicitParts[i]);
                    LogicExpr? autoRelated = conditions[i];
                    conditions[i] = autoRelated is null ? explicitOn : new AndExpr(autoRelated, explicitOn);
                }
            }
            finally
            {
                foreach (ParameterExpression leftTableParam in leftParams)
                {
                    _parameterAliases.Remove(leftTableParam);
                    _parameterExprs.Remove(leftTableParam);
                }
                foreach (ParameterExpression rightParam in rightParams)
                {
                    _parameterAliases.Remove(rightParam);
                    _parameterExprs.Remove(rightParam);
                }
                _currentAlias = lastAlias!;
            }

            return conditions;
        }

        /// <summary>
        /// 将一个布尔表达式按顶层 <see cref="ExpressionType.AndAlso"/> 拆成多个独立条件。
        /// </summary>
        private static IEnumerable<Expression> SplitConjuncts(Expression expression)
        {
            if (expression is BinaryExpression binary && binary.NodeType == ExpressionType.AndAlso)
            {
                foreach (Expression part in SplitConjuncts(binary.Left)) yield return part;
                foreach (Expression part in SplitConjuncts(binary.Right)) yield return part;
            }
            else
            {
                yield return expression;
            }
        }

        /// <summary>
        /// 判断条件引用了最右侧的哪个 Join 参数，用于把多表显式条件拆分到各 JOIN 段。
        /// 只引用左源时返回 -1。
        /// </summary>
        private static int DetermineJoinIndex(Expression expression, ParameterExpression leftParam, IReadOnlyList<ParameterExpression> rightParams)
        {
            var visitor = new JoinParameterVisitor(leftParam, rightParams);
            visitor.Visit(expression);
            return visitor.MaxRightIndex;
        }

        /// <summary>
        /// 收集表达式中引用到的 Join 参数，记录最右侧右表参数的下标。
        /// </summary>
        private sealed class JoinParameterVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _leftParam;
            private readonly IReadOnlyList<ParameterExpression> _rightParams;

            public JoinParameterVisitor(ParameterExpression leftParam, IReadOnlyList<ParameterExpression> rightParams)
            {
                _leftParam = leftParam;
                _rightParams = rightParams;
            }

            /// <summary>引用到的最右侧右表参数下标；仅引用左源时为 -1。</summary>
            public int MaxRightIndex { get; private set; } = -1;

            /// <inheritdoc/>
            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node != _leftParam)
                {
                    for (int i = 0; i < _rightParams.Count; i++)
                    {
                        if (node == _rightParams[i])
                        {
                            if (i > MaxRightIndex) MaxRightIndex = i;
                            break;
                        }
                    }
                }
                return node;
            }
        }

        /// <summary>
        /// 提取 Join / JoinRelated 的显式条件 Lambda。
        /// 实例方法的条件从第一个参数开始，扩展方法的第一个参数是接收者，条件位于其后。
        /// </summary>
        private static LambdaExpression? GetJoinLambda(MethodCallExpression node)
        {
            int start = node.Object is null ? 1 : 0;
            for (int i = start; i < node.Arguments.Count; i++)
            {
                Expression argument = node.Arguments[i];
                if (argument is UnaryExpression { NodeType: ExpressionType.Quote } || argument is LambdaExpression)
                    return UnwrapLambda(argument);
            }
            return null;
        }

        /// <summary>
        /// 读取连接类型的可选参数；未指定或显式传 null 时返回 null，交由默认规则决定。
        /// </summary>
        private static TableJoinType? GetJoinTypeArgument(MethodCallExpression node)
        {
            if (node.Arguments.Count == 0) return null;
            Expression last = node.Arguments[node.Arguments.Count - 1];
            // 显式传入非 nullable 枚举时，编译器会插入到 Nullable<T> 的转换节点
            while (last is UnaryExpression unary && (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
                last = unary.Operand;
            return last is ConstantExpression { Value: TableJoinType joinType } ? joinType : null;
        }

        /// <summary>
        /// 解析连接类型：显式指定的连接类型优先，JoinRelated 其次沿用元数据中声明的连接方式，其余情况使用左连接。
        /// </summary>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Type comes from the lambda's generic arguments; entity metadata is preserved via [Table] under AOT.")]
#endif
        private static TableJoinType ResolveJoinType(string methodName, TableJoinType? explicitJoinType, TableExpr? leftTable, Type rightType)
        {
            if (explicitJoinType.HasValue) return explicitJoinType.Value;
            if (methodName == nameof(QuerySource<object>.JoinRelated) && leftTable?.Type != null)
            {
                var view = TableInfoProvider.Instance.GetTableView(leftTable.Type);
                if (view != null)
                {
                    foreach (JoinedTable joinedTable in view.JoinedTables)
                    {
                        if (joinedTable.TableDefinition is null) continue;
                        if (joinedTable.TableDefinition.DefinitionType.IsAssignableFrom(rightType))
                            return joinedTable.JoinType;
                    }
                }
            }
            return TableJoinType.Left;
        }

        /// <summary>
        /// 去掉 Lambda 参数上的 Quote 包装。
        /// </summary>
        private static LambdaExpression UnwrapLambda(Expression expression)
        {
            if (expression is UnaryExpression quote && quote.NodeType == ExpressionType.Quote) expression = quote.Operand;
            return expression as LambdaExpression
                ?? throw new ArgumentException($"Expected a lambda expression, but got {expression.GetType().Name}.");
        }

        /// <summary>
        /// 判断方法调用是否为根级 LINQ 聚合函数（Count/Sum/Average/Max/Min 等）。
        /// </summary>
        private static bool IsAggregateMethod(MethodCallExpression node)
            => node.Method.IsStatic
                && node.Arguments.Count > 0
                && GetAggregateName(node.Method.Name) != null;

        /// <summary>
        /// 聚合函数处理器：将 <c>q.Count()</c>、<c>q.Sum(x => x.Age)</c> 等转换为只含单个聚合列的 <see cref="SelectExpr"/>。
        /// 生成的表达式既可作为根查询，也可作为标量子查询嵌入其他表达式中。
        /// </summary>
        /// <param name="node">方法调用表达式节点</param>
        /// <returns>聚合查询表达式</returns>
        private Expr HandleAggregate(MethodCallExpression node)
        {
            string aggregateName = GetAggregateName(node.Method.Name)!;
            Expr source = ConvertInternal(node.Arguments[0]);
            if (source is not SqlSegment sourceSegment)
                throw new ArgumentException($"Aggregate source must be a query expression, but got {source?.GetType().Name}.", nameof(node));

            string lastAlias = _currentAlias;
            if (source is SelectExpr selectExpr && selectExpr.Alias != null) _currentAlias = selectExpr.Alias;

            ValueTypeExpr aggregateArg = Expr.Const(1);
            try
            {
                if (node.Arguments.Count > 1)
                {
                    Expression selector = node.Arguments[1];
                    if (selector is UnaryExpression quote && quote.NodeType == ExpressionType.Quote) selector = quote.Operand;
                    if (selector is LambdaExpression selectorLambda)
                    {
                        ParameterExpression parameter = selectorLambda.Parameters[0];
                        _parameterAliases[parameter] = _currentAlias;
                        aggregateArg = ConvertInternal(selectorLambda.Body).AsValue();
                        _parameterAliases.Remove(parameter);
                    }
                    else
                    {
                        aggregateArg = ConvertInternal(selector).AsValue();
                    }
                }
            }
            finally
            {
                _currentAlias = lastAlias;
            }

            var aggregate = new FunctionExpr(aggregateName.ToUpperInvariant(), aggregateArg) { IsAggregate = true };
            return new SelectExpr(sourceSegment, new SelectItemExpr(aggregate));
        }

        /// <summary>
        /// 根据方法名获取对应的聚合函数名称（用于将 LINQ 聚合函数映射到 SQL 聚合函数）。
        /// </summary>
        /// <param name="name">方法名</param>
        /// <returns>聚合函数名称或 null</returns>
        private static string? GetAggregateName(string name) => name switch
        {
            "Count" or "LongCount" => "Count",
            "Sum" => "Sum",
            "Average" or "Avg" => "Avg",
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
        private bool IsParameterAccess(Expression? node, ParameterExpression lambdaParam)
        {
            if (node is null) return false;
            if (node is ParameterExpression pe) return ReferenceEquals(pe, lambdaParam);
            return false;
        }
        #endregion

        /// <summary>
        /// 表达式参数检测器
        /// </summary>
        public class ExpressionDetector : ExpressionVisitor
        {
            private bool _result = true;
            private Dictionary<Expression, bool> _expressionFindResults = new Dictionary<Expression, bool>();
            /// <summary>
            /// 遍历表达式树以检测是否可以直接计算（不包含参数表达式或已注册的函数调用），结果会被缓存以优化后续相同表达式的检测。
            /// </summary>
            /// <param name="expression">要检查的表达式。</param>
            /// <returns>如果表达式可以直接计算（不依赖于Lambda参数）则返回true，否则返回false。</returns>
            public bool CanEvaluate(Expression expression)
            {
                if (_expressionFindResults.TryGetValue(expression, out var cachedResult))
                {
                    return cachedResult;
                }
                _result = true;
                Visit(expression);
                return _result;
            }

            /// <summary>
            /// 访问表达式节点，如果当前结果已经是 false 则直接返回原节点以避免不必要的遍历；否则继续访问并缓存结果。
            /// </summary>
            /// <param name="node"></param>
            /// <returns></returns>
            public override Expression? Visit(Expression? node)
            {
                if (node == null) return null;
                if (!_result) return node;
                Expression? exp = base.Visit(node);
                _expressionFindResults[node] = _result;
                return exp;
            }

            /// <summary>
            /// 访问常量表达式。IQueryable 常量代表查询根，本地求值会真的执行查询，必须交给解析器。
            /// </summary>
            /// <param name="node">常量表达式节点。</param>
            /// <returns>返回原节点。</returns>
            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (node.Value is IQueryable)
                {
                    _result = false;
                    return node;
                }
                return base.VisitConstant(node);
            }

            /// <summary>
            /// 访问参数表达式。
            /// </summary>
            /// <param name="node">参数表达式节点。</param>
            /// <returns>返回处理后的表达式节点。</returns>
            protected override Expression VisitParameter(ParameterExpression node)
            {
                _result = false;
                return node; // 直接返回原节点，避免继续访问子表达式影响结果
            }

            /// <summary>
            /// 访问函数调用表达式，如果方法属于已注册的类型成员处理器或方法名处理器，则标记结果为 false。
            /// </summary>
            /// <param name="node"></param>
            /// <returns></returns>
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                // Expr.From / Expr.FromRelated 是纯占位方法，调用即抛异常，必须交给 ConvertMethodCall 解析而非本地求值
                if (node.Method.DeclaringType == typeof(Expr)
                    && (node.Method.Name == nameof(Expr.From) || node.Method.Name == nameof(Expr.FromRelated)))
                {
                    _result = false;
                    return node; // 直接返回原节点，避免继续访问子表达式影响结果
                }
                // QuerySource 各元数（QuerySource<T1> / QuerySource<T1,T2> ...）上的连接方法都必须交给 ConvertMethodCall 解析
                if (node.Method.DeclaringType is Type declaring && typeof(IQuerySource).IsAssignableFrom(declaring))
                {
                    _result = false;
                    return node; // 直接返回原节点，避免继续访问子表达式影响结果
                }
                if (TryGetTypeMethodHandler(node.Method.DeclaringType, node.Method.Name, out _) || _methodNameHandlers.TryGetValue(node.Method.Name, out _))
                {
                    _result = false;
                    return node; // 直接返回原节点，避免继续访问子表达式影响结果
                }
                return base.VisitMethodCall(node);
            }

            /// <summary>
            /// 访问成员访问表达式，如果成员属于已注册的类型成员处理器或方法名处理器，则标记结果为 false。
            /// </summary>
            /// <param name="node"></param>
            /// <returns></returns>
            protected override Expression VisitMember(MemberExpression node)
            {
                if (_typeMemberHandlers.TryGetValue((node.Member.DeclaringType!, node.Member.Name), out _) || _memberNameHandlers.TryGetValue(node.Member.Name, out _) || _typeFallbackMemberHandlers.ContainsKey(node.Member.DeclaringType!))
                {
                    _result = false;
                    return node; // 直接返回原节点，避免继续访问子表达式影响结果
                }
                return base.VisitMember(node);
            }
        }

        /// <summary>
        /// 表别名编号序列。父子转换器共享同一实例，避免嵌套作用域分配到重复别名。
        /// </summary>
        public sealed class AliasSequence
        {
            private int _next;

            /// <summary>
            /// 初始化 <see cref="AliasSequence"/> 类的新实例。
            /// </summary>
            /// <param name="start">起始编号，首个可用编号为 start。</param>
            public AliasSequence(int start)
            {
                _next = start;
            }

            /// <summary>
            /// 取下一个编号并递增。
            /// </summary>
            public int Next() => _next++;

            /// <summary>
            /// 按 T{n} 规则生成下一个表别名。
            /// </summary>
            public string NextAlias() => "T" + Next();
        }
    }
}
