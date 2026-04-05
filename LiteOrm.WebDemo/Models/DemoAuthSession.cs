using LiteOrm.Common;

namespace LiteOrm.WebDemo.Models;

[Table("DemoAuthSessions")]
public class DemoAuthSession : ObjectBase
{
    [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [Column("Token")]
    public string Token { get; set; } = string.Empty;

    [Column("UserId")]
    [ForeignType(typeof(DemoUser), Alias = "User")]
    public int UserId { get; set; }

    [Column("CreatedTime")]
    public DateTime CreatedTime { get; set; }

    [Column("ExpiresAt")]
    public DateTime ExpiresAt { get; set; }

    [Column("RevokedTime")]
    public DateTime? RevokedTime { get; set; }
}
