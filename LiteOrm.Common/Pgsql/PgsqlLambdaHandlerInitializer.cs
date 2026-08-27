using System;
using System.Linq;
using System.Linq.Expressions;
using LiteOrm.Common;

namespace LiteOrm.Pgsql
{
    /// <summary>
    /// PostgreSQL 专用的 Lambda 方法处理器初始化器。
    /// <para>
    /// 将数组类型属性上的方法调用（如 <c>Contains</c>）映射为 <see cref="PgsqlExprExtensions"/> 生成的
    /// PostgreSQL 数组表达式（<c>element = ANY(array)</c>）；对非数组类型的方法调用，
    /// 回退到查询到的原处理器（<see cref="LambdaExprConverter.FindMethodHandler(MethodCallExpression)"/>），
    /// 保证既有行为（如字符串 LIKE、集合 IN）不受影响。
    /// </para>
    /// <para>
    /// 请仅在 PostgreSQL 场景下调用 <see cref="Register"/>，该方法会覆盖对应方法名的全局处理器。
    /// </para>
    /// </summary>
    public static class PgsqlLambdaHandlerInitializer
    {
        /// <summary>
        /// 注册 PostgreSQL Lambda 方法处理器。
        /// </summary>
        public static void Register()
        {
            RegisterContainsHandler();
        }

        /// <summary>
        /// 注册 <c>Contains</c> 处理器：
        /// 数组类型属性 → <c>element = ANY(array)</c>（<see cref="PgsqlExprExtensions.Contains"/>）；
        /// 非数组类型 → 回退到查询到的原处理器。
        /// </summary>
        private static void RegisterContainsHandler()
        {
            // 注册前先查询当前命中的 Contains 处理器作为非数组场景的回退目标
            // IList.Contains 等注册为全局方法名 "Contains"（见 LiteOrmLambdaHandlerInitializer）
            var originalContains = LambdaExprConverter.FindMethodHandler(typeof(Enumerable), nameof(Enumerable.Contains))
                                   ?? LambdaExprConverter.FindMethodHandler("Contains");

            LambdaExprConverter.RegisterMethodHandler(nameof(Enumerable.Contains), (node, converter) =>
            {
                // 静态扩展方法 Enumerable.Contains(source, element) 取首个参数作为调用源；实例方法取 Object
                var staticCall = node.Method.IsStatic;
                Expression? source = staticCall ? node.Arguments[0] : node.Object;

                // 解析出其底层数组表达式；.NET 编译器可能将数组的 Contains 编译为
                // MemoryExtensions.Contains(ReadOnlySpan<T>, T)，需先解包 Span 转换。
                ValueTypeExpr? array = ResolveArray(source, converter);
                if (array is not null)
                {
                    // 元素参数：静态形式位于 Arguments[1]，实例形式位于 Arguments[0]
                    ValueTypeExpr element = converter.Convert(staticCall ? node.Arguments[1] : node.Arguments[0]).AsValue();
                    return PgsqlExprExtensions.Contains(array, element);
                }

                // 非数组：回退到查询到的原处理器
                return originalContains?.Invoke(node, converter) ?? LambdaExprConverter.DefaultFunctionHandler(node, converter);
            });
        }

        /// <summary>
        /// 从调用源表达式中解析数组值表达式；若底层是数组类型则返回其 <see cref="ValueTypeExpr"/>，否则返回 <c>null</c>。
        /// <para>
        /// 兼容将数组隐式转换为 <see cref="Span{T}"/> / <see cref="ReadOnlySpan{T}"/> 的编译优化
        /// （如 <see cref="MemoryExtensions"/> 上的 <c>Contains</c> 重载），解包后识别底层数组。
        /// </para>
        /// </summary>
        /// <param name="source">调用源表达式。</param>
        /// <param name="converter">当前转换器。</param>
        private static ValueTypeExpr? ResolveArray(Expression? source, LambdaExprConverter converter)
        {
            if (source is null) return null;

            Expression current = source;
            // 逐层解包 Span/ReadOnlySpan 构造或转换调用，得到底层数组
            while (current is MethodCallExpression { Arguments.Count: > 0, Type: var type } spanCall && IsSpanType(type))
            {
                current = spanCall.Arguments[0];
            }

            return current.Type.IsArray ? converter.Convert(current).AsValue() : null;
        }

        /// <summary>
        /// 判断类型是否为泛型 <see cref="Span{T}"/> 或 <see cref="ReadOnlySpan{T}"/>。
        /// </summary>
        private static bool IsSpanType(Type type)
            => type.IsGenericType
               && type.GetGenericTypeDefinition().FullName is "System.Span`1" or "System.ReadOnlySpan`1";
    }
}