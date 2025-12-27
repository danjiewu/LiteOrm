using System;
using System.Collections.Generic;
using System.Text;
using MyOrm.Common;
using System.Text.RegularExpressions;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;

namespace MyOrm
{
    public sealed class ExpressionParser : ExpressionVisitor
    {
        #region 默认构造函数

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ExpressionParser(SqlBuilder sqlBuilder, SqlBuildContext context)
        {
            this.Arguments = new SortedList<string, object>();
            SqlBuilder = sqlBuilder;
            SqlBuildContext = context;
        }
        #endregion

        #region 属性
        /// <summary>
        /// 参数
        /// </summary>
        public SortedList<string, object> Arguments { get; private set; }

        /// <summary>
        /// 参数开始索引
        /// </summary>
        public int ArgumentsStartIndex { get; set; }

        /// <summary>
        ///  返回值
        /// </summary>
        public string Result
        {
            get
            {
                return LastNode?.Result;
            }
        }

        private Stack<StackNode> functionStack = new Stack<StackNode>();
        private Stack<StackNode> resultStack = new Stack<StackNode>();
        private StackNode CurrentNode
        {
            get
            {
                return functionStack.Count > 0 ? functionStack.Peek() : null;
            }
        }

        public StackNode LastNode
        {
            get
            {
                return resultStack.Count > 0 ? resultStack.Peek() : null;
            }
        }

        private SqlBuilder SqlBuilder;
        private SqlBuildContext SqlBuildContext;

        #endregion

        public override Expression Visit(Expression node)
        {
            functionStack.Push(new StackNode() { Expression = node });
            var exp = base.Visit(node);
            if (CurrentNode.Result == null) { CurrentNode.Result = LastNode?.Result; }
            resultStack.Push(CurrentNode);
            functionStack.Pop();
            return exp;
        }
        private string BinaryExpressionResult(string left, ExpressionType type, string right, Type returnType)
        {
            switch (type)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    return String.Format("({0}) and ({1})", left, right);
                case ExpressionType.Equal:
                    if (right == "null")
                    {
                        return String.Format("{0} is null", left);
                    }
                    else if (left == "null")
                    {
                        return String.Format("{0} is null", right);
                    }
                    else
                    {
                        return String.Format("{0}={1}", left, right);
                    }
                case ExpressionType.GreaterThan:
                    return String.Format("{0}>{1}", left, right);
                case ExpressionType.GreaterThanOrEqual:
                    return String.Format("{0}>={1}", left, right);
                case ExpressionType.LessThan:
                    return String.Format("{0}<{1}", left, right);
                case ExpressionType.LessThanOrEqual:
                    return String.Format("{0}<={1}", left, right);
                case ExpressionType.NotEqual:
                    if (right == "null")
                    {
                        return String.Format("{0} is not null", left);
                    }
                    else if (left == "null")
                    {
                        return String.Format("{0} is not null", right);
                    }
                    else
                    {
                        return String.Format("{0}<>{1}", left, right);
                    }
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return String.Format("({0}) or ({1})", left, right);
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                    if (returnType == typeof(string))
                    {
                        return SqlBuilder.ConcatSql(left, right);
                    }
                    return String.Format("{0}+{1}", left, right);
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                    return String.Format("{0}-{1}", left, right);
                case ExpressionType.Divide:
                    return String.Format("{0}/{1}", left, right);
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                    return String.Format("{0}*{1}", left, right);
                default:
                    return String.Format("{0} {1}", left, right); ;
            }
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            if (node.NodeType == ExpressionType.Not)
            {
                if (node.Operand is MethodCallExpression)
                {
                    this.VisitMethodCall(node.Operand as MethodCallExpression);
                }
                if (CurrentNode.Result == null)
                    CurrentNode.Result = $"not ({LastNode.Result})";
                return node;
            }
            else if (node.NodeType == ExpressionType.Convert)
            {
                this.Visit(node.Operand);
                if (Arguments.ContainsKey(LastNode.Result))
                {
                    if (Arguments[LastNode.Result] != null)
                        Arguments[LastNode.Result] = Convert.ChangeType(Arguments[LastNode.Result], Nullable.GetUnderlyingType(node.Type) ?? node.Type);
                }
                return node;
            }
            else
            {
                return base.VisitUnary(node);
            }
        }

