using System.ComponentModel;
using LiteOrm.Common;

namespace LiteOrm.WebDemo.Models;

[DisplayName("产品")]
[Table("DemoProducts")]
public class DemoProduct : ObjectBase
{
    [DisplayName("编号")]
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [DisplayName("SKU")]
    [Column("Sku")]
    public string Sku { get; set; } = string.Empty;

    [DisplayName("名称")]
    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [DisplayName("分类")]
    [Column("Category")]
    public string Category { get; set; } = string.Empty;

    [DisplayName("单价")]
    [Column("UnitPrice")]
    public decimal UnitPrice { get; set; }

    [DisplayName("库存数量")]
    [Column("StockQuantity")]
    public int StockQuantity { get; set; }

    [DisplayName("是否启用")]
    [Column("IsActive")]
    public bool IsActive { get; set; }

    [DisplayName("发布时间")]
    [Column("PublishedTime")]
    public DateTime PublishedTime { get; set; }
}
