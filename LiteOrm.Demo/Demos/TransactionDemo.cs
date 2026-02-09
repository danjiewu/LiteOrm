using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// ÑÝÊ¾ÉùÃ÷Ê½ÊÂÎñ´¦ÀíÓë¶à²ãÐ­µ÷
    /// </summary>
    public static class TransactionDemo
    {
        public static async Task RunThreeTierDemoAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥");
            Console.WriteLine("  6. ÊÂÎñÓëÈý²ã¼Ü¹¹£º");
            Console.WriteLine("©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥");
            var newUser = new User { UserName = "ThreeTierUser", Age = 25 };
            var initialSale = new SalesRecord { ProductName = "Starter Pack", Amount = 1 };

            await factory.UserService.DeleteAsync(u => u.UserName == newUser.UserName);
            Console.WriteLine($"ÏÖÔÚÍ¨¹ýÊÂÎñ×¢²áÐÂÓÃ»§ {newUser.UserName} ²¢Ö´ÐÐ³õÊ¼»¯ÏúÊÛ...");

            try
            {
                bool success = await factory.BusinessService.RegisterUserWithInitialSaleAsync(newUser, initialSale);
                if (success)
                {
                    Console.WriteLine("ÊÂÎñÖ´ÐÐ³É¹¦£¬ÓÃ»§ºÍ¶©µ¥Í¬Ê±³Ö¾Ã»¯");
                    var savedUser = await factory.UserService.GetByUserNameAsync(newUser.UserName);
                    if (savedUser != null)
                    {
                        Console.WriteLine($"ÑéÖ¤³É¹¦£ºÓÃ»§ ID={savedUser.Id}, ÓÃ»§Ãû={savedUser.UserName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ÊÂÎñÖ´ÐÐÊ§°Ü£¬ÒÑ»Ø¹ö: {ex.Message}");
                var savedUser = await factory.UserService.GetByUserNameAsync(newUser.UserName);
                if (savedUser == null)
                {
                    Console.WriteLine("»Ø¹ö³É¹¦£ºÓÃ»§Î´´´½¨");
                }
            }
        }
    }
}
