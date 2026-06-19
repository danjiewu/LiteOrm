using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LiteOrm.SqlToExpr;

internal sealed class SelectSqlParser
{
    private List<SqlToken> _tokens = null!;
    private int _index;

    public ParsedSelectQuery Parse(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new InvalidOperationException("SQL 不能为空。");

        _tokens = SqlTokenizer.Tokenize(sql);
        _index = 0;

        RejectUnsupportedKeyword("WITH");
        RejectUnsupportedKeyword("UNION");
        RejectUnsupportedKeyword("INTERSECT");
        RejectUnsupportedKeyword("EXCEPT");
        RejectUnsupportedKeyword("OVER");
        RejectUnsupportedKeyword("HAVING");

        ExpectIdentifier("SELECT");
        if (MatchIdentifier("DISTINCT"))
            throw new InvalidOperationException("首版暂不支持 DISTINCT。");

        var query = new ParsedSelectQuery();
        foreach (var segment in ReadSegmentsUntil("FROM"))
            query.Projections.Add(ParseProjection(segment));

        ExpectIdentifier("FROM");
        query.MainSource = ParseTableSource();

        while (IsJoinStart())
            query.Joins.Add(ParseJoin());

        if (MatchIdentifier("WHERE"))
            query.Where = ParseFilterExpression(stopKeywords: ["GROUP", "ORDER"]);

        if (MatchIdentifier("GROUP"))
        {
            ExpectIdentifier("BY");
            foreach (var segment in ReadSegmentsUntil("ORDER"))
                query.GroupBy.Add(ParseColumnReference(segment));
        }

        if (MatchIdentifier("ORDER"))
        {
            ExpectIdentifier("BY");
            foreach (var segment in ReadSegmentsUntil())
                query.OrderBy.Add(ParseOrderBy(segment));
        }

        ExpectEnd();
        return query;
    }

