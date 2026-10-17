using System.Text;
using System.Text.RegularExpressions;

namespace LiteOrm.AotDemo.Scenarios
{
    /// <summary>
    /// 演示输出助手：统一分节标题、缩进与 SQL 打印。
    /// </summary>
    public static class DemoOutput
    {
        /// <summary>打印场景标题。</summary>
        /// <param name="title">场景名称。</param>
        public static void Section(string title)
        {
            Console.WriteLine();
            Console.WriteLine($"── {title} ──");
        }

        /// <summary>打印一行普通输出。</summary>
        /// <param name="text">文本内容。</param>
        public static void Line(string text) => Console.WriteLine($"  {text}");

        /// <summary>打印键值对形式的输出。</summary>
        /// <param name="key">字段名。</param>
        /// <param name="value">字段值。</param>
        public static void Pair(string key, object? value) => Console.WriteLine($"  {key}: {value}");

        /// <summary>打印最近一条执行的 SQL（压缩空白后单行显示）。</summary>
        public static void LastSql()
        {
            string? sql = LiteOrm.SessionManager.Current?.SqlStack?.LastOrDefault();
            if (string.IsNullOrEmpty(sql)) return;
            Console.WriteLine($"  SQL: {Collapse(sql)}");
        }

        /// <summary>打印异常信息，不中断演示。</summary>
        /// <param name="ex">异常实例。</param>
        public static void Error(Exception ex) => Console.WriteLine($"  [错误] {ex.GetType().Name}: {ex.Message}");

        private static string Collapse(string sql)
        {
            return Regex.Replace(sql, @"\s+", " ").Trim();
        }

        /// <summary>把一组值拼接为可读的表格行，便于控制台对齐输出。</summary>
        /// <param name="columns">各列文本。</param>
        /// <returns>以 " | " 连接的字符串。</returns>
        public static string Row(params object?[] columns)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < columns.Length; i++)
            {
                if (i > 0) sb.Append(" | ");
                sb.Append(columns[i]);
            }
            return sb.ToString();
        }
    }
}
