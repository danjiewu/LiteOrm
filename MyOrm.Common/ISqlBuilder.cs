using System;
using System.Collections.Generic;
using System.Data;
using MyOrm;

namespace MyOrm.Common
{
    public interface ISqlBuilder
    {
        string BuildConditionSql(SqlBuildContext context, Condition conditon, ICollection<KeyValuePair<string, object>> outputParams);
        string GetSelectSectionSql(string select, string from, string where, string orderBy, int startIndex, int sectionSize);
        string ReplaceSqlName(string sql);
        string ToNativeName(string paramName);
        string ToParamName(string nativeName);
        string ToSqlName(string name);
        string ToSqlParam(string nativeName);
        string ToSqlLikeValue(string value);
        string ConcatSql(params string[] strs);
        DbType GetDbType(Type type);
        /// <summary>
        /// 获取指定数据类型的默认长度
        /// </summary>
        /// <param name="columnType">数据库列的数据类型</param>
        /// <returns></returns>
        int GetDefaultLength(DbType columnType);
    }
}
