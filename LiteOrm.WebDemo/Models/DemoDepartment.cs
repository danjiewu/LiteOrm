using System.ComponentModel;
using LiteOrm.Common;

namespace LiteOrm.WebDemo.Models;

[DisplayName("部门")]
[Table("DemoDepartments")]
public class DemoDepartment : ObjectBase
{
    [DisplayName("编号")]
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [DisplayName("名称")]
    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [DisplayName("编码")]
    [Column("Code")]
    public string Code { get; set; } = string.Empty;
}
