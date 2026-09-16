using LiteOrm.Common;

namespace LiteOrm.Tests.Models
{
    /// <summary>
    /// SQLite 专用测试实体。Test 进程同时持有多个数据库源时，若让共享实体
    /// （如 <see cref="TestUser"/>）被 SQLite 与 MySql 两套 SqlBuilder 同时绑定，
    /// 共享列的转换器会被来回污染（SQLite 把 DateTime 存成 TEXT、MySql 存成 DATETIME）。
    /// 该实体仅供在 SQLite 会话/宿主上执行的测试使用，与 MySql 的 <see cref="TestUser"/> 隔离。
    /// </summary>
    [Table("SqliteTestUsers")]
    public class SqliteTestUser
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("Name")]
        public string? Name { get; set; }

        [Column("Age")]
        public int Age { get; set; }

        [Column("CreateTime")]
        public DateTime CreateTime { get; set; }
    }
}