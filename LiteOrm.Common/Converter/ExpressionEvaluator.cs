using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace LiteOrm.Common
{
    internal static class ExpressionEvaluator
    {
        /// <summary>
        /// 递归解释执行表达式树，返回计算结果。
        /// 覆盖常见的常量、成员访问、方法调用、一元/二元运算、条件、构造等。
        /// </summary> 
        public static object? Evaluate(Expression expr)
        {
            if (expr == null) throw new ArgumentNullException(nameof(expr));

            switch (expr)
            {
                case ConstantExpression constant:
                    return constant.Value;

                case MemberExpression member:
                    return EvaluateMember(member);

                case MethodCallExpression methodCall:
                    return EvaluateMethodCall(methodCall);

                case UnaryExpression unary:
                    return EvaluateUnary(unary);

                case BinaryExpression binary:
                    return EvaluateBinary(binary);

                case ConditionalExpression conditional:
                    return EvaluateConditional(conditional);

                case NewExpression newExpr:
                    return EvaluateNew(newExpr);

                case NewArrayExpression newArray:
                    return EvaluateNewArray(newArray);

                case LambdaExpression lambda:
                    throw new NotSupportedException("Direct evaluation of LambdaExpression is not supported.");

                default:
                    throw new NotSupportedException($"Unsupported expression type: {expr.GetType().Name}");
            }
        }

        // ---------- 成员访问 ----------
        private static object? EvaluateMember(MemberExpression member)
        {
            object? instance = null;
            if (member.Expression != null)
                instance = Evaluate(member.Expression);

            switch (member.Member)
            {
                case FieldInfo field:
                    return field.GetValue(instance);
                case PropertyInfo property:
                    return property.GetValue(instance);
                default:
                    throw new InvalidOperationException($"Unsupported member type: {member.Member.GetType().Name}");
            }
        }

        // ---------- 方法调用 ----------
        private static object? EvaluateMethodCall(MethodCallExpression methodCall)
        {
            object? instance = null;
            if (methodCall.Object != null)
                instance = Evaluate(methodCall.Object);

            var args = new object?[methodCall.Arguments.Count];
            for (int i = 0; i < args.Length; i++)
                args[i] = Evaluate(methodCall.Arguments[i]);

            try
            {
                return methodCall.Method.Invoke(instance, args);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        // ---------- 一元运算 ----------
        private static object? EvaluateUnary(UnaryExpression unary)
        {
            var operand = Evaluate(unary.Operand);

            switch (unary.NodeType)
            {
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.TypeAs:
                    // 如果显式提供了转换方法，则调用；否则用 Convert.ChangeType
                    if (unary.Method != null)
                        return unary.Method.Invoke(null, new[] { operand });
                    if (operand == null) return null;
                    return Convert.ChangeType(operand, unary.Type);

                case ExpressionType.Not when unary.Type == typeof(bool):
                    return !(bool)operand!;

                case ExpressionType.Not:
                    // 按位非（整数）
                    return EvaluateBitwiseNot(operand);

                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                    return EvaluateNegate(operand, unary.NodeType == ExpressionType.NegateChecked);

                case ExpressionType.UnaryPlus:
                    return operand;

                case ExpressionType.Increment:
                    return EvaluateIncrementDecrement(operand, increment: true);

                case ExpressionType.Decrement:
                    return EvaluateIncrementDecrement(operand, increment: false);

                case ExpressionType.OnesComplement:
                    return EvaluateBitwiseNot(operand);

                default:
                    throw new NotSupportedException($"Unsupported unary operator: {unary.NodeType}");
            }
        }

        // ---------- 二元运算 ----------
        private static object? EvaluateBinary(BinaryExpression binary)
        {
            var left = Evaluate(binary.Left);
            var right = Evaluate(binary.Right);

            // 如果用户自定义了运算符（如 operator +），直接调用
            if (binary.Method != null)
                return binary.Method.Invoke(null, new[] { left, right });

            // 处理 null 合并
            if (binary.NodeType == ExpressionType.Coalesce)
                return left ?? right;

            // 处理字符串连接
            if (binary.NodeType == ExpressionType.Add && (binary.Left.Type == typeof(string) || binary.Right.Type == typeof(string)))
                return (left?.ToString() ?? "") + (right?.ToString() ?? "");

            // 比较与相等性
            if (IsComparison(binary.NodeType))
                return EvaluateComparison(left, right, binary.NodeType);

            // 算术/位运算（整数与浮点）
            return EvaluateArithmeticOrBitwise(left, right, binary.NodeType);
        }

        // ---------- 条件表达式 ----------
        private static object? EvaluateConditional(ConditionalExpression conditional)
        {
            var test = (bool)Evaluate(conditional.Test)!;
            return Evaluate(test ? conditional.IfTrue : conditional.IfFalse);
        }

        // ---------- 新建对象 ----------
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Expression evaluation is only used under JIT (LiteOrm does not call this path under AOT); the IL3050 warning from Activator.CreateInstance does not apply here.")]
        [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Expression evaluation is only used under JIT (LiteOrm does not call this path under AOT); newExpr.Type constructors are naturally available under JIT.")]
#endif
        private static object? EvaluateNew(NewExpression newExpr)
        {
            var args = new object?[newExpr.Arguments.Count];
            for (int i = 0; i < args.Length; i++)
                args[i] = Evaluate(newExpr.Arguments[i]);

            if (newExpr.Constructor != null)
                return newExpr.Constructor.Invoke(args);

            return Activator.CreateInstance(newExpr.Type, args);
        }

        // ---------- 新建数组 ----------
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "AOT safe for primitive types")]
#endif
        private static object? EvaluateNewArray(NewArrayExpression newArray)
        {
            var elementType = newArray.Type.GetElementType()!;
#if NET10_0_OR_GREATER
            Type arrayType = elementType.MakeArrayType();
            var array = Array.CreateInstanceFromArrayType(arrayType, newArray.Expressions.Count);
#else
            var array = Array.CreateInstance(elementType, newArray.Expressions.Count);
#endif
            for (int i = 0; i < newArray.Expressions.Count; i++)
                array.SetValue(Evaluate(newArray.Expressions[i]), i);
            return array;
        }

        // ==================== 辅助运算函数 ====================

        private static bool IsComparison(ExpressionType nodeType)
        {
            return nodeType switch
            {
                ExpressionType.Equal or ExpressionType.NotEqual or
                ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual or
                ExpressionType.LessThan or ExpressionType.LessThanOrEqual => true,
                _ => false
            };
        }

        private static object? EvaluateComparison(object? left, object? right, ExpressionType op)
        {
            // 处理 null 情况
            if (left == null && right == null)
                return op == ExpressionType.Equal || op == ExpressionType.GreaterThanOrEqual || op == ExpressionType.LessThanOrEqual;
            if (left == null || right == null)
                return op == ExpressionType.NotEqual;

            // 尝试数值比较
            if (IsNumeric(left) && IsNumeric(right))
            {
                var dl = Convert.ToDouble(left);
                var dr = Convert.ToDouble(right);
                return op switch
                {
                    ExpressionType.Equal => dl == dr,
                    ExpressionType.NotEqual => dl != dr,
                    ExpressionType.GreaterThan => dl > dr,
                    ExpressionType.GreaterThanOrEqual => dl >= dr,
                    ExpressionType.LessThan => dl < dr,
                    ExpressionType.LessThanOrEqual => dl <= dr,
                    _ => throw new NotSupportedException()
                };
            }

            // 其他类型使用 IComparable 或 Equals
            if (left is IComparable cl && right is IComparable)
            {
                int cmp = cl.CompareTo(right);
                return op switch
                {
                    ExpressionType.Equal => cmp == 0,
                    ExpressionType.NotEqual => cmp != 0,
                    ExpressionType.GreaterThan => cmp > 0,
                    ExpressionType.GreaterThanOrEqual => cmp >= 0,
                    ExpressionType.LessThan => cmp < 0,
                    ExpressionType.LessThanOrEqual => cmp <= 0,
                    _ => throw new NotSupportedException()
                };
            }

            // fallback to Equals
            if (op == ExpressionType.Equal) return left.Equals(right);
            if (op == ExpressionType.NotEqual) return !left.Equals(right);
            throw new NotSupportedException($"Comparison {op} not supported between {left.GetType()} and {right.GetType()}");
        }

        private static object? EvaluateArithmeticOrBitwise(object? left, object? right, ExpressionType op)
        {
            // 只处理数值类型和位移
            if (IsInteger(left) && IsInteger(right))
            {
                // 统一转为 long 进行计算（注意 unchecked/checked 语义，这里简单处理为不检查溢出）
                long l = Convert.ToInt64(left);
                long r = Convert.ToInt64(right);
                return op switch
                {
                    ExpressionType.Add => l + r,
                    ExpressionType.AddChecked => checked(l + r),
                    ExpressionType.Subtract => l - r,
                    ExpressionType.SubtractChecked => checked(l - r),
                    ExpressionType.Multiply => l * r,
                    ExpressionType.MultiplyChecked => checked(l * r),
                    ExpressionType.Divide => l / r,
                    ExpressionType.Modulo => l % r,
                    ExpressionType.And => l & r,
                    ExpressionType.Or => l | r,
                    ExpressionType.ExclusiveOr => l ^ r,
                    ExpressionType.LeftShift => l << (int)r,
                    ExpressionType.RightShift => l >> (int)r,
                    _ => throw new NotSupportedException($"Arithmetic operator {op} not supported")
                };
            }

            if (IsFloatingPoint(left) || IsFloatingPoint(right))
            {
                double l = Convert.ToDouble(left);
                double r = Convert.ToDouble(right);
                return op switch
                {
                    ExpressionType.Add => l + r,
                    ExpressionType.AddChecked => l + r,
                    ExpressionType.Subtract => l - r,
                    ExpressionType.SubtractChecked => l - r,
                    ExpressionType.Multiply => l * r,
                    ExpressionType.MultiplyChecked => l * r,
                    ExpressionType.Divide => l / r,
                    ExpressionType.Modulo => l % r,
                    _ => throw new NotSupportedException($"Floating point operator {op} not supported")
                };
            }

            throw new NotSupportedException($"Arithmetic/bitwise operator {op} not supported between {left?.GetType()} and {right?.GetType()}");
        }

        private static object EvaluateNegate(object? operand, bool isChecked)
        {
            if (operand == null) throw new ArgumentNullException(nameof(operand));
            if (isChecked)
            {
                return operand switch
                {
                    int v => checked(-v),
                    long v => checked(-v),
                    float v => -v,
                    double v => -v,
                    decimal v => -v,
                    _ => throw new NotSupportedException($"Negate not supported for type {operand.GetType()}")
                };
            }
            else
            {
                return operand switch
                {
                    int v => -v,
                    long v => -v,
                    float v => -v,
                    double v => -v,
                    decimal v => -v,
                    _ => throw new NotSupportedException($"Negate not supported for type {operand.GetType()}")
                };
            }
        }

        private static object EvaluateBitwiseNot(object? operand)
        {
            if (operand == null) throw new ArgumentNullException(nameof(operand));
            return operand switch
            {
                int v => ~v,
                uint v => ~v,
                long v => ~v,
                ulong v => ~v,
                short v => ~v,
                ushort v => ~v,
                byte v => ~v,
                sbyte v => ~v,
                _ => throw new NotSupportedException($"Bitwise NOT not supported for type {operand.GetType()}")
            };
        }

        private static object EvaluateIncrementDecrement(object? operand, bool increment)
        {
            if (operand == null) throw new ArgumentNullException(nameof(operand));
            return operand switch
            {
                int v => increment ? v + 1 : v - 1,
                long v => increment ? v + 1 : v - 1,
                uint v => increment ? v + 1 : v - 1,
                ulong v => increment ? v + 1 : v - 1,
                short v => increment ? (short)(v + 1) : (short)(v - 1),
                ushort v => increment ? (ushort)(v + 1) : (ushort)(v - 1),
                byte v => increment ? (byte)(v + 1) : (byte)(v - 1),
                sbyte v => increment ? (sbyte)(v + 1) : (sbyte)(v - 1),
                float v => increment ? v + 1 : v - 1,
                double v => increment ? v + 1 : v - 1,
                decimal v => increment ? v + 1 : v - 1,
                _ => throw new NotSupportedException($"Increment/Decrement not supported for type {operand.GetType()}")
            };
        }

        private static bool IsNumeric(object? value)
        {
            return value is byte or sbyte or short or ushort or int or uint or long or ulong
                or float or double or decimal;
        }

        private static bool IsInteger(object? value)
        {
            return value is byte or sbyte or short or ushort or int or uint or long or ulong;
        }

        private static bool IsFloatingPoint(object? value)
        {
            return value is float or double or decimal;
        }
    }
}