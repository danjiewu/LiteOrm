using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// 演示 LiteOrm 的性能优势：批量操作 vs 循环操作
    /// </summary>
    public static class PerformanceDemo
    {
        public static async Task ShowPerformanceComparisonAsync(ISalesService salesService)
        {
            Console.WriteLine("\n--- 性能对比 (BatchInsert) vs 循环插入 (Insert) ---");
            string currentMonth = DateTime.Now.ToString("yyyyMM");
            int testCount = 100;
            var testData = Enumerable.Range(1, testCount).Select(i => new SalesRecord
            {
                ProductName = "TestPerf",
                Amount = i,
                SaleTime = DateTime.Now
            }).ToList();

            // 循环插入
            var sw = Stopwatch.StartNew();
            foreach (var item in testData) await salesService.InsertAsync(item);
            sw.Stop();
            Console.WriteLine($"循环插入 {testCount} 条: {sw.ElapsedMilliseconds} ms");

            // 批量插入
            sw.Restart();
            await salesService.BatchInsertAsync(testData);
            sw.Stop();
            Console.WriteLine($"BatchInsert 批量插入 {testCount} 条: {sw.ElapsedMilliseconds} ms");

            await salesService.DeleteAsync(Expr.Property(nameof(SalesRecord.ProductName)) == "TestPerf", [currentMonth]);
        }

        public static async Task ShowBatchUpdatePerformanceAsync(ISalesService salesService)
        {
            Console.WriteLine("\n--- 性能对比 (BatchUpdate) vs 循环更新 ---");
            string currentMonth = DateTime.Now.ToString("yyyyMM");
            int testCount = 1000;

            var testData = Enumerable.Range(1, testCount).Select(i => new SalesRecord
            {
                ProductName = "TestUpdatePerf",
                Amount = i,
                SaleTime = DateTime.Now
            }).ToList();
            await salesService.BatchInsertAsync(testData);

            var records = await salesService.SearchAsync(Expr.Property(nameof(SalesRecord.ProductName)) == "TestUpdatePerf", [currentMonth]);
            
            var sw = Stopwatch.StartNew();
            foreach (var item in records)
            {
                item.Amount += 100;
                await salesService.UpdateAsync(item);
            }
            sw.Stop();
            Console.WriteLine($"循环更新 {records.Count} 条: {sw.ElapsedMilliseconds} ms");

            var recordsToUpdate = records.Select(r => { r.Amount += 100; return r; }).ToList();
            sw.Restart();
            await salesService.BatchUpdateAsync(recordsToUpdate);
            sw.Stop();
            Console.WriteLine($"BatchUpdate 批量更新 {recordsToUpdate.Count} 条: {sw.ElapsedMilliseconds} ms");

            await salesService.DeleteAsync(Expr.Property(nameof(SalesRecord.ProductName)) == "TestUpdatePerf", [currentMonth]);
        }
    }
}
