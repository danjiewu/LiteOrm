using LiteOrm.Common;
using LiteOrm.Service;
using LiteOrm.Demo.Services;
using LiteOrm.Demo.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示 ObjectViewDAO 的 ExprString 语法
    /// </summary>
    public class ObjectViewDAOExprStringDemo
    {
        private readonly IUserService _userService;
        private readonly ObjectViewDAO<User> _objectViewDAO;

        public ObjectViewDAOExprStringDemo(IUserService userService, ObjectViewDAO<User> objectViewDAO)
        {
            _userService = userService;
            _objectViewDAO = objectViewDAO;
        }

        public async Task RunDemo()
        {
            Console.WriteLine("=== ObjectViewDAO ExprString 语法演示 ===");
            
            // 准备测试数据
            await PrepareTestData();
            
            // 演示 1: 基本查询
            await BasicQueryDemo();
            
            // 演示 2: 多条件查询
            await MultiConditionQueryDemo();
            
            // 演示 3: 空条件查询（返回所有记录）
            await EmptyConditionDemo();
            
            // 演示 4: 异步查询
            await AsyncQueryDemo();
            
            // 演示 5: 复杂表达式查询
            await ComplexExprDemo();
            
            // 演示 6: SQL 和 Expr 混用
            await MixedSqlAndExprDemo();
            
            Console.WriteLine("\n=== 演示完成 ===");
        }

        private async Task MixedSqlAndExprDemo()
        {
            Console.WriteLine("\n7. SQL 和 Expr 混用演示:");
            
            // 混合使用 SQL 片段和 Expr 表达式
            int ageThreshold = 25;
            var ageExpr = Expr.Prop("Age") > ageThreshold;
            var users = _objectViewDAO.Search($"{ageExpr} AND UserName LIKE '张%'");
            
            Console.WriteLine($"年龄大于 {ageThreshold} 且姓名以 '张' 开头的用户 ({users.Count} 个):");
            foreach (var user in users)
            {
                Console.WriteLine($"  - {user.UserName}, 年龄: {user.Age}");
            }
            
            // 更复杂的混用示例
            var deptExpr = Expr.Prop("DeptId") == 2;
            users = _objectViewDAO.Search($"{deptExpr} AND Age BETWEEN 20 AND 30");
            
            Console.WriteLine($"部门 2 且年龄在 20-30 之间的用户 ({users.Count} 个):");
            foreach (var user in users)
            {
                Console.WriteLine($"  - {user.UserName}, 年龄: {user.Age}, 部门: {user.DeptId}");
            }
        }

        private async Task PrepareTestData()
        {
            Console.WriteLine("\n1. 准备测试数据...");
            
            // 清空现有数据
            var existingUsers = await _userService.SearchAsync(null);
            foreach (var user in existingUsers)
            {
                await _userService.DeleteAsync(user);
            }
            
            // 添加测试用户
            var users = new List<User>
            {
                new User { UserName = "张三", Age = 25, DeptId = 1 },
                new User { UserName = "李四", Age = 30, DeptId = 1 },
                new User { UserName = "王五", Age = 22, DeptId = 2 },
                new User { UserName = "赵六", Age = 35, DeptId = 2 },
                new User { UserName = "钱七", Age = 28, DeptId = 3 }
            };
            
            foreach (var user in users)
            {
                await _userService.InsertAsync(user);
            }
            
            Console.WriteLine($"添加了 {users.Count} 个测试用户");
        }

        private async Task BasicQueryDemo()
        {
            Console.WriteLine("\n2. 基本查询演示:");
            
            // 使用 ExprString 语法，使用 Expr 作为格式化片段
            int ageThreshold = 25;
            var ageExpr = Expr.Prop("Age") > ageThreshold;
            var users = _objectViewDAO.Search($"{ageExpr}");
            
            Console.WriteLine($"年龄大于 {ageThreshold} 的用户 ({users.Count} 个):");
            foreach (var user in users)
            {
                Console.WriteLine($"  - {user.UserName}, 年龄: {user.Age}");
            }
        }

        private async Task MultiConditionQueryDemo()
        {
            Console.WriteLine("\n3. 多条件查询演示:");
            
            // 使用 ExprString 语法，使用 Expr 作为格式化片段
            int deptId = 1;
            int ageLimit = 30;
            var deptExpr = Expr.Prop("DeptId") == deptId;
            var ageExpr = Expr.Prop("Age") < ageLimit;
            var users = _objectViewDAO.Search($"{deptExpr} AND {ageExpr}");
            
            Console.WriteLine($"部门 {deptId} 且年龄小于 {ageLimit} 的用户 ({users.Count} 个):");
            foreach (var user in users)
            {
                Console.WriteLine($"  - {user.UserName}, 年龄: {user.Age}, 部门: {user.DeptId}");
            }
        }

        private async Task ComplexExprDemo()
        {
            Console.WriteLine("\n6. 复杂表达式演示:");
            
            // 使用复杂的 Expr 表达式
            var complexExpr = (Expr.Prop("Age") > 20) & (Expr.Prop("Age") < 35);
            var users = _objectViewDAO.Search($"{complexExpr}");
            
            Console.WriteLine("年龄在 20-35 之间的用户 ({users.Count} 个):");
            foreach (var user in users)
            {
                Console.WriteLine($"  - {user.UserName}, 年龄: {user.Age}");
            }
        }

        private async Task EmptyConditionDemo()
        {
            Console.WriteLine("\n4. 空条件查询演示:");
            
            // 使用空的 ExprString 查询所有用户
            var users = _objectViewDAO.Search($"");
            
            Console.WriteLine($"所有用户 ({users.Count} 个):");
            foreach (var user in users)
            {
                Console.WriteLine($"  - {user.UserName}, 年龄: {user.Age}");
            }
        }

        private async Task AsyncQueryDemo()
        {
            Console.WriteLine("\n5. 异步查询演示:");
            
            // 注意：ExprString 语法暂时不支持异步方法
            Console.WriteLine("ExprString 语法目前仅支持同步方法");
        }
    }
}
