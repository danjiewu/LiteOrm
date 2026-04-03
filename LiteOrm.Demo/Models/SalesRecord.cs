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

        //自动扩展关联的用户表，引入用户所在部门，演示 ForeignTypeAttribute 的 AutoExpand 功能
        [Column("SalesUserId")]
        [ForeignType(typeof(User), AutoExpand = true)]
        public int SalesUserId { get; set; }

        /// <summary>
        /// 实现 IArged 接口，返回表名后缀参数 (例如: "202512")
        /// </summary>
        public string[] TableArgs => [SaleTime.ToString("yyyyMM")];
    }

    /// <summary>
    /// 销售记录视图，包含关联的用户信息
    /// </summary>
    public class SalesRecordView : SalesRecord
    {
        [ForeignColumn(typeof(User))]
        public string? UserName { get; set; }
        //无需显式关联 Department 表，已通过User表的 AutoExpand 引入 Department 表，直接使用 ForeignColumn 引用 Department 的 Name 列
        [ForeignColumn(typeof(Department), Property = nameof(Department.Name))]
        public string? DepartmentName { get; set; }
    }
}
