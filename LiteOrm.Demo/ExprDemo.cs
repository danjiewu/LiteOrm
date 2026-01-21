using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using LiteOrm.Demo.DAO;
using System.Collections;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Diagnostics;
using System.Threading.Tasks;
using LiteOrm;

namespace LiteOrm.Demo
{
    /// <summary>
    /// LiteOrm 表达式和功能演示类
    /// </summary>
    public static class ExprDemo
    {
        private static JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// 运行所有演示示例
        /// </summary>
        public static async Task RunAllExamplesAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n=== Expr 表达式全示例展示 ===");

            ShowBinaryExpr();
            ShowValueExpr();
            ShowPropertyExpr();
            ShowUnaryExpr();
            ShowExprSet();
            ShowLambdaExpr();
            ShowExprConvert();
            ShowSqlGeneration();

            await ShowJoinQueryAsync(factory.DepartmentService);
            await ShowArgedQueryAsync(factory.SalesService);
            await ShowQueryResultsAsync(factory.UserService, factory.SalesService);
            await ShowPerformanceComparisonAsync(factory.SalesService);
            await ShowCustomDaoDemoAsync(factory.UserCustomDAO);
        }

        /// <summary>
        /// 自定义 DAO 演示
        /// </summary>
        public static async Task ShowCustomDaoDemoAsync(IUserCustomDAO userCustomDao)
        {
            Console.WriteLine("\n--- 自定义 DAO (UserCustomDAO) 展示 ---");
            string deptName = "销售部";
            var users = await userCustomDao.GetActiveUsersByDeptAsync(deptName);
            Console.WriteLine($" {deptName} 部门中年龄 > 18 的活跃用户数量: {users.Count}");
            foreach (var user in users)
            {
                Console.WriteLine($"    - ID:{user.Id}, 用户名:{user.UserName}, 年龄:{user.Age}, 部门:{user.DeptName}");
            }
        }

