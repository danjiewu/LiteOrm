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
            Console.WriteLine("===== LiteOrm 表达式全方案演示 =====");

            ShowSection("1. 基础值与属性表达式", () => {
                // [方式1] 构造函数
                var p1 = new PropertyExpr("Age");
                Print(p1, "属性 (构造函数方式)", "new PropertyExpr(\"Age\")");

                // [方式2] 静态工厂 (最推荐)
                var v2 = Expr.Const("张三");
                Print(v2, "常量值 (工厂方式)", "Expr.Const(\"张三\")");
            });

            ShowSection("2. 逻辑比较与组合", () => {
                var age = Expr.Property("Age");
                var dept = Expr.Property("DeptId");

                // [方式1] 运算符重载
                var cond1 = age > 18;
                Print(cond1, "二元比较 (运算符方式)", "Expr.Property(\"Age\") > 18");

                var composite1 = cond1 & (dept == 101);
                Print(composite1, "逻辑组合 (运算符方式)", "(age > 18) & (dept == 101)");

                // [方式2] 链式扩展方法
                var composite2 = age.GreaterThan(20).And(dept.In(101, 102));
                Print(composite2, "逻辑组合 (Fluent方式)", "age.GreaterThan(20).And(dept.In(101, 102))");
            });

            ShowSection("3. 结构化查询模型 (Select/Update/Delete)", () => {
                // Select 链式构建
                var query = Expr.Table<User>()
                    .Where(Expr.Property("Age") > 20)
                    .Select(Expr.Property("Id"), Expr.Property("UserName").As("Name"))
                    .OrderBy(Expr.Property("Id").Desc());
                
                string code = "Expr.Table<User>()\n" +
                              "    .Where(Expr.Property(\"Age\") > 20)\n" +
                              "    .Select(Expr.Property(\"Id\"), Expr.Property(\"UserName\").As(\"Name\"))\n" +
                              "    .OrderBy(Expr.Property(\"Id\").Desc())";
                Print(query, "SELECT 完整模型", code);

                // Update 模型
                var update = new UpdateExpr(Expr.Table<User>(), Expr.Property("Id") == 1);
                update.Sets.Add(("UserName", "NewName"));
                Print(update, "UPDATE 模型", "new UpdateExpr(Expr.Table<User>(), Expr.Property(\"Id\") == 1) { Sets = { (\"UserName\", \"NewName\") } }");
            });

            ShowSection("4. Lambda 自动转换", () => {
                var composite = Expr.Exp<User>(u => u.Age > 25 && u.UserName.Contains("A"));
                Print(composite, "Lambda 转 Expr", "Expr.Exp<User>(u => u.Age > 25 && u.UserName.Contains(\"A\"))");
            });

            ShowSection("5. 删除与其它片段 (Delete)", () => {
                // 删除模型
                var delete = new DeleteExpr(Expr.Table<User>(), Expr.Property("Age") < 18);
                Print(delete, "DELETE 模型", "new DeleteExpr(Expr.Table<User>(), Expr.Property(\"Age\") < 18)");
            });
        }

        private static void ShowSection(string title, Action action)
        {
            Console.WriteLine($"\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine($"  {title}");
            Console.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            action();
        }

        private static void Print(Expr expr, string label, string csharpCode)
        {
            Console.WriteLine($"\n[示例: {label}]");
            Console.WriteLine($"------------------------------------------------------------");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"→ 关键构建代码:");
            Console.WriteLine(csharpCode);
            Console.ResetColor();

            Console.WriteLine($"→ 内容输出 (ToString): {expr}");

            // 序列化
            string json = JsonSerializer.Serialize(expr, _jsonOptions);
            Console.WriteLine($"→ 序列化 (JSON):");
            Console.WriteLine(json);

            // 反序列化并校验
            var deserialized = JsonSerializer.Deserialize<Expr>(json, _jsonOptions);
            bool isEqual = expr.Equals(deserialized);
            Console.WriteLine($"→ 反序列化校验: {(isEqual ? "一致 √" : "不一致 X")}");
        }
    }
}
