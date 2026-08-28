using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// JsonNode 属性演示：实体 <see cref="User.Info"/> 以 JSON 文本存储，经列级
    /// <see cref="JsonNodeConverter"/> 完成 JsonNode ↔ 数据库文本 的双向转换。
    /// </summary>
    public static class JsonNodeDemo
    {
        public static async Task RunAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n══════════════════════════════════════════════════════════");
            Console.WriteLine("    JsonNode 属性演示（Info 列：JSON 文本存储）");
            Console.WriteLine("══════════════════════════════════════════════════════════");

            var userSvc = factory.UserService;

            // Insert（写入 Info）
            var user = new User
            {
                UserName = "json_demo",
                Age = 30,
                CreateTime = DateTime.Now,
                Info = JsonNode.Parse("{\"Level\":\"gold\",\"Tags\":[\"vip\",\"demo\"]}")
            };
            userSvc.Insert(user);
            Console.WriteLine($"Inserted user Id={user.Id}, Info={user.Info?.ToJsonString()}");

            // Search 回读并打印 Info
            var found = await userSvc.SearchAsync(Expr.Prop("UserName") == "json_demo");
            foreach (var u in found)
                Console.WriteLine($"  Id={u.Id}, UserName={u.UserName}, Age={u.Age}, Info={u.Info?.ToJsonString()}");

            // Lambda 方式：JsonNode 索引 + GetValue<T>() 过滤
            // 映射为 json_extract(Info, '$.Level') = 'gold'（在 SQL 端完成 JSON 路径取值过滤）
            Expression<Func<UserView, bool>> jsonFilter = u => u.Info!["Level"]!.GetValue<string>() == "gold";
            var goldUsers = await userSvc.SearchAsync(jsonFilter);
            Console.WriteLine($"Lambda JSON 过滤 (Info['Level'] == \"gold\") 命中 {goldUsers.Count} 条：");
            foreach (var g in goldUsers)
                Console.WriteLine($"  {g.UserName}: {g.Info?.ToJsonString()}");
        }
    }
}