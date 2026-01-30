using LiteOrm.Common;
using LiteOrm.Demo.Models;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示基础表达式 (Binary, Value, Property, Unary, ExprSet) 的构建
    /// </summary>
    public static class ExprBasicDemo
    {
        public static void ShowBinaryExpr()
        {
            Console.WriteLine("\n[BinaryExpr] 二元表达式:");
            // 等于, 大于等于, 不等于, 小于, 大于, 小于等于
            Expr e1 = Expr.Property(nameof(User.Age)) == 18;
            Expr e2 = Expr.Property(nameof(User.Age)) >= 18;
            Expr e3 = Expr.Property(nameof(User.UserName)) != "Admin";

            Console.WriteLine($"  Age == 18: {e1}");
            Console.WriteLine($"  Age >= 18: {e2}");
            Console.WriteLine($"  UserName != 'Admin': {e3}");
        }

        public static void ShowValueExpr()
        {
            Console.WriteLine("\n[ValueExpr] 值表达式:");
            Expr v1 = (ValueExpr)100; // 显式转换
            Expr v2 = Expr.Null;

            Console.WriteLine($"  Value 100: {v1}");
            Console.WriteLine($"  Value Null: {v2}");
        }

        public static void ShowPropertyExpr()
        {
            Console.WriteLine("\n[PropertyExpr] 属性表达式:");
            Expr p1 = Expr.Property("CreateTime");
            Console.WriteLine($"  Property: {p1}");
        }

        public static void ShowUnaryExpr()
        {
            Console.WriteLine("\n[UnaryExpr] 一元表达式:");
            Expr u1 = !Expr.Property(nameof(User.UserName)).Contains("Guest");
            Console.WriteLine($"  Not Contains Guest: {u1}");
        }

        public static void ShowExprSet()
        {
            Console.WriteLine("\n[ExprSet] 表达式集合 (And/Or):");
            Expr set = (Expr.Property("Age") > 10 & Expr.Property("Age") < 20) | Expr.Property("DeptId") == 1;
            Console.WriteLine($"  (Age > 10 AND Age < 20) OR DeptId == 1: {set}");
        }
    }
}
