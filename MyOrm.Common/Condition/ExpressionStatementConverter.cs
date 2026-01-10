using MyOrm.Service;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace MyOrm.Common
{
    /// <summary>
    /// 将 Lambda 表达式转换为 Statement 对象树的纯转换器
    /// </summary>
    public class ExpressionStatementConverter
    {
        private readonly ParameterExpression _rootParameter;

        /// <summary>
        /// 初始化 ExpressionStatementConverter
        /// </summary>
        /// <param name="rootParameter">Lambda 表达式的根参数</param>
        public ExpressionStatementConverter(ParameterExpression rootParameter = null)
        {
            _rootParameter = rootParameter;
        }

        /// <summary>
        /// 将 Lambda 表达式转换为 Statement
        /// </summary>
        public Statement Convert(Expression expression)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            var stmt = ConvertInternal(expression);
            if (stmt == null) throw new ArgumentException($"无法转换表达式: {expression}");
            return stmt;
        }

        #region 基础表达式处理

        private Statement ConvertInternal(Expression node)
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
                    return new ValueStatement(constant.Value);
                case ParameterExpression param:
                    throw new NotSupportedException($"参数表达式 '{param.Name}' 不能直接转换为 Statement");
                case NewArrayExpression newArray:
                    return ConvertNewArray(newArray);
                case ListInitExpression listInit:
                    return ConvertListInit(listInit);
                case MethodCallExpression methodCall:
                    return ConvertMethodCall(methodCall);
                case NewExpression newExpression:
                    return EvaluateExpression(newExpression);
                default:
                    throw new NotSupportedException($"不支持的表达式类型: {node.NodeType} ({node.GetType().Name})");
            }
        }

        private Statement ConvertBinary(BinaryExpression node)
        {
            // 将表达式节点类型转换为 BinaryOperator
            BinaryOperator op = ConvertExpressionTypeToBinaryOperator(node.NodeType);

            // 特殊处理 CompareTo 方法调用
            if (node.Left is MethodCallExpression leftCallExpression && leftCallExpression.Method.Name == "CompareTo")
            {
                if (!EvaluateExpression(node.Right).Equals(0)) throw new ArgumentException($"CompareTo 方法只能与 0 进行比较: {node}");
                BinaryStatement res = ConvertMethodCall(leftCallExpression) as BinaryStatement;
                res.Operator = op switch
                {
                    BinaryOperator.Equal => BinaryOperator.Equal,
                    BinaryOperator.NotEqual => BinaryOperator.NotEqual,
                    BinaryOperator.GreaterThan => BinaryOperator.GreaterThan,
                    BinaryOperator.GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
                    BinaryOperator.LessThan => BinaryOperator.LessThan,
                    BinaryOperator.LessThanOrEqual => BinaryOperator.LessThanOrEqual,
                    _ => throw new ArgumentException($"CompareTo 方法只能用 ==, !=, >, >=, <, <= 进行比较: {node}")
                };
                return res;
            }
            else if (node.Right is MethodCallExpression rightCallExpression && rightCallExpression.Method.Name == "CompareTo")
            {
                if (!EvaluateExpression(node.Left).Equals(0)) throw new ArgumentException($"CompareTo 方法只能与 0 进行比较: {node}");
                BinaryStatement res = ConvertMethodCall(rightCallExpression) as BinaryStatement;
                // 反转操作符
                res.Operator = op switch
                {
                    BinaryOperator.Equal => BinaryOperator.Equal,
                    BinaryOperator.NotEqual => BinaryOperator.NotEqual,
                    BinaryOperator.GreaterThan => BinaryOperator.LessThan,
                    BinaryOperator.GreaterThanOrEqual => BinaryOperator.LessThanOrEqual,
                    BinaryOperator.LessThan => BinaryOperator.GreaterThan,
                    BinaryOperator.LessThanOrEqual => BinaryOperator.GreaterThanOrEqual,
                    _ => throw new ArgumentException($"CompareTo 方法只能用 ==, !=, >, >=, <, <= 进行比较: {node}")
                };
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
                        return new BinaryStatement(left, BinaryOperator.Concat, right);
                    else
                        return new BinaryStatement(left, BinaryOperator.Add, right);
                default:
                    return new BinaryStatement(left, op, right);
            }
        }

        private Statement ConvertUnary(UnaryExpression node)
        {
            var operand = ConvertInternal(node.Operand);

            if (operand == null)
            {
                throw new ArgumentException($"无法转换一元表达式: {node}");
            }

            switch (node.NodeType)
            {
                case ExpressionType.OnesComplement:
                    return new UnaryStatement(UnaryOperator.BitwiseNot, operand);
                case ExpressionType.Not:
                    return new UnaryStatement(UnaryOperator.Not, operand);
                case ExpressionType.Negate:
                    return new UnaryStatement(UnaryOperator.Nagive, operand);
                case ExpressionType.Convert:
                    // 类型转换通常不需要特殊处理
                    return operand;
                default:
                    throw new NotSupportedException($"不支持的一元操作符: {node.NodeType}");
            }
        }

        private ValueStatement EvaluateExpression(Expression node)
        {
            try
            {
                // 尝试计算 Expression 的值
                var lambda = Expression.Lambda(node);
                var compiled = lambda.Compile();
                var value = compiled.DynamicInvoke();
                return new ValueStatement(value);
            }
            catch
            {
                throw new ArgumentException($"无法计算 Expression 的值: {node}");
            }
        }

        private Statement ConvertMember(MemberExpression node)
        {
            if (Nullable.GetUnderlyingType(node.Member.DeclaringType) != null && node.Member.Name == "Value")
            {
                // 处理 Nullable<T>.Value  
                return ConvertInternal(node.Expression);
            }
            // 处理参数属性访问（如 x => x.Name）
            if (node.Expression is ParameterExpression paramExpr &&
                (_rootParameter == null || paramExpr == _rootParameter))
            {
                if (node.Member is PropertyInfo propertyInfo)
                {
                    return Statement.Property(propertyInfo.Name);
                }
                else if (node.Member is FieldInfo fieldInfo)
                {
                    return Statement.Property(fieldInfo.Name);
                }
            }

            if (IsFunction(node))
            {
                // 处理字符串或数组的 Length 属性
                var targetExpr = node.Expression;
                if (targetExpr == null) return new FunctionStatement(node.Member.Name);
                else
                    return new FunctionStatement(node.Member.Name, ConvertInternal(targetExpr));
            }

            if (new ParameterExpressionDetector().ContainsParameter(node))
            {
                // 处理复杂属性访问（如 x => x.Address.City）
                var parts = new List<string>();
                Expression current = node;
                while (current is MemberExpression memberExpr)
                {
                    parts.Add(memberExpr.Member.Name);
                    current = memberExpr.Expression;
                }
                parts.Reverse();
                var propertyName = string.Join(".", parts);
                return Statement.Property(propertyName);
            }
            else// 处理常量成员访问（如 DateTime.Now）
                return EvaluateExpression(node);
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

        private Statement ConvertNewArray(NewArrayExpression node)
        {
            var items = new List<Statement>();
            foreach (var expression in node.Expressions)
            {
                var item = ConvertInternal(expression);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            return new ValueStatement(items);
        }

        private Statement ConvertListInit(ListInitExpression node)
        {
            var items = new List<Statement>();
            foreach (var init in node.Initializers)
            {
                foreach (var arg in init.Arguments)
                {
                    var item = ConvertInternal(arg);
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
            }

            return new ValueStatement(items);
        }

        #endregion

        #region 方法调用处理

        private Statement ConvertMethodCall(MethodCallExpression node)
        {
            // 处理字符串方法
            if (node.Method.DeclaringType == typeof(string))
            {
                return HandleStringMethod(node);
            }

            // 处理 Enumerable 扩展方法
            if (node.Method.DeclaringType == typeof(Enumerable))
            {
                return HandleEnumerableMethod(node);
            }

            // 处理对象方法
            if (node.Object != null)
            {
                return HandleInstanceMethod(node);
            }

            // 处理静态方法
            return HandleStaticMethod(node);
        }

        private Statement HandleStringMethod(MethodCallExpression node)
        {
            var methodName = node.Method.Name;

            Statement left = null;
            Statement right = null;

            if (node.Object != null)
            {
                left = ConvertInternal(node.Object);
                if (node.Arguments.Count > 0)
                {
                    right = ConvertInternal(node.Arguments[0]);
                }
            }
            else
            {
                // 静态方法，如 string.Concat
                left = node.Arguments.Count > 0 ? ConvertInternal(node.Arguments[0]) : null;
                right = node.Arguments.Count > 1 ? ConvertInternal(node.Arguments[1]) : null;
            }

            if (left == null)
            {
                throw new ArgumentException($"无法解析字符串方法调用: {node}");
            }

            switch (methodName)
            {
                case "StartsWith":
                    if (right == null) throw new ArgumentException("StartsWith 方法需要参数");
                    return new BinaryStatement(left, BinaryOperator.StartsWith, right);

                case "EndsWith":
                    if (right == null) throw new ArgumentException("EndsWith 方法需要参数");
                    return new BinaryStatement(left, BinaryOperator.EndsWith, right);

                case "Contains":
                    if (right == null) throw new ArgumentException("Contains 方法需要参数");
                    return new BinaryStatement(left, BinaryOperator.Contains, right);

                case "Concat":
                    if (right == null) throw new ArgumentException("Concat 方法需要参数");
                    return new BinaryStatement(left, BinaryOperator.Concat, right);

                case "Equals":
                    if (right == null) throw new ArgumentException("Equals 方法需要参数");
                    return new BinaryStatement(left, BinaryOperator.Equal, right);
                case "Compare":
                    if (right == null) throw new ArgumentException("Compare 方法需要两个参数");
                    return new BinaryStatement(left, BinaryOperator.Equal, right);
                case "CompareTo":
                    if (right == null) throw new ArgumentException("CompareTo 方法需要参数");
                    return new BinaryStatement(left, BinaryOperator.Equal, right);
                default:
                    return ConvertMethodCallDefault(node, true);
            }
        }

        private Statement HandleEnumerableMethod(MethodCallExpression node)
        {
            var methodName = node.Method.Name;

            if (methodName == "Contains")
            {
                // Enumerable.Contains(source, value)
                if (node.Arguments.Count < 2)
                {
                    throw new ArgumentException("Contains 方法需要两个参数");
                }

                var collection = ConvertInternal(node.Arguments[0]);
                var value = ConvertInternal(node.Arguments[1]);

                if (collection == null || value == null)
                {
                    throw new ArgumentException($"无法解析 Contains 方法调用: {node}");
                }

                // SQL 中是 value IN collection，所以需要反转
                return new BinaryStatement(value, BinaryOperator.In, collection);
            }

            return ConvertMethodCallDefault(node, true);
        }

        private Statement HandleInstanceMethod(MethodCallExpression node)
        {
            var methodName = node.Method.Name;

            // 处理常见实例方法
            if (methodName == "Equals")
            {
                var left = ConvertInternal(node.Object);
                var right = node.Arguments.Count > 0 ? ConvertInternal(node.Arguments[0]) : null;

                if (left == null || right == null)
                {
                    throw new ArgumentException($"无法解析 Equals 方法调用: {node}");
                }

                return new BinaryStatement(left, BinaryOperator.Equal, right);
            }
            else if (methodName == "CompareTo")
            {
                // CompareTo 方法通常在二元表达式中处理
                // 这里返回一个二元表达式，具体比较在 ConvertBinary 中处理
                if (node.Arguments.Count < 1) throw new ArgumentException("CompareTo 方法需要参数");
                var left = ConvertInternal(node.Object);
                var right = ConvertInternal(node.Arguments[0]);

                if (left == null || right == null)
                {
                    throw new ArgumentException($"无法解析 CompareTo 方法调用: {node}");
                }

                // CompareTo 返回比较结果，需要与 0 比较
                // 这里返回相等比较，实际会在二元表达式中替换
                return new BinaryStatement(left, BinaryOperator.Equal, right);
            }
            else
                return ConvertMethodCallDefault(node, methodName != "ToString");
        }

        private Statement ConvertMethodCallDefault(MethodCallExpression node, bool useFunction)
        {
            if (new ParameterExpressionDetector().ContainsParameter(node))
            {
                if (useFunction)
                    // 其他方法作为函数调用
                    return CreateFunctionStatement(node);
                else
                    return ConvertInternal(node.Object);
            }
            else
                return EvaluateExpression(node);
        }

        private Statement HandleStaticMethod(MethodCallExpression node)
        {
            // 处理常见静态方法
            var methodName = node.Method.Name;
            var declaringType = node.Method.DeclaringType;

            if (declaringType == typeof(object) && methodName == "Equals")
            {
                // object.Equals(a, b)
                if (node.Arguments.Count < 2)
                {
                    throw new ArgumentException("object.Equals 需要两个参数");
                }

                var left = ConvertInternal(node.Arguments[0]);
                var right = ConvertInternal(node.Arguments[1]);

                if (left == null || right == null)
                {
                    throw new ArgumentException($"无法解析 object.Equals 方法调用: {node}");
                }

                return new BinaryStatement(left, BinaryOperator.Equal, right);
            }
            else if (methodName == "Compare")
            {
                if (node.Arguments.Count < 2)
                {
                    throw new ArgumentException("Compare 需要两个参数");
                }

                var left = ConvertInternal(node.Arguments[0]);
                var right = ConvertInternal(node.Arguments[1]);

                if (left == null || right == null)
                {
                    throw new ArgumentException($"无法解析 Compare 方法调用: {node}");
                }

                return new BinaryStatement(left, BinaryOperator.Equal, right);
            }

            return ConvertMethodCallDefault(node, true);
        }

        private Statement CreateFunctionStatement(MethodCallExpression node)
        {
            var parameters = new List<Statement>();

            // 添加对象实例（非静态方法）
            if (node.Object != null)
            {
                var obj = ConvertInternal(node.Object);
                if (obj != null)
                {
                    parameters.Add(obj);
                }
            }

            // 添加方法参数
            foreach (var arg in node.Arguments)
            {
                var param = ConvertInternal(arg);
                if (param != null)
                {
                    parameters.Add(param);
                }
            }

            // 方法名作为函数名
            var functionName = node.Method.Name;

            // 特殊处理一些常见方法
            if (node.Method.DeclaringType == typeof(Math))
            {
                // Math 类方法，直接使用原方法名或映射
                functionName = node.Method.Name.ToUpper();
            }

            return new FunctionStatement(functionName, parameters.ToArray());
        }

        #endregion

        #region 辅助方法

        private BinaryOperator ConvertExpressionTypeToBinaryOperator(ExpressionType expressionType)
        {
            return expressionType switch
            {
                ExpressionType.Equal => BinaryOperator.Equal,
                ExpressionType.NotEqual => BinaryOperator.NotEqual,
                ExpressionType.GreaterThan => BinaryOperator.GreaterThan,
                ExpressionType.GreaterThanOrEqual => BinaryOperator.GreaterThanOrEqual,
                ExpressionType.LessThan => BinaryOperator.LessThan,
                ExpressionType.LessThanOrEqual => BinaryOperator.LessThanOrEqual,
                ExpressionType.Add => BinaryOperator.Add,
                ExpressionType.AddChecked => BinaryOperator.Add,
                ExpressionType.Subtract => BinaryOperator.Subtract,
                ExpressionType.SubtractChecked => BinaryOperator.Subtract,
                ExpressionType.Multiply => BinaryOperator.Multiply,
                ExpressionType.MultiplyChecked => BinaryOperator.Multiply,
                ExpressionType.Divide => BinaryOperator.Divide,
                _ => BinaryOperator.Equal
            };
        }

        #endregion
    }


    public class ParameterExpressionDetector : ExpressionVisitor
    {
        private bool _hasParameter = false;
        /// <summary>
        /// 检查表达式中是否包含参数
        /// </summary>
        public bool ContainsParameter(Expression expression)
        {
            _hasParameter = false;
            Visit(expression);
            return _hasParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            _hasParameter = true;
            return base.VisitParameter(node);
        }
    }

    /// <summary>
    /// Expression 到 Statement 的扩展方法
    /// </summary>
    public static class ExpressionToStatementExtensions
    {

        public static List<T> Search<T>(this IObjectViewDAO<T> dao, Expression<Func<T, bool>> expression)
        {
            var condition = expression.ToStatement();
            return dao.Search(condition);
        }

        public static List<T> Search<T>(this IEntityViewService<T> entityViewService, Expression<Func<T, bool>> expression)
        {
            return entityViewService.Search(expression.ToStatement());
        }
        public static T SearchOne<T>(this IEntityViewService<T> entityViewService, Expression<Func<T, bool>> expression)
        {
            return entityViewService.SearchOne(expression.ToStatement());
        }
        public static List<T> SearchSection<T>(this IEntityViewService<T> entityViewService, Expression<Func<T, bool>> expression, SectionSet sectionSet, params string[] tableArgs)
        {
            return entityViewService.SearchSection(expression.ToStatement(), sectionSet, tableArgs);
        }
        /// <summary>
        /// 将 Lambda 表达式转换为 Statement
        /// </summary>
        public static Statement ToStatement<T>(this Expression<Func<T, bool>> expression)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            var converter = new ExpressionStatementConverter(expression.Parameters[0]);
            return converter.Convert(expression.Body);
        }

        /// <summary>
        /// 将属性选择表达式转换为 Statement
        /// </summary>
        public static Statement ToStatement<T, TResult>(this Expression<Func<T, TResult>> expression)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            var converter = new ExpressionStatementConverter(expression.Parameters[0]);
            return converter.Convert(expression.Body);
        }

        /// <summary>
        /// 从表达式中获取属性名
        /// </summary>
        public static string GetPropertyName<T, TProp>(this Expression<Func<T, TProp>> expression)
        {
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));

            if (expression.Body is MemberExpression memberExpression)
            {
                return memberExpression.Member.Name;
            }
            else if (expression.Body is UnaryExpression unaryExpression &&
                     unaryExpression.Operand is MemberExpression unaryMember)
            {
                return unaryMember.Member.Name;
            }

            throw new ArgumentException("表达式必须是属性访问表达式", nameof(expression));
        }

        /// <summary>
        /// 创建属性 Statement
        /// </summary>
        public static Statement Prop<T>(this Expression<Func<T, object>> expression)
        {
            var propertyName = expression.GetPropertyName();
            return Statement.Property(propertyName);
        }

        /// <summary>
        /// 创建属性 Statement（强类型）
        /// </summary>
        public static Statement Prop<T, TProp>(this Expression<Func<T, TProp>> expression)
        {
            var propertyName = expression.GetPropertyName();
            return Statement.Property(propertyName);
        }
    }

    /// <summary>
    /// 简化的查询构建工具
    /// </summary>
    public static class Query
    {
        /// <summary>
        /// 创建属性 Statement
        /// </summary>
        public static Statement Prop<T>(Expression<Func<T, object>> expression)
        {
            return expression.Prop();
        }

        /// <summary>
        /// 创建属性 Statement（强类型）
        /// </summary>
        public static Statement Prop<T, TProp>(Expression<Func<T, TProp>> expression)
        {
            return expression.Prop();
        }

        /// <summary>
        /// 创建条件 Statement
        /// </summary>
        public static Statement Where<T>(Expression<Func<T, bool>> expression)
        {
            return expression.ToStatement();
        }
    }
}