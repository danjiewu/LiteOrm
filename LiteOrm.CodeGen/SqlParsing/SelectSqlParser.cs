using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LiteOrm.CodeGen;

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
            if (!ContainsSymbol(tokens[..^1], ".") || ContainsSymbol(tokens[..^1], "("))
            {
                alias = NormalizeIdentifier(tokens[^1].Text);
                tokens = tokens[..^1];
            }
        }

        if (ContainsSymbol(tokens, "("))
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
        if (tokens.Count < 3 || tokens[1].Text != "(" || tokens[^1].Text != ")")
            throw new InvalidOperationException("仅支持简单函数投影。");

        var function = new ParsedFunctionCall
        {
            Name = NormalizeIdentifier(tokens[0].Text)
        };

        var argumentTokens = tokens.Skip(2).Take(tokens.Count - 3).ToList();
        if (argumentTokens.Count == 1 && argumentTokens[0].Text == "*")
        {
            function.IsStarArgument = true;
            return function;
        }

        if (argumentTokens.Count > 0 && IsIdentifier(argumentTokens[0], "DISTINCT"))
        {
            function.IsDistinct = true;
            argumentTokens = argumentTokens.Skip(1).ToList();
        }

        function.Argument = ParseColumnReference(argumentTokens);
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
                return new FilterComparisonNode
                {
                    Left = left,
                    Operator = not ? FilterComparisonOperator.NotLike : FilterComparisonOperator.Like,
                    RightLiteral = ParseLiteral()
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
