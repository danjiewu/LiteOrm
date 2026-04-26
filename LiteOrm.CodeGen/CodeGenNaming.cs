using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace LiteOrm.CodeGen;

public static class CodeGenNaming
{
    private static readonly Regex SplitRegex = new(@"[^A-Za-z0-9]+", RegexOptions.Compiled);

    public static string ToClassName(string tableName)
    {
        return Singularize(ToPascalCase(tableName));
    }

    public static string ToPropertyName(string columnName)
    {
        var name = ToPascalCase(columnName);
        return char.IsDigit(name[0]) ? "_" + name : name;
    }

    public static string ToPascalCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "GeneratedName";

        var parts = SplitRegex.Split(text)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        if (parts.Length == 0)
            return "GeneratedName";

        var sb = new StringBuilder();
        foreach (var rawPart in parts)
        {
            var chars = SplitCamelLike(rawPart);
            foreach (var part in chars)
            {
                if (part.Length == 0)
                    continue;

                var lower = part.ToLowerInvariant();
                sb.Append(char.ToUpperInvariant(lower[0]));
                if (lower.Length > 1)
                    sb.Append(lower.Substring(1));
            }
        }

        return sb.Length == 0 ? "GeneratedName" : sb.ToString();
    }

    public static string ToCSharpTypeName(Type type, bool nullable)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
            return ToCSharpTypeName(underlying, true);
        }

        string name = type == typeof(string) ? "string"
            : type == typeof(bool) ? "bool"
            : type == typeof(byte) ? "byte"
            : type == typeof(short) ? "short"
            : type == typeof(int) ? "int"
            : type == typeof(long) ? "long"
            : type == typeof(float) ? "float"
            : type == typeof(double) ? "double"
            : type == typeof(decimal) ? "decimal"
            : type == typeof(DateTime) ? "DateTime"
            : type == typeof(Guid) ? "Guid"
            : type == typeof(TimeSpan) ? "TimeSpan"
            : type == typeof(byte[]) ? "byte[]"
            : type.FullName == "System.DateOnly" ? "DateOnly"
            : type.FullName == "System.TimeOnly" ? "TimeOnly"
            : type.Name;

        if (nullable && type.IsValueType && type != typeof(byte[]))
            return name + "?";

        return name;
    }

    private static IEnumerable<string> SplitCamelLike(string text)
    {
        if (text.Length == 0)
            yield break;

        var sb = new StringBuilder();
        sb.Append(text[0]);

        for (int i = 1; i < text.Length; i++)
        {
            var ch = text[i];
            var prev = text[i - 1];
            if (char.IsUpper(ch) && (char.IsLower(prev) || (i + 1 < text.Length && char.IsLower(text[i + 1]))))
            {
                yield return sb.ToString();
                sb.Clear();
            }
            sb.Append(ch);
        }

        yield return sb.ToString();
    }

    private static string Singularize(string name)
    {
        if (name.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && name.Length > 3)
            return name.Substring(0, name.Length - 3) + "y";
        if (name.EndsWith("sses", StringComparison.OrdinalIgnoreCase) || name.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
            return name;
        if (name.EndsWith("s", StringComparison.OrdinalIgnoreCase) && name.Length > 1)
            return name.Substring(0, name.Length - 1);
        return name;
    }
}
