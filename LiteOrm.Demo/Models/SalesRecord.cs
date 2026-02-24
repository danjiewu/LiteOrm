using LiteOrm.Common;

namespace LiteOrm.Demo.Models
{
    /// <summary>
    /// 销售记录实体，演示 IArged 分表功能 (按月分表: Sales_yyyyMM)
    /// </summary>
    [Table("Sales_{0}")]
    public class SalesRecord : ObjectBase, IArged
    {
        [Column("Id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [Column("ProductId")]
        public int ProductId { get; set; }

        [Column("ProductName")]
        public string ProductName { get; set; } = string.Empty;

        [Column("Amount", AllowNull = false)]
        public int Amount { get; set; }

        [Column("SaleTime", AllowNull = false)]
        public DateTime SaleTime { get; set; }

        [Column("ShipTime")]
        public DateTime? ShipTime { get; set; }

        [Column("SalesUserId")]
        [ForeignType(typeof(User))]
        public int SalesUserId { get; set; }

        /// <summary>
        /// 实现 IArged 接口，返回表名后缀参数 (例如: "202512")
        /// </summary>
        string[] IArged.TableArgs => [SaleTime.ToString("yyyyMM")];
    }

    /// <summary>
    /// 销售记录视图，包含关联的用户信息
    /// </summary>
    public class SalesRecordView : SalesRecord
    {
        [ForeignColumn(typeof(User), Property = "UserName")]
        public string? UserName { get; set; }
    }
}
