using System.ComponentModel;
using LiteOrm.Common;

namespace LiteOrm.WebDemo.Models;

[DisplayName("订单")]
[Table("DemoOrders")]
[TableJoin("Creator", typeof(DemoDepartment), nameof(DemoUser.DepartmentId), Alias = "CreatorDept", JoinType = TableJoinType.Left)]
public class DemoOrder : ObjectBase
{
    [DisplayName("编号")]
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [DisplayName("订单号")]
    [Column("OrderNo")]
    public string OrderNo { get; set; } = string.Empty;

    [DisplayName("客户名称")]
    [Column("CustomerName")]
    public string CustomerName { get; set; } = string.Empty;

    [DisplayName("产品名称")]
    [Column("ProductName")]
    public string ProductName { get; set; } = string.Empty;

    [DisplayName("数量")]
    [Column("Quantity")]
    public int Quantity { get; set; }

    [DisplayName("单价")]
    [Column("UnitPrice")]
    public decimal UnitPrice { get; set; }

    [DisplayName("总金额")]
    [Column("TotalAmount")]
    public decimal TotalAmount { get; set; }

    [DisplayName("状态")]
    [Column("Status")]
    public string Status { get; set; } = DemoOrderStatuses.Pending;

    [DisplayName("备注")]
    [Column("Note")]
    public string? Note { get; set; }

    [DisplayName("创建时间")]
    [Column("CreatedTime")]
    public DateTime CreatedTime { get; set; }

    [DisplayName("更新时间")]
    [Column("UpdatedTime")]
    public DateTime UpdatedTime { get; set; }

    [DisplayName("创建人编号")]
    [Column("CreatedByUserId")]
    [ForeignType(typeof(DemoUser), Alias = "Creator")]
    public int CreatedByUserId { get; set; }
}

public class DemoOrderView : DemoOrder
{
    [DisplayName("创建人")]
    [ForeignColumn("Creator", Property = nameof(DemoUser.DisplayName))]
    public string? CreatedByUserName { get; set; }

    [DisplayName("创建人登录名")]
    [ForeignColumn("Creator", Property = nameof(DemoUser.UserName))]
    public string? CreatedByLoginName { get; set; }

    [DisplayName("部门")]
    [ForeignColumn("CreatorDept", Property = nameof(DemoDepartment.Name))]
    public string? DepartmentName { get; set; }
}

public static class DemoOrderStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Shipped = "Shipped";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    [
        Pending,
        Paid,
        Shipped,
        Completed,
        Cancelled
    ];

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) &&
        All.Any(item => string.Equals(item, status, StringComparison.OrdinalIgnoreCase));
}
