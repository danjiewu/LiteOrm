using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示声明式事务处理与多层协调
    /// </summary>
    public static class TransactionDemo
    {
        public static async Task RunThreeTierDemoAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  6. 事务与三层架构：");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            var newUser = new User { UserName = "ThreeTierUser", Age = 25 };
            var initialSale = new SalesRecord { ProductName = "Starter Pack", Amount = 1 };

            await factory.UserService.DeleteAsync(u => u.UserName == newUser.UserName);
            Console.WriteLine($"现在通过事务注册新用户 {newUser.UserName} 并执行初始化销售...");

            try
            {
                bool success = await factory.BusinessService.RegisterUserWithInitialSaleAsync(newUser, initialSale);
                if (success)
                {
                    Console.WriteLine("事务执行成功，用户和订单同时持久化");
                    var savedUser = await factory.UserService.GetByUserNameAsync(newUser.UserName);
                    if (savedUser != null)
                    {
                        Console.WriteLine($"验证成功：用户 ID={savedUser.Id}, 用户名={savedUser.UserName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"事务执行失败，已回滚: {ex.Message}");
                var savedUser = await factory.UserService.GetByUserNameAsync(newUser.UserName);
                if (savedUser == null)
                {
                    Console.WriteLine("回滚成功：用户未创建");
                }
            }
        }
    }
}
