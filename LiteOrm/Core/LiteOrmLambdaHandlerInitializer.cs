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
                var left = converter.Convert(node.Object);
                var right = converter.Convert(node.Arguments[0]);
                return new BinaryExpr(left, BinaryOperator.StartsWith, right);
            });

            LambdaExprConverter.RegisterMethodHandler(typeof(string), "EndsWith", (node, converter) =>
            {
                var left = converter.Convert(node.Object);
                var right = converter.Convert(node.Arguments[0]);
                return new BinaryExpr(left, BinaryOperator.EndsWith, right);
            });

            LambdaExprConverter.RegisterMethodHandler(typeof(string), "Contains", (node, converter) =>
            {
                var left = converter.Convert(node.Object);
                var right = converter.Convert(node.Arguments[0]);
                return new BinaryExpr(left, BinaryOperator.Contains, right);
            });

            LambdaExprConverter.RegisterMethodHandler("Contains", (node, converter) =>
            {
                if (node.Method.DeclaringType == typeof(Enumerable) || typeof(IEnumerable).IsAssignableFrom(node.Method.DeclaringType))
                {
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

            LambdaExprConverter.RegisterMethodHandler(typeof(string), "Concat", (node, converter) =>
            {
                List<Expr> args = new List<Expr>();
                if (node.Object != null) args.Add(converter.Convert(node.Object));

                if (node.Arguments.Count == 1)
                {
                    var arg = converter.Convert(node.Arguments[0]);
                    if (arg is IEnumerable<Expr> enumerable)
                        args.AddRange(enumerable);
                    else
                        args.Add(arg);
                }
                else
                {
                    foreach (var arg in node.Arguments)
                    {
                        args.Add(converter.Convert(arg));
                    }
                }
                return new ExprSet(ExprJoinType.Concat, args);
            });

            LambdaExprConverter.RegisterMethodHandler("Equals", (node, converter) =>
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

            LambdaExprConverter.RegisterMethodHandler("ToString", (node, converter) =>
            {
                return converter.Convert(node.Object);
            });

            LambdaExprConverter.RegisterMethodHandler("Compare", (node, converter) =>
            {
                var left = converter.Convert(node.Arguments[0]);
                var right = converter.Convert(node.Arguments[1]);
                return new BinaryExpr(left, BinaryOperator.Equal, right);
            });

            LambdaExprConverter.RegisterMethodHandler("CompareTo", (node, converter) =>
            {
                var left = node.Object != null ? converter.Convert(node.Object) : converter.Convert(node.Arguments[0]);
                var right = node.Object != null ? converter.Convert(node.Arguments[0]) : converter.Convert(node.Arguments[1]);
                return new BinaryExpr(left, BinaryOperator.Equal, right);
            });
        }
    }
}
