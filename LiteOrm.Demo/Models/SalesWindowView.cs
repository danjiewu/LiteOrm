namespace LiteOrm.Demo.Models
{
    /// <summary>
    /// 窗口函数查询视图，包含每条销售记录及其窗口聚合结果。
    /// </summary>
    public class SalesWindowView
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Amount { get; set; }
        public DateTime SaleTime { get; set; }

        /// <summary>
        /// 同一产品内按销售时间排序的累计金额（PARTITION BY ProductId ORDER BY SaleTime）
        /// </summary>
        public int RunningTotal { get; set; }

        /// <summary>
        /// 同一产品的总销售金额（PARTITION BY ProductId）
        /// </summary>
        public int ProductTotal { get; set; }
    }
}
