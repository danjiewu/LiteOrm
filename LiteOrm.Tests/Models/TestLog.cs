using LiteOrm.Common;

namespace LiteOrm.Tests.Models
{
    [Table("TestLog_{0}")]
    public class TestLog : ObjectBase, IArged
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Event")]
        public string? Event { get; set; }

        [Column("Amount")]
        public int Amount { get; set; }

        [Column("CreateTime", ColumnMode = ColumnMode.Final)]
        public DateTime CreateTime { get; set; }

        [Column("UserID")]
        [ForeignType(typeof(TestUser), Alias = "User")]
        public int UserID { get; set; }
        [Column(false)]

        public string[] TableArgs => [CreateTime.ToString("yyyyMM")];
    }

    public class TestLogView : TestLog
    {
        [ForeignColumn("User", Property = "Name")]
        public string? UserName { get; set; }    
    }
}
