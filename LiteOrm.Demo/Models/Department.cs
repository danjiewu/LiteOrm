using LiteOrm.Common;

namespace LiteOrm.Demo.Models
{
    /// <summary>
    /// 部门实体类
    /// </summary>
    [Table("Departments")]
    public class Department : ObjectBase
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 上级部门 ID
        /// 使用 ForeignType 标记关联类型，Alias 用于区分多次关联同一个表的情况
        /// </summary>
        [Column("ParentId")]
        [ForeignType(typeof(Department), Alias = "Parent")]
        public int? ParentId { get; set; }

        /// <summary>
        /// 部门负责人 ID
        /// </summary>
        [Column("ManagerId")]
        [ForeignType(typeof(User))]
        public int? ManagerId { get; set; }
    }

    /// <summary>
    /// 部门视图实体，包含关联的外表字段
    /// </summary>
    public class DepartmentView : Department
    {
        /// <summary>
        /// 使用 ForeignColumn 自动关联查询上级部门名称
        /// 参数需对应 ForeignType 的 Alias 或 类型名称
        /// </summary>
        [ForeignColumn("Parent", Property = "Name")]
        public string? ParentName { get; set; }

        /// <summary>
        /// 使用 ForeignColumn 自动关联查询负责人姓名
        /// </summary>
        [ForeignColumn(typeof(User), Property = "UserName")]
        public string? ManagerName { get; set; }
    }
}
