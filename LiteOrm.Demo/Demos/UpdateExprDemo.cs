using LiteOrm.Common;
using LiteOrm.Demo.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteOrm.Demo.Demos
{
    public static class UpdateExprDemo
    {
        public static async Task RunAsync(ServiceFactory serviceFactory)
        {
            Console.WriteLine("\n[5] UpdateExpr 演示 (复杂更新操作)");
            Console.WriteLine("=====================================");

            // 获取用户服务
            var userService = serviceFactory.UserService;

            // 1. 准备测试数据
            Console.WriteLine("1. 准备测试数据...");
            var testUser = new Models.User
            {
                UserName = "UpdateExprTest",
                Age = 20,
                CreateTime = DateTime.Now,
                DeptId = 1
            };
            bool inserted = await userService.InsertAsync(testUser);
            Console.WriteLine($"   测试用户创建成功: {inserted}, ID: {testUser.Id}");

            // 2. 使用 UpdateExpr 进行简单更新
            Console.WriteLine("\n2. 使用 UpdateExpr 进行简单更新...");
            var simpleUpdateExpr = new UpdateExpr
            {
                Source = Expr.From<Models.User>(),
                Sets = new List<(string, ValueTypeExpr)> 
                {
                    ("UserName", Expr.Const("UpdatedUser")),
                    ("Age", Expr.Const(25))
                },
                Where = Expr.Exp<Models.User>(u => u.Id == testUser.Id)
            };
            int affectedRows = userService.Update(simpleUpdateExpr);
            Console.WriteLine($"   简单更新影响行数: {affectedRows}");

            // 3. 使用运算符重载和 Expr.Const 进行加法运算更新
            Console.WriteLine("\n3. 使用运算符重载和 Expr.Const 进行加法运算更新...");
            var addUpdateExpr = new UpdateExpr
            {
                Source = Expr.From<Models.User>(),
                Sets = new List<(string, ValueTypeExpr)> 
                {
                    ("Age", Expr.Prop("Age") + Expr.Const(5)), // Age = Age + 5
                    ("UserName", Expr.Const("AgeUpdatedUser"))
                },
                Where = Expr.Exp<Models.User>(u => u.Id == testUser.Id)
            };
            affectedRows = userService.Update(addUpdateExpr);
            Console.WriteLine($"   加法运算更新影响行数: {affectedRows}");

            // 4. 使用异步 UpdateExpr 更新
            Console.WriteLine("\n4. 使用异步 UpdateExpr 更新...");
            var asyncUpdateExpr = new UpdateExpr
            {
                Source = Expr.From<Models.User>(),
                Sets = new List<(string, ValueTypeExpr)> 
                {
                    ("Age", Expr.Prop("Age") + Expr.Const(10)), // Age = Age + 10
                    ("UserName", Expr.Const("AsyncUpdatedUser"))
                },
                Where = Expr.Exp<Models.User>(u => u.Id == testUser.Id)
            };
            affectedRows = await userService.UpdateAsync(asyncUpdateExpr);
            Console.WriteLine($"   异步更新影响行数: {affectedRows}");

            // 5. 验证最终结果
            Console.WriteLine("\n5. 验证最终结果...");
            var updatedUser = await userService.GetObjectAsync(testUser.Id);
            if (updatedUser != null)
            {
                Console.WriteLine($"   用户ID: {updatedUser.Id}");
                Console.WriteLine($"   用户名: {updatedUser.UserName}");
                Console.WriteLine($"   年龄: {updatedUser.Age}");
                Console.WriteLine($"   部门ID: {updatedUser.DeptId}");
            }
            else
            {
                Console.WriteLine("   未找到测试用户");
            }

            // 7. 使用 UpdateExpr 进行批量更新（放在最后，避免影响之前的演示）
            Console.WriteLine("\n7. 使用 UpdateExpr 进行批量更新...");
            var batchUpdateExpr = new UpdateExpr
            {
                Source = Expr.From<Models.User>(),
                Sets = new List<(string, ValueTypeExpr)> 
                {
                    ("UserName", Expr.Const("BatchUpdatedUser"))
                },
                Where = Expr.Exp<Models.User>(u => u.Age > 20)
            };
            int batchAffectedRows = userService.Update(batchUpdateExpr);
            Console.WriteLine($"   批量更新影响行数: {batchAffectedRows}");

            Console.WriteLine("\nUpdateExpr 演示完成！");

        }
    }
}