        public static async Task RunThreeTierDemo(ServiceFactory factory)
        {
            var newUser = new User { UserName = "ThreeTierUser", Age = 25 };
            var initialSale = new SalesRecord { ProductName = "Starter Pack", Amount = 1 };

            factory.UserService.DeleteAsync(u => u.UserName == newUser.UserName, null).Wait();
            Console.WriteLine($"正在尝试通过事务注册用户 {newUser.UserName} 并执行初始销售...");

            try
            {
                bool success = await factory.BusinessService.RegisterUserWithInitialSaleAsync(newUser, initialSale);
                if (success)
                {
                    Console.WriteLine("事务执行成功，用户和订单已同时保存");

                    // 验证
                    var savedUser = await factory.UserService.GetByUserNameAsync(newUser.UserName);
                    if (savedUser != null)
                    {
                        Console.WriteLine($"验证成功，用户 ID={savedUser.Id}, 用户名={savedUser.UserName}");
                        var sales = await factory.SalesService.SearchAsync(s => s.SalesUserId == savedUser.Id, [DateTime.Now.ToString("yyyyMM")]);
                        Console.WriteLine($"该用户订单数 {sales.Count}");
                        foreach (SalesRecordView sale in sales)
                        {
                            Console.WriteLine($"    - ID:{sale.Id}, 产品:{sale.ProductName}, 数量:{sale.Amount}, 销售员:{sale.UserName}, 销售时间:{sale.SaleTime:yyyy-MM-dd HH:mm} 发货时间:{sale.ShipTime:yyyy-MM-dd HH:mm}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"事务执行失败并已回滚: {ex.Message}");
                var savedUser = await factory.UserService.GetByUserNameAsync(newUser.UserName);
                if (savedUser != null)
                {
                    Console.WriteLine($"回滚失败，用户 ID={savedUser.Id}, 用户名={savedUser.UserName}");
                }
                else
                {
                    Console.WriteLine("回滚成功，用户未创建");
                }
            }
        }
        public static async Task ShowArgedQueryAsync(ISalesService salesService)
        {
            Console.WriteLine("\n--- IArged 分表查询展示 ---");
            string currentMonth = DateTime.Now.ToString("yyyyMM");
            var sales = await salesService.SearchAsync(null, [currentMonth]);
            Console.WriteLine($"{currentMonth} 月份销售总记录量: {sales.Count}");
            foreach (var sale in sales)
            {
                Console.WriteLine($"    - ID:{sale.Id}, 产品:{sale.ProductName}, 数量:{sale.Amount}, 销售员:{sale.UserName}, 销售时间:{sale.SaleTime:yyyy-MM-dd HH:mm} 发货时间:{sale.ShipTime:yyyy-MM-dd HH:mm}");
            }
        }

        public static async Task ShowPerformanceComparisonAsync(ISalesService salesService)
        {
            Console.WriteLine("\n--- 性能对比 (BatchInsert) vs 循环插入 (Insert) 耗时对比 ---");
            string currentMonth = DateTime.Now.ToString("yyyyMM");
            int testCount = 100;
            var testData = Enumerable.Range(1, testCount).Select(i => new SalesRecord
            {
                ProductName = "TestPerf",
                Amount = i,
                SaleTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, Random.Shared.Next(1, 28))
            }).ToList();

            await salesService.InsertAsync(new SalesRecord { ProductName = "Warmup", SaleTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1), ShipTime = DateTime.Now });

            var sw = Stopwatch.StartNew();
            foreach (var item in testData) await salesService.InsertAsync(item);
            sw.Stop();
            Console.WriteLine($"循环插入 {testCount} 条: {sw.ElapsedMilliseconds} ms");

            sw.Restart();
            await salesService.BatchInsertAsync(testData);
            sw.Stop();
            Console.WriteLine($"BatchInsert 批量插入 {testCount} 条: {sw.ElapsedMilliseconds} ms");

            int deletedCount = await salesService.DeleteAsync(Expr.Property(nameof(SalesRecord.ProductName)) == "TestPerf", [currentMonth]);
            Console.WriteLine($"清理测试数据完成，删除记录数: {deletedCount}");
        }

        public static async Task ShowJoinQueryAsync(IDepartmentService deptService)
        {
            Console.WriteLine("\n--- 关联查询展示 (自动查询关联视图字段) ---");
            var depts = await deptService.SearchAsync(null);
            foreach (var d in depts)
            {
                Console.WriteLine($" ID: {d.Id}, 名称: {d.Name}, 管理员: {d.ManagerName ?? "未指定"}, 上级: {d.ParentName ?? "无"}");
            }
        }

        private static async Task ShowQueryResultsAsync(IUserService userService, ISalesService salesService)
        {
            Console.WriteLine("\n[QueryResults] 使用 Expr 进行实际查询展示:");
            string currentMonth = DateTime.Now.ToString("yyyyMM");

            // 示例 1: 查询年龄 > 25 且用户名第三至第四个字为 "经理" 的用户
            var expr1 = Expr.Exp<UserView>(u => u.Age > 25 && u.CreateTime.AddDays(10) > DateTime.Now && u.UserName.Substring(2, 2) == "经理");
            var users1 = await userService.SearchAsync(expr1);
            Console.WriteLine($"\n[示例 1] 年龄 > 25 且用户名第三至第四个字为 '经理':");
            Console.WriteLine($"  Expr 序列化结果: {JsonSerializer.Serialize(expr1, jsonOptions)}");
            Console.WriteLine($"  查询结果数量: {users1.Count}");
            foreach (var user in users1)
            {
                Console.WriteLine($"    - ID:{user.Id}, 账号:{user.UserName}, 年龄:{user.Age}, 部门:{user.DeptName}, 创建日期:{user.CreateTime:yyyy-MM-dd}");
            }

            // 示例 2: 3天前的订单且未发货，按金额降序取前10条
            var threeDaysAgo = DateTime.Now.AddDays(-3);
            var expr2 = Expr.Exp<SalesRecordView>(s => s.SaleTime < threeDaysAgo && s.ShipTime == null);
            // 如果指明分表参数，使用 SearchSection 演示分页及排序
            var sales2 = await salesService.SearchSectionAsync(expr2, new PageSection(0, 10).OrderByDesc(nameof(SalesRecord.Amount)), [currentMonth]);
            Console.WriteLine($"\n[示例 2] 3天前的订单且未发货，按金额降序取前10条:");
            Console.WriteLine($"  Expr 序列化结果: {JsonSerializer.Serialize(expr2, jsonOptions)}");
            Console.WriteLine($"  查询结果数量: {sales2.Count}");
            foreach (var sale in sales2)
            {
                Console.WriteLine($"    - ID:{sale.Id}, 产品:{sale.ProductName}, 金额:{sale.Amount}, 业务员:{sale.UserName}, 销售时间:{sale.SaleTime:yyyy-MM-dd HH:mm} 发货时间:{sale.ShipTime:yyyy-MM-dd HH:mm}");
            }

            // 示例 3: 使用 GenericSqlExpr 自定义 SQL 模板查询：能够跨分表汇总该主管下辖部门的所有 3 天内的订单总和
            // 注册名为 "DirectorDeptOrders" 的表达式
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

            var directorId = 6; // 销售部经理 ID
            // 组合 GenericSqlExpr 和普通表达式
            var complexExpr = GenericSqlExpr.Get("DirectorDeptOrders", directorId) & Expr.Property(nameof(SalesRecord.SaleTime)) > threeDaysAgo;
            var directorOrders = await salesService.SearchAsync(complexExpr, [currentMonth]);

            Console.WriteLine($"\n[示例 3] 销售部经理(ID:{directorId})下辖部门所有 3 天内的订单 ({currentMonth}):");
            Console.WriteLine($"  Expr 序列化结果: {JsonSerializer.Serialize(complexExpr, jsonOptions)}");
            Console.WriteLine($"  查询结果数量: {directorOrders.Count}");
            foreach (var sale in directorOrders)
            {
                Console.WriteLine($"    - ID:{sale.Id}, 产品:{sale.ProductName}, 金额:{sale.Amount}, 业务员:{sale.UserName}, 销售时间:{sale.SaleTime:yyyy-MM-dd HH:mm} 发货时间:{sale.ShipTime:yyyy-MM-dd HH:mm}");
            }
        }

        public static void ShowExprConvert()
        {
            Console.WriteLine("\n[ExprConvert] 字符串和表达式转换演示");

            // 1. 将 QueryString 格式字符串进行解析
            var ageProps = Util.GetFilterProperties(typeof(User));
            var ageProp = ageProps.Find(nameof(User.Age), true);

            string queryValue = ">20";
            var parsedExpr = ExprConvert.Parse(ageProp, queryValue);
            Console.WriteLine($"  解析字符串 '{queryValue}' 为 Expr: {parsedExpr}");

            string inValue = "25,30,35";
            var inExpr = ExprConvert.Parse(ageProp, inValue);
            Console.WriteLine($"  解析字符串 '{inValue}' 为 Expr: {inExpr}");

            // 2. 将已经生成的表达式对象转换为字符串
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

            // 3. 模拟混合多维 QueryString 解析 (Util.ParseQueryCondition)
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
            Expr v1 = (Expr)100; // 显式转换
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

        public static void ShowLambdaExpr()
        {
            Console.WriteLine("\n[LambdaExpr] Lambda 表达式转换演示:");

            // 使用 Expr.Exp<T> 将 C# Lambda 转换为 Expr 对象
            // 这种方式最接近原生的 LINQ 写法
            var lambdaExpr = Expr.Exp<SalesRecordView>(s => (s.ShipTime ?? DateTime.Now.AddDays(-1)) > s.SaleTime + TimeSpan.FromDays(3) && s.ProductName.Contains("机"));

            Console.WriteLine("  C# Lambda: s => (s.ShipTime ?? DateTime.Now) > s.SaleTime + TimeSpan.FromDays(3) && s.ProductName.Contains(\"机\")");
            Console.WriteLine($"  转换结果 Expr: {lambdaExpr}");

            string json = JsonSerializer.Serialize((Expr)lambdaExpr, jsonOptions);
            Console.WriteLine($"  序列化结果: {json}");

            // 演示从 JSON 反序列化出 Expr 对象
            var deserializedExpr = JsonSerializer.Deserialize<Expr>(json, jsonOptions);
            Console.WriteLine($"  反序列化后 Expr 类型: {deserializedExpr?.GetType().Name}");
            Console.WriteLine($"  反序列化后 Expr 内容: {deserializedExpr}");
        }

        public static void ShowSqlGeneration()
        {
            Console.WriteLine("\n[SqlGen] 表达式生成 SQL 展示:");

            // 构建一个复杂的组合逻辑表达式
            var expr = (Expr.Property(nameof(User.Age)) > 18) & (Expr.Property(nameof(User.UserName)).Contains("admin_"));

            // 使用 SqlGen 将表达式转换为特定实体的 SQL 片段
            // 它会自动根据实体映射到的表信息、字段映射及数据库语法
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
