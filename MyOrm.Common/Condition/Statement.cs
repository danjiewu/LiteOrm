using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Quic;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm.Common
{
    /// <summary>
    /// 抽象语句基类。子类应实现 <see cref="ToSql"/> 将语句转换为 SQL 片段并把所需参数写入。
    /// </summary>
    public abstract class Statement
    {
        /// <summary>
        /// 将当前语句转换为 SQL 字符串片段。
        /// </summary>
        /// <param name="context">生成 SQL 所需的上下文（表定义、别名等）。</param>
        /// <param name="sqlBuilder">提供数据库特定 SQL 生成辅助的方法。</param>
        /// <param name="outputParams">输出参数集合，方法应在此集合中添加本语句所需的参数（键为参数名，值为参数值）。</param>
        /// <returns>表示本语句的 SQL 字符串片段（不包含外层分号）。</returns>
        public abstract string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams);
        public static PropertyStatement Property(string propertyName)
        {
            return new PropertyStatement(propertyName);
        }
        public static BinaryStatement Property(string propertyName, object value)
        {
            return new BinaryStatement()
            {
                Left = new PropertyStatement(propertyName),
                Right = new ValueStatement(value)
            };
        }

        public static BinaryStatement Property(string propertyName, BinaryOperator oper, object value)
        {
            return new BinaryStatement()
            {
                Left = new PropertyStatement(propertyName),
                Operator = oper,
                Right = new ValueStatement(value)
            };
        }

        // 重载true运算符
        public static bool operator true(Statement a) => a == null;

        // 重载false运算符
        public static bool operator false(Statement a) => false;

        // 重载与运算符&
        public static Statement operator &(Statement left, Statement right)
        {
            if (left is null) return right;
            else if (right is null) return left;
            else
                return left.And(right);
        }

        // 重载或运算符|
        public static Statement operator |(Statement left, Statement right)
        {
            if (left is null || right is null) return null;
            return left.Or(right);
        }

        public static implicit operator Statement(ValueType value)
        {
            return new ValueStatement(value);
        }

        // 重载加法运算符|
        public static Statement operator +(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.Add, right);
        }

        //
        public static Statement operator -(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.Subtract, right);
        }

        public static Statement operator *(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.Multiply, right);
        }

        public static Statement operator /(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.Divide, right);
        }
        public static Statement operator >(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.GreaterThan, right);
        }
        public static Statement operator <(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.LessThan, right);
        }
        public static Statement operator >=(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.GreaterThanOrEqual, right);
        }
        public static Statement operator <=(Statement left, Statement right)
        {
            return new BinaryStatement(left, BinaryOperator.LessThanOrEqual, right);
        }
        public static Statement operator !(Statement operand)
        {
            return new UnaryStatement(UnaryOperator.Not, operand);
        }
        public static Statement operator ~(Statement operand)
        {
            return new UnaryStatement(UnaryOperator.BitwiseNot, operand);
        }
        public static Statement operator -(Statement operand)
        {
            return new UnaryStatement(UnaryOperator.Nagive, operand);
        }
         public static Expression<Func<T,bool>> Expression<T>(Expression<Func<T, bool>> expression)
        {
            return expression;
        }

        public static implicit operator Statement(LambdaExpression expression)
        {
            var converter = new ExpressionStatementConverter(expression.Parameters[0]);
            return converter.Convert(expression.Body);
        }
    }

    public class ExpressionStatement<T> : Statement
    {
        private Statement statement;
        public ExpressionStatement()
        {
        }
        public ExpressionStatement(Expression<Func<T, bool>> expression)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression)); ;
        }
        public Expression<Func<T, bool>> Expression
        {
            get;
        }

        public Statement Statement
        {
            get
            {
                if (statement == null)
                {
                    var converter = new ExpressionStatementConverter(Expression.Parameters[0]);
                    statement = converter.Convert(Expression.Body);
                }

                return statement;
            }
        }
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            return Statement.ToSql(context, sqlBuilder, outputParams);
        }
        public override string ToString()
        {
            return Expression.ToString();
        }

        public static implicit operator ExpressionStatement<T>(Expression<Func<T, bool>> expression)
        {
            return new ExpressionStatement<T>(expression);
        }
    }

    /// <summary>
    /// 表示实体属性（列）的语句，例如用于生成 "Table.Column" 或列的表达式。
    /// </summary>
    public class PropertyStatement : Statement
    {
        /// <summary>
        /// 用于序列化/反序列化 的无参构造。
        /// </summary>
        public PropertyStatement()
        {
        }

        /// <summary>
        /// 使用属性名构造一个属性语句。
        /// </summary>
        /// <param name="propertyName">属性（列）名称</param>
        public PropertyStatement(string propertyName)
        {
            PropertyName = propertyName;
        }

        /// <summary>
        /// 属性（列）名称
        /// </summary>
        public string PropertyName { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// 会根据上下文（是否单表查询、是否存在表别名）选择使用列的格式化名称或表达式。
        /// 如果属性不存在则抛出异常。
        /// </remarks>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            Column column = context.Table.GetColumn(PropertyName);
            if (column == null) throw new Exception($"Property \"{PropertyName}\" does not exist in type \"{context.Table.DefinitionType.FullName}\". ");
            string tableAlias = context.TableAliasName;
            return tableAlias == null ? (context.SingleTable ? column.FormattedName(sqlBuilder) : column.FormattedExpression(sqlBuilder)) : $"[{tableAlias}].[{column.Name}]";
        }

        public override string ToString()
        {
            return $"[{PropertyName}]";
        }
    }


    public class UnaryStatement : Statement
    {
        /// <summary>
        /// 无参构造。
        /// </summary>
        public UnaryStatement()
        {
        }
        /// <summary>
        /// 使用单目操作符与操作对象构造语句。
        /// </summary>
        /// <param name="oper">单目操作符</param>
        /// <param name="operand">操作对象</param>
        public UnaryStatement(UnaryOperator oper, Statement operand)
        {
            Operator = oper;
            Operand = operand;
        }
        /// <summary>
        /// 单目操作符
        /// </summary>
        public UnaryOperator Operator { get; set; }
        /// <summary>
        /// 操作对象
        /// </summary>
        public Statement Operand { get; set; }
        /// <inheritdoc/>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            switch (Operator)
            {
                case UnaryOperator.Not:
                    return $"NOT {Operand.ToSql(context, sqlBuilder, outputParams)}";
                case UnaryOperator.Nagive:
                    return $"-{Operand.ToSql(context, sqlBuilder, outputParams)}";
                case UnaryOperator.BitwiseNot:
                    return $"~{Operand.ToSql(context, sqlBuilder, outputParams)}";
                default:
                    return Operand.ToSql(context, sqlBuilder, outputParams);
            }
        }

        public override string ToString()
        {
            switch (Operator)
            {
                case UnaryOperator.Not:
                    return $"NOT {Operand?.ToString()}";
                case UnaryOperator.Nagive:
                    return $"-{Operand?.ToString()}";
                case UnaryOperator.BitwiseNot:
                    return $"~{Operand?.ToString()}";
                default:
                    return Operand?.ToString();
            }
        }
    }

    /// <summary>
    /// 表示一个值常量或一组常量（用于 IN 列表）的语句。
    /// </summary>
    public class ValueStatement : Statement
    {
        /// <summary>
        /// 无参构造。
        /// </summary>
        public ValueStatement()
        {
        }

        /// <summary>
        /// 使用值构造 ValueStatement。
        /// </summary>
        /// <param name="value">值，可以是单个值或可枚举集合（用于 IN）</param>
        public ValueStatement(object value)
        {
            Value = value;
        }

        /// <summary>
        /// 常量值或集合
        /// </summary>
        public object Value { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// - 如果值为 null，返回 "NULL"。
        /// - 如果值为集合（且不是字符串），将为集合中每个元素生成参数并返回类似 "( @p0, @p1, ... )" 的字符串，适用于 IN 表达式。
        /// - 否则生成单个参数并返回对应的参数占位符。
        /// </remarks>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            if (Value == null) return "NULL";
            else if (Value is IEnumerable enumerable && !(Value is string))
            {
                StringBuilder sb = new StringBuilder('(');
                foreach (var item in enumerable)
                {
                    if (item is Statement s)
                    {
                        if (sb.Length > 0) sb.Append(", ");
                        sb.Append(s.ToSql(context, sqlBuilder, outputParams));
                    }
                    else
                    {
                        string paramName = outputParams.Count.ToString();
                        outputParams.Add(new(sqlBuilder.ToParamName(paramName), item));
                        if (sb.Length > 0) sb.Append(", ");
                        sb.Append(sqlBuilder.ToSqlParam(paramName));
                    }
                }
                sb.Append(')');
                return sb.ToString();
            }
            else
            {
                string paramName = outputParams.Count.ToString();
                outputParams.Add(new(sqlBuilder.ToParamName(paramName), Value));
                return sqlBuilder.ToSqlParam(paramName);
            }
        }

        public override string ToString()
        {
            if (Value == null) return "NULL";
            else if (Value is IEnumerable enumerable && !(Value is string))
            {
                StringBuilder sb = new StringBuilder('(');
                foreach (var item in enumerable)
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(item);
                }
                sb.Append(')');
                return sb.ToString();
            }
            else
                return Value.ToString();
        }

        public override bool Equals(object obj)
        {
            return Equals(Value,obj)|| obj is ValueStatement vs && Equals(Value, vs.Value); 
        }
    }

    /// <summary>
    /// 表示函数调用语句，例如 <c>SUM(column)</c>、<c>COALESCE(a,b)</c> 等。
    /// </summary>
    public class FunctionStatement : Statement
    {
        /// <summary>
        /// 构造函数，初始化空参数列表。
        /// </summary>
        public FunctionStatement()
        {
            Parameters = new List<Statement>();
        }

        /// <summary>
        /// 使用函数名与参数构造函数语句。
        /// </summary>
        /// <param name="functionName">函数名</param>
        /// <param name="parameters">参数语句列表</param>
        public FunctionStatement(string functionName, params Statement[] parameters)
        {
            FunctionName = functionName;
            Parameters = parameters.ToList();
        }

        /// <summary>
        /// 函数名
        /// </summary>
        public string FunctionName { get; set; }

        /// <summary>
        /// 参数语句列表
        /// </summary>
        public List<Statement> Parameters { get; set; }

        /// <inheritdoc/>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(FunctionName);
            sb.Append('(');
            for (int i = 0; i < Parameters.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Parameters[i].ToSql(context, sqlBuilder, outputParams));
            }
            sb.Append(')');
            return sb.ToString();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(FunctionName);
            sb.Append('(');
            for (int i = 0; i < Parameters.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Parameters[i]);
            }
            sb.Append(')');
            return sb.ToString();
        }
    }

    /// <summary>
    /// 二元条件语句，例如 <c>a = b</c>, <c>name LIKE '%abc%'</c>, <c>id IN (1,2,3)</c> 等。
    /// 支持带 NOT 前缀的操作（例如 NOT IN、NOT LIKE）。
    /// </summary>
    public class BinaryStatement : Statement
    {
        private static Dictionary<BinaryOperator, string> operatorSymbols = new(){
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
        public BinaryStatement()
        {
        }

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
        public BinaryOperator OriginOperator => Operator & (BinaryOperator.Not - 1);

        /// <summary>
        /// 右侧语句
        /// </summary>
        public Statement Right { get; set; }

        /// <inheritdoc/>
        /// <remarks>
        /// - 对于 REGEXP_LIKE，生成形如 <c>REGEXP_LIKE(left,right)</c> 的调用。
        /// - 对于 equals 且右值为 NULL，生成 IS NULL / IS NOT NULL。
        /// - 对于 LIKE/StartsWith/EndsWith/Contains 等，会依据右侧是 ValueStatement 还是表达式生成带参数或带通配符的 SQL，并为需要的值添加参数到 <paramref name="outputParams"/>。
        /// - 对于复杂字符串拼接或需要转义的情况，使用 <see cref="ISqlBuilder.ConcatSql"/> 以便兼容不同数据库的拼接语法。
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
                    if (Right is ValueStatement vs && vs.Value == null)
                    {
                        if (Operator == BinaryOperator.Equal)
                            return $"{Left.ToSql(context, sqlBuilder, outputParams)} is null";
                        else
                            return $"{Left.ToSql(context, sqlBuilder, outputParams)} is not null";
                    }
                    else
                        return $"{Left.ToSql(context, sqlBuilder, outputParams)} {op} {Right.ToSql(context, sqlBuilder, outputParams)}";
                case BinaryOperator.Concat:
                    return sqlBuilder.ConcatSql(Left.ToSql(context, sqlBuilder, outputParams), Right.ToSql(context, sqlBuilder, outputParams));
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
                                right = sqlBuilder.ConcatSql(right, "%"); break;
                            case BinaryOperator.EndsWith:
                                right = sqlBuilder.ConcatSql("%", right); break;
                            case BinaryOperator.Contains:
                                right = sqlBuilder.ConcatSql("%", right, "%"); break;
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
            return $"{Left?.ToString()} {op} {Right?.ToString()}";
        }
    }

    /// <summary>
    /// 多语句集合（AND / OR / 逗号分隔），通常用于组合多个条件或生成列列表等。
    /// </summary>
    public class StatementSet : Statement, ICollection<Statement>
    {
        /// <summary>
        /// 构造并初始化空集合。
        /// </summary>
        public StatementSet()
        {
            Items = new List<Statement>();
        }

        /// <summary>
        /// 使用一组语句初始化集合。
        /// </summary>
        /// <param name="items">语句项</param>
        public StatementSet(params Statement[] items)
        {
            Items = items.ToList();
        }

        /// <summary>
        /// 使用指定连接类型和语句项初始化集合。
        /// </summary>
        /// <param name="joinType">连接类型（And/Or/Comma）</param>
        /// <param name="items">语句项</param>
        public StatementSet(StatementJoinType joinType, params Statement[] items)
        {
            JoinType = joinType;
            Items = items.ToList();
        }

        /// <summary>
        /// 集合的连接类型
        /// </summary>
        public StatementJoinType JoinType { get; set; }

        /// <summary>
        /// 集合中的语句项
        /// </summary>
        public List<Statement> Items { get; set; }

        public int Count => Items.Count;

        public bool IsReadOnly => false;

        public void Add(Statement item)
        {
            Items.Add(item);
        }

        public void Clear()
        {
            Items.Clear();
        }

        public bool Contains(Statement item)
        {
            return Items.Contains(item);
        }

        public void CopyTo(Statement[] array, int arrayIndex)
        {
            Items.CopyTo(array, arrayIndex);
        }

        public IEnumerator<Statement> GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        public bool Remove(Statement item)
        {
            return Items.Remove(item);
        }

        /// <inheritdoc/>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            StringBuilder sb = new StringBuilder();
            string joinStr = JoinType == StatementJoinType.And ? " AND " : (JoinType == StatementJoinType.Or ? " OR " : ",");
            sb.Append('(');
            for (int i = 0; i < Items.Count; i++)
            {
                if (i > 0) sb.Append(joinStr);
                sb.Append(Items[i].ToSql(context, sqlBuilder, outputParams));
            }
            sb.Append(')');
            return sb.ToString();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            string joinStr = JoinType == StatementJoinType.And ? " AND " : (JoinType == StatementJoinType.Or ? " OR " : ",");
            sb.Append('(');
            for (int i = 0; i < Items.Count; i++)
            {
                if (i > 0) sb.Append(joinStr);
                sb.Append(Items[i]);
            }
            sb.Append(')');
            return sb.ToString();
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// 语句集合的连接类型枚举。
    /// </summary>
    public enum StatementJoinType
    {
        /// <summary>使用 AND 连接</summary>
        And,
        /// <summary>使用 OR 连接</summary>
        Or,
        /// <summary>使用逗号连接（通常用于列列表）</summary>
        Comma
    }

    /// <summary>
    /// 单目操作符
    /// </summary>
    public enum UnaryOperator
    {
        /// <summary>
        /// 逻辑取反
        /// </summary>
        Not = 0,
        /// <summary>
        /// 负号
        /// </summary>
        Nagive = 1,
        /// <summary>
        /// 按位取反
        /// </summary>
        BitwiseNot = 2
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
    /// <summary>
    /// 通过委托生成的 SQL 片段。
    /// </summary>
    public class GeneralSqlStatement : Statement
    {
        /// <summary>
        /// 使用委托构造，可以在生成 SQL 时依据上下文动态生成字符串。
        /// </summary>
        /// <param name="func">处理上下文并返回 SQL 字符串的委托</param>
        public GeneralSqlStatement(Expression<Func<SqlBuildContext, ISqlBuilder, ICollection<KeyValuePair<string, object>>, string>> func)
        {
            sqlHandler = func;
        }

        private Expression<Func<SqlBuildContext, ISqlBuilder, ICollection<KeyValuePair<string, object>>, string>> sqlHandler;
        /// <inheritdoc/>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            return sqlHandler?.Compile()?.Invoke(context, sqlBuilder, outputParams);
        }

        public override string ToString()
        {
            return sqlHandler?.ToString();
        }
    }

    /// <summary>
    /// 表示静态原始 SQL 片段。
    /// </summary>
    public class RawSqlStatement : Statement
    {
        /// <summary>
        /// 无参构造。
        /// </summary>
        public RawSqlStatement()
        {
        }

        /// <summary>
        /// 使用指定 SQL 字符串构造。
        /// </summary>
        /// <param name="sql">原始 SQL 片段</param>
        public RawSqlStatement(string sql)
        {
            Sql = sql;
        }

        /// <summary>
        /// 指定的静态 SQL 字符串。
        /// </summary>
        public string Sql { get; set; }
        /// <inheritdoc/>
        public override string ToSql(SqlBuildContext context, ISqlBuilder sqlBuilder, ICollection<KeyValuePair<string, object>> outputParams)
        {
            return Sql;
        }

        public override string ToString()
        {
            return Sql;
        }
    }


    /// <summary>
    /// 
    /// </summary>
    public static class StatementExt
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static Statement And(this Statement left, Statement right)
        {
            if (left == null) return right;
            else if (right == null) return left;
            StatementSet leftSet = left as StatementSet;
            StatementSet rightSet = right as StatementSet;
            if (leftSet?.JoinType == StatementJoinType.And && rightSet?.JoinType == StatementJoinType.And)
            {
                foreach (var item in rightSet.Items)
                {
                    leftSet.Add(item);
                }
                return leftSet;
            }
            else if (leftSet?.JoinType == StatementJoinType.And)
            {
                leftSet.Add(right);
                return leftSet;
            }
            else if (rightSet?.JoinType == StatementJoinType.And)
            {
                rightSet.Add(left);
                return rightSet;
            }
            else
                return new StatementSet(left, right);
        }

        public static Statement Or(this Statement left, Statement right)
        {
            if (left == null || right == null) return null;
            StatementSet leftSet = left as StatementSet;
            StatementSet rightSet = right as StatementSet;
            if (leftSet?.JoinType == StatementJoinType.Or)
            {
                leftSet.Add(right);
                return leftSet;
            }
            else if (rightSet?.JoinType == StatementJoinType.Or)
            {
                rightSet.Add(left);
                return rightSet;
            }
            else
                return new StatementSet(StatementJoinType.Or, left, right);
        }
    }
}