        #region 重写 二元操作符
        /// <summary>
        /// 重写 二元操作符
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        protected sealed override Expression VisitBinary(BinaryExpression node)
        {
            Type returnType = ResolveExpressionType(node);
            //特殊处理CompareTo函数
            MethodCallExpression compareToExp = null;
            //CompareTo表达式位置
            bool reverse = false;
            if (IsCompareToMethodCall(node.Left))
            {
                compareToExp = node.Left as MethodCallExpression;
                ConstantExpression valueExp = node.Right as ConstantExpression;
                if (valueExp == null || !Equals(valueExp.Value, 0))
                {
                    throw new InvalidConstraintException("Compare函数只支持与0比较");
                }
            }
            else if (IsCompareToMethodCall(node.Right))
            {
                compareToExp = node.Right as MethodCallExpression;
                reverse = true;
                ConstantExpression valueExp = node.Left as ConstantExpression;
                if (valueExp == null || !Equals(valueExp.Value, 0))
                {
                    throw new InvalidConstraintException("Compare函数只支持与0比较");
                }
            }
            if (compareToExp != null)
            {
                Expression field;
                Expression par = null;

                if (compareToExp.Object != null)
                {
                    field = compareToExp.Object;
                    if (compareToExp.Arguments.Count > 0)
                    {
                        par = compareToExp.Arguments[0];
                    }
                    else
                    {
                        throw new InvalidExpressionException("Compare函数必须有一个参数");
                    }
                }
                else
                {
                    if (compareToExp.Arguments.Count < 2)
                    {
                        throw new InvalidExpressionException("静态Compare函数必须有两个参数");
                    }
                    field = compareToExp.Arguments[0];
                    par = compareToExp.Arguments[1];
                }

                this.Visit(field);
                var left = LastNode.Result;
                this.Visit(par);
                var right = LastNode.Result;

                //CompareTo函数在右侧，则调转左右侧对象
                if (reverse)
                {
                    CurrentNode.Result = BinaryExpressionResult(right, node.NodeType, left, returnType);
                }
                else
                {
                    CurrentNode.Result = BinaryExpressionResult(left, node.NodeType, right, returnType);
                }
            }
            else
            {
                this.Visit(node.Left);
                var left = LastNode.Result;

                this.Visit(node.Right);
                var right = LastNode.Result;

                CurrentNode.Result = BinaryExpressionResult(left, node.NodeType, right, returnType);
            }
            return node;
        }

        /// <summary>
        /// 类型优先级字典：Key=类型，Value=优先级分数（分数越高优先级越高）
        /// 直接定义映射关系，后续修改/扩展只需调整字典
        /// </summary>
        private static readonly Dictionary<Type, int> _typePriorityDict = new Dictionary<Type, int>()
    {
        { typeof(string), 100 },   // string 优先级最高
        { typeof(decimal), 90 },
        { typeof(double), 80 },
        { typeof(float), 70 },
        { typeof(long), 60 },
        { typeof(int), 50 },
        { typeof(short), 40 },
        { typeof(byte), 30 },
        { typeof(bool), 20 }
    };

        /// <summary>
        /// 定义类型优先级（通过字典查询获取分数）
        /// </summary>
        private static int GetTypePriority(Type type)
        {
            if (type == null)
                return 0; // 未知类型优先级最低

            // 特殊处理：可空类型（如 string?、int?）取其基础类型
            type = Nullable.GetUnderlyingType(type) ?? type;

            // 字典查询：存在则返回对应分数，不存在返回默认值 10
            return _typePriorityDict.TryGetValue(type, out int priority)
                ? priority
                : 10;
        }

