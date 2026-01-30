using LiteOrm.Common;

namespace LiteOrm.Tests.Models
{
    [Table("TestUsers")]
    public class TestUser : ObjectBase
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name")]
        public string? Name { get; set; }

        [Column("Age")]
        public int Age { get; set; }

        [Column("CreateTime")]
        public DateTime CreateTime { get; set; }

        [Column("DeptId")]
        [ForeignType(typeof(TestDepartment), Alias = "Dept")]
        public int DeptId { get; set; }
    }

    [TableJoin(typeof(TestDepartment), "DeptId", AliasName = "Dept", JoinType = TableJoinType.Left)]
    [TableJoin("Dept", typeof(TestDepartment), "ParentId", AliasName = "ParentDept", JoinType = TableJoinType.Left)]
    public class TestUserView : TestUser
    {
        [ForeignColumn("Dept", Property = "Name")]
        public string? DeptName { get; set; }

        [ForeignColumn("ParentDept", Property = "Name")]
        public string? ParentDeptName { get; set; }
    }
}
