using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace LiteOrm.Common
{
    /// <summary>
    /// 分页设置类，用于数据库查询的分页和排序
    /// </summary>
    [Serializable]
    public class PageSection
    {
        /// <summary>
        /// 需要得到的起始记录号
        /// </summary>
        public int StartIndex { get; private set; }

        /// <summary>
        /// 需要得到的记录数
        /// </summary>
        public int SectionSize { get; private set; }

        /// <summary>
        /// 排序项的集合，按优先级顺序排列
        /// </summary>
        public List<Sorting> Orders { get; private set; } = new List<Sorting>();

        /// <summary>
        /// 创建分页设置（使用默认值）
        /// </summary>
        public PageSection() { }

        /// <summary>
        /// 创建分页设置
        /// </summary>
        public PageSection(int startIndex, int sectionSize)
        {
            StartIndex = startIndex;
            SectionSize = sectionSize;
        }

        /// <summary>
        /// 创建分页设置
        /// </summary>
        public PageSection(int startIndex, int sectionSize, params Sorting[] orders)
        {
            StartIndex = startIndex;
            SectionSize = sectionSize;
            if (orders is not null)
                Orders.AddRange(orders);
        }

        /// <summary>
        /// 设置分页起始位置
        /// </summary>
        public PageSection StartAt(int startIndex)
        {
            StartIndex = startIndex;
            return this;
        }

        /// <summary>
        /// 设置分页大小
        /// </summary>
        public PageSection Take(int sectionSize)
        {
            SectionSize = sectionSize;
            return this;
        }

        /// <summary>
        /// 添加升序排序
        /// </summary>
        public PageSection OrderBy(string propertyName)
        {
            Orders.Add(new Sorting(propertyName, ListSortDirection.Ascending));
            return this;
        }

        /// <summary>
        /// 添加降序排序
        /// </summary>
        public PageSection OrderByDesc(string propertyName)
        {
            Orders.Add(new Sorting(propertyName, ListSortDirection.Descending));
            return this;
        }

        /// <summary>
        /// 添加自定义排序
        /// </summary>
        public PageSection OrderBy(string propertyName, ListSortDirection direction)
        {
            Orders.Add(new Sorting(propertyName, direction));
            return this;
        }

        /// <summary>
        /// 添加多个排序
        /// </summary>
        public PageSection OrderBy(params (string propertyName, ListSortDirection direction)[] orders)
        {
            if (orders is not null)
            {
                foreach (var (propertyName, direction) in orders)
                {
                    Orders.Add(new Sorting(propertyName, direction));
                }
            }
            return this;
        }

        /// <summary>
        /// 清除所有排序
        /// </summary>
        public PageSection ClearOrders()
        {
            Orders.Clear();
            return this;
        }

        /// <summary>
        /// 返回表示当前对象的字符串
        /// </summary>
        /// <returns>表示当前对象的字符串</returns>
        public override string ToString()
        {
            if (Orders is not null && Orders.Count > 0)
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

        /// <summary>
        /// 创建升序排序项
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <returns></returns>
        public static Sorting Asc(string propertyName) => new Sorting(propertyName, ListSortDirection.Ascending);
        /// <summary>
        /// 创建降序排序项
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <returns></returns>
        public static Sorting Desc(string propertyName) => new Sorting(propertyName, ListSortDirection.Descending);        

        /// <summary>
        /// 返回表示当前对象的字符串
        /// </summary>
        /// <returns>表示当前对象的字符串</returns>
        public override string ToString()
        {
            return $"{PropertyName} {(Direction == ListSortDirection.Ascending ? "asc" : "desc")}";
        }
    }
}
