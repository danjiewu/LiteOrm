using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示 Lambda 表达式查询功能：Where、OrderBy、Skip、Take、GroupBy、Select
    /// </summary>
    public static class LambdaQueryDemo
    {
        /// <summary>
        /// 展示 Lambda 表达式查询功能
        /// </summary>
        public static void ShowLambdaQueryDemo()
        {
            Console.WriteLine("\n=== Lambda 表达式查询演示 ===");

            ShowBasicLambdaQuery();
            ShowLambdaWithOrderByAndPaging();
            ShowLambdaWithGroupBy();
            ShowLambdaWithSelect();
            ShowLambdaWithMultipleConditions();
        }

        /// <summary>
        /// 展示 Lambda 表达式查询并执行演示
        /// </summary>
        public static async Task ShowLambdaQueryWithResultsAsync(IUserService userService)
        {
            Console.WriteLine("\n=== Lambda 表达式查询执行演示 ===");

            await ShowLambdaQueryWithWhereAsync(userService);
            await ShowLambdaQueryWithOrderByAndPagingAsync(userService);
            await ShowLambdaQueryWithSortingAsync(userService);
            await ShowLambdaQueryWithMultipleConditionsAsync(userService);
        }

        /// <summary>
        /// 演示基础 Lambda 查询 (Where) - 带执行结果
        /// </summary>
        private static async Task ShowLambdaQueryWithWhereAsync(IUserService userService)
        {
            Console.WriteLine("\n[1] Lambda 查询年龄 > 18 的用户:");

            // Lambda 表达式
            Expression<Func<IQueryable<UserView>, IQueryable<UserView>>> queryExpr = q => q.Where(u => u.Age > 18);

            // 转换为 SqlSegment
            var sqlSegment = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);

            Console.WriteLine("  Lambda: q => q.Where(u => u.Age > 18)");
            Console.WriteLine($"  转换结果: {sqlSegment}");
            Console.WriteLine($"  类型: {sqlSegment?.GetType().Name}");

            // 执行查询并输出结果
            var users = await userService.SearchAsync(sqlSegment);
            Console.WriteLine($"  查询结果: 找到 {users.Count} 条记录");
            foreach (var user in users.Take(5))
            {
                Console.WriteLine($"    - ID:{user.Id}, 用户名:{user.UserName}, 年龄:{user.Age}");
            }
            if (users.Count > 5)
            {
                Console.WriteLine($"    ... 还有 {users.Count - 5} 条记录");
            }
        }

        /// <summary>
        /// 演示 Lambda 查询 + 排序 + 分页 (Skip/Take) - 带执行结果
        /// </summary>
        private static async Task ShowLambdaQueryWithOrderByAndPagingAsync(IUserService userService)
        {
            Console.WriteLine("\n[2] Lambda 查询 + 排序 + 分页 (OrderBy + Skip + Take):");
            Console.WriteLine("  说明: Skip(10) 跳过前10条，Take(10) 取10条（共20条，页码2）");

            // Lambda 表达式：查询年龄 > 18 的用户，按年龄升序，跳过前10条，取10条
            Expression<Func<IQueryable<UserView>, IQueryable<UserView>>> queryExpr = q => q
                .Where(u => u.Age > 18)
                .OrderBy(u => u.Age)
                .Skip(10)
                .Take(10);

            var sqlSegment = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);

            Console.WriteLine("  Lambda: q => q.Where(u => u.Age > 18).OrderBy(u => u.Age).Take(10)");
            Console.WriteLine($"  转换结果: {sqlSegment}");

            if (sqlSegment is SectionExpr section)
            {
                Console.WriteLine($"  分页: Take={section.Take}");
            }

            // 执行查询并输出结果
            var users = await userService.SearchAsync(sqlSegment);
            Console.WriteLine($"  查询结果: 找到 {users.Count} 条记录");
            foreach (var user in users)
            {
                Console.WriteLine($"    - ID:{user.Id}, 用户名:{user.UserName}, 年龄:{user.Age}");
            }
        }

        /// <summary>
        /// 演示 Lambda 排序控制 (OrderByDescending/ThenBy) - 带执行结果
        /// </summary>
        private static async Task ShowLambdaQueryWithSortingAsync(IUserService userService)
        {
            Console.WriteLine("\n[3] Lambda 排序控制 (OrderByDescending + ThenBy):");
            Console.WriteLine("  说明: OrderByDescending 主排序（降序），ThenBy 次排序（升序）");

            // Lambda 表达式：按年龄降序，再按用户名升序
            Expression<Func<IQueryable<UserView>, IQueryable<UserView>>> queryExpr = q => q
                .Where(u => u.Age > 0)
                .OrderByDescending(u => u.Age)
                .ThenBy(u => u.UserName)
                .Take(10);

            var sqlSegment = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);

            Console.WriteLine("  Lambda: q => q.Where(u => u.Age > 0).OrderByDescending(u => u.Age).ThenBy(u => u.UserName).Take(10)");
            Console.WriteLine($"  转换结果: {sqlSegment}");

            if (sqlSegment is OrderByExpr orderBy)
            {
                Console.WriteLine("  排序规则:");
                foreach (var (expr, asc) in orderBy.OrderBys)
                {
                    Console.WriteLine($"    - {(expr as PropertyExpr)?.PropertyName} {(asc ? "ASC" : "DESC")}");
                }
            }

            // 执行查询并输出结果
            var users = await userService.SearchAsync(sqlSegment);
            Console.WriteLine($"  查询结果: 找到 {users.Count} 条记录");
            foreach (var user in users)
            {
                Console.WriteLine($"    - ID:{user.Id}, 用户名:{user.UserName}, 年龄:{user.Age}");
            }
        }

        /// <summary>
        /// 演示 Lambda 多条件查询 (多个 Where 合并为 AND) - 带执行结果
        /// </summary>
        private static async Task ShowLambdaQueryWithMultipleConditionsAsync(IUserService userService)
        {
            Console.WriteLine("\n[4] Lambda 多条件查询 (多个 Where 自动合并为 AND):");
            Console.WriteLine("  说明: 多个 Where() 调用会自动合并为一个 WHERE 子句，条件之间用 AND 连接");

            // Lambda 表达式：年龄 > 18 且用户名不为空
            Expression<Func<IQueryable<UserView>, IQueryable<UserView>>> queryExpr = q => q
                .Where(u => u.Age > 18)
                .Where(u => u.UserName != null)
                .OrderBy(u => u.UserName)
                .Take(10);

            var sqlSegment = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);

            Console.WriteLine("  Lambda: q => q.Where(u => u.Age > 18).Where(u => u.UserName != null).OrderBy(u => u.UserName).Take(10)");
            Console.WriteLine($"  转换结果: {sqlSegment}");

            // 执行查询并输出结果
            var users = await userService.SearchAsync(sqlSegment);
            Console.WriteLine($"  查询结果: 找到 {users.Count} 条记录");
            foreach (var user in users)
            {
                Console.WriteLine($"    - ID:{user.Id}, 用户名:{user.UserName}, 年龄:{user.Age}");
            }
        }

        /// <summary>
        /// 演示基础 Lambda 查询 (Where)
        /// </summary>
        private static void ShowBasicLambdaQuery()
        {
            Console.WriteLine("\n[1] 基础 Lambda 查询 (Where):");

            // Lambda 表达式：查询年龄大于 18 的用户
            Expression<Func<IQueryable<UserView>, IQueryable<UserView>>> queryExpr = q => q.Where(u => u.Age > 18);

            // 转换为 SqlSegment
            var sqlSegment = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);

            Console.WriteLine("  Lambda: q => q.Where(u => u.Age > 18)");
            Console.WriteLine($"  转换结果: {sqlSegment}");
            Console.WriteLine($"  类型: {sqlSegment?.GetType().Name}");
        }

        /// <summary>
        /// 演示 Lambda 查询 + 排序 + 分页 (Skip/Take)
        /// </summary>
        private static void ShowLambdaWithOrderByAndPaging()
        {
            Console.WriteLine("\n[2] Lambda 查询 + 排序 + 分页 (OrderBy + Skip + Take):");
            Console.WriteLine("  说明: Skip(10) 跳过前10条，Take(20) 取20条（第2页，每页20条）");

            // Lambda 表达式：查询年龄 > 18 的用户，按年龄升序，跳过前10条，取20条
            Expression<Func<IQueryable<UserView>, IQueryable<UserView>>> queryExpr = q => q
                .Where(u => u.Age > 18)
                .OrderBy(u => u.Age)
                .Skip(10)
                .Take(20);

            var sqlSegment = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);

            Console.WriteLine("  Lambda: q => q.Where(u => u.Age > 18).OrderBy(u => u.Age).Skip(10).Take(20)");
            Console.WriteLine($"  转换结果: {sqlSegment}");

            if (sqlSegment is SectionExpr section)
            {
                Console.WriteLine($"  分页: Skip={section.Skip}, Take={section.Take}");
            }
        }

        /// <summary>
        /// 演示 Lambda 分组查询 (GroupBy)
        /// </summary>
        private static void ShowLambdaWithGroupBy()
        {
            Console.WriteLine("\n[3] Lambda 分组查询 (GroupBy):");
            Console.WriteLine("  说明: GroupBy 按指定字段分组，Select 选择分组结果");

            // Lambda 表达式：按部门 ID 分组 (DeptId 为 int? 类型)
            Expression<Func<IQueryable<UserView>, IQueryable<int?>>> queryExpr = q => q
                .GroupBy(u => u.DeptId)
                .Select(g => g.Key);

            var sqlSegment = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);

            Console.WriteLine("  Lambda: q => q.GroupBy(u => u.DeptId).Select(g => g.Key)");
            Console.WriteLine($"  转换结果: {sqlSegment}");
            Console.WriteLine($"  类型: {sqlSegment?.GetType().Name}");
        }

        /// <summary>
        /// 演示 Lambda 选择特定字段 (Select)
        /// </summary>
        private static void ShowLambdaWithSelect()
        {
            Console.WriteLine("\n[4] Lambda 选择特定字段 (Select):");
            Console.WriteLine("  说明: Select 可以选择需要的字段，支持匿名对象");

            // Lambda 表达式：选择用户姓名和年龄
            Expression<Func<IQueryable<UserView>, IQueryable<object>>> queryExpr = q => q
                .Where(u => u.Age > 18)
                .Select(u => new { u.UserName, u.Age });

            var sqlSegment = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);

            Console.WriteLine("  Lambda: q => q.Where(u => u.Age > 18).Select(u => new {{ u.UserName, u.Age }})");
            Console.WriteLine($"  转换结果: {sqlSegment}");
            Console.WriteLine($"  类型: {sqlSegment?.GetType().Name}");

            if (sqlSegment is SelectExpr select)
            {
                Console.WriteLine($"  选择字段数: {select.Selects.Count}");
            }
        }

        /// <summary>
        /// 演示 Lambda 多条件查询 (多个 Where 合并为 AND)
        /// </summary>
        private static void ShowLambdaWithMultipleConditions()
        {
            Console.WriteLine("\n[5] Lambda 多条件查询 (多个 Where 自动合并为 AND):");
            Console.WriteLine("  说明: 多个 Where() 调用会自动合并为一个 WHERE 子句，条件之间用 AND 连接");

            // Lambda 表达式：年龄大于 18 且用户名包含 "Admin"
            Expression<Func<IQueryable<UserView>, IQueryable<UserView>>> queryExpr = q => q
                .Where(u => u.Age > 18)
                .Where(u => u.UserName != null && u.UserName.Contains("Admin"));

            var sqlSegment = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);

            Console.WriteLine("  Lambda: q => q.Where(u => u.Age > 18).Where(u => u.UserName != null && u.UserName.Contains(\"Admin\"))");
            Console.WriteLine($"  转换结果: {sqlSegment}");

            // 验证多个 Where 条件已合并为 LogicSet（AND 连接）
            if (sqlSegment is WhereExpr where && where.Where is LogicSet logicSet)
            {
                Console.WriteLine($"  ✓ 验证: 多个 Where 条件已合并为单个 WhereExpr，条件用 AND 连接 (包含 {logicSet.Count} 个条件)");
            }
        }

        /// <summary>
        /// 演示 Lambda 排序方向控制 (OrderByDescending/ThenBy)
        /// </summary>
        public static void ShowLambdaOrderingDemo()
        {
            Console.WriteLine("\n[6] Lambda 排序方向控制 (OrderByDescending + ThenBy):");
            Console.WriteLine("  说明: OrderByDescending 降序主排序，ThenBy 升序次排序");

            // Lambda 表达式：按年龄降序，再按用户名升序
            Expression<Func<IQueryable<UserView>, IQueryable<UserView>>> queryExpr = q => q
                .Where(u => u.Age > 18)
                .OrderByDescending(u => u.Age)
                .ThenBy(u => u.UserName);

            var sqlSegment = LambdaSqlSegmentConverter.ToSqlSegment(queryExpr);

            Console.WriteLine("  Lambda: q => q.Where(u => u.Age > 18).OrderByDescending(u => u.Age).ThenBy(u => u.UserName)");
            Console.WriteLine($"  转换结果: {sqlSegment}");

            if (sqlSegment is OrderByExpr orderBy)
            {
                Console.WriteLine($"  排序字段数: {orderBy.OrderBys.Count}");
                foreach (var (expr, asc) in orderBy.OrderBys)
                {
                    Console.WriteLine($"    - {(expr as PropertyExpr)?.PropertyName} {(asc ? "ASC" : "DESC")}");
                }
            }
        }
    }
}
