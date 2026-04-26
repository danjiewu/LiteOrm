using System;
using System.Collections.Generic;
using System.Globalization;

namespace LiteOrm.CodeGen;

internal enum SqlTokenKind
{
    Identifier,
    String,
    Number,
    Symbol,
    End
}

internal readonly record struct SqlToken(SqlTokenKind Kind, string Text, int Position);

internal static class SqlTokenizer
{
    public static List<SqlToken> Tokenize(string sql)
    {
        var tokens = new List<SqlToken>();
        int i = 0;
        while (i < sql.Length)
        {
            char ch = sql[i];
            if (char.IsWhiteSpace(ch))
            {
                i++;
                continue;
            }

            if (ch == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                i += 2;
                while (i < sql.Length && sql[i] != '\n')
                    i++;
                continue;
            }

            if (char.IsLetter(ch) || ch == '_')
            {
                int start = i++;
                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_' || sql[i] == '$'))
                    i++;
                tokens.Add(new SqlToken(SqlTokenKind.Identifier, sql[start..i], start));
                continue;
            }

            if (char.IsDigit(ch))
            {
                int start = i++;
                while (i < sql.Length && (char.IsDigit(sql[i]) || sql[i] == '.'))
                    i++;
                tokens.Add(new SqlToken(SqlTokenKind.Number, sql[start..i], start));
                continue;
            }

            if (ch == '\'' )
            {
                int start = i++;
                while (i < sql.Length)
                {
                    if (sql[i] == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'')
                    {
                        i += 2;
                        continue;
                    }
                    if (sql[i] == '\'')
                    {
                        i++;
                        break;
                    }
                    i++;
                }
                tokens.Add(new SqlToken(SqlTokenKind.String, sql[start..i], start));
                continue;
            }

            if (ch == '[')
            {
                int start = ++i;
                while (i < sql.Length && sql[i] != ']')
                    i++;
                string text = sql[start..i];
                if (i < sql.Length)
                    i++;
                tokens.Add(new SqlToken(SqlTokenKind.Identifier, text, start - 1));
                continue;
            }

            if (ch == '"' || ch == '`')
            {
                char quote = ch;
                int start = ++i;
                while (i < sql.Length && sql[i] != quote)
                    i++;
                string text = sql[start..i];
                if (i < sql.Length)
                    i++;
                tokens.Add(new SqlToken(SqlTokenKind.Identifier, text, start - 1));
                continue;
            }

            if (i + 1 < sql.Length)
            {
                string two = sql.Substring(i, 2);
                if (two is "<=" or ">=" or "<>" or "!=")
                {
                    tokens.Add(new SqlToken(SqlTokenKind.Symbol, two, i));
                    i += 2;
                    continue;
                }
            }

            tokens.Add(new SqlToken(SqlTokenKind.Symbol, ch.ToString(CultureInfo.InvariantCulture), i));
            i++;
        }

        tokens.Add(new SqlToken(SqlTokenKind.End, string.Empty, sql.Length));
        return tokens;
    }
}
