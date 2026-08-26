using LiteOrm.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace LiteOrm
{
    /// <summary>
    /// LiteOrm Lambda 处理器初始化器，负责注册 Lambda 表达式到 Expr 对象的转换句柄。
    /// </summary>
    public static class LiteOrmLambdaHandlerInitializer
    {
        /// <summary>
        /// 启动时初始化 Lambda 处理器。
        /// </summary>
        public static void InitialRegister()
        {
            // 注册 Lambda 表达式转换到 Expr 对象的成员句柄 (如 DateTime.Now)
            RegisterLambdaMemberHandlers();
            // 注册 Lambda 表达式转换到 Expr 对象的方法句柄 (如 StartsWith, Contains)
            RegisterLambdaMethodHandlers();
        }

        /// <summary>
        /// 注册 Lambda 表达式中的成员访问处理器（属性或字段）。
        /// </summary>
        private static void RegisterLambdaMemberHandlers()
        {
            // DateTime.Now：当前日期时间
            // 对应 SqlFunction: CURRENT_TIMESTAMP (NOW())
            LambdaExprConverter.RegisterMemberHandler(typeof(DateTime), "Now");

            // DateTime.Today：当天日期（不含时间部分）
            // 对应 SqlFunction: CURRENT_DATE (CURDATE())
            LambdaExprConverter.RegisterMemberHandler(typeof(DateTime), "Today");

            // string.Length：字符串长度
            // 各数据库实现：MySQL 用 CHAR_LENGTH()，SQL Server 用 LEN()
            LambdaExprConverter.RegisterMemberHandler(typeof(string), "Length");
        }

        /// <summary>
        /// 注册 Lambda 表达式中的方法调用处理器。
        /// </summary>
#if NET8_0_OR_GREATER
        [UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "Registers Lambda method handlers via reflection; under AOT, method members are preserved via the source generator.")]
        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Only uses JsonNode indexer, does not call ReplaceWith or other dangerous methods.")]
#endif
        private static void RegisterLambdaMethodHandlers()
        {
            // DateTime 类型方法：AddYears/Month/Day/Hour/Minute/Second 等日期加减操作
            // 对应 SqlFunction: DATE_ADD / DATEADD 等
            LambdaExprConverter.RegisterMethodHandler(typeof(DateTime));

            // Math 类型方法：Abs, Max, Min, Floor, Ceiling, Round, Pow, Sqrt, Truncate 等
            // 直接转换为 SQL 数学函数
            LambdaExprConverter.RegisterMethodHandler(typeof(Math));

            // string 类型方法：ToLower, ToUpper, Trim, TrimStart, TrimEnd 等
            // 直接转换为 SQL 字符串函数
            LambdaExprConverter.RegisterMethodHandler(typeof(string));

            // ExprExtensions.To()：将对象转换为 Expr，用于在Lambda表达式中嵌入Expr
            LambdaExprConverter.RegisterMethodHandler(typeof(ExprExtensions), nameof(ExprExtensions.To), (node, converter) =>
                converter.Convert(node.Arguments[0])
            );

            // JsonNode 索引访问 json["a"]["b"]（编译器以 get_Item 方法调用表示）：映射到 JsonExtract
            // 常量键（字符串/整数）拼接为固定 JSON 路径；动态键（不可编译期求值，转出非 ValueExpr）作为参数经 string Concat 拼入路径。
            LambdaExprConverter.RegisterMethodHandler(typeof(JsonNode), "get_Item", (node, converter) =>
            {
                var (baseExpr, path) = BuildJsonAccess(converter, node);
                return new FunctionExpr("JsonExtract", baseExpr, path);
            });

            // JsonNode.GetValue<T>()：提取当前 JSON 节点的标量值，映射到 JsonValue
            // 直接从对象链构建单一路径（JSON_VALUE(col, '$.path')），避免在已 JsonExtract 的结果上再套一层 '$'。
            LambdaExprConverter.RegisterMethodHandler(typeof(JsonNode), "GetValue", (node, converter) =>
            {
                var (baseExpr, path) = BuildJsonAccess(converter, node.Object!);
                return new FunctionExpr("JsonValue", baseExpr, path);
            });

            // TimeSpan.TotalSeconds 属性
            // 特殊处理：如果两个日期相减 (date1 - date2).TotalSeconds，转换为 DateDiffSeconds 函数
            // 否则转换为 TotalSeconds 函数
            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalSeconds), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression!);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffSeconds", binaryExpr.Left!, binaryExpr.Right!);
                return new FunctionExpr("TotalSeconds", timeSpanExpr.AsValue());
            });
            // 特殊处理：如果两个日期相减 (date1 - date2).TotalDays，转换为 DateDiffDays 函数
            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalDays), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression!);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffDays", binaryExpr.Left!, binaryExpr.Right!);
                return new FunctionExpr("TotalDays", timeSpanExpr.AsValue());
            });

            // TimeSpan.TotalHours 属性
            // 特殊处理：如果两个日期相减 (date1 - date2).TotalHours，转换为 DateDiffHours 函数
            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalHours), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression!);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffHours", binaryExpr.Left!, binaryExpr.Right!);
                return new FunctionExpr("TotalHours", timeSpanExpr.AsValue());
            });

            // TimeSpan.TotalMinutes 属性
            // 特殊处理：如果两个日期相减 (date1 - date2).TotalMinutes，转换为 DateDiffMinutes 函数
            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalMinutes), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression!);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffMinutes", binaryExpr.Left!, binaryExpr.Right!);
                return new FunctionExpr("TotalMinutes", timeSpanExpr.AsValue());
            });

            // TimeSpan.TotalMilliseconds 属性
            // 特殊处理：如果两个日期相减 (date1 - date2).TotalMilliseconds，转换为 DateDiffMilliseconds 函数
            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalMilliseconds), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression!);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffMilliseconds", binaryExpr.Left!, binaryExpr.Right!);
                return new FunctionExpr("TotalMilliseconds", timeSpanExpr.AsValue());
            });

            // string.StartsWith()：前缀匹配
            // 转换为 SQL LIKE 'xxx%' (LogicBinaryExpr with StartsWith operator)
            LambdaExprConverter.RegisterMethodHandler(typeof(string), nameof(string.StartsWith), (node, converter) =>
            {
                var left = converter.Convert(node.Object!).AsValue();
                var right = converter.Convert(node.Arguments[0]).AsValue();
                return new LogicBinaryExpr(left, LogicOperator.StartsWith, right);
            });

            // string.EndsWith()：后缀匹配
            // 转换为 SQL LIKE '%xxx' (LogicBinaryExpr with EndsWith operator)
            LambdaExprConverter.RegisterMethodHandler(typeof(string), nameof(string.EndsWith), (node, converter) =>
            {
                var left = converter.Convert(node.Object!).AsValue();
                var right = converter.Convert(node.Arguments[0]).AsValue();
                return new LogicBinaryExpr(left, LogicOperator.EndsWith, right);
            });

            // string.Contains()：包含子串
            // 转换为 SQL LIKE '%xxx%' (LogicBinaryExpr with Contains operator)
            LambdaExprConverter.RegisterMethodHandler(typeof(string), nameof(string.Contains), (node, converter) =>
            {
                var left = converter.Convert(node.Object!).AsValue();
                var right = converter.Convert(node.Arguments[0]).AsValue();
                return new LogicBinaryExpr(left, LogicOperator.Contains, right);
            });

            // Regex.IsMatch(input, pattern)：正则匹配谓词
            // 静态形式 Regex.IsMatch(input, pattern) 与实例形式 new Regex(pattern).IsMatch(input) / regex.IsMatch(input)（闭包变量）
            // 均映射到 REGEXP_LIKE(input, pattern)（由 ExprSqlConverter 的 RegexpLike 分支渲染各方言原生语法）
            LambdaExprConverter.RegisterMethodHandler(typeof(Regex), nameof(Regex.IsMatch), (node, converter) =>
            {
                ValueTypeExpr input, pattern;
                if (node.Object is not null)
                {
                    // 实例形式：new Regex(pattern).IsMatch(input) 或通过闭包变量得到的 Regex 实例
                    pattern = ExtractRegexPattern(node.Object, converter);
                    input = converter.Convert(node.Arguments[0]).AsValue();
                }
                else
                {
                    // 静态形式：Regex.IsMatch(input, pattern[, options[, timeout]])
                    input = converter.Convert(node.Arguments[0]).AsValue();
                    pattern = converter.Convert(node.Arguments[1]).AsValue();
                }
                return new LogicBinaryExpr(input, LogicOperator.RegexpLike, pattern);
            });

            // Regex.Replace(input, pattern, replacement)：正则替换
            // 静态形式 Regex.Replace(input, pattern, replacement) 与实例形式 new Regex(pattern).Replace(input, replacement) / regex.Replace(input, replacement)
            // 均映射到 REGEXP_REPLACE(input, pattern, replacement)
            LambdaExprConverter.RegisterMethodHandler(typeof(Regex), nameof(Regex.Replace), (node, converter) =>
            {
                ValueTypeExpr input, pattern, replacement;
                if (node.Object is not null)
                {
                    // 实例形式：new Regex(pattern).Replace(input, replacement) 或通过闭包变量得到的 Regex 实例
                    pattern = ExtractRegexPattern(node.Object, converter);
                    input = converter.Convert(node.Arguments[0]).AsValue();
                    replacement = converter.Convert(node.Arguments[1]).AsValue();
                }
                else
                {
                    // 静态形式：Regex.Replace(input, pattern, replacement[, options])
                    input = converter.Convert(node.Arguments[0]).AsValue();
                    pattern = converter.Convert(node.Arguments[1]).AsValue();
                    replacement = converter.Convert(node.Arguments[2]).AsValue();
                }
                return new FunctionExpr("REGEXP_REPLACE", input, pattern, replacement);
            });

            // IList.Contains() / Enumerable.Contains()：集合包含判断
            // 支持静态方法 (Enumerable.Contains(collection, value)) 和实例方法 (collection.Contains(value))
            // 转换为 SQL IN 操作 (LogicBinaryExpr with In operator)
            LambdaExprConverter.RegisterMethodHandler(nameof(IList.Contains), (node, converter) =>
            {
                if (node.Method.IsStatic)
                {
                    if (node.Arguments.Count != 2)
                        throw new ArgumentException($"Invalid number of arguments for extension method {node.Method.Name}. Expected 2, got {node.Arguments.Count}.");

                    var collection = converter.Convert(node.Arguments[0]).AsValue();
                    var value = converter.Convert(node.Arguments[1]).AsValue();

                    return new LogicBinaryExpr(value, LogicOperator.In, collection);
                }
                else
                {
                    if (node.Arguments.Count != 1) throw new ArgumentException($"Invalid number of arguments for method {node.Method.Name}. Expected 1, got {node.Arguments.Count}.");
                    ValueTypeExpr collection = collection = converter.Convert(node.Object!).AsValue();
                    ValueTypeExpr value = converter.Convert(node.Arguments[0]).AsValue();
                    return new LogicBinaryExpr(value, LogicOperator.In, collection);
                }
            });

            // string.Concat()：字符串拼接
            // 支持多种重载：Concat(str1, str2), Concat(str1, str2, str3), Concat(collection)
            // 转换为 ValueSet with Concat join type
            LambdaExprConverter.RegisterMethodHandler(typeof(string), nameof(string.Concat), (node, converter) =>
            {
                List<ValueTypeExpr> args = new List<ValueTypeExpr>();
                if (node.Object != null) args.Add(converter.Convert(node.Object!).AsValue());

                if (node.Arguments.Count == 1)
                {
                    var arg = converter.Convert(node.Arguments[0]);
                    if (arg is IEnumerable<ValueTypeExpr> enumerable)
                        args.AddRange(enumerable);
                    else
                        args.Add(arg.AsValue());
                }
                else
                {
                    foreach (var arg in node.Arguments)
                    {
                        args.Add(converter.Convert(arg).AsValue());
                    }
                }
                return new ValueSet(ValueJoinType.Concat, args);
            });

            // Equals()：实例或静态相等比较
            // 支持实例方法 (obj.Equals(other)) 和静态方法 (Equals(obj1, obj2))
            // 转换为 LogicBinaryExpr with Equal operator
            LambdaExprConverter.RegisterMethodHandler(nameof(Equals), (node, converter) =>
            {
                ValueTypeExpr? left = null;
                ValueTypeExpr? right = null;
                if (node.Object != null)
                {
                    left = converter.Convert(node.Object!).AsValue();
                    right = converter.Convert(node.Arguments[0]).AsValue();
                }
                else
                {
                    left = converter.Convert(node.Arguments[0]).AsValue();
                    right = converter.Convert(node.Arguments[1]).AsValue();
                }
                return new LogicBinaryExpr(left, LogicOperator.Equal, right);
            });

            // ToString()：转换为字符串
            // 带参数 ToString(format) 转换为 Format 函数，如 date.ToString("yyyy-MM-dd")
            // 不带参数直接返回原对象
            LambdaExprConverter.RegisterMethodHandler(nameof(ToString), (node, converter) =>
            {
                if (node.Arguments.Count > 0)
                {
                    var obj = converter.Convert(node.Object!).AsValue();
                    var format = converter.Convert(node.Arguments[0]).AsValue();
                    if (obj is not null && format is not null)
                        return new FunctionExpr("Format", obj, format);
                }
                return converter.Convert(node.Object!);
            });
        }

        /// <summary>
        /// 从 Regex 实例表达式中提取正则模式字符串并包装为 <see cref="ValueTypeExpr"/>。
        /// 支持 <see cref="NewExpression"/>（如 <c>new Regex(pattern)</c>，从构造参数取模式）
        /// 与可求值的实例表达式（如闭包变量、字段，通过求值得到 <see cref="Regex"/> 对象后读取其 Pattern 成员）。
        /// </summary>
        /// <param name="regexExpr">表示 Regex 实例的表达式节点。</param>
        /// <param name="converter">当前转换器，用于子表达式转换与求值。</param>
        /// <returns>表示正则模式字符串的值表达式。</returns>
        private static ValueTypeExpr ExtractRegexPattern(Expression regexExpr, LambdaExprConverter converter)
        {
            if (regexExpr is NewExpression regexNew)
            {
                return converter.Convert(regexNew.Arguments[0]).AsValue();
            }
            // 闭包变量、字段等可求值形式：求值得到 Regex 实例后读取 Pattern（protected internal，经反射访问）
            if (converter.Evaluate(regexExpr) is Regex regex)
            {                
                return new ValueExpr(regex.ToString());
            }
            throw new NotSupportedException($"Cannot extract regex pattern from expression: {regexExpr}");
        }

        /// <summary>
        /// 从 JsonNode 访问链构建基础表达式与 JSON 路径。
        /// <para>
        /// 逐层解包 <c>get_Item</c> 索引链（对应 <c>json["a"]["b"]</c>）到内层 JsonNode 列/成员；
        /// 常量键（字符串/整数）拼接为固定 JSON 路径，动态键（转出非 ValueExpr）作为参数经 string Concat 拼入路径。
        /// 无索引链（收到的是 JsonNode 列本身）时路径为 <c>$</c>。
        /// </para>
        /// </summary>
        /// <param name="converter">当前转换器。</param>
        /// <param name="node">JsonNode 访问表达式（可能是 get_Item 链，也可能是直接的 JsonNode 成员列）。</param>
        /// <returns>(基础表达式, JSON 路径表达式)。</returns>
        private static (ValueTypeExpr Base, ValueTypeExpr Path) BuildJsonAccess(LambdaExprConverter converter, Expression node)
        {
            // 逐层解包 get_Item 索引链到内层 JsonNode 基础表达式
            Expression current = node;
            var keysReverse = new List<Expression>();
            while (current is MethodCallExpression mc
                   && mc.Method.Name == "get_Item"
                   && mc.Object is { } obj
                   && typeof(JsonNode).IsAssignableFrom(mc.Method.DeclaringType))
            {
                if (mc.Arguments.Count != 1)
                    throw new InvalidOperationException("JsonNode index access requires exactly one key.");
                keysReverse.Add(mc.Arguments[0]);
                current = obj;
            }

            ValueTypeExpr baseExpr = converter.Convert(current).AsValue()
                ?? throw new InvalidOperationException("JsonNode access requires a resolvable base expression.");

            if (keysReverse.Count == 0)
                return (baseExpr, new ValueExpr("$"));

            // 逆转为根端→叶端顺序，用于拼接 JSON 路径
            keysReverse.Reverse();

            var parts = new List<ValueTypeExpr>();
            var literal = new StringBuilder(16);
            literal.Append('$');
            bool hasDynamic = false;

            void Flush()
            {
                if (literal.Length > 0)
                {
                    parts.Add(Expr.Const(literal.ToString()));
                    literal.Clear();
                }
            }

            foreach (Expression key in keysReverse)
            {
                ValueTypeExpr k = converter.Convert(key).AsValue() ?? throw new InvalidOperationException("Unsupported JsonNode index key.");
                if (k is ValueExpr { Value: string s })
                    literal.Append('.').Append(s);
                else if (k is ValueExpr { Value: int i })
                    literal.Append('[').Append(i).Append(']');
                else
                {
                    // 动态键：作为参数经 string Concat 拼入路径
                    // JsonNode 索引器键仅可为 string/int（不可空），无需 GetUnderlyingType
                    hasDynamic = true;
                    Flush();
                    if (key.Type == typeof(int) || key.Type == typeof(long))
                    {
                        parts.Add(Expr.Const("["));
                        parts.Add(k);
                        parts.Add(Expr.Const("]"));
                    }
                    else
                    {
                        parts.Add(Expr.Const("."));
                        parts.Add(k);
                    }
                }
            }
            Flush();

            ValueTypeExpr path = hasDynamic
                ? new ValueSet(ValueJoinType.Concat, parts)
                : parts[0];

            return (baseExpr, path);
        }
    }
}
