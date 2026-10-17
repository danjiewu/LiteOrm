using LiteOrm.Common;
using System.Text.Json.Nodes;

namespace LiteOrm.AotDemo.Models
{
    /// <summary>
    /// AOT 演示用用户实体：基础类型列 + 枚举列 + JsonNode 复杂列 + 指向部门的关联键。
    /// </summary>
    [Table("AotUsers")]
    public class AotUser : ObjectBase
    {
        /// <summary>主键。</summary>
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>用户名。</summary>
        [Column("UserName")]
        public string? UserName { get; set; }

        /// <summary>年龄。</summary>
        [Column("Age")]
        public int Age { get; set; }

        /// <summary>角色（枚举列）。</summary>
        [Column("Role")]
        public UserRole? Role { get; set; }

        /// <summary>
        /// 所属部门主键。<c>[ForeignType]</c> 在 AOT 下同样生效：
        /// 源生成器把它写进 <c>ColumnInfo.ForeignTables</c>，由 <c>CommonTableInfoProvider</c> 还原成关联表。
        /// </summary>
        [Column("DeptId")]
        [ForeignType(typeof(AotDepartment), Alias = "Dept", AutoExpand = true)]
        public int? DeptId { get; set; }

        /// <summary>创建时间。</summary>
        [Column("CreateTime")]
        public DateTime CreateTime { get; set; }

        /// <summary>Guid 列（SQLite 以文本存储）。</summary>
        [Column("Guid")]
        public Guid Guid { get; set; }

        /// <summary>JSON 列，映射为 JSON 文本。</summary>
        [Column("Info")]
        public JsonNode? Info { get; set; }

        /// <summary>时间跨度列。</summary>
        [Column("Duration")]
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// 用户角色。
    /// </summary>
    public enum UserRole
    {
        /// <summary>员工。</summary>
        Staff,
        /// <summary>主管。</summary>
        Manager,
        /// <summary>管理员。</summary>
        Admin
    }

    /// <summary>
    /// 用户视图实体，继承自 <see cref="AotUser"/>。
    /// <see cref="ForeignColumnAttribute"/> 投影列在 AOT 下由源生成器转入元数据，
    /// 查询时自动 JOIN 部门表并按别名取列。
    /// </summary>
    public class AotUserView : AotUser
    {
        /// <summary>所属部门名称，来自 <c>Alias = "Dept"</c> 的关联表。</summary>
        [ForeignColumn("Dept", Property = nameof(AotDepartment.Name))]
        public string? DeptName { get; set; }
    }
}
