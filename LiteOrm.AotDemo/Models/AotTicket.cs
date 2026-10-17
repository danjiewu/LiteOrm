using LiteOrm.Common;

namespace LiteOrm.AotDemo.Models
{
    /// <summary>
    /// 工单实体，用于演示 <c>[Column(Constant = ...)]</c> 在 AOT 下的固定筛选。
    /// <para>
    /// 两个固定值（<c>false</c> 与枚举成员 <see cref="TicketStatus.Open"/>）都由源生成器
    /// 写进 <c>ColumnInfo.Constant</c>，运行期 <c>CommonTableInfoProvider</c> 据此拼出
    /// <c>TableDefinition.ConstFilter</c>，与 JIT 路径的结果一致。
    /// </para>
    /// </summary>
    [Table("AotTickets")]
    public class AotTicket : ObjectBase
    {
        /// <summary>主键。</summary>
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>工单标题。</summary>
        [Column("Title", Length = 100)]
        public string? Title { get; set; }

        /// <summary>软删除标记。固定筛选只暴露未删除的工单。</summary>
        [Column("IsDeleted", Constant = false)]
        public bool IsDeleted { get; set; }

        /// <summary>工单状态。固定筛选只暴露待处理的工单。</summary>
        [Column("Status", Constant = TicketStatus.Open)]
        public TicketStatus Status { get; set; }
    }

    /// <summary>工单状态。</summary>
    public enum TicketStatus
    {
        /// <summary>待处理。</summary>
        Open = 0,
        /// <summary>已关闭。</summary>
        Closed = 1
    }
}
