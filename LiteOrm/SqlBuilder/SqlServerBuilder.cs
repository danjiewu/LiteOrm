using System;
using System.Collections.Generic;
using System.Text;

namespace LiteOrm.SqlServer
{
    /// <summary>
    /// SQL Server 生成 SQL 语句的辅助类。
    /// </summary>
    public class SqlServerBuilder : SqlBuilder
    {
        /// <summary>
        /// 获取 <see cref="SqlServerBuilder"/> 的单例实例。
        /// </summary>
        public static readonly new SqlServerBuilder Instance = new SqlServerBuilder();

        /// <summary>
        /// 初始化函数映射关系。
        /// </summary>
        /// <param name="functionMappings">函数名映射字典。</param>
        protected override void InitializeFunctionMappings(Dictionary<string, string> functionMappings)
        {
            functionMappings["Length"] = "LEN";
            functionMappings["IndexOf"] = "CHARINDEX";   // CHARINDEX(substr, str)
            functionMappings["Substring"] = "SUBSTRING";
        }

        /// <summary>
        /// 构建函数的 SQL 语句，
        /// </summary>
        /// <param name="functionName">SQL函数名</param>
        /// <param name="args">SQL函数参数</param>
        /// <returns>构建的SQL语句</returns>
        /// <remarks>SQL Server 中IndexOf(str,substr)函数被映射为 CHARINDEX(substr, str) ，需特殊处理调转前后参数</remarks>
        public override string BuildFunctionSql(string functionName, params string[] args)
        {
            if ("IndexOf".Equals(functionName, StringComparison.OrdinalIgnoreCase) && args.Length == 2)
            {
                // SQL Server 中 CHARINDEX 的参数顺序为 (substr, str)
                return $"CHARINDEX({args[1]}, {args[0]})";
            }
            return base.BuildFunctionSql(functionName, args);
        }
        /// <summary>
        /// 生成分页查询的SQL语句
        /// </summary>
        /// <param name="select">select内容</param>
        /// <param name="from">from块</param>
        /// <param name="where">where条件</param>
        /// <param name="orderBy">排序</param>
        /// <param name="startIndex">起始位置，从0开始</param>
        /// <param name="sectionSize">查询条数</param>
        /// <returns></returns>
        public override string GetSelectSectionSql(string select, string from, string where, string orderBy, int startIndex, int sectionSize)
        {
            if (startIndex == 0)
                return $"select top {sectionSize} {select} \nfrom {from} \nwhere {where} Order by {orderBy} ";
            else
                return base.GetSelectSectionSql(select, from, where, orderBy, startIndex, sectionSize);
        }
    }
}
