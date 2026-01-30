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
    [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
    public class LiteOrmLambdaHandlerInitializer : IComponentInitializer
    {
        /// <summary>
        /// 使用指定的组件上下文初始化 Lambda 处理器。
        /// </summary>
        /// <param name="componentContext">用于解析服务的组件上下文。</param>
        public void Initialize(IComponentContext componentContext)
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

            LambdaExprConverter.RegisterMethodHandler(typeof(string), "StartsWith", (node, converter) =>
            {
                var left = converter.Convert(node.Object) as ValueTypeExpr;
                var right = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicBinaryOperator.StartsWith, right);
            });

            LambdaExprConverter.RegisterMethodHandler(typeof(string), "EndsWith", (node, converter) =>
            {
                var left = converter.Convert(node.Object) as ValueTypeExpr;
                var right = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicBinaryOperator.EndsWith, right);
            });

            LambdaExprConverter.RegisterMethodHandler(typeof(string), "Contains", (node, converter) =>
            {
                var left = converter.Convert(node.Object) as ValueTypeExpr;
                var right = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicBinaryOperator.Contains, right);
            });

            LambdaExprConverter.RegisterMethodHandler("Contains", (node, converter) =>
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
                    return new LogicBinaryExpr(value, LogicBinaryOperator.In, collection);
                }
                return null;
            });

            LambdaExprConverter.RegisterMethodHandler(typeof(string), "Concat", (node, converter) =>
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
                return new ValueExprSet(ValueJoinType.Concat, args);
            });

            LambdaExprConverter.RegisterMethodHandler("Equals", (node, converter) =>
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
                return new LogicBinaryExpr(left, LogicBinaryOperator.Equal, right);
            });

            LambdaExprConverter.RegisterMethodHandler("ToString", (node, converter) =>
            {
                return converter.Convert(node.Object);
            });

            LambdaExprConverter.RegisterMethodHandler("Compare", (node, converter) =>
            {
                var left = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                var right = converter.Convert(node.Arguments[1]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicBinaryOperator.Equal, right);
            });

            LambdaExprConverter.RegisterMethodHandler("CompareTo", (node, converter) =>
            {
                var left = node.Object != null ? converter.Convert(node.Object) as ValueTypeExpr : converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                var right = node.Object != null ? converter.Convert(node.Arguments[0]) as ValueTypeExpr : converter.Convert(node.Arguments[1]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicBinaryOperator.Equal, right);
            });
        }
    }
}
