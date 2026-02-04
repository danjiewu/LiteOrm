#nullable disable
using LiteOrm.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LiteOrm.Benchmark
{
    /// <summary>
    /// 基准测试用户模型，用于数据库性能测试
    /// </summary>
    /// <remarks>
    /// 该类定义了用于基准测试的用户模型，包含基本的用户信息字段
    /// 同时支持LiteOrm、SqlSugar和EF Core三种ORM框架
    /// </remarks>
    [LiteOrm.Common.Table("BenchmarkUser")]
    [SqlSugar.SugarTable("BenchmarkUser")]
    [System.ComponentModel.DataAnnotations.Schema.Table("BenchmarkUser")]
    public class BenchmarkUser
    {
        /// <summary>
        /// 用户ID，主键，自增
        /// </summary>
        [LiteOrm.Common.Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        [SqlSugar.SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        [LiteOrm.Common.Column("Name")]
        [SqlSugar.SugarColumn]
        public string Name { get; set; }

        /// <summary>
        /// 用户年龄
        /// </summary>
        [LiteOrm.Common.Column("Age")]
        [SqlSugar.SugarColumn]
        public int Age { get; set; }

        /// <summary>
        /// 用户邮箱
        /// </summary>
        [LiteOrm.Common.Column("Email")]
        [SqlSugar.SugarColumn]
        public string Email { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [LiteOrm.Common.Column("CreateTime", ColumnMode = ColumnMode.Final)]
        [SqlSugar.SugarColumn]
        public DateTime CreateTime { get; set; }
    }

    /// <summary>
    /// 基准测试日志模型，用于数据库性能测试
    /// </summary>
    /// <remarks>
    /// 该类定义了用于基准测试的日志模型，包含日志信息和关联的用户ID
    /// 同时支持LiteOrm、SqlSugar和EF Core三种ORM框架
    /// </remarks>
    [LiteOrm.Common.Table("BenchmarkLog")]
    [SqlSugar.SugarTable("BenchmarkLog")]
    [System.ComponentModel.DataAnnotations.Schema.Table("BenchmarkLog")]
    public class BenchmarkLog
    {
        /// <summary>
        /// 日志ID，主键，自增
        /// </summary>
        [LiteOrm.Common.Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        [SqlSugar.SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 用户ID，外键，关联BenchmarkUser表
        /// </summary>
        [LiteOrm.Common.ForeignType(typeof(BenchmarkUser))]
        [LiteOrm.Common.Column("UserId")]
        [SqlSugar.SugarColumn]
        public int UserId { get; set; }

        /// <summary>
        /// 日志消息
        /// </summary>
        [LiteOrm.Common.Column("Message")]
        [SqlSugar.SugarColumn]
        public string Message { get; set; }

        /// <summary>
        /// 日志时间
        /// </summary>
        [LiteOrm.Common.Column("LogTime")]
        [SqlSugar.SugarColumn]
        public DateTime LogTime { get; set; }

        /// <summary>
        /// 关联的用户对象（仅SqlSugar使用）
        /// </summary>
        [SqlSugar.SugarColumn(IsIgnore = true)]
        public BenchmarkUser User { get; set; }
    }

    /// <summary>
    /// 基准测试日志视图模型，包含关联的用户信息
    /// </summary>
    /// <remarks>
    /// 该类继承自BenchmarkLog，添加了关联的用户名称和年龄信息
    /// 用于测试ORM框架的关联查询性能
    /// </remarks>
    [LiteOrm.Common.Table("BenchmarkLog")]
    public class BenchmarkLogView : BenchmarkLog
    {
        /// <summary>
        /// 关联的用户名（来自BenchmarkUser表）
        /// </summary>
        [LiteOrm.Common.ForeignColumn(typeof(BenchmarkUser), Property = "Name")]
        public string UserName { get; set; }

        /// <summary>
        /// 关联的用户年龄（来自BenchmarkUser表）
        /// </summary>
        [LiteOrm.Common.ForeignColumn(typeof(BenchmarkUser), Property = "Age")]
        public int Age { get; set; }
    }
}





