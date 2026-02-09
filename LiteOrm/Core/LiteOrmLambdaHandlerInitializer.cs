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
    /// LiteOrm Lambda ´¦ÀíÆ÷³õÊ¼»¯Æ÷£¬¸ºÔð×¢²á Lambda ±í´ïÊ½µ½ Expr ¶ÔÏóµÄ×ª»»¾ä±ú¡£
    /// </summary>
    [AutoRegister(Lifetime = ServiceLifetime.Singleton)]
    public class LiteOrmLambdaHandlerInitializer : IStartable
    {
        /// <summary>
        /// Æô¶¯Ê±³õÊ¼»¯ Lambda ´¦ÀíÆ÷¡£
        /// </summary>
        public void Start()
        {
            // ×¢²á Lambda ±í´ïÊ½×ª»»µ½ Expr ¶ÔÏóµÄ³ÉÔ±¾ä±ú (Èç DateTime.Now)
            RegisterLambdaMemberHandlers();
            // ×¢²á Lambda ±í´ïÊ½×ª»»µ½ Expr ¶ÔÏóµÄ·½·¨¾ä±ú (Èç StartsWith, Contains)
            RegisterLambdaMethodHandlers();
        }

        /// <summary>
        /// ×¢²á Lambda ±í´ïÊ½ÖÐµÄ³ÉÔ±·ÃÎÊ´¦ÀíÆ÷£¨ÊôÐÔ»ò×Ö¶Î£©¡£
        /// </summary>
        private void RegisterLambdaMemberHandlers()
        {
            LambdaExprConverter.RegisterMemberHandler(typeof(DateTime), "Now");
            LambdaExprConverter.RegisterMemberHandler(typeof(DateTime), "Today");
            LambdaExprConverter.RegisterMemberHandler(typeof(string), "Length");
        }

        /// <summary>
        /// ×¢²á Lambda ±í´ïÊ½ÖÐµÄ·½·¨µ÷ÓÃ´¦ÀíÆ÷¡£
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
                return new LogicBinaryExpr(left, LogicOperator.StartsWith, right);
            });

            LambdaExprConverter.RegisterMethodHandler(typeof(string), "EndsWith", (node, converter) =>
            {
                var left = converter.Convert(node.Object) as ValueTypeExpr;
                var right = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicOperator.EndsWith, right);
            });

            LambdaExprConverter.RegisterMethodHandler(typeof(string), "Contains", (node, converter) =>
            {
                var left = converter.Convert(node.Object) as ValueTypeExpr;
                var right = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicOperator.Contains, right);
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
                    return new LogicBinaryExpr(value, LogicOperator.In, collection);
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
                return new ValueSet(ValueJoinType.Concat, args);
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
                return new LogicBinaryExpr(left, LogicOperator.Equal, right);
            });

            LambdaExprConverter.RegisterMethodHandler("ToString", (node, converter) =>
            {
                return converter.Convert(node.Object);
            });

            LambdaExprConverter.RegisterMethodHandler("Compare", (node, converter) =>
            {
                var left = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                var right = converter.Convert(node.Arguments[1]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicOperator.Equal, right);
            });

            LambdaExprConverter.RegisterMethodHandler("CompareTo", (node, converter) =>
            {
                var left = node.Object != null ? converter.Convert(node.Object) as ValueTypeExpr : converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                var right = node.Object != null ? converter.Convert(node.Arguments[0]) as ValueTypeExpr : converter.Convert(node.Arguments[1]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicOperator.Equal, right);
            });
        }
    }
}
