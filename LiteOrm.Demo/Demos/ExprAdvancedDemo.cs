using LiteOrm.Common;
using LiteOrm.Demo.Models;
using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示高级表达式功能：外键查询、Lambda 转换、序列化、SQL 生成
    /// </summary>
    public static class ExprAdvancedDemo
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static void ShowForeignExpr()
        {
            Console.WriteLine("\n[ForeignExpr] 外键表达式 (EXISTS 关联查询):");

            // 示例 1: 查询所属部门名称包含 "销售" 的用户
            Expr f1 = Expr.Foreign(nameof(User.DeptId), Expr.Property(nameof(Department.Name)).Contains("销售"));
            Console.WriteLine($"  用户所属部门名称包含 '销售': {f1}");

            // 示例 2: 查询销售记录，其中关联的用户年龄大于 30
            Expr f2 = Expr.Foreign(nameof(SalesRecord.SalesUserId), Expr.Property(nameof(User.Age)) > 30);
            Console.WriteLine($"  销售记录其关联用户年龄 > 30: {f2}");
        }

        public static void ShowLambdaExpr()
        {
            Console.WriteLine("\n[LambdaExpr] Lambda 表达式转换演示:");

            var lambdaExpr = Expr.Exp<SalesRecordView>(s => (s.ShipTime ?? DateTime.Now.AddDays(-1)) > s.SaleTime + TimeSpan.FromDays(3) && s.ProductName.Contains("机"));

            Console.WriteLine("  C# Lambda: s => (s.ShipTime ?? DateTime.Now) > s.SaleTime + TimeSpan.FromDays(3) && s.ProductName.Contains(\"机\")");
            Console.WriteLine($"  转换结果 Expr: {lambdaExpr}");

            string json = JsonSerializer.Serialize((Expr)lambdaExpr, jsonOptions);
            Console.WriteLine($"  序列化结果: {json}");

            var deserializedExpr = JsonSerializer.Deserialize<Expr>(json, jsonOptions);
            Console.WriteLine($"  反序列化后 Expr 类型: {deserializedExpr?.GetType().Name}");
            Console.WriteLine($"  反序列化后 Expr 内容: {deserializedExpr}");
        }

        public static void ShowExprConvert()
        {
            Console.WriteLine("\n[ExprConvert] 字符串和表达式转换演示");

            var ageProps = Util.GetFilterProperties(typeof(User));
            var ageProp = ageProps.Find(nameof(User.Age), true);

            string queryValue = ">20";
            var parsedExpr = ExprConvert.Parse(ageProp, queryValue);
            Console.WriteLine($"  解析字符串 '{queryValue}' 为 Expr: {parsedExpr}");

            string inValue = "25,30,35";
            var inExpr = ExprConvert.Parse(ageProp, inValue);
            Console.WriteLine($"  解析字符串 '{inValue}' 为 Expr: {inExpr}");

            if (parsedExpr is BinaryExpr be)
            {
                string backToText = ExprConvert.ToText(be.Operator, (be.Right as ValueExpr)?.Value);
                Console.WriteLine($"  反向转换 Expr '{be}' 为字符串: {backToText}");
            }

            if (inExpr is BinaryExpr beIn)
            {
                string inToText = ExprConvert.ToText(beIn.Operator, (beIn.Right as ValueExpr)?.Value);
                Console.WriteLine($"  反向转换 IN 表达式 '{beIn}' 为字符串: {inToText}");
            }

            var queryString = new Dictionary<string, string>
            {
                { "UserName", "%Admin" },
                { "Age", ">20" }
            };
            var conditions = Util.ParseQueryCondition(queryString, typeof(User));
            Console.WriteLine($"\n  解析 QueryString (UserName=%Admin, Age=>20) 为 Expr 列表:");
            foreach (var cond in conditions)
            {
                Console.WriteLine($"    - {cond}");
            }
        }

        public static void ShowSqlGeneration()
        {
            Console.WriteLine("\n[SqlGen] 表达式生成 SQL 展示:");
            var expr = (Expr.Property(nameof(User.Age)) > 18) & (Expr.Property(nameof(User.UserName)).Contains("admin_"));
            var sqlGen = new SqlGen(typeof(User));
            var result = sqlGen.ToSql(expr);

            Console.WriteLine($"  Expr: {expr}");
            Console.WriteLine($"  生成 SQL: {result.Sql}");
            Console.WriteLine("  参数列表:");
            foreach (var p in result.Params)
            {
                Console.WriteLine($"    - {p.Key} = {p.Value}");
            }
        }
    }
}
