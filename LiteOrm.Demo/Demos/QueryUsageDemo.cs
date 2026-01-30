using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示各种查询用法：包括关联查询、分表查询以及复杂表达式查询
    /// </summary>
    public static class QueryUsageDemo
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static async Task ShowJoinQueryAsync(IDepartmentService deptService)
        {
            Console.WriteLine("\n--- 关联查询展示 (自动查询关联视图字段) ---");
            var depts = await deptService.SearchAsync(null);
            foreach (var d in depts)
            {
                Console.WriteLine($" ID: {d.Id}, 部门: {d.Name}, 管理员: {d.ManagerName ?? "未指定"}, 上级: {d.ParentName ?? "无"}");
            }
        }

        public static async Task ShowArgedQueryAsync(ISalesService salesService)
        {
            Console.WriteLine("\n--- 分表查询展示 ---");
            string currentMonth = DateTime.Now.ToString("yyyyMM");
            var sales = await salesService.SearchAsync(null, [currentMonth]);
            Console.WriteLine($"{currentMonth} 月份销售总记录数: {sales.Count}");
            foreach (var sale in sales)
            {
                Console.WriteLine($"    - ID:{sale.Id}, 商品:{sale.ProductName}, 金额:{sale.Amount}, 业务员:{sale.UserName}, 销售时间:{sale.SaleTime:yyyy-MM-dd HH:mm} 发货时间:{sale.ShipTime:yyyy-MM-dd HH:mm}");
            }
        }

        public static async Task ShowQueryResultsAsync(IUserService userService, ISalesService salesService)
        {
            Console.WriteLine("\n[QueryResults] 使用 Expr 构建实际查询展示:");
            string currentMonth = DateTime.Now.ToString("yyyyMM");

            // 示例 1
            var expr1 = Expr.Exp<UserView>(u => u.Age > 25 && u.CreateTime.AddDays(10) > DateTime.Now && u.UserName.Substring(2, 2) == "测试");
            var users1 = await userService.SearchAsync(expr1);
            Console.WriteLine($"\n[示例 1] 年龄 > 25 且用户名包含 '测试' 的用户:");
            Console.WriteLine($"  Expr 序列化结果: {JsonSerializer.Serialize(expr1, jsonOptions)}");
            foreach (var user in users1)
            {
                Console.WriteLine($"    - ID:{user.Id}, 账号:{user.UserName}, 年龄:{user.Age}, 部门:{user.DeptName}");
            }

            // 示例 2
            var threeDaysAgo = DateTime.Now.AddDays(-3);
            var expr2 = Expr.Exp<SalesRecordView>(s => s.SaleTime < threeDaysAgo && s.ShipTime == null);
            var sales2 = await salesService.SearchSectionAsync(expr2, new PageSection(0, 10).OrderByDesc(nameof(SalesRecord.Amount)), [currentMonth]);
            Console.WriteLine($"\n[示例 2] 3天前的订单且尚未发货，按金额降序取前10条:");
            Console.WriteLine($"  Expr 序列化结果: {JsonSerializer.Serialize(expr2, jsonOptions)}");
            foreach (var sale in sales2)
            {
                Console.WriteLine($"    - ID:{sale.Id}, 商品:{sale.ProductName}, 金额:{sale.Amount}, 业务员:{sale.UserName}");
            }

            // 示例 3: GenericSqlExpr
            GenericSqlExpr.Register("DirectorDeptOrders", (ctx, builder, pms, arg) =>
            {
                string paramName = pms.Count.ToString();
                pms.Add(new KeyValuePair<string, object>(paramName, arg));
                return $@"SalesUserId IN (
                    SELECT u.Id FROM Users u 
                    WHERE u.DeptId IN (
                        WITH RECURSIVE SubDepts(Id) AS (
                            SELECT Id FROM Departments WHERE ManagerId = {builder.ToSqlParam(paramName)}
                            UNION ALL
                            SELECT d.Id FROM Departments d JOIN SubDepts s ON d.ParentId = s.Id
                        ) SELECT Id FROM SubDepts
                    )
                )";
            });

            var directorId = 6;
            var complexExpr = GenericSqlExpr.Get("DirectorDeptOrders", directorId) & Expr.Property(nameof(SalesRecord.SaleTime)) > threeDaysAgo;
            var directorOrders = await salesService.SearchAsync(complexExpr, [currentMonth]);
            Console.WriteLine($"\n[示例 3] 销售部主管 (ID:{directorId}) 麾下最近 3 天内的订单 ({currentMonth}):");
            Console.WriteLine($"  Expr 序列化结果: {JsonSerializer.Serialize(complexExpr, jsonOptions)}");
            Console.WriteLine($"  查询到记录数: {directorOrders.Count}");

            // 示例 4: ForeignExpr
            var expr4 = Expr.Foreign("Dept", Expr.Property(nameof(Department.Name)) == "销售部");
            var users4 = await userService.SearchAsync(expr4);
            Console.WriteLine($"\n[示例 4] 属于 '销售部' 的用户 (使用 ForeignExpr):");
            Console.WriteLine($"  查询到记录数: {users4.Count}");
        }
    }
}
