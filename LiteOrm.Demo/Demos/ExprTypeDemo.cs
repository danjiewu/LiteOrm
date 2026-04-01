using LiteOrm.Common;
using LiteOrm.Demo.Models;
using System.Text.Encodings.Web;
using System.Text.Json;
using static LiteOrm.Common.Expr;

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
            Console.WriteLine("===== 1. 表达式全方案演示 =====");

            ShowSection("1.1 基础值与属性表达式", () =>
            {
                // [方式1] 构造函数
                var p1 = new PropertyExpr("Age");
                Print(p1, "属性 (构造函数方式)", "new PropertyExpr(\"Age\")");

                // [方式2] 静态工厂 (最推荐)
                var v2 = Const("张三");
                Print(v2, "常量值 (工厂方式)", "Const(\"张三\")");
            });

            ShowSection("1.2 逻辑比较与组合", () =>
            {
                var age = Prop("Age");
                var dept = Prop("DeptId");

                // [方式1] 运算符重载
                var cond1 = age > 18;
                Print(cond1, "二元比较 (运算符方式)", "Prop(\"Age\") > 18");

                var composite1 = cond1 & (dept == 101);
                Print(composite1, "逻辑组合 (运算符方式)", "(age > 18) & (dept == 101)");

                // [方式2] 链式扩展方法
                var composite2 = age.GreaterThan(20).And(dept.In(101, 102));
                Print(composite2, "逻辑组合 (Fluent方式)", "age.GreaterThan(20).And(dept.In(101, 102))");
            });

            ShowSection("1.3 结构化查询模型 (Select/Update/Delete)", () =>
            {
                // Select 链式构建
                var query = From<User>()
                    .Where(Prop("Age") > 20)
                    .Select(Prop("Id"), Prop("UserName").As("Name"))
                    .OrderBy(Prop("Id").Desc());

                string code = "From<User>()\n" +
                              "    .Where(Prop(\"Age\") > 20)\n" +
                              "    .Select(Prop(\"Id\"), Prop(\"UserName\").As(\"Name\"))\n" +
                              "    .OrderBy(Prop(\"Id\").Desc())";
                Print(query, "SELECT 完整模型", code);

                // Update 模型
                var update = Expr.Update<User>()
                .Where(Prop("Id") == 1)
                .Set(("UserName", Value("NewName")));
                Print(update, "UPDATE 模型", "new UpdateExpr(From<User>(), Prop(\"Id\") == 1).Set((\"UserName\", Value(\"NewName\")))");
            });

            ShowSection("1.4 Lambda 自动转换", () =>
            {
                var composite = Lambda<User>(u => u.Age > 25 && u.UserName.Contains("A"));
                Print(composite, "Lambda 转 Expr", "Exp<User>(u => u.Age > 25 && u.UserName.Contains(\"A\"))");
            });

            ShowSection("1.5 删除与其它片段 (Delete)", () =>
            {
                // 删除模型
                var delete = new DeleteExpr(new TableExpr(typeof(User)), Prop("Age") < 18);
                Print(delete, "DELETE 模型", "new DeleteExpr(new TableExpr(typeof(User)), Prop(\"Age\") < 18)");
            });
        }

        private static void ShowSection(string title, Action action)
        {
            DemoHelper.PrintSection(title, "");
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

            Console.WriteLine($"→ Expr内容 (ToString): {expr}");

            // 序列化
            string json = JsonSerializer.Serialize(expr, _jsonOptions);
            Console.WriteLine($"→ 序列化 (JSON):");
            Console.WriteLine(json);

            // 反序列化并校验
            var deserialized = JsonSerializer.Deserialize<Expr>(json, _jsonOptions);
            bool isEqual = expr.Equals(deserialized);
            Console.WriteLine($"→ 反序列化校验: {(isEqual ? "通过" : "未通过")}");
        }
    }
}
