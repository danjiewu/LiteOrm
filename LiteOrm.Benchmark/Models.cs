#nullable disable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SqlSugar;
using LiteOrm.Common;

namespace LiteOrm.Benchmark
{
    [LiteOrm.Common.Table("BenchmarkUser")]
    [SqlSugar.SugarTable("BenchmarkUser")]
    [System.ComponentModel.DataAnnotations.Schema.Table("BenchmarkUser")]
    public class BenchmarkUser
    {
        [LiteOrm.Common.Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        [SqlSugar.SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [LiteOrm.Common.Column("Name")]
        [SqlSugar.SugarColumn]
        public string Name { get; set; }

        [LiteOrm.Common.Column("Age")]
        [SqlSugar.SugarColumn]
        public int Age { get; set; }

        [LiteOrm.Common.Column("Email")]
        [SqlSugar.SugarColumn]
        public string Email { get; set; }

        [LiteOrm.Common.Column("CreateTime", ColumnMode = ColumnMode.Final)]
        [SqlSugar.SugarColumn]
        public DateTime CreateTime { get; set; }
    }

    [LiteOrm.Common.Table("BenchmarkLog")]
    [SqlSugar.SugarTable("BenchmarkLog")]
    [System.ComponentModel.DataAnnotations.Schema.Table("BenchmarkLog")]
    public class BenchmarkLog
    {
        [LiteOrm.Common.Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        [SqlSugar.SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [LiteOrm.Common.ForeignType(typeof(BenchmarkUser))]
        [LiteOrm.Common.Column("UserId")]
        [SqlSugar.SugarColumn]
        public int UserId { get; set; }

        [LiteOrm.Common.Column("Message")]
        [SqlSugar.SugarColumn]
        public string Message { get; set; }

        [LiteOrm.Common.Column("LogTime")]
        [SqlSugar.SugarColumn]
        public DateTime LogTime { get; set; }

        [SqlSugar.SugarColumn(IsIgnore = true)]
        public BenchmarkUser User { get; set; }
    }

    [LiteOrm.Common.Table("BenchmarkLog")]
    public class BenchmarkLogView : BenchmarkLog
    {
        [LiteOrm.Common.ForeignColumn(typeof(BenchmarkUser), Property = "Name")]
        public string UserName { get; set; }

        [LiteOrm.Common.ForeignColumn(typeof(BenchmarkUser), Property = "Age")]
        public int Age { get; set; }
    }
}





