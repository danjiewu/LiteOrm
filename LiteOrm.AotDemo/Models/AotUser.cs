using LiteOrm.Common;
using System.Text.Json.Nodes;

namespace LiteOrm.AotDemo.Models
{
    /// <summary>
    /// AOT 演示用实体：基础类型列 + 一个 JsonNode 复杂列。
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
        [Column("Role")]
        public UserRole? Role { get; set; }

        [Column("CreateTime")]
        public DateTime CreateTime { get; set; }
        [Column("Guid")]
        public Guid Guid { get; set; }

        [Column("Info")]
        public JsonNode? Info { get; set; }
        [Column("Duration")]
        public TimeSpan Duration { get; set; }
    }

    public enum UserRole
    {
        Staff,
        Manager,
        Admin
    }

    /// <summary>
    /// 用户视图实体，继承自 <see cref="AotUser"/>，用于演示 EntityService&lt;T, TView&gt; 模式。
    /// </summary>
    public class AotUserView : AotUser
    {
    }
}
