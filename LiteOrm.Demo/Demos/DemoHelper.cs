using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm.Demo.Demos
{
    public static class DemoHelper
    {
        /// <summary>
        /// 输出格式化的演示部分
        /// </summary>
        public static void PrintSection(string title, string content)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"【{title}】");
            Console.ResetColor();
            Console.WriteLine(content);
        }
    }
}
