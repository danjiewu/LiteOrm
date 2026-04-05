using LiteOrm;

namespace LiteOrm.WebDemo.Infrastructure;

public static class SqlTraceHelper
{
    public static void Reset() => SessionManager.Current?.Reset();

    public static IReadOnlyList<string> GetLatestSql(int maxCount = 5)
    {
        var sql = SessionManager.Current?.SqlStack ?? Array.Empty<string>();
        return sql.Reverse().Take(maxCount).ToArray();
    }
}
