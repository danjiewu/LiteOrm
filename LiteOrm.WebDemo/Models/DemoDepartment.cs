using LiteOrm.Common;

namespace LiteOrm.WebDemo.Models;

[Table("DemoDepartments")]
public class DemoDepartment : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Name")]
    public string Name { get; set; } = string.Empty;

    [Column("Code")]
    public string Code { get; set; } = string.Empty;
}
