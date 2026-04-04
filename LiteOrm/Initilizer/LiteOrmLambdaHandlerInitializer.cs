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
    public class LiteOrmLambdaHandlerInitializer : ILiteOrmInitializer
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
        private void RegisterLambdaMethodHandlers()
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

            // ExprExtensions.To()：将对象转换为 Expr，用于在表达式中嵌入常量值
            LambdaExprConverter.RegisterMethodHandler(typeof(ExprExtensions), nameof(ExprExtensions.To), (node, converter) =>
                converter.Convert(node.Arguments[0])
            );

            // TimeSpan.TotalSeconds 属性
            // 特殊处理：如果两个日期相减 (date1 - date2).TotalSeconds，转换为 DateDiffSeconds 函数
            // 否则转换为 TotalSeconds 函数
            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalSeconds), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffSeconds", binaryExpr.Left, binaryExpr.Right);
                return new FunctionExpr("TotalSeconds", timeSpanExpr as ValueTypeExpr);
            });

            // TimeSpan.TotalDays 属性
            // 特殊处理：如果两个日期相减 (date1 - date2).TotalDays，转换为 DateDiffDays 函数
            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalDays), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffDays", binaryExpr.Left, binaryExpr.Right);
                return new FunctionExpr("TotalDays", timeSpanExpr as ValueTypeExpr);
            });

            // TimeSpan.TotalHours 属性
            // 特殊处理：如果两个日期相减 (date1 - date2).TotalHours，转换为 DateDiffHours 函数
            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalHours), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffHours", binaryExpr.Left, binaryExpr.Right);
                return new FunctionExpr("TotalHours", timeSpanExpr as ValueTypeExpr);
            });

            // TimeSpan.TotalMinutes 属性
            // 特殊处理：如果两个日期相减 (date1 - date2).TotalMinutes，转换为 DateDiffMinutes 函数
            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalMinutes), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffMinutes", binaryExpr.Left, binaryExpr.Right);
                return new FunctionExpr("TotalMinutes", timeSpanExpr as ValueTypeExpr);
            });

            // TimeSpan.TotalMilliseconds 属性
            // 特殊处理：如果两个日期相减 (date1 - date2).TotalMilliseconds，转换为 DateDiffMilliseconds 函数
            LambdaExprConverter.RegisterMemberHandler(typeof(TimeSpan), nameof(TimeSpan.TotalMilliseconds), (node, converter) =>
            {
                var timeSpanExpr = converter.Convert(node.Expression);
                if (timeSpanExpr is ValueBinaryExpr binaryExpr && binaryExpr.Operator == ValueOperator.Subtract)
                    return new FunctionExpr("DateDiffMilliseconds", binaryExpr.Left, binaryExpr.Right);
                return new FunctionExpr("TotalMilliseconds", timeSpanExpr as ValueTypeExpr);
            });

            // string.StartsWith()：前缀匹配
            // 转换为 SQL LIKE 'xxx%' (LogicBinaryExpr with StartsWith operator)
            LambdaExprConverter.RegisterMethodHandler(typeof(string), nameof(string.StartsWith), (node, converter) =>
            {
                var left = converter.Convert(node.Object) as ValueTypeExpr;
                var right = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicOperator.StartsWith, right);
            });

            // string.EndsWith()：后缀匹配
            // 转换为 SQL LIKE '%xxx' (LogicBinaryExpr with EndsWith operator)
            LambdaExprConverter.RegisterMethodHandler(typeof(string), nameof(string.EndsWith), (node, converter) =>
            {
                var left = converter.Convert(node.Object) as ValueTypeExpr;
                var right = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicOperator.EndsWith, right);
            });

            // string.Contains()：包含子串
            // 转换为 SQL LIKE '%xxx%' (LogicBinaryExpr with Contains operator)
            LambdaExprConverter.RegisterMethodHandler(typeof(string), nameof(string.Contains), (node, converter) =>
            {
                var left = converter.Convert(node.Object) as ValueTypeExpr;
                var right = converter.Convert(node.Arguments[0]) as ValueTypeExpr;
                return new LogicBinaryExpr(left, LogicOperator.Contains, right);
            });

            // IList.Contains() / Enumerable.Contains()：集合包含判断
            // 支持静态方法 (Enumerable.Contains(collection, value)) 和实例方法 (collection.Contains(value))
            // 转换为 SQL IN 操作 (LogicBinaryExpr with In operator)
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

            // string.Concat()：字符串拼接
            // 支持多种重载：Concat(str1, str2), Concat(str1, str2, str3), Concat(collection)
            // 转换为 ValueSet with Concat join type
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

            // Equals()：实例或静态相等比较
            // 支持实例方法 (obj.Equals(other)) 和静态方法 (Equals(obj1, obj2))
            // 转换为 LogicBinaryExpr with Equal operator
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

            // ToString()：转换为字符串
            // 带参数 ToString(format) 转换为 Format 函数，如 date.ToString("yyyy-MM-dd")
            // 不带参数直接返回原对象
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

            // string.Trim() / TrimStart() / TrimEnd()：去除字符串首尾/头部/尾部空格
            // 直接使用默认处理转换为 FunctionExpr("Trim") / FunctionExpr("TrimStart") / FunctionExpr("TrimEnd")
            // SQL 函数的具体生成逻辑在 LiteOrmSqlFunctionInitializer 中注册

            // string.Remove()：删除从指定位置到结尾的字符
            // 默认转换为 FunctionExpr("Remove", str, count)
            // SqlFunction 中注册为 SQL LEFT(str, count)

            // string.ToUpper() / string.ToLower()：大小写转换
            // 直接使用基类注册的映射：ToUpper -> UPPER, ToLower -> LOWER

        }
    }
}
