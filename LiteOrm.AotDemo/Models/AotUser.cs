using LiteOrm.Common;

namespace LiteOrm.AotDemo.Models
{
    /// <summary>
    /// AOT 演示用实体，仅使用基础类型以避免触发 JSON 序列化路径。
    /// </summary>
    [Table("AotUsers")]
    public class AotUser : ObjectBase
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("UserName")]
        public string? UserName { get; set; }

        [Column("Age")]
        public int Age { get; set; }
        [Column("Role", DbType = DbValueType.Int32)]
        public UserRole Role { get; set; }

        [Column("CreateTime")]
        public DateTime CreateTime { get; set; }
        [Column("Guid")]
        public Guid Guid { get; set; }
    }

    public enum UserRole
    {
        Manager = 1,
        Staff = 2,
        Boss = 3
    }

    /// <summary>
    /// 用户视图实体，继承自 <see cref="AotUser"/>，用于演示 EntityService&lt;T, TView&gt; 模式。
    /// </summary>
    public class AotUserView : AotUser
    {
    }
}
