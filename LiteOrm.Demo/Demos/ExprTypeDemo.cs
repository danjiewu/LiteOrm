using LiteOrm.Common;
using LiteOrm.Demo.Models;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LiteOrm.Demo.Demos
{
    public static class ExprTypeDemo
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static void RunAll()
        {
            Console.WriteLine("===== LiteOrm ±í´ïÊ½È«·½°¸ÑÝÊ¾ =====");

            ShowSection("1. »ù´¡ÖµÓëÊôÐÔ±í´ïÊ½", () => {
                // [·½Ê½1] ¹¹Ôìº¯Êý
                var p1 = new PropertyExpr("Age");
                Print(p1, "ÊôÐÔ (¹¹Ôìº¯Êý·½Ê½)", "new PropertyExpr(\"Age\")");

                // [·½Ê½2] ¾²Ì¬¹¤³§ (×îÍÆ¼ö)
                var v2 = Expr.Const("ÕÅÈý");
                Print(v2, "³£Á¿Öµ (¹¤³§·½Ê½)", "Expr.Const(\"ÕÅÈý\")");
            });

            ShowSection("2. Âß¼­±È½ÏÓë×éºÏ", () => {
                var age = Expr.Property("Age");
                var dept = Expr.Property("DeptId");

                // [·½Ê½1] ÔËËã·ûÖØÔØ
                var cond1 = age > 18;
                Print(cond1, "¶þÔª±È½Ï (ÔËËã·û·½Ê½)", "Expr.Property(\"Age\") > 18");

                var composite1 = cond1 & (dept == 101);
                Print(composite1, "Âß¼­×éºÏ (ÔËËã·û·½Ê½)", "(age > 18) & (dept == 101)");

                // [·½Ê½2] Á´Ê½À©Õ¹·½·¨
                var composite2 = age.GreaterThan(20).And(dept.In(101, 102));
                Print(composite2, "Âß¼­×éºÏ (Fluent·½Ê½)", "age.GreaterThan(20).And(dept.In(101, 102))");
            });

            ShowSection("3. ½á¹¹»¯²éÑ¯Ä£ÐÍ (Select/Update/Delete)", () => {
                // Select Á´Ê½¹¹½¨
                var query = Expr.Table<User>()
                    .Where(Expr.Property("Age") > 20)
                    .Select(Expr.Property("Id"), Expr.Property("UserName").As("Name"))
                    .OrderBy(Expr.Property("Id").Desc());
                
                string code = "Expr.Table<User>()\n" +
                              "    .Where(Expr.Property(\"Age\") > 20)\n" +
                              "    .Select(Expr.Property(\"Id\"), Expr.Property(\"UserName\").As(\"Name\"))\n" +
                              "    .OrderBy(Expr.Property(\"Id\").Desc())";
                Print(query, "SELECT ÍêÕûÄ£ÐÍ", code);

                // Update Ä£ÐÍ
                var update = new UpdateExpr(Expr.Table<User>(), Expr.Property("Id") == 1);
                update.Sets.Add(("UserName", "NewName"));
                Print(update, "UPDATE Ä£ÐÍ", "new UpdateExpr(Expr.Table<User>(), Expr.Property(\"Id\") == 1) { Sets = { (\"UserName\", \"NewName\") } }");
            });

            ShowSection("4. Lambda ×Ô¶¯×ª»»", () => {
                var composite = Expr.Exp<User>(u => u.Age > 25 && u.UserName.Contains("A"));
                Print(composite, "Lambda ×ª Expr", "Expr.Exp<User>(u => u.Age > 25 && u.UserName.Contains(\"A\"))");
            });

            ShowSection("5. É¾³ýÓëÆäËüÆ¬¶Î (Delete)", () => {
                // É¾³ýÄ£ÐÍ
                var delete = new DeleteExpr(Expr.Table<User>(), Expr.Property("Age") < 18);
                Print(delete, "DELETE Ä£ÐÍ", "new DeleteExpr(Expr.Table<User>(), Expr.Property(\"Age\") < 18)");
            });
        }

        private static void ShowSection(string title, Action action)
        {
            Console.WriteLine($"\n©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥");
            Console.WriteLine($"  {title}");
            Console.WriteLine($"©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥");
            action();
        }

        private static void Print(Expr expr, string label, string csharpCode)
        {
            Console.WriteLine($"\n[Ê¾Àý: {label}]");
            Console.WriteLine($"------------------------------------------------------------");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"¡ú ¹Ø¼ü¹¹½¨´úÂë:");
            Console.WriteLine(csharpCode);
            Console.ResetColor();

            Console.WriteLine($"¡ú ÄÚÈÝÊä³ö (ToString): {expr}");

            // ÐòÁÐ»¯
            string json = JsonSerializer.Serialize(expr, _jsonOptions);
            Console.WriteLine($"¡ú ÐòÁÐ»¯ (JSON):");
            Console.WriteLine(json);

            // ·´ÐòÁÐ»¯²¢Ð£Ñé
            var deserialized = JsonSerializer.Deserialize<Expr>(json, _jsonOptions);
            bool isEqual = expr.Equals(deserialized);
            Console.WriteLine($"¡ú ·´ÐòÁÐ»¯Ð£Ñé: {(isEqual ? "Ò»ÖÂ ¡Ì" : "²»Ò»ÖÂ X")}");
        }
    }
}
