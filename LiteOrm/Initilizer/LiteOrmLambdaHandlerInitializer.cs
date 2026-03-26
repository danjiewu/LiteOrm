using Autofac;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm Lambda 处理器初始化器，负责注册 Lambda 表达式到 Expr 对象的转换句柄。
    /// </summary>
    [AutoRegister(Lifetime = Lifetime.Singleton)]
    public class LiteOrmLambdaHandlerInitializer : IStartable
    {
        /// <summary>
        /// 启动时初始化 Lambda 处理器。
        /// </summary>
        public void Start()
        {
            // 注册 Lambda 表达式转换到 Expr 对象的成员句柄 (如 DateTime.Now)
            RegisterLambdaMemberHandlers();
            // 注册 Lambda 表达式转换到 Expr 对象的方法句柄 (如 StartsWith, Contains)
            RegisterLambdaMethodHandlers();
        }

        /// <summary>
        /// 注册 Lambda 表达式中的成员访问处理器（属性或字段）。
        /// </summary>
        private void RegisterLambdaMemberHandlers()
        {
            LambdaExprConverter.RegisterMemberHandler(typeof(DateTime), "Now");
            LambdaExprConverter.RegisterMemberHandler(typeof(DateTime), "Today");
            LambdaExprConverter.RegisterMemberHandler(typeof(string), "Length");
        }

        /// <summary>
        /// 注册 Lambda 表达式中的方法调用处理器。
        /// </summary>
        private void RegisterLambdaMethodHandlers()
        {
            LambdaExprConverter.RegisterMethodHandler(typeof(DateTime));
            LambdaExprConverter.RegisterMethodHandler(typeof(Math));
            LambdaExprConverter.RegisterMethodHandler(typeof(string));

            LambdaExprConverter.RegisterMethodHandler(typeof(ExprExtensions), nameof(ExprExtensions.To), (node, converter) =>
                converter.Convert(node.Arguments[0])
            );

            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalSeconds), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffSeconds", binaryExpr.Left, binaryExpr.Right);
                return new FunctionExpr("TotalSeconds", timeSpanExpr as ValueTypeExpr);
            });

            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalDays), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffDays", binaryExpr.Left, binaryExpr.Right);
                return new FunctionExpr("TotalDays", timeSpanExpr as ValueTypeExpr);
            });

            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalHours), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffHours", binaryExpr.Left, binaryExpr.Right);
                return new FunctionExpr("TotalHours", timeSpanExpr as ValueTypeExpr);
            });

            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalMinutes), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffMinutes", binaryExpr.Left, binaryExpr.Right);
                return new FunctionExpr("TotalMinutes", timeSpanExpr as ValueTypeExpr);
            });

            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalMilliseconds), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffMilliseconds", binaryExpr.Left, binaryExpr.Right);
                return new FunctionExpr("TotalMilliseconds", timeSpanExpr as ValueTypeExpr);
            });

            LambdaExprConverter.RegisterMethodHandler(typeof(string), nameof(string.StartsWith), (node, converter) =>
            {
                var left = converter.Convert(node.Object) as ValueTypeExpr;
                var right = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicOperator.StartsWith, right);
            });

            LambdaExprConverter.RegisterMethodHandler(typeof(string), nameof(string.EndsWith), (node, converter) =>
            {
                var left = converter.Convert(node.Object) as ValueTypeExpr;
                var right = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicOperator.EndsWith, right);
            });

            LambdaExprConverter.RegisterMethodHandler(typeof(string), nameof(string.Contains), (node, converter) =>
            {
                var left = converter.Convert(node.Object) as ValueTypeExpr;
                var right = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicOperator.Contains, right);
            });

            LambdaExprConverter.RegisterMethodHandler(nameof(IList.Contains), (node, converter) =>
            {
                if (node.Method.DeclaringType == typeof(Enumerable) || typeof(IEnumerable).IsAssignableFrom(node.Method.DeclaringType))
                {
                    ValueTypeExpr collection = null;
                    ValueTypeExpr value = null;
                    if (node.Method.IsStatic)
                    {
                        collection = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                        value = converter.Convert(node.Arguments[1]) as ValueTypeExpr;
                    }
                    else
                    {
                        collection = converter.Convert(node.Object) as ValueTypeExpr;
                        value = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                    }
                    return new LogicBinaryExpr(value, LogicOperator.In, collection);
                }
                return null;
            });

            LambdaExprConverter.RegisterMethodHandler(typeof(string), nameof(string.Concat), (node, converter) =>
            {
                List<ValueTypeExpr> args = new List<ValueTypeExpr>();
                if (node.Object != null) args.Add(converter.Convert(node.Object) as ValueTypeExpr);

                if (node.Arguments.Count == 1)
                {
                    var arg = converter.Convert(node.Arguments[0]);
                    if (arg is IEnumerable<ValueTypeExpr> enumerable)
                        args.AddRange(enumerable);
                    else
                        args.Add(arg as ValueTypeExpr);
                }
                else
                {
                    foreach (var arg in node.Arguments)
                    {
                        args.Add(converter.Convert(arg) as ValueTypeExpr);
                    }
                }
                return new ValueSet(ValueJoinType.Concat, args);
            });

            LambdaExprConverter.RegisterMethodHandler(nameof(Equals), (node, converter) =>
            {
                ValueTypeExpr left = null;
                ValueTypeExpr right = null;
                if (node.Object != null)
                {
                    left = converter.Convert(node.Object) as ValueTypeExpr;
                    right = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                }
                else
                {
                    left = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                    right = converter.Convert(node.Arguments[1]) as ValueTypeExpr;
                }
                return new LogicBinaryExpr(left, LogicOperator.Equal, right);
            });

            LambdaExprConverter.RegisterMethodHandler(nameof(ToString), (node, converter) =>
            {
                if (node.Arguments.Count > 0)
                {
                    var obj = converter.Convert(node.Object) as ValueTypeExpr;
                    var format = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                    if (obj is not null && format is not null)
                        return new FunctionExpr("Format", obj, format);
                }
                return converter.Convert(node.Object);
            });

        }
    }
}
