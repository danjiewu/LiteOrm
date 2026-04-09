using LiteOrm.Common;

namespace LiteOrm.WebDemo.Models;

[Table("DemoExprQueryHistories")]
public class DemoExprQueryHistory : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("UserId")]
    [ForeignType(typeof(DemoUser), Alias = "User")]
    public int UserId { get; set; }

    [Column("ExprJson")]
    public string ExprJson { get; set; } = string.Empty;

    [Column("CreatedTime")]
    public DateTime CreatedTime { get; set; }
}
