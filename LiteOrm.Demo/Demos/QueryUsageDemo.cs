using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示各种查询使用方法，包括普通查询、直接查询以及关联表表达式查询
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
            Console.WriteLine("\n--- 关联查询展示 (自动查询关联图字段) ---");
            var depts = await deptService.SearchAsync(null);
            foreach (var d in depts)
            {
                Console.WriteLine($" ID: {d.Id}, 名称: {d.Name}, 负责人: {d.ManagerName ?? "未指定"}, 上级: {d.ParentName ?? "无"}");
            }
        }

        public static async Task ShowArgedQueryAsync(ISalesService salesService)
        {
            Console.WriteLine("\n--- 直接查询展示 ---");
            string currentMonth = DateTime.Now.ToString("yyyyMM");
            var sales = await salesService.SearchAsync(null, [currentMonth]);
            Console.WriteLine($"{currentMonth} 月份销售总记录数: {sales.Count}");
            foreach (var sale in sales)
            {
                Console.WriteLine($"    - ID:{sale.Id}, 产品:{sale.ProductName}, 金额:{sale.Amount}, 业务员:{sale.UserName}, 销售时间:{sale.SaleTime:yyyy-MM-dd HH:mm} 发货时间:{sale.ShipTime:yyyy-MM-dd HH:mm}");
            }
        }

        public static async Task ShowQueryResultsAsync(IUserService userService, ISalesService salesService, IDepartmentService deptService)
        {
            Console.WriteLine("\n[QueryResults] 使用 Expr 实现实体查询展示:");
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
            var sales2 = await salesService.SearchAsync(
                Expr.Where<SalesRecordView>(s => s.SaleTime < threeDaysAgo && s.ShipTime == null)
                    .OrderBy((nameof(SalesRecord.Amount), false))
                    .Section(0, 10),
                [currentMonth]
            );
            Console.WriteLine($"\n[示例 2] 3天前的订单且未发货，按金额降序取前10条:");
            Console.WriteLine($"  Expr 序列化结果: {JsonSerializer.Serialize(expr2, jsonOptions)}");
            foreach (var sale in sales2)
            {
                Console.WriteLine($"    - ID:{sale.Id}, 产品:{sale.ProductName}, 金额:{sale.Amount}, 业务员:{sale.UserName}");
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
            Console.WriteLine($"\n[示例 3] 销售总监 (ID:{directorId}) 管辖部门 3 天内的订单 ({currentMonth}):");
            Console.WriteLine($"  Expr 序列化结果: {JsonSerializer.Serialize(complexExpr, jsonOptions)}");
            Console.WriteLine($"  查询结果记录数: {directorOrders.Count}");

            // 示例 4: ForeignExpr
            var expr4 = Expr.Foreign("Dept", Expr.Property(nameof(Department.Name)) == "销售部");
            var users4 = await userService.SearchAsync(expr4);
            Console.WriteLine($"\n[示例 4] 属于 '销售部' 的用户 (使用 ForeignExpr):");
            Console.WriteLine($"  查询结果记录数: {users4.Count}");

            // 示例 5: 多表关联的排序、分页查询，使用 ForeignColumn 字段
            Console.WriteLine($"\n[示例 5] 多表关联的排序、分页查询 (使用 ForeignColumn 字段):");
            // 1. 使用 DeptName (ForeignColumn) 作为查询条件和排序条件，同时使用 DeptName 作为排序条件
            var expr5 = Expr.Where<UserView>(u => u.Age > 20 && u.DeptName != null)
                .OrderBy((nameof(UserView.DeptName), true))  // 按部门名称升序
                .OrderBy((nameof(User.Age), false))          // 再按年龄降序
                .Section(0, 5);                              // 分页，取前5条
            var users5 = await userService.SearchAsync(expr5);
            Console.WriteLine($"  按部门名称排序的用户 (年龄 > 20):");
            foreach (var user in users5)
            {
                Console.WriteLine($"    - ID:{user.Id}, 账号:{user.UserName}, 年龄:{user.Age}, 部门:{user.DeptName}");
            }

            // 2. 部门查询，使用 ParentName 和 ManagerName (ForeignColumn) 作为查询和排序条件
            var deptExpr = Expr.Where<DepartmentView>(d => d.ParentName != null || d.ManagerName != null)
                .OrderBy((nameof(DepartmentView.ParentName), true))   // 按上级部门名称升序
                .OrderBy((nameof(DepartmentView.ManagerName), true))  // 再按负责人名称升序
                .Section(0, 5);                                       // 分页，取前5条
            var depts = await deptService.SearchAsync(deptExpr);
            Console.WriteLine($"\n  按上级部门和负责人排序的部门:");
            foreach (var dept in depts)
            {
                Console.WriteLine($"    - ID:{dept.Id}, 名称:{dept.Name}, 上级:{dept.ParentName ?? "无"}, 负责人:{dept.ManagerName ?? "未指定"}");
            }
        }
    }
}