        /// <summary>
        /// 解析任意Expression的值类型（递归处理嵌套表达式）
        /// 嵌套BinaryExpression时，返回左右两边优先级更高的类型（string最高）
        /// </summary>
        /// <param name="expression">要解析的表达式</param>
        /// <returns>表达式对应的值类型（无法解析时返回null）</returns>
        public static Type ResolveExpressionType(Expression expression)
        {
            if (expression == null)
                return null;

            // 1. 常量表达式（直接值，如 123、"abc"、null）
            if (expression is ConstantExpression constantExpr)
            {
                return constantExpr.Value?.GetType() ?? constantExpr.Type;
            }

            // 2. 字段/属性访问表达式（如 user.Name、order.TotalAmount）
            if (expression is MemberExpression memberExpr)
            {
                return memberExpr.Member is FieldInfo field
                    ? field.FieldType
                    : (memberExpr.Member as PropertyInfo)?.PropertyType;
            }

            // 3. 参数表达式（如方法参数 x、lambda 参数 item）
            if (expression is ParameterExpression paramExpr)
            {
                return paramExpr.Type;
            }

            // 4. 一元表达式（如 !flag、(int)num、(string?)null）
            if (expression is UnaryExpression unaryExpr)
            {
                return ResolveExpressionType(unaryExpr.Operand);
            }

            // 5. 嵌套二进制表达式（核心修改：取优先级更高的类型）
            if (expression is BinaryExpression binaryExpr)
            {
                var leftType = ResolveExpressionType(binaryExpr.Left);
                var rightType = ResolveExpressionType(binaryExpr.Right);

                // 计算左右类型的优先级分数
                int leftPriority = GetTypePriority(leftType);
                int rightPriority = GetTypePriority(rightType);

                // 返回优先级更高的类型（分数高的）；分数相同则返回左边（可按需调整）
                return leftPriority >= rightPriority ? leftType : rightType;
            }

            // 其他表达式类型（可按需扩展）
            return null;
        }
        #endregion

        protected override Expression VisitListInit(ListInitExpression node)
        {
            List<string> result = new List<string>();
            foreach (var init in node.Initializers)
            {
                this.Visit(init.Arguments);
                result.Add(LastNode.Result);
            }
            CurrentNode.Result = "(" + String.Join(",", result) + ")";
            return node;
        }

        private bool IsCompareToMethodCall(Expression exp)
        {
            return exp is MethodCallExpression && (((MethodCallExpression)exp).Method.Name == "CompareTo" || ((MethodCallExpression)exp).Method.Name == "Compare");
        }

        #region 重写常量

