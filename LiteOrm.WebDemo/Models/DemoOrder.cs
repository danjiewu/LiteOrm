using LiteOrm.Common;

namespace LiteOrm.WebDemo.Models;

[Table("DemoOrders")]
[TableJoin("Creator", typeof(DemoDepartment), nameof(DemoUser.DepartmentId), Alias = "CreatorDept", JoinType = TableJoinType.Left)]
public class DemoOrder : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("OrderNo")]
    public string OrderNo { get; set; } = string.Empty;

    [Column("CustomerName")]
    public string CustomerName { get; set; } = string.Empty;

    [Column("ProductName")]
    public string ProductName { get; set; } = string.Empty;

    [Column("Quantity")]
    public int Quantity { get; set; }

    [Column("UnitPrice")]
    public decimal UnitPrice { get; set; }

    [Column("TotalAmount")]
    public decimal TotalAmount { get; set; }

    [Column("Status")]
    public string Status { get; set; } = DemoOrderStatuses.Pending;

    [Column("Note")]
    public string? Note { get; set; }

    [Column("CreatedTime")]
    public DateTime CreatedTime { get; set; }

    [Column("UpdatedTime")]
    public DateTime UpdatedTime { get; set; }

    [Column("CreatedByUserId")]
    [ForeignType(typeof(DemoUser), Alias = "Creator")]
    public int CreatedByUserId { get; set; }
}

public class DemoOrderView : DemoOrder
{
    [ForeignColumn("Creator", Property = nameof(DemoUser.DisplayName))]
    public string? CreatedByUserName { get; set; }

    [ForeignColumn("Creator", Property = nameof(DemoUser.UserName))]
    public string? CreatedByLoginName { get; set; }

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
