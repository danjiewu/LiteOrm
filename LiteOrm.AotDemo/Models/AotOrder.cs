using LiteOrm.Common;

namespace LiteOrm.AotDemo.Models
{
    /// <summary>
    /// 订单实体：<see cref="UserId"/> 通过 <c>[ForeignType]</c> 指向 <see cref="AotUser"/>。
    /// <para>
    /// <c>AutoExpand = true</c> 让关联链可以继续往下走：订单 → 用户 → 部门，
    /// 视图里的 <see cref="AotOrderView.DeptName"/> 依赖这条扩展路径解析。
    /// </para>
    /// </summary>
    [Table("AotOrders")]
    public class AotOrder : ObjectBase
    {
        /// <summary>主键。</summary>
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>下单用户主键。</summary>
        [Column("UserId")]
        [ForeignType(typeof(AotUser), Alias = "User", AutoExpand = true)]
        public int UserId { get; set; }

        /// <summary>订单金额。</summary>
        [Column("Amount")]
        public double Amount { get; set; }

        /// <summary>订单状态（枚举列）。</summary>
        [Column("Status")]
        public OrderStatus Status { get; set; }

        /// <summary>下单时间。</summary>
        [Column("CreateTime")]
        public DateTime CreateTime { get; set; }

        /// <summary>软删除标记，配合 TableDefinition.ConstFilter 做固定过滤。</summary>
        [Column("IsDeleted")]
        public bool IsDeleted { get; set; }

        /// <summary>
        /// 计算列：由 <c>{Amount}</c> 派生的含税金额。
        /// <para>
        /// 计算列不落库（建表与写入都会跳过），只在 SELECT 时按表达式渲染，
        /// 源生成器在 AOT 下同样会为它生成读取映射。
        /// </para>
        /// </summary>
        [Column("AmountWithTax", Expression = "{Amount} * 1.1")]
        public double AmountWithTax { get; set; }
    }

    /// <summary>
    /// 订单视图：一个外键列取用户名的投影，一个两跳关联取部门名。
    /// </summary>
    public class AotOrderView : AotOrder
    {
        /// <summary>下单用户名，来自 <c>Alias = "User"</c> 的关联表。</summary>
        [ForeignColumn("User", Property = nameof(AotUser.UserName))]
        public string? UserName { get; set; }

        /// <summary>下单用户所属部门名，靠 <c>AutoExpand</c> 展开出的第二级关联。</summary>
        [ForeignColumn(typeof(AotDepartment), Property = nameof(AotDepartment.Name))]
        public string? DeptName { get; set; }
    }

    /// <summary>
    /// 订单状态。
    /// </summary>
    public enum OrderStatus
    {
        /// <summary>待支付。</summary>
        Pending,
        /// <summary>已支付。</summary>
        Paid,
        /// <summary>已取消。</summary>
        Cancelled
    }
}
