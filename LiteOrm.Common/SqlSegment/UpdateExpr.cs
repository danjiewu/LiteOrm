using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace LiteOrm.Common
{
    /// <summary>
    /// 更新片段，表示 UPDATE 语句
    /// </summary>
    [JsonConverter(typeof(SqlSegmentJsonConverterFactory))]
    public class UpdateExpr : SqlSegment
    {
        /// <summary>
        /// 初始化 UpdateExpr 类的新实例
        /// </summary>
        public UpdateExpr() { }

        /// <summary>
        /// 使用指定的源片段和筛选条件初始化 UpdateExpr 类的新实例
        /// </summary>
        /// <param name="source">源片段</param>
        /// <param name="where">筛选条件表达式</param>
        public UpdateExpr(SqlSegment source, LogicExpr where = null)
        {
            Source = source;
            Where = where;
        }

        /// <summary>
        /// 获取片段类型，返回 Update 类型标识
        /// </summary>
        public override SqlSegmentType SegmentType => SqlSegmentType.Update;

        /// <summary>
        /// 获取或设置要更新的字段和值列表
        /// </summary>
        public List<(string, ValueTypeExpr)> Sets { get; set; } = new List<(string, ValueTypeExpr)>();

        /// <summary>
        /// 获取或设置筛选条件表达式
        /// </summary>
        public LogicExpr Where { get; set; }

        /// <summary>
        /// 判断两个 UpdateExpr 是否相等
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回 true，否则返回 false</returns>
        public override bool Equals(object obj) => obj is UpdateExpr other 
            && Equals(Source, other.Source) 
            && Sets.SequenceEqual(other.Sets)
            && Equals(Where, other.Where);
            
        /// <summary>
        /// 获取当前对象的哈希码
        /// </summary>
        /// <returns>哈希码值</returns>
        public override int GetHashCode() => OrderedHashCodes(typeof(UpdateExpr).GetHashCode(), Source?.GetHashCode() ?? 0, SequenceHash(Sets), Where?.GetHashCode() ?? 0);
        
        /// <summary>
        /// 返回更新片段的字符串表示
        /// </summary>
        /// <returns>字符串表示</returns>
        public override string ToString()
        {
            string setStr = string.Join(", ", Sets.Select(s => $"[{s.Item1}] = {s.Item2}"));
            return $"UPDATE {Source} SET {setStr}{(Where != null ? $" WHERE {Where}" : "")}";
        }
    }
}
