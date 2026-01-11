using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 二元条件语句，例如 <c>a = b</c>, <c>name LIKE '%abc%'</c>, <c>id IN (1,2,3)</c> 等。
    /// 支持带 NOT 前缀的操作（例如 NOT IN、NOT LIKE）。
    /// </summary>
    public sealed class BinaryStatement : Statement
    {
        private static Dictionary<BinaryOperator, string> operatorSymbols = new()
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
        public BinaryStatement() { }

        /// <summary>
        /// 使用左右表达式与操作符构造条件语句。
        /// </summary>
        /// <param name="left">左侧语句</param>
        /// <param name="oper">二元操作符</param>
        /// <param name="right">右侧语句</param>
        public BinaryStatement(Statement left, BinaryOperator oper, Statement right)
        {
            Left = left;
            Operator = oper;
            Right = right;
        }

        /// <summary>
        /// 左侧语句
        /// </summary>
        public Statement Left { get; set; }

        /// <summary>
        /// 使用的操作符（可能包含 Not 标志）
        /// </summary>
        public BinaryOperator Operator { get; set; }

        /// <summary>
        /// 获取不含 Not 标志的原始操作符（例如 Not|In => In）。
        /// </summary>
        public BinaryOperator OriginOperator => Operator & ~BinaryOperator.Not;

        /// <summary>
        /// 右侧语句
        /// </summary>
        public Statement Right { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// - 对于 REGEXP_LIKE，生成形如 <c>REGEXP_LIKE(left,right)</c> 的调用。
        /// - 对于 equals 且右值为 NULL，生成 IS NULL / IS NOT NULL。
        /// - 对于 LIKE/StartsWith/EndsWith/Contains 等，会依据右侧是 ValueStatement 还是表达式生成带参数或带通配符的 SQL，并为需要的值添加参数到 <paramref name="outputParams"/>。
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
                    if (Right == null || Right is ValueStatement vs && vs.Value == null)
                    {
                        if (Operator == BinaryOperator.Equal)
                            return $"{Left.ToSql(context, sqlBuilder, outputParams)} is null";
                        else
                            return $"{Left.ToSql(context, sqlBuilder, outputParams)} is not null";
                    }
                    else if (Left == null || Left is ValueStatement vsl && vsl.Value == null)
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
                    if (Right is ValueStatement vs2)
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

        public override string ToString()
        {
            string op = String.Empty;
            if (!operatorSymbols.TryGetValue(Operator, out op)) op = Operator.ToString();
            return $"{Left} {op} {Right}";
        }

        public override bool Equals(object obj)
        {
            return obj is BinaryStatement b &&
                   b.Operator == Operator &&
                   Equals(b.Left, Left) &&
                   Equals(b.Right, Right);
        }

        public override int GetHashCode()
        {
            return OrderedHashCodes(GetType().GetHashCode(), Operator.GetHashCode(), (Left?.GetHashCode() ?? 0), (Right?.GetHashCode() ?? 0));
        }
    }

    /// <summary>
    /// 双目操作符
    /// </summary>
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

    public static class BinaryOperatorExt
    {
        public static bool IsNot(this BinaryOperator oper)
        {
            return (oper & BinaryOperator.Not) == BinaryOperator.Not;
        }

        public static BinaryOperator Origin(this BinaryOperator oper)
        {
            return oper | (~BinaryOperator.Not);
        }

        public static BinaryOperator Opposite(this BinaryOperator oper)
        {
            return oper ^ BinaryOperator.Not;
        }
    }
}
