using LiteOrm.Common;

namespace LiteOrm.WebDemo.Models;

[Table("DemoUsers")]
public class DemoUser : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserName")]
    public string UserName { get; set; } = string.Empty;

    [Column("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [Column("PasswordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("PasswordSalt")]
    public string PasswordSalt { get; set; } = string.Empty;

    [Column("Role")]
    public string Role { get; set; } = string.Empty;

    [Column("DepartmentId")]
    [ForeignType(typeof(DemoDepartment), Alias = "Dept")]
    public int DepartmentId { get; set; }

    [Column("CreatedTime")]
    public DateTime CreatedTime { get; set; }
}

public class DemoUserView : DemoUser
{
    [ForeignColumn("Dept", Property = nameof(DemoDepartment.Name))]
    public string? DepartmentName { get; set; }
}
