using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel;
using System.Linq;

namespace MyOrm.Common
{
    /// <summary>
    /// 分页查询时的分页设定
    /// </summary>
    [Serializable]
    public class SectionSet
    {
        /// <summary>
        /// 需要得到的起始记录号
        /// </summary>
        public int StartIndex { get; set; }
        /// <summary>
        /// 需要得到的记录数
        /// </summary>
        public int SectionSize { get; set; }
        /// <summary>
        /// 排序项的集合，按优先级顺序排列
        /// </summary>
        public Sorting[] Orders { get; set; }
        public override string ToString()
        {
            if (Orders != null && Orders.Length > 0)
            {
                return $"Start:{StartIndex} Size:{SectionSize} Orders:{string.Join(",", Orders.Select(o => o.ToString()))}";
            }
            else
                return $"Start:{StartIndex} Size:{SectionSize}";
        }
    }

    /// <summary>
    /// 排序项
    /// </summary>
    [Serializable]
    public struct Sorting
    {
        /// <summary>
        /// 排序属性名
        /// </summary>
        public string PropertyName { get; set; }
        /// <summary>
        /// 排序方向
        /// </summary>
        public ListSortDirection Direction { get; set; }
        /// <summary>
        /// 初始化排序项
        /// </summary>
        /// <param name="propertyName">排序属性名</param>
        /// <param name="direction">排序方向</param>
        public Sorting(string propertyName, ListSortDirection direction) { PropertyName = propertyName; Direction = direction; }

        public override string ToString()
        {
            return $"{PropertyName} {(Direction == ListSortDirection.Ascending ? "asc" : "desc")}";
        }
    }
}
