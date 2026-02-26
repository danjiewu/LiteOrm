using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// Expr.Exists 子查询演示
    /// 展示如何使用 Exists 表达式进行高效的存在性查询
    /// </summary>
    public static class ExistsSubqueryDemo
    {
        public static async Task RunAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  Expr.Exists 子查询演示");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var userSvc = factory.UserService;
            var deptSvc = factory.DepartmentService;

            // ========== 示例 1: 基础 Exists 查询 ==========
            Console.WriteLine("\n[1] 基础 Exists 查询：查询拥有部门的所有用户");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    SQL: SELECT * FROM Users u WHERE EXISTS (SELECT 1 FROM Departments d WHERE d.Id = u.DeptId)");
            Console.WriteLine("    Lambda: q => q.Where(u => Expr.Exists<Department>(d => d.Id == u.DeptId))");
            Console.ResetColor();

            var usersWithDept = await userSvc.SearchAsync(
                q => q.Where(u => Expr.Exists<Department>(d => d.Id == u.DeptId))
            );
            Console.WriteLine($"    → 结果：找到 {usersWithDept.Count} 个拥有部门的用户");
            PrintUsers(usersWithDept.Cast<User>().ToList());

            // ========== 示例 2: Exists + 其他条件 ==========
            Console.WriteLine("\n[2] Exists 与其他条件组合：年龄>25 且拥有部门的用户");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    SQL: SELECT * FROM Users u WHERE u.Age > 25 AND EXISTS (...)");
            Console.WriteLine("    Lambda: q => q.Where(u => u.Age > 25 && Expr.Exists<Department>(d => d.Id == u.DeptId))");
            Console.ResetColor();

            var adultUsersWithDept = await userSvc.SearchAsync(
                q => q.Where(u => u.Age > 25 && Expr.Exists<Department>(d => d.Id == u.DeptId))
            );
            Console.WriteLine($"    → 结果：找到 {adultUsersWithDept.Count} 个年龄>25且拥有部门的用户");

            // ========== 示例 3: Exists + 复杂子查询条件 ==========
            Console.WriteLine("\n[3] 复杂子查询条件：拥有名称为 'IT' 的部门的用户");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    SQL: SELECT * FROM Users u WHERE EXISTS (SELECT 1 FROM Departments d WHERE d.Id = u.DeptId AND d.Name = 'IT')");
            Console.WriteLine("    Lambda: q => q.Where(u => Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == \"IT\"))");
            Console.ResetColor();

            var itUsers = await userSvc.SearchAsync(
                q => q.Where(u => Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == "IT"))
            );
            Console.WriteLine($"    → 结果：找到 {itUsers.Count} 个在 IT 部门的用户");
            PrintUsers(itUsers.Cast<User>().ToList());

            // ========== 示例 4: Exists + 排序和分页 ==========
            Console.WriteLine("\n[4] Exists + 排序和分页：按名称排序，分页显示");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    SQL: SELECT * FROM Users u WHERE EXISTS (...) ORDER BY u.UserName OFFSET 0 ROWS FETCH NEXT 5 ROWS ONLY");
            Console.WriteLine("    Lambda: q => q.Where(...).OrderBy(u => u.UserName).Skip(0).Take(5)");
            Console.ResetColor();

            var pagedItUsers = await userSvc.SearchAsync(
                q => q.Where(u => Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == "IT"))
                      .OrderBy(u => u.UserName)
                      .Skip(0)
                      .Take(5)
            );
            Console.WriteLine($"    → 结果：按名称排序，返回前 5 个用户");
            PrintUsers(pagedItUsers.Cast<User>().ToList());

            // ========== 示例 5: 多个 Exists 条件 (AND) ==========
            Console.WriteLine("\n[5] 多个 Exists 条件 (AND)：满足多个存在性条件");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    SQL: SELECT * FROM Users u WHERE EXISTS (...) AND EXISTS (...)");
            Console.WriteLine("    Lambda: q => q.Where(u => Expr.Exists<Department>(...) && Expr.Exists<Department>(...))");
            Console.ResetColor();

            var multiExistsUsers = await userSvc.SearchAsync(
                q => q.Where(u => 
                    Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == "IT") &&
                    Expr.Exists<Department>(d => d.ParentId != null))  // 拥有上级部门
            );
            Console.WriteLine($"    → 结果：找到 {multiExistsUsers.Count} 个同时满足两个条件的用户");

            // ========== 示例 6: NOT EXISTS (使用 ! 操作符) ==========
            Console.WriteLine("\n[6] NOT EXISTS：查询没有部门的用户");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    SQL: SELECT * FROM Users u WHERE NOT EXISTS (SELECT 1 FROM Departments d WHERE d.Id = u.DeptId)");
            Console.WriteLine("    Lambda: q => q.Where(u => !Expr.Exists<Department>(d => d.Id == u.DeptId))");
            Console.ResetColor();

            var usersWithoutDept = await userSvc.SearchAsync(
                q => q.Where(u => !Expr.Exists<Department>(d => d.Id == u.DeptId))
            );
            Console.WriteLine($"    → 结果：找到 {usersWithoutDept.Count} 个没有部门的用户");
            PrintUsers(usersWithoutDept.Cast<User>().ToList());

            // ========== 示例 7: Exists vs Join 的区别 ==========
            Console.WriteLine("\n[7] Exists vs Join 的性能对比说明");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("    EXISTS 适用场景：");
            Console.WriteLine("    - 只关心数据是否存在，不需要返回关联表的数据");
            Console.WriteLine("    - 关联表可能有大量数据，EXISTS 可以短路优化");
            Console.WriteLine("    - 右表数据量大的情况下，EXISTS 性能优于 JOIN");
            Console.WriteLine();
            Console.WriteLine("    JOIN 适用场景：");
            Console.WriteLine("    - 需要返回或过滤关联表的数据");
            Console.WriteLine("    - 需要进行复杂的关联条件组合");
            Console.WriteLine("    - 数据量不是很大的情况");
            Console.ResetColor();

            // ========== 示例 8: 使用 Expr.Foreign API ==========
            Console.WriteLine("\n[8] 直接使用 Expr.Foreign API（用于组合复杂条件）");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    var foreignExpr = Expr.Foreign<Department>(d => d.Id == 1);");
            Console.WriteLine("    // 可用于组合复杂条件");
            Console.ResetColor();

            var directForeignExpr = Expr.Foreign<Department>(Expr.Prop("Id") == 1);
            Console.WriteLine($"    → 创建的 Expr 类型：{directForeignExpr.GetType().Name}");

            // ========== 示例 9: 与部门查询结合 ==========
            Console.WriteLine("\n[9] 反向查询：查询拥有员工的部门");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    SQL: SELECT * FROM Departments d WHERE EXISTS (SELECT 1 FROM Users u WHERE u.DeptId = d.Id)");
            Console.WriteLine("    Lambda: q => q.Where(d => Expr.Exists<User>(u => u.DeptId == d.Id))");
            Console.ResetColor();

            var deptsWithUsers = await deptSvc.SearchAsync(
                q => q.Where(d => Expr.Exists<User>(u => u.DeptId == d.Id))
            );
            Console.WriteLine($"    → 结果：找到 {deptsWithUsers.Count} 个拥有员工的部门");

            // ========== 示例 10: 完整的复杂查询 ==========
            Console.WriteLine("\n[10] 完整复杂查询：年龄 18-40，在 IT 部门，按创建时间排序，分页");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    Lambda: q => q");
            Console.WriteLine("        .Where(u => u.Age >= 18 && u.Age <= 40)");
            Console.WriteLine("        .Where(u => Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == \"IT\"))");
            Console.WriteLine("        .OrderByDescending(u => u.CreateTime)");
            Console.WriteLine("        .Skip(0).Take(10)");
            Console.ResetColor();

            var complexQuery = await userSvc.SearchAsync(
                q => q.Where(u => u.Age >= 18 && u.Age <= 40)
                      .Where(u => Expr.Exists<Department>(d => d.Id == u.DeptId && d.Name == "IT"))
                      .OrderByDescending(u => u.CreateTime)
                      .Skip(0)
                      .Take(10)
            );
            Console.WriteLine($"    → 结果：找到 {complexQuery.Count} 个满足条件的用户");
            PrintUsers(complexQuery.Cast<User>().ToList());

            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  Expr.Exists 演示完成");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
        }

        private static void PrintUsers(System.Collections.Generic.List<User> users)
        {
            if (users == null || users.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("      (无结果)");
                Console.ResetColor();
                return;
            }

            foreach (var user in users)
            {
                Console.WriteLine($"      - {user.UserName} (年龄: {user.Age}, 部门ID: {user.DeptId})");
            }
        }
    }
}
