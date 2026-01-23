using LiteOrm.Common;

namespace LiteOrm.Tests.Models
{
    [Table("TestDepartments")]
    public class TestDepartment : ObjectBase
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name")]
        public string? Name { get; set; }

        [Column("ParentId")]
        public int? ParentId { get; set; }
    }

    [TableJoin(typeof(TestDepartment), "ParentId", AliasName = "Parent", JoinType = TableJoinType.Left)]
    public class TestDepartmentView : TestDepartment
    {
        [ForeignColumn("Parent", Property = "Name")]
        public string? ParentName { get; set; }
    }
}
