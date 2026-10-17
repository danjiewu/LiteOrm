using LiteOrm.Common;

namespace LiteOrm.AotDemo.Models
{
    /// <summary>
    /// 部门实体。
    /// <para>
    /// <c>[TableJoin]</c> 声明与自身（上级部门）的关联，<c>Alias = "Parent"</c> 供
    /// <see cref="AotDepartmentView"/> 的 <c>[ForeignColumn]</c> 引用。
    /// AOT 下这条声明由源生成器写进 <c>JoinedTableInfo</c>，运行时不再读特性。
    /// </para>
    /// </summary>
    [Table("AotDepartments")]
    [TableJoin(typeof(AotDepartment), "ParentId", Alias = "Parent")]
    public class AotDepartment : ObjectBase
    {
        /// <summary>主键。</summary>
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>部门名称。</summary>
        [Column("Name")]
        public string? Name { get; set; }

        /// <summary>上级部门主键，顶级部门为 null。</summary>
        [Column("ParentId")]
        public int? ParentId { get; set; }
    }

    /// <summary>
    /// 部门视图：通过类级 <c>[TableJoin]</c> 的别名 <c>Parent</c> 取上级部门名称。
    /// </summary>
    public class AotDepartmentView : AotDepartment
    {
        /// <summary>上级部门名称。</summary>
        [ForeignColumn("Parent", Property = nameof(AotDepartment.Name))]
        public string? ParentName { get; set; }
    }
}
