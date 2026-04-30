using LiteOrm.Common;
using System;

namespace LiteOrm.Tests.Models
{
    [Table("TestShortIdentityEntities")]
    public class TestShortIdentityEntity : ObjectBase
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public short Id { get; set; }

        [Column("Name")]
        public string? Name { get; set; }

        [Column("Quantity")]
        public int Quantity { get; set; }
    }

    [Table("TestLongIdentityEntities")]
    public class TestLongIdentityEntity : ObjectBase
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [Column("Name")]
        public string? Name { get; set; }

        [Column("Quantity")]
        public int Quantity { get; set; }
    }

    [Table("TestCompositeKeyEntities")]
    public class TestCompositeKeyEntity : ObjectBase
    {
        [Column("Id", IsPrimaryKey = true)]
        public int Id { get; set; }

        [Column("Code", IsPrimaryKey = true)]
        public string Code { get; set; } = string.Empty;

        [Column("Name")]
        public string? Name { get; set; }

        [Column("Quantity")]
        public int Quantity { get; set; }
    }
}