    private void RejectUnsupportedKeyword(string keyword)
    {
        if (_tokens.Any(t => t.Kind == SqlTokenKind.Identifier && t.Text.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"首版暂不支持 {keyword}。");
    }

    private List<SqlToken> ReadUntil(params string[] stopKeywords)
    {
        var result = new List<SqlToken>();
        int depth = 0;
        while (true)
        {
            var token = Peek();
            if (token.Kind == SqlTokenKind.End)
                break;
            if (depth == 0 && stopKeywords.Length > 0 && token.Kind == SqlTokenKind.Identifier &&
                stopKeywords.Any(k => token.Text.Equals(k, StringComparison.OrdinalIgnoreCase)))
                break;

            Advance();
            if (token.Text == "(") depth++;
            else if (token.Text == ")") depth--;
            result.Add(token);
        }
        return result;
    }

    private List<List<SqlToken>> ReadSegmentsUntil(params string[] stopKeywords)
    {
        var raw = ReadUntil(stopKeywords);
        return SplitByComma(raw);
    }

    private ParsedProjection ParseProjection(List<SqlToken> tokens)
    {
        if (tokens.Count == 1 && tokens[0].Text == "*")
            return new ParsedProjection { Kind = ProjectionKind.Wildcard };

        if (tokens.Count == 3 && tokens[1].Text == "." && tokens[2].Text == "*")
        {
            return new ParsedProjection
            {
                Kind = ProjectionKind.Wildcard,
                WildcardAlias = NormalizeIdentifier(tokens[0].Text)
            };
        }

        string? alias = null;
        if (tokens.Count >= 3 && IsIdentifier(tokens[^2], "AS"))
        {
            alias = NormalizeIdentifier(tokens[^1].Text);
            tokens = tokens[..^2];
        }
        else if (tokens.Count >= 2 && tokens[^1].Kind == SqlTokenKind.Identifier)
        {
            if (!ContainsSymbol(tokens[..^1], ".") || ContainsSymbol(tokens[..^1], "(") || LooksLikeCaseExpression(tokens[..^1]))
            {
                alias = NormalizeIdentifier(tokens[^1].Text);
                tokens = tokens[..^1];
            }
        }

        if (LooksLikeCaseExpression(tokens))
        {
            return new ParsedProjection
            {
                Kind = ProjectionKind.Function,
                Function = ParseCaseFunction(tokens),
                Alias = alias
            };
        }

        if (LooksLikeFunctionCall(tokens))
        {
            return new ParsedProjection
            {
                Kind = ProjectionKind.Function,
                Function = ParseFunction(tokens),
                Alias = alias
            };
        }

        return new ParsedProjection
        {
            Kind = ProjectionKind.Column,
            Column = ParseColumnReference(tokens),
            Alias = alias
        };
    }

    private ParsedFunctionCall ParseFunction(List<SqlToken> tokens)
    {
        if (!LooksLikeFunctionCall(tokens))
            throw new InvalidOperationException("仅支持简单函数投影。");

        var function = new ParsedFunctionCall
        {
            Name = NormalizeIdentifier(tokens[0].Text)
        };

        var argumentTokens = tokens.Skip(2).Take(tokens.Count - 3).ToList();
        if (argumentTokens.Count == 0)
            return function;

        if (argumentTokens.Count == 1 && argumentTokens[0].Text == "*")
        {
            function.IsStarArgument = true;
            return function;
        }

        if (function.Name.Equals("CAST", StringComparison.OrdinalIgnoreCase))
        {
            int asIndex = FindTopLevelKeyword(argumentTokens, "AS");
            if (asIndex <= 0 || asIndex >= argumentTokens.Count - 1)
                throw new InvalidOperationException("CAST 仅支持 CAST(value AS type) 形式。");

            function.Arguments.Add(ParseScalarExpression(argumentTokens[..asIndex]));
            function.Arguments.Add(new ParsedSqlTypeExpression
            {
                TypeName = BuildSqlTypeName(argumentTokens[(asIndex + 1)..])
            });
            return function;
        }

        var args = SplitByComma(argumentTokens);
        if (args.Count > 0 && args[0].Count > 0 && IsIdentifier(args[0][0], "DISTINCT"))
        {
            function.IsDistinct = true;
            args[0] = args[0].Skip(1).ToList();
            if (args[0].Count == 0)
                throw new InvalidOperationException("DISTINCT 后缺少函数参数。");
        }

        foreach (var arg in args)
            function.Arguments.Add(ParseScalarExpression(arg));
        return function;
    }

    private ParsedScalarExpression ParseScalarExpression(List<SqlToken> tokens)
    {
        if (tokens.Count == 0)
            throw new InvalidOperationException("函数参数不能为空。");

        if (LooksLikeCaseExpression(tokens))
            return new ParsedFunctionExpression { Function = ParseCaseFunction(tokens) };

        if (LooksLikeFunctionCall(tokens))
            return new ParsedFunctionExpression { Function = ParseFunction(tokens) };

        if (tokens.Count == 1 && IsLiteralToken(tokens[0]))
            return new ParsedLiteralExpression { Value = ParseLiteralToken(tokens[0]) };

        return new ParsedColumnExpression { Column = ParseColumnReference(tokens) };
    }

    private ParsedFunctionCall ParseCaseFunction(List<SqlToken> tokens)
    {
        if (!LooksLikeCaseExpression(tokens))
            throw new InvalidOperationException("仅支持 searched CASE 表达式。");

        var function = new ParsedFunctionCall { Name = "CASE" };
        int index = 1;
        if (index < tokens.Count && !IsIdentifier(tokens[index], "WHEN"))
            throw new InvalidOperationException("仅支持 CASE WHEN ... THEN ... END 形式。");

        while (index < tokens.Count)
        {
            if (IsIdentifier(tokens[index], "WHEN"))
            {
                index++;
                var conditionTokens = ReadCaseSegment(tokens, ref index, ["THEN"]);
                if (index >= tokens.Count || !IsIdentifier(tokens[index], "THEN"))
                    throw new InvalidOperationException("CASE WHEN 后缺少 THEN。");

                index++;
                var resultTokens = ReadCaseSegment(tokens, ref index, ["WHEN", "ELSE", "END"]);
                if (resultTokens.Count == 0)
                    throw new InvalidOperationException("CASE THEN 后缺少结果表达式。");

                function.WhenClauses.Add(new ParsedCaseWhenClause
                {
                    Condition = new FilterTokenParser(conditionTokens).Parse(),
                    Result = ParseScalarExpression(resultTokens)
                });
                continue;
            }

            if (IsIdentifier(tokens[index], "ELSE"))
            {
                index++;
                var elseTokens = ReadCaseSegment(tokens, ref index, ["END"]);
                if (elseTokens.Count == 0)
                    throw new InvalidOperationException("CASE ELSE 后缺少结果表达式。");
                function.ElseArgument = ParseScalarExpression(elseTokens);
                continue;
            }

            if (IsIdentifier(tokens[index], "END"))
            {
                index++;
                break;
            }

            throw new InvalidOperationException($"无法解析 CASE 表达式，遇到 {tokens[index].Text}。");
        }

        if (function.WhenClauses.Count == 0)
            throw new InvalidOperationException("CASE 至少需要一个 WHEN ... THEN 分支。");
        if (index != tokens.Count)
            throw new InvalidOperationException("CASE 表达式包含未解析内容。");
        return function;
    }

    private ParsedJoin ParseJoin()
    {
        var join = new ParsedJoin();
        if (MatchIdentifier("INNER"))
        {
            join.JoinType = SqlJoinType.Inner;
        }
        else if (MatchIdentifier("LEFT"))
        {
            join.JoinType = SqlJoinType.Left;
            MatchIdentifier("OUTER");
        }
        else if (MatchIdentifier("RIGHT"))
        {
            join.JoinType = SqlJoinType.Right;
            MatchIdentifier("OUTER");
        }
        else
        {
            join.JoinType = SqlJoinType.Inner;
        }

        ExpectIdentifier("JOIN");
        join.Table = ParseTableSource();
        ExpectIdentifier("ON");

        var onTokens = ReadUntilJoinBoundary();
        foreach (var segment in SplitByKeyword(onTokens, "AND"))
        {
            int eqIndex = segment.FindIndex(t => t.Text == "=");
            if (eqIndex <= 0 || eqIndex >= segment.Count - 1)
                throw new InvalidOperationException("首版仅支持以 AND 连接的等值 JOIN 条件。");

            join.Conditions.Add(new ParsedJoinCondition
            {
                Left = ParseColumnReference(segment[..eqIndex]),
                Right = ParseColumnReference(segment[(eqIndex + 1)..])
            });
        }

        return join;
    }

    private List<SqlToken> ReadUntilJoinBoundary()
    {
        var tokens = new List<SqlToken>();
        int depth = 0;
        while (true)
        {
            var token = Peek();
            if (token.Kind == SqlTokenKind.End)
                break;
            if (depth == 0 && (IsJoinStart() || IsIdentifier(token, "WHERE") || IsIdentifier(token, "GROUP") || IsIdentifier(token, "ORDER")))
                break;

            Advance();
            if (token.Text == "(") depth++;
            else if (token.Text == ")") depth--;
            tokens.Add(token);
        }
        return tokens;
    }

    private ParsedTableSource ParseTableSource()
    {
        var table = ReadIdentifierValue();
        string alias = table;
        if (MatchIdentifier("AS"))
            alias = ReadIdentifierValue();
        else if (Peek().Kind == SqlTokenKind.Identifier && !IsReservedKeyword(Peek().Text))
            alias = ReadIdentifierValue();

        return new ParsedTableSource
        {
            TableName = NormalizeIdentifier(table),
            Alias = NormalizeIdentifier(alias)
        };
    }

    private ParsedOrderBy ParseOrderBy(List<SqlToken> tokens)
    {
        bool ascending = true;
        if (tokens.Count > 1 && tokens[^1].Kind == SqlTokenKind.Identifier)
        {
            if (tokens[^1].Text.Equals("DESC", StringComparison.OrdinalIgnoreCase))
            {
                ascending = false;
                tokens = tokens[..^1];
            }
            else if (tokens[^1].Text.Equals("ASC", StringComparison.OrdinalIgnoreCase))
            {
                tokens = tokens[..^1];
            }
        }

        return new ParsedOrderBy
        {
            Column = ParseColumnReference(tokens),
            Ascending = ascending
        };
    }

    private FilterNode ParseFilterExpression(params string[] stopKeywords)
    {
        var parser = new FilterTokenParser(ReadUntil(stopKeywords));
        return parser.Parse();
    }

    private ParsedColumnReference ParseColumnReference(List<SqlToken> tokens)
    {
        if (tokens.Count == 1 && tokens[0].Kind == SqlTokenKind.Identifier)
        {
            return new ParsedColumnReference
            {
                ColumnName = NormalizeIdentifier(tokens[0].Text)
            };
        }

        if (tokens.Count == 3 && tokens[0].Kind == SqlTokenKind.Identifier && tokens[1].Text == "." && tokens[2].Kind == SqlTokenKind.Identifier)
        {
            return new ParsedColumnReference
            {
                Alias = NormalizeIdentifier(tokens[0].Text),
                ColumnName = NormalizeIdentifier(tokens[2].Text)
            };
        }

        throw new InvalidOperationException("仅支持简单列引用。");
    }

    private static List<List<SqlToken>> SplitByComma(List<SqlToken> tokens)
    {
        var result = new List<List<SqlToken>>();
        var current = new List<SqlToken>();
        int depth = 0;
        foreach (var token in tokens)
        {
            if (token.Text == "(") depth++;
            else if (token.Text == ")") depth--;

            if (depth == 0 && token.Text == ",")
            {
                result.Add(current);
                current = new List<SqlToken>();
                continue;
            }

            current.Add(token);
        }

        if (current.Count > 0)
            result.Add(current);

        return result;
    }

    private static List<List<SqlToken>> SplitByKeyword(List<SqlToken> tokens, string keyword)
    {
        var result = new List<List<SqlToken>>();
        var current = new List<SqlToken>();
        int depth = 0;
        foreach (var token in tokens)
        {
            if (token.Text == "(") depth++;
            else if (token.Text == ")") depth--;

            if (depth == 0 && IsIdentifier(token, keyword))
            {
                result.Add(current);
                current = new List<SqlToken>();
                continue;
            }

            current.Add(token);
        }

        if (current.Count > 0)
            result.Add(current);

        return result;
    }

    private static bool ContainsSymbol(List<SqlToken> tokens, string symbol)
    {
        return tokens.Any(t => t.Text == symbol);
    }

    private static bool LooksLikeFunctionCall(List<SqlToken> tokens)
    {
        if (tokens.Count < 3 || tokens[0].Kind != SqlTokenKind.Identifier || tokens[1].Text != "(" || tokens[^1].Text != ")")
            return false;

        int depth = 0;
        for (int i = 1; i < tokens.Count; i++)
        {
            if (tokens[i].Text == "(") depth++;
            else if (tokens[i].Text == ")") depth--;

            if (depth == 0 && i < tokens.Count - 1)
                return false;
            if (depth < 0)
                return false;
        }

        return depth == 0;
    }

    private static bool LooksLikeCaseExpression(List<SqlToken> tokens)
    {
        if (tokens.Count < 4 || !IsIdentifier(tokens[0], "CASE"))
            return false;

        int caseDepth = 0;
        int parenDepth = 0;
        bool sawWhen = false;
        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.Text == "(") parenDepth++;
            else if (token.Text == ")") parenDepth--;

            if (parenDepth == 0 && token.Kind == SqlTokenKind.Identifier)
            {
                if (token.Text.Equals("CASE", StringComparison.OrdinalIgnoreCase))
                    caseDepth++;
                else if (token.Text.Equals("WHEN", StringComparison.OrdinalIgnoreCase) && caseDepth == 1)
                    sawWhen = true;
                else if (token.Text.Equals("END", StringComparison.OrdinalIgnoreCase))
                {
                    caseDepth--;
                    if (caseDepth == 0)
                        return i == tokens.Count - 1 && sawWhen;
                }
            }
        }

