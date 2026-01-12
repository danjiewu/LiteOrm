using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 二元条件表达式，例如 <c>a = b</c>, <c>name LIKE '%abc%'</c>, <c>id IN (1,2,3)</c> 等。
    /// 支持带 NOT 前缀的操作（例如 NOT IN、NOT LIKE）。
    /// </summary>
    public sealed class BinaryExpr : Expr
    {
        private static Dictionary<BinaryOperator, string> operatorSymbols = new Dictionary<BinaryOperator, string>()
        {
            { BinaryOperator.Equal,"=" },
            { BinaryOperator.GreaterThan,">" },
            { BinaryOperator.LessThan,"<" },
            { BinaryOperator.Like,"LIKE" },
            { BinaryOperator.StartsWith,"LIKE" },
            { BinaryOperator.EndsWith,"LIKE" },
            { BinaryOperator.Contains,"LIKE" },
            { BinaryOperator.RegexpLike,"REGEXP_LIKE" },
            { BinaryOperator.In,"IN" },
            { BinaryOperator.NotEqual,"<>" },
            { BinaryOperator.GreaterThanOrEqual,">=" },
            { BinaryOperator.LessThanOrEqual,"<=" },
            { BinaryOperator.NotIn,"NOT IN" },
            { BinaryOperator.NotContains,"NOT LIKE" },
            { BinaryOperator.NotLike,"NOT LIKE" },
            { BinaryOperator.NotStartsWith,"NOT LIKE" },
            { BinaryOperator.NotEndsWith,"NOT LIKE" },
            { BinaryOperator.NotRegexpLike,"NOT REGEXP_LIKE" },
            { BinaryOperator.Add,"+"  },
            { BinaryOperator.Subtract,"-" },
            { BinaryOperator.Multiply,"*" },
            { BinaryOperator.Divide,"/" },
            { BinaryOperator.Concat,"||" }
        };

        /// <summary>
        /// 无参构造
        /// </summary>
        public BinaryExpr() { }

        /// <summary>
        /// 使用左右表达式与操作符构造条件表达式。
        /// </summary>
        /// <param name="left">左侧表达式</param>
        /// <param name="oper">二元操作符</param>
        /// <param name="right">右侧表达式</param>
        public BinaryExpr(Expr left, BinaryOperator oper, Expr right)
        {
            Left = left;
            Operator = oper;
            Right = right;
        }

        /// <summary>
        /// 左侧表达式
        /// </summary>
        public Expr Left { get; set; }

        /// <summary>
        /// 使用的操作符（可能包含 Not 标志）
        /// </summary>
        public BinaryOperator Operator { get; set; }

        /// <summary>
        /// 获取不含 Not 标志的原始操作符（例如 Not|In => In）。
        /// </summary>
        public BinaryOperator OriginOperator => Operator & ~BinaryOperator.Not;

        /// <summary>
        /// 右侧表达式
        /// </summary>
        public Expr Right { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// - 对于 REGEXP_LIKE，生成形如 <c>REGEXP_LIKE(left,right)</c> 的调用。
        /// - 对于 equals 且右值为 NULL，生成 IS NULL / IS NOT NULL。
        /// - 对于 LIKE/StartsWith/EndsWith/Contains 等，会依据右侧是 ValueExpr 还是表达式生成带参数或带通配符的 SQL，并为需要的值添加参数到 <paramref name="outputParams"/>。
        /// - 对于复杂字符串拼接或需要转义的情况，使用 <see cref="ISqlBuilder.BuildConcatSql"/> 以便兼容不同数据库的拼接语法。
        /// </remarks>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            string op = String.Empty;
            operatorSymbols.TryGetValue(Operator, out op);
            switch (OriginOperator)
            {
                case BinaryOperator.RegexpLike:
                    return $"{op}({Left.ToSql(context, sqlBuilder, outputParams)},{Right.ToSql(context, sqlBuilder, outputParams)})";
                case BinaryOperator.Equal:
                    if (Right is null || Right is ValueExpr vs && vs.Value is null)
                    {
                        if (Operator == BinaryOperator.Equal)
                            return $"{Left.ToSql(context, sqlBuilder, outputParams)} is null";
                        else
                            return $"{Left.ToSql(context, sqlBuilder, outputParams)} is not null";
                    }
                    else if (Left is null || Left is ValueExpr vsl && vsl.Value is null)
                    {
                        if (Operator == BinaryOperator.Equal)
                            return $"{Right.ToSql(context, sqlBuilder, outputParams)} is null";
                        else
                            return $"{Right.ToSql(context, sqlBuilder, outputParams)} is not null";
                    }
                    else
                        return $"{Left.ToSql(context, sqlBuilder, outputParams)} {op} {Right.ToSql(context, sqlBuilder, outputParams)}";
                case BinaryOperator.Concat:
                    return sqlBuilder.BuildConcatSql(Left.ToSql(context, sqlBuilder, outputParams), Right.ToSql(context, sqlBuilder, outputParams));
                case BinaryOperator.Contains:
                case BinaryOperator.EndsWith:
                case BinaryOperator.StartsWith:
                    if (Right is ValueExpr vs2)
                    {
                        string paramName = outputParams.Count.ToString();
                        string val = sqlBuilder.ToSqlLikeValue(vs2.Value?.ToString());
                        switch (OriginOperator)
                        {
                            case BinaryOperator.StartsWith:
                                val = $"{val}%"; break;
                            case BinaryOperator.EndsWith:
                                val = $"%{val}"; break;
                            case BinaryOperator.Contains:
                                val = $"%{val}%"; break;
                        }
                        outputParams.Add(new KeyValuePair<string, object>(sqlBuilder.ToParamName(paramName), val));
                        return $@"{Left.ToSql(context, sqlBuilder, outputParams)} {op} {sqlBuilder.ToSqlParam(paramName)} escape '\\'";
                    }
                    else
                    {
                        string left = Left.ToSql(context, sqlBuilder, outputParams);
                        string right = $@"REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE({Right.ToSql(context, sqlBuilder, outputParams)},'\\', '\\\\'),'_', '\\_'),'%', '\\%'),'/', '\\/'),'[', '\\['),']', '\\]')";
                        switch (OriginOperator)
                        {
                            case BinaryOperator.StartsWith:
                                right = sqlBuilder.BuildConcatSql(right, "%"); break;
                            case BinaryOperator.EndsWith:
                                right = sqlBuilder.BuildConcatSql("%", right); break;
                            case BinaryOperator.Contains:
                                right = sqlBuilder.BuildConcatSql("%", right, "%"); break;
                        }
                        return $@"{left} {op} {right} escape '\\'";
                    }
                default:
                    return $"{Left.ToSql(context, sqlBuilder, outputParams)} {op} {Right.ToSql(context, sqlBuilder, outputParams)}";
            }
        }

        /// <summary>
        /// 返回表示当前表达式的字符串。
        /// </summary>
        /// <returns>表示当前表达式的字符串。</returns>
        public override string ToString()
        {
            string op = String.Empty;
            if (!operatorSymbols.TryGetValue(Operator, out op)) op = Operator.ToString();
            return $"{Left} {op} {Right}";
        }

        /// <summary>
        /// 确定指定的对象是否等于当前对象。
        /// </summary>
        /// <param name="obj">要与当前对象进行比较的对象。</param>
        /// <returns>如果指定的对象等于当前对象，则为 true；否则为 false。</returns>
        public override bool Equals(object obj)
        {
            return obj is BinaryExpr b &&
                   b.Operator == Operator &&
                   Equals(b.Left, Left) &&
                   Equals(b.Right, Right);
        }

        /// <summary>
        /// 作为默认哈希函数。
        /// </summary>
        /// <returns>当前对象的哈希代码。</returns>
        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), Operator.GetHashCode(), (Left?.GetHashCode() ?? 0), (Right?.GetHashCode() ?? 0));
        }
    }

    /// <summary>
    /// 双目操作符
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BinaryOperator
    {
        /// <summary>
        /// 相等
        /// </summary>
        Equal = 0,
        /// <summary>
        /// 大于
        /// </summary>
        GreaterThan = 1,
        /// <summary>
        /// 小于
        /// </summary>
        LessThan = 2,
        /// <summary>
        /// 以指定字符串为开始（作为字符串比较）
        /// </summary>
        StartsWith = 3,
        /// <summary>
        /// 以指定字符串为结尾（作为字符串比较）
        /// </summary>
        EndsWith = 4,
        /// <summary>
        /// 包含指定字符串（作为字符串比较）
        /// </summary>
        Contains = 5,
        /// <summary>
        /// 匹配字符串格式（作为字符串比较）
        /// </summary>
        Like = 6,
        /// <summary>
        /// 包含在集合中
        /// </summary>
        In = 7,
        /// <summary>
        /// 正则表达式匹配
        /// </summary>
        RegexpLike = 8,
        /// <summary>
        /// 加法
        /// </summary>
        Add = 9,
        /// <summary>
        /// 减法
        /// </summary>
        Subtract = 10,
        /// <summary>
        /// 乘法
        /// </summary>
        Multiply = 11,
        /// <summary>
        /// 除法
        /// </summary>
        Divide = 12,
        /// <summary>
        /// 字符串连接
        /// </summary>
        Concat = 13,
        /// <summary>
        /// 逻辑非 
        /// </summary>
        Not = 64,
        /// <summary>
        /// 不等于
        /// </summary>
        NotEqual = Equal | Not,
        /// <summary>
        /// 不小于
        /// </summary>
        GreaterThanOrEqual = LessThan | Not,
        /// <summary>
        /// 不大于
        /// </summary>
        LessThanOrEqual = GreaterThan | Not,
        /// <summary>
        /// 不以指定字符串为开始
        /// </summary>
        NotStartsWith = StartsWith | Not,
        /// <summary>
        /// 不以指定字符串为结尾
        /// </summary>
        NotEndsWith = EndsWith | Not,
        /// <summary>
        /// 不包含指定字符串（作为字符串比较）
        /// </summary>
        NotContains = Contains | Not,
        /// <summary>
        /// 不匹配字符串格式（作为字符串比较）
        /// </summary>
        NotLike = Like | Not,
        /// <summary>
        /// 不包含在集合中
        /// </summary>
        NotIn = In | Not,
        /// <summary>
        /// 不匹配正则表达式
        /// </summary>
        NotRegexpLike = RegexpLike | Not
    }

    /// <summary>
    /// 二元操作符的扩展方法
    /// </summary>
    public static class BinaryOperatorExt
    {
        /// <summary>
        /// 检查操作符是否包含NOT标志
        /// </summary>
        /// <param name="oper">要检查的二元操作符</param>
        /// <returns>如果操作符包含NOT标志则返回true，否则返回false</returns>
        public static bool IsNot(this BinaryOperator oper)
        {
            return (oper & BinaryOperator.Not) == BinaryOperator.Not;
        }

        /// <summary>
        /// 获取操作符的原始操作符（去除NOT标志）
        /// </summary>
        /// <param name="oper">要获取原始操作符的二元操作符</param>
        /// <returns>去除NOT标志后的原始操作符</returns>
        public static BinaryOperator Positive(this BinaryOperator oper)
        {
            return oper | (~BinaryOperator.Not);
        }

        /// <summary>
        /// 获取操作符的相反操作符（添加或去除NOT标志）
        /// </summary>
        /// <param name="oper">要获取相反操作符的二元操作符</param>
        /// <returns>相反的操作符</returns>
        public static BinaryOperator Opposite(this BinaryOperator oper)
        {
            return oper ^ BinaryOperator.Not;
        }
    }
}
