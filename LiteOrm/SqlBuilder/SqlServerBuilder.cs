using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace LiteOrm
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
                return $"select top {sectionSize} {select} \nfrom {from} {where} Order by {orderBy} ";
            else
                return base.GetSelectSectionSql(select, from, where, orderBy, startIndex, sectionSize);
        }

        /// <summary>
        /// 是否支持带自增列的批量插入并返回首个 ID。
        /// </summary>
        public override bool SupportBatchInsertWithIdentity => true;

        /// <summary>
        /// 生成带标识列的批量插入 SQL，返回首个插入的 ID。
        /// </summary>
        public override string BuildBatchIdentityInsertSql(IDbCommand command, ColumnDefinition identityColumn, string tableName, string columns, List<string> valuesList)
        {
            return $"{BuildBatchInsertSql(tableName, columns, valuesList)}; select SCOPE_IDENTITY() - ({valuesList.Count - 1}) as [ID];";
        }
    }
}

