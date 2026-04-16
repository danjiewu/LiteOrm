using System.ComponentModel;
using LiteOrm.Common;

namespace LiteOrm.WebDemo.Models;

[DisplayName("客户")]
[Table("DemoCustomers")]
public class DemoCustomer : ObjectBase
{
    [DisplayName("编号")]
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [DisplayName("名称")]
    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [DisplayName("等级")]
    [Column("Level")]
    public string Level { get; set; } = string.Empty;

    [DisplayName("城市")]
    [Column("City")]
    public string City { get; set; } = string.Empty;

    [DisplayName("是否 VIP")]
    [Column("IsVip")]
    public bool IsVip { get; set; }

    [DisplayName("创建时间")]
    [Column("CreatedTime")]
    public DateTime CreatedTime { get; set; }
}
