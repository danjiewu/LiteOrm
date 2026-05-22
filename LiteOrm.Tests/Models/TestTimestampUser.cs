using LiteOrm.Common;

namespace LiteOrm.Tests.Models
{
    [Table("TestTimestampUsers")]
    public class TestTimestampUser : ObjectBase
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name")]
        public string? Name { get; set; }

        [Column("Version", IsTimestamp = true)]
        public int Version { get; set; }
    }
}
