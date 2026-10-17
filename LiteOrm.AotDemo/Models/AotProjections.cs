namespace LiteOrm.AotDemo.Models
{
    /// <summary>
    /// 订单关联查询的自定义投影。
    /// <para>
    /// AOT 下投影类型没有源生成的 DataReader 映射，走 <c>AotMapper</c> 按列名匹配属性，
    /// 因此 SELECT 别名必须与属性名一致；属性需要公开可写、类型需有公开无参构造函数。
    /// </para>
    /// </summary>
    public class AotOrderRow
    {
        /// <summary>订单主键。</summary>
        public int OrderId { get; set; }

        /// <summary>订单金额。</summary>
        public double Amount { get; set; }

        /// <summary>订单状态。</summary>
        public OrderStatus Status { get; set; }

        /// <summary>下单用户名（来自关联表）。</summary>
        public string? UserName { get; set; }

        /// <summary>用户所属部门名（两跳关联）。</summary>
        public string? DeptName { get; set; }
    }

    /// <summary>
    /// 按用户分组的订单统计投影。
    /// </summary>
    public class AotUserOrderStat
    {
        /// <summary>用户主键。</summary>
        public int UserId { get; set; }

        /// <summary>用户名。</summary>
        public string? UserName { get; set; }

        /// <summary>订单数。</summary>
        public int OrderCount { get; set; }

        /// <summary>订单金额合计。</summary>
        public double TotalAmount { get; set; }
    }
}
