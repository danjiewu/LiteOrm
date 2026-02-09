using LiteOrm.Common;
using LiteOrm.Demo.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Data;
using System.Threading.Tasks;

namespace LiteOrm.Demo.Demos
{
    /// <summary>
    /// ÑÝÊ¾ DataViewDAO µÄÊ¹ÓÃ£¬Ö±½Ó»ñÈ¡ DataTable ½á¹û
    /// </summary>
    public static class DataViewDemo
    {
        public static async Task RunAsync(IServiceProvider serviceProvider)
        {
            Console.WriteLine("\n©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥");
            Console.WriteLine("  7. DataViewDAO ÑÝÊ¾ (Ö±½Ó·µ»Ø DataTable)");
            Console.WriteLine("©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥");

            // 1. »ñÈ¡ DataViewDAO<User>
            // DataViewDAO<T> Ä¬ÈÏÒÑÓÉ¿ò¼Ü×¢²áÎª Scoped
            var userDataView = serviceProvider.GetRequiredService<DataViewDAO<User>>();

            // 2. »ù´¡²éÑ¯£º»ñÈ¡ËùÓÐÓÃ»§
            Console.WriteLine("[1] Ö´ÐÐÈ«±í²éÑ¯...");
            DataTable dtAll = await userDataView.SearchAsync(null);
            PrintDataTable(dtAll);

            // 3. ´øÌõ¼þµÄ²éÑ¯ (Ê¹ÓÃ Expr)
            Console.WriteLine("\n[2] Ö´ÐÐÌõ¼þ²éÑ¯ (ÄêÁä >= 20)...");
            Expr condition = Expr.Property("Age") >= 20;
            DataTable dtFiltered = await userDataView.SearchAsync(condition);
            PrintDataTable(dtFiltered);

            // 4. Ö¸¶¨×Ö¶Î²éÑ¯ (SelectItem)
            Console.WriteLine("\n[3] Ö¸¶¨×Ö¶Î²éÑ¯ (Ö»²é Id, UserName)...");
            string[] fields = { "Id", "UserName" };
            DataTable dtFields = await userDataView.SearchAsync(fields, condition);
            PrintDataTable(dtFields);

            // 5. ¸´ÔÓÁ´Ê½¹¹½¨²éÑ¯ (Select + Where + OrderBy)
            Console.WriteLine("\n[4] ¸´ÔÓÁ´Ê½¹¹½¨²éÑ¯ (DataTable ½á¹û)...");
            var complexQuery = Expr.Table<User>()
                .Select(Expr.Property("Id"), Expr.Property("UserName").As("Full_Name"), Expr.Property("Age"))
                .Where(Expr.Property("Age") > 15)
                .OrderBy(Expr.Property("Age").Desc())
                .Section(0, 5)
                .Select("Id", "Full_Name", "Age");

            DataTable dtComplex = await userDataView.SearchAsync(complexQuery);
            PrintDataTable(dtComplex);
        }

        private static void PrintDataTable(DataTable dt)
        {
            Console.WriteLine($"¡ú ²éÑ¯½á¹û: {dt.Rows.Count} ÐÐ, {dt.Columns.Count} ÁÐ");

            // ´òÓ¡±íÍ·
            foreach (DataColumn col in dt.Columns)
            {
                Console.Write($"{col.ColumnName,-15}\t");
            }
            Console.WriteLine();
            Console.WriteLine(new string('-', dt.Columns.Count * 16));

            // ´òÓ¡Ç°3ÐÐ¼ÇÂ¼
            int count = 0;
            foreach (DataRow row in dt.Rows)
            {
                if (count++ >= 3) { Console.WriteLine("... (½öÏÔÊ¾Ç°3Ìõ)"); break; }
                foreach (var item in row.ItemArray)
                {
                    Console.Write($"{item?.ToString() ?? "NULL",-15}\t");
                }
                Console.WriteLine();
            }
        }
    }
}
