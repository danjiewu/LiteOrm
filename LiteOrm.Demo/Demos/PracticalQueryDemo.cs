using LiteOrm.Common;
using LiteOrm.Demo.Models;
using LiteOrm.Demo.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiteOrm.Demo.Demos
{
    public static class PracticalQueryDemo
    {
        public static async Task RunAsync(ServiceFactory factory)
        {
            Console.WriteLine("\n©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥");
            Console.WriteLine("  5. ×ÛºÏ²éÑ¯Êµ¼ù£º´Ó Lambda µ½ SQL");
            Console.WriteLine("©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥©¥");

            // 1. ×¼±¸¶¯Ì¬Ìõ¼þ
            int minAge = 18;
            string searchName = "ÕÅ";
            var userSvc = factory.UserService;

            // ·½Ê½ 1: ÍêÕûµÄ Lambda ±í´ïÊ½ÑÝÊ¾ (Where + OrderBy + Skip/Take)
            // ÕâÖÖ·½Ê½×î½Ó½ü EF/LINQ Ï°¹ß£¬¿ò¼Ü»á×Ô¶¯×ª»»Îª Expr Ä£ÐÍ
            Console.WriteLine("[1] ÍêÕû Lambda Á´Ê½²éÑ¯ (ÍÆ¼ö)");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    var results = await userSvc.SearchAsync(\n" +
                              "        q => q.Where(u => u.Age >= minAge && u.UserName.Contains(searchName))\n" +
                              "              .OrderByDescending(u => u.Id)\n" +
                              "              .Skip(0).Take(10)\n" +
                              "    );");
            Console.ResetColor();

            var resultsA = await userSvc.SearchAsync(
                q => q.Where(u => u.Age >= minAge && u.UserName.Contains(searchName))
                      .OrderByDescending(u => u.Id)
                      .Skip(0).Take(10)
            );
            Console.WriteLine($"    ¡ú ²éÑ¯Íê³É£¬·µ»Ø {resultsA.Count} Ìõ¼ÇÂ¼¡£");


            // ·½Ê½ 2: ×î¼òµ¥µÄ Expression À©Õ¹²éÑ¯
            // Èç¹ûÖ»ÓÐ¼òµ¥µÄ¹ýÂË£¬¿ÉÒÔÖ±½Ó´«Èë Expression<Func<T, bool>>
            Console.WriteLine("\n[2] »ù´¡ Expression À©Õ¹²éÑ¯");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("    var results = await userSvc.SearchAsync(u => u.Age >= minAge);");
            Console.ResetColor();

            var resultsC = await userSvc.SearchAsync(u => u.Age >= minAge);
            Console.WriteLine($"    ¡ú ²éÑ¯Íê³É£¬·µ»Ø {resultsC.Count} Ìõ¼ÇÂ¼¡£");

            // 3. ¹¹½¨²¢Êä³ö×îÖÕ SQL Ä£ÐÍÔ¤ÀÀ
            var queryModel = LambdaSqlSegmentConverter.ToSqlSegment(
                (System.Linq.Expressions.Expression<Func<IQueryable<User>, IQueryable<User>>>)(
                    q => q.Where(u => u.Age >= minAge && u.UserName.Contains(searchName))
                          .OrderByDescending(u => u.Id)
                          .Skip(0).Take(10)
                )
            );

            Console.WriteLine("\n[3] ¿ò¼ÜÉú³ÉµÄÂß¼­Ä£ÐÍ (JSON ÐòÁÐ»¯ºó¿É¿ç¶Ë´«µÝ):");
            Console.WriteLine($"> Âß¼­Ä£ÐÍÔ¤ÀÀ: {queryModel}");
        }
    }
}