        return false;
    }

    private static List<SqlToken> ReadCaseSegment(List<SqlToken> tokens, ref int index, string[] stopKeywords)
    {
        var result = new List<SqlToken>();
        int parenDepth = 0;
        int nestedCaseDepth = 0;
        while (index < tokens.Count)
        {
            var token = tokens[index];
            if (token.Text == "(") parenDepth++;
            else if (token.Text == ")") parenDepth--;

            if (parenDepth == 0 && token.Kind == SqlTokenKind.Identifier)
            {
                if (token.Text.Equals("CASE", StringComparison.OrdinalIgnoreCase))
                {
                    nestedCaseDepth++;
                }
                else if (token.Text.Equals("END", StringComparison.OrdinalIgnoreCase) && nestedCaseDepth > 0)
                {
                    nestedCaseDepth--;
                }
                else if (nestedCaseDepth == 0 && stopKeywords.Any(keyword => token.Text.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    break;
                }
            }

            result.Add(token);
            index++;
        }

        return result;
    }

    private static int FindTopLevelKeyword(List<SqlToken> tokens, string keyword)
    {
        int depth = 0;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Text == "(") depth++;
            else if (tokens[i].Text == ")") depth--;
            else if (depth == 0 && IsIdentifier(tokens[i], keyword))
                return i;
        }

        return -1;
    }

    private static string BuildSqlTypeName(List<SqlToken> tokens)
    {
        var builder = new StringBuilder();
        SqlToken previous = default;
        bool hasPrevious = false;
        foreach (var token in tokens)
        {
            bool needsSpace = hasPrevious
                && previous.Kind is SqlTokenKind.Identifier or SqlTokenKind.Number
                && token.Kind is SqlTokenKind.Identifier or SqlTokenKind.Number;
            if (needsSpace)
                builder.Append(' ');
            builder.Append(token.Text);
            previous = token;
            hasPrevious = true;
        }

        return builder.ToString();
    }

    private static bool IsLiteralToken(SqlToken token)
    {
        return token.Kind == SqlTokenKind.String
               || token.Kind == SqlTokenKind.Number
               || token.Kind == SqlTokenKind.Identifier && (token.Text.Equals("NULL", StringComparison.OrdinalIgnoreCase)
                                                            || token.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase)
                                                            || token.Text.Equals("FALSE", StringComparison.OrdinalIgnoreCase));
    }

    private static object? ParseLiteralToken(SqlToken token)
    {
        return token.Kind switch
        {
            SqlTokenKind.String => token.Text[1..^1].Replace("''", "'", StringComparison.Ordinal),
            SqlTokenKind.Number => token.Text.Contains('.') ? double.Parse(token.Text, CultureInfo.InvariantCulture) : int.Parse(token.Text, CultureInfo.InvariantCulture),
            SqlTokenKind.Identifier when token.Text.Equals("NULL", StringComparison.OrdinalIgnoreCase) => null,
            SqlTokenKind.Identifier when token.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase) => true,
            SqlTokenKind.Identifier when token.Text.Equals("FALSE", StringComparison.OrdinalIgnoreCase) => false,
            _ => throw new InvalidOperationException($"不支持的字面量：{token.Text}")
        };
    }

    private bool IsJoinStart()
    {
        var token = Peek();
        if (IsIdentifier(token, "JOIN"))
            return true;
        if (IsIdentifier(token, "INNER") || IsIdentifier(token, "LEFT") || IsIdentifier(token, "RIGHT"))
            return true;
        return false;
    }

    private static string NormalizeIdentifier(string text)
    {
        return text.Trim();
    }

    private static bool IsIdentifier(SqlToken token, string value)
    {
        return token.Kind == SqlTokenKind.Identifier && token.Text.Equals(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReservedKeyword(string text)
    {
        return text.Equals("INNER", StringComparison.OrdinalIgnoreCase)
            || text.Equals("LEFT", StringComparison.OrdinalIgnoreCase)
            || text.Equals("RIGHT", StringComparison.OrdinalIgnoreCase)
            || text.Equals("JOIN", StringComparison.OrdinalIgnoreCase)
            || text.Equals("WHERE", StringComparison.OrdinalIgnoreCase)
            || text.Equals("GROUP", StringComparison.OrdinalIgnoreCase)
            || text.Equals("ORDER", StringComparison.OrdinalIgnoreCase)
            || text.Equals("ON", StringComparison.OrdinalIgnoreCase);
    }

    private SqlToken Peek() => _tokens[_index];

    private void Advance() => _index++;

    private bool MatchIdentifier(string value)
    {
        if (IsIdentifier(Peek(), value))
        {
            _index++;
            return true;
        }
        return false;
    }

    private void ExpectIdentifier(string value)
    {
        if (!MatchIdentifier(value))
            throw new InvalidOperationException($"期望关键字 {value}，实际得到 {Peek().Text}。");
    }

    private string ReadIdentifierValue()
    {
        var token = Peek();
        if (token.Kind != SqlTokenKind.Identifier)
            throw new InvalidOperationException($"期望标识符，实际得到 {token.Text}。");
        _index++;
        return token.Text;
    }

    private void ExpectEnd()
    {
        if (Peek().Kind != SqlTokenKind.End)
            throw new InvalidOperationException($"存在未解析内容：{Peek().Text}");
    }

    private sealed class FilterTokenParser
    {
        private readonly List<SqlToken> _tokens;
        private int _index;

        public FilterTokenParser(List<SqlToken> tokens)
        {
            _tokens = tokens;
            _tokens.Add(new SqlToken(SqlTokenKind.End, string.Empty, tokens.Count == 0 ? 0 : tokens[^1].Position + tokens[^1].Text.Length));
        }

        public FilterNode Parse()
        {
            var expr = ParseOr();
            if (Peek().Kind != SqlTokenKind.End)
                throw new InvalidOperationException("WHERE 子句包含暂不支持的结构。");
            return expr;
        }

        private FilterNode ParseOr()
        {
            var left = ParseAnd();
            while (Match("OR"))
            {
                left = new FilterLogicalNode { Operator = FilterLogicalOperator.Or, Left = left, Right = ParseAnd() };
            }
            return left;
        }

        private FilterNode ParseAnd()
        {
            var left = ParsePrimary();
            while (Match("AND"))
            {
                left = new FilterLogicalNode { Operator = FilterLogicalOperator.And, Left = left, Right = ParsePrimary() };
            }
            return left;
        }

        private FilterNode ParsePrimary()
        {
            if (MatchSymbol("("))
            {
                var expr = ParseOr();
                ExpectSymbol(")");
                return expr;
            }

            var left = ParseColumn();

            if (Match("IS"))
            {
                if (Match("NOT"))
                {
                    Expect("NULL");
                    return new FilterComparisonNode { Left = left, Operator = FilterComparisonOperator.IsNotNull };
                }

                Expect("NULL");
                return new FilterComparisonNode { Left = left, Operator = FilterComparisonOperator.IsNull };
            }

            bool not = Match("NOT");
            if (Match("LIKE"))
            {
                char? likeEscapeChar = null;
                var rightLiteral = ParseLiteral();
                if (Match("ESCAPE"))
                {
                    var escapeLiteral = ParseLiteral();
                    if (escapeLiteral is not string escapeText || escapeText.Length != 1)
                        throw new InvalidOperationException("LIKE ESCAPE 仅支持单字符字符串。");
                    likeEscapeChar = escapeText[0];
                }

                return new FilterComparisonNode
                {
                    Left = left,
                    Operator = not ? FilterComparisonOperator.NotLike : FilterComparisonOperator.Like,
                    RightLiteral = rightLiteral,
                    LikeEscapeChar = likeEscapeChar
                };
            }

            if (Match("IN"))
            {
                ExpectSymbol("(");
                var values = new List<object?>();
                do
                {
                    values.Add(ParseLiteral());
                }
                while (MatchSymbol(","));
                ExpectSymbol(")");

                return new FilterComparisonNode
                {
                    Left = left,
                    Operator = not ? FilterComparisonOperator.NotIn : FilterComparisonOperator.In,
                    RightValues = values
                };
            }

            var op = ReadComparisonOperator();
            if (Peek().Kind == SqlTokenKind.Identifier && PeekNext().Text == ".")
            {
                return new FilterComparisonNode
                {
                    Left = left,
                    Operator = op,
                    RightColumn = ParseColumn()
                };
            }

            return new FilterComparisonNode
            {
                Left = left,
                Operator = op,
                RightLiteral = ParseLiteral()
            };
        }

        private ParsedColumnReference ParseColumn()
        {
            var first = ReadIdentifier();
            if (MatchSymbol("."))
            {
                var second = ReadIdentifier();
                return new ParsedColumnReference { Alias = first, ColumnName = second };
            }

            return new ParsedColumnReference { ColumnName = first };
        }

        private FilterComparisonOperator ReadComparisonOperator()
        {
            var token = Peek();
            Advance();
            return token.Text switch
            {
                "=" => FilterComparisonOperator.Equal,
                "!=" or "<>" => FilterComparisonOperator.NotEqual,
                ">" => FilterComparisonOperator.GreaterThan,
                ">=" => FilterComparisonOperator.GreaterThanOrEqual,
                "<" => FilterComparisonOperator.LessThan,
                "<=" => FilterComparisonOperator.LessThanOrEqual,
                _ => throw new InvalidOperationException($"不支持的比较操作符：{token.Text}")
            };
        }

        private object? ParseLiteral()
        {
            var token = Peek();
            Advance();
            return token.Kind switch
            {
                SqlTokenKind.String => token.Text[1..^1].Replace("''", "'", StringComparison.Ordinal),
                SqlTokenKind.Number => token.Text.Contains('.') ? double.Parse(token.Text, CultureInfo.InvariantCulture) : int.Parse(token.Text, CultureInfo.InvariantCulture),
                SqlTokenKind.Identifier when token.Text.Equals("NULL", StringComparison.OrdinalIgnoreCase) => null,
                SqlTokenKind.Identifier when token.Text.Equals("TRUE", StringComparison.OrdinalIgnoreCase) => true,
                SqlTokenKind.Identifier when token.Text.Equals("FALSE", StringComparison.OrdinalIgnoreCase) => false,
                _ => throw new InvalidOperationException($"不支持的字面量：{token.Text}")
            };
        }

        private bool Match(string keyword)
        {
            if (Peek().Kind == SqlTokenKind.Identifier && Peek().Text.Equals(keyword, StringComparison.OrdinalIgnoreCase))
            {
                _index++;
                return true;
            }
            return false;
        }

        private void Expect(string keyword)
        {
            if (!Match(keyword))
                throw new InvalidOperationException($"期望 {keyword}。");
        }

        private bool MatchSymbol(string symbol)
        {
            if (Peek().Text == symbol)
            {
                _index++;
                return true;
            }
            return false;
        }

        private void ExpectSymbol(string symbol)
        {
            if (!MatchSymbol(symbol))
                throw new InvalidOperationException($"期望符号 {symbol}。");
        }

        private string ReadIdentifier()
        {
            if (Peek().Kind != SqlTokenKind.Identifier)
                throw new InvalidOperationException($"期望列名，实际得到 {Peek().Text}");
            var value = Peek().Text;
            _index++;
            return value;
        }

        private SqlToken Peek() => _tokens[_index];

        private SqlToken PeekNext() => _tokens[_index + 1];

        private void Advance() => _index++;
    }
}