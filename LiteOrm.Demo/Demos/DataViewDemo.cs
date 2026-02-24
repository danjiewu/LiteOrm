using LiteOrm.Common;
using LiteOrm.Demo.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data;
using System.Threading.Tasks;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示 DataViewDAO 的使用，直接获取 DataTable 结果
    /// </summary>
    public static class DataViewDemo
    {
        public static async Task RunAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("  7. DataViewDAO 演示 (直接返回 DataTable)");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // 1. 获取 DataViewDAO<User>
            // DataViewDAO<T> 默认已由框架注册为 Scoped
            var userDataView = serviceProvider.GetRequiredService<DataViewDAO<User>>();

            // 2. 基础查询：获取所有用户
            Console.WriteLine("[1] 执行全表查询...");
            DataTable dtAll = await userDataView.SearchAsync(null);
            PrintDataTable(dtAll);

            // 3. 带条件的查询 (使用 Expr)
            Console.WriteLine("\n[2] 执行条件查询 (年龄 >= 20)...");
            Expr condition = Expr.Prop("Age") >= 20;
            DataTable dtFiltered = await userDataView.SearchAsync(condition);
            PrintDataTable(dtFiltered);

            // 4. 指定字段查询 (SelectItem)
            Console.WriteLine("\n[3] 指定字段查询 (只查 Id, UserName)...");
            string[] fields = { "Id", "UserName" };
            DataTable dtFields = await userDataView.SearchAsync(fields, condition);
            PrintDataTable(dtFields);

            // 5. 复杂链式构建查询 (Select + Where + OrderBy)
            Console.WriteLine("\n[4] 复杂链式构建查询 (DataTable 结果)...");
            var complexQuery = Expr.From<User>()
                .Select(Expr.Prop("Id"), Expr.Prop("UserName").As("Full_Name"), Expr.Prop("Age"))
                .Where(Expr.Prop("Age") > 15)
                .OrderBy(Expr.Prop("Age").Desc())
                .Section(0, 5)
                .Select("Id", "Full_Name", "Age");

            DataTable dtComplex = await userDataView.SearchAsync(complexQuery);
            PrintDataTable(dtComplex);
        }

        private static void PrintDataTable(DataTable dt)
        {
            Console.WriteLine($"→ 查询结果: {dt.Rows.Count} 行, {dt.Columns.Count} 列");

            // 打印表头
            foreach (DataColumn col in dt.Columns)
            {
                Console.Write($"{col.ColumnName,-15}\t");
            }
            Console.WriteLine();
            Console.WriteLine(new string('-', dt.Columns.Count * 16));

            // 打印前3行记录
            int count = 0;
            foreach (DataRow row in dt.Rows)
            {
                if (count++ >= 3) { Console.WriteLine("... (仅显示前3条)"); break; }
                foreach (var item in row.ItemArray)
                {
                    Console.Write($"{item?.ToString() ?? "NULL",-15}\t");
                }
                Console.WriteLine();
            }
        }
    }
}