        /// <summary>
        /// 重写常量
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        protected sealed override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value == null)
            {
                CurrentNode.Result = "null";
            }
            else
            {
                string parName = AddArgument(node.Value);
                CurrentNode.Result = parName;
            }
            return node;
        }

        public string AddArgument(object argValue)
        {
            var parName = SqlBuilder.ToSqlParam((this.Arguments.Count + this.ArgumentsStartIndex).ToString());
            this.Arguments.Add(parName, argValue);
            return parName;
        }
        #endregion

        #region 重写 字段 属性
        /// <summary>
        /// 重写 字段 属性
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>00.
        protected sealed override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ParameterExpression)
            {
                PropertyInfo propertyInfo = node.Member as PropertyInfo;
                if (propertyInfo == null)
                {
                    return node;
                }
                CurrentNode.Result = GetColumnExpression(propertyInfo);
            }
            else
            {
                if (node.Expression != null)
                {
                    Visit(node.Expression);
                    if (this.Arguments.ContainsKey(LastNode.Result))
                    {
                        object memberValue;
                        if (node.Member is FieldInfo)
                        {
                            var fieldInfo = node.Member as FieldInfo;
                            memberValue = fieldInfo.GetValue(this.Arguments[LastNode.Result]);
                        }
                        else
                        {
                            var propertyInfo = node.Member as PropertyInfo;
                            memberValue = propertyInfo.GetValue(this.Arguments[LastNode.Result]);
                        }
                        this.Arguments[LastNode.Result] = memberValue;
                    }
                }
                else
                {
                    object memberValue;
                    if (node.Member is FieldInfo)
                    {
                        var fieldInfo = node.Member as FieldInfo;
                        memberValue = fieldInfo.GetValue(null);
                    }
                    else
                    {
                        var propertyInfo = node.Member as PropertyInfo;
                        memberValue = propertyInfo.GetValue(null);
                    }
                    string parName = AddArgument(memberValue);
                    CurrentNode.Result = parName;
                }
            }
            return node;

        }
        #endregion

        private string GetColumnExpression(PropertyInfo property)
        {
            Column col = SqlBuildContext.Table.GetColumn(property.Name);
            if (col == null) throw new ArgumentException("属性不存在", property.Name);
            if (SqlBuildContext.SingleTable)
            {
                return col.FormattedName(SqlBuilder);
            }
            else
            {
                return col.FormattedExpression(SqlBuilder);
            }
        }

        #region 重写方法处理

        /// <summary>
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        protected sealed override Expression VisitMethodCall(MethodCallExpression node)
        {
            Expression field = null;
            Expression par = null;

            if (node.Object != null)
            {
                field = node.Object;
                if (node.Arguments.Count > 0)
                {
                    par = node.Arguments[0];
                }
            }
            else
            {
                if (node.Arguments.Count > 0)
                    field = node.Arguments[0];
                if (node.Arguments.Count > 1)
                    par = node.Arguments[1];
            }

            MethodCall(node.Method, field, par);
            if (CurrentNode.Result == null)
            {
                LambdaExpression lambda = Expression.Lambda(node);
                var func = lambda.Compile();
                CurrentNode.Result = AddArgument(func.DynamicInvoke());
            }
            return node;
        }

        /// <summary>
        /// 自定义方法和公用方法处理
        /// </summary>
        /// <param name="methodName">方法名称</param>
        private void MethodCall(MethodInfo method, Expression leftExp, Expression rightExp)
        {
            var methodName = method.Name;
            if (methodName == "Equals" || methodName == "EndsWith" || methodName == "StartsWith" || methodName == "Contains")
            {
                this.Visit(leftExp);
                var left = LastNode.Result;
                this.Visit(rightExp);
                var right = LastNode.Result;

                bool opposite = false;

                if (CurrentNode.Expression is UnaryExpression && CurrentNode.Expression.NodeType == ExpressionType.Not)
                {
                    opposite = true;
                }

                switch (methodName)//系统级
                {
                    case "Equals":
                        CurrentNode.Result = String.Format("{0} {1} {2}", left, opposite ? "<>" : "=", right);
                        break;
                    case "StartsWith":
                        CurrentNode.Result = String.Format("{0} {1}like {2} escape '{3}'", left, opposite ? "not " : "", SqlBuilder.ConcatSql(right, "'%'"), SqlBuilder.LikeEscapeChar);
                        break;
                    case "EndsWith":
                        CurrentNode.Result = String.Format("{0} {1}like {2} escape '{3}'", left, opposite ? "not " : "", SqlBuilder.ConcatSql("'%'", right), SqlBuilder.LikeEscapeChar);
                        break;
                    case "Contains":
                        if (method.DeclaringType != typeof(string))
                        {
                            if (this.Arguments.ContainsKey(left) && Arguments[left] is IEnumerable && !(Arguments[left] is String))
                            {
                                var value = Arguments[left];
                                this.Arguments.Remove(left);
                                var sb = new StringBuilder();
                                var ls = "";
                                foreach (var item in (value as IEnumerable))
                                {
                                    var parName = AddArgument(item);
                                    sb.Append(ls + parName);
                                    ls = ",";
                                }

                                if (!string.IsNullOrEmpty(sb.ToString()))
                                    CurrentNode.Result = string.Format("{0} {1}in ({2})", right, opposite ? "not " : "", sb);
                                else
                                    CurrentNode.Result = string.Format("{0} is {1}null", right, opposite ? "not " : "");
                            }
                            else
                            {
                                CurrentNode.Result = string.Format("{0} {1}in {2}", right, opposite ? "not " : "", left);
                            }
                        }
                        else
                        {
                            CurrentNode.Result = String.Format("{0} {1}like {2} escape '{3}'", left, opposite ? "not " : "", SqlBuilder.ConcatSql("'%'", right, "'%'"), SqlBuilder.LikeEscapeChar);
                        }
                        break;
                }
            }

        }
    }

    #endregion

    public class StackNode
    {
        public Expression Expression { get; set; }
        public string Result { get; set; }
    }
}


