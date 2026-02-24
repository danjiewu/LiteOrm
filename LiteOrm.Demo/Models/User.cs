using LiteOrm.Common;

namespace LiteOrm.Demo.Models
{
    /// <summary>
    /// 用户实体类
    /// </summary>
    [Table("Users")]
    public class User : ObjectBase
    {
        /// <summary>
        /// 用户 ID
        /// </summary>
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        [Column("UserName")]
        public string? UserName { get; set; }

        /// <summary>
        /// 年龄
        /// </summary>
        [Column("Age")]
        public int Age { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [Column("CreateTime")]
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 所属部门 ID
        /// </summary>
        [Column("DeptId")]
        [ForeignType(typeof(Department), Alias = "Dept")]
        public int? DeptId { get; set; }
    }

    /// <summary>
    /// 用户视图实体，包含关联的外表字段
    /// </summary>
    public class UserView : User
    {
        /// <summary>
        /// 使用 ForeignColumn 自动关联查询部门名称
        /// </summary>
        [ForeignColumn("Dept", Property = "Name")]
        public string? DeptName { get; set; }
    }
}
