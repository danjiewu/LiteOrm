using MyOrm.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace MyOrm.Test
{
    [Table("so1.ins_srvpkg", DataSource = "BOSS")]
    [Serializable]
    [DisplayName("产品订购信息")]
    public class InsPkg : ObjectBase
    {
        [Column(IsPrimaryKey = true)]
        public long SRVPKG_INST_ID { get; set; }
        public string OS_Status { get; set; }
        [ForeignType(typeof(InsProdRes))]
        public long PROD_INST_ID { get; set; }
        [ForeignColumn(typeof(InsProdRes))]
        public string RES_EQU_NO { get; set; }
        [ForeignType(typeof(BossProduct))]
        public long SRVPKG_ID { get; set; }

        [ForeignColumn(typeof(Addr))]
        public string SEGM_NAME { get; set; }
    }

    [Table("so1.ins_prod_res", DataSource = "BOSS")]
    [Serializable]
    [DisplayName("设备实例")]
    public class InsProdRes : ObjectBase
    {
        [Column(IsPrimaryKey = true)]
        public long PROD_INST_ID { get; set; }
        public string RES_EQU_NO { get; set; }
        [ForeignType(typeof(CustInfo))]
        public long CUST_ID { get; set; }
    }

    [Table("so1.ord_cust_f_{0}", DataSource = "BOSS")]
    [Serializable]
    [DisplayName("订单")]
    public class OrdCust : ObjectBase
    {
        [Column(IsPrimaryKey = true)]
        public long CUST_ORDER_ID { get; set; }
        public long CUST_ID { get; set; }
    }


    [Table("sz.rep_customer_temp", DataSource = "CloudBoss")]
    [Serializable]
    [DisplayName("客户信息")]
    public class CustInfo : ObjectBase
    {
        [Column(IsPrimaryKey = true)]
        public long CUST_ID { get; set; }
        public string CUST_CODE { get; set; }
        public string REGION_NAME1 { get; set; }
        public string REGION_NAME2 { get; set; }
        public string REGION_NAME3 { get; set; }
        public string CUST_NAME { get; set; }
        [ForeignType(typeof(Addr))]
        public string REGION_ID { get; set; }
    }

    [Table("files2.um_subscriber", DataSource = "CloudBoss")]
    [Serializable]
    [DisplayName("终端信息")]
    public class InsSubscriber : ObjectBase
    {
        [Column(IsPrimaryKey = true)]
        public long SUBSCRIBER_INS_ID { get; set; }        
        public string BILL_ID { get; set; }
        [ForeignType(typeof(InsSubscriber), Alias = "MainSub")]
        public long? MAIN_SUBSCRIBER_INS_ID { get; set; }
        [ForeignColumn("MainSub", Property = "BILL_ID")]
        public string MAIN_BILL_ID { get; set; }
        [ForeignType(typeof(CustInfo))]
        public string CUST_ID { get; set; }
        [ForeignColumn("CustInfo")]
        public string CUST_CODE { get; set; }
        [ForeignColumn("MainSub",Property ="CUST_ID")]
        [ForeignType(typeof(CustInfo), Alias ="MainCust")]
        public string MAIN_CUST_ID { get; set; }
        [ForeignColumn("MainCust", Property = "CUST_CODE")]
        public string MAIN_CUST_CODE { get; set; }
    }

    [Table("files2.cm_account", DataSource = "CloudBoss")]
    [Serializable]
    [DisplayName("账户信息")]
    public class Account : ObjectBase
    {
        [Column(IsPrimaryKey = true)]
        public long ACCT_ID { get; set; }
        [ForeignType(typeof(CustInfo))]
        public long CUST_ID { get; set; }
        public int CORP_ORG_ID { get; set; }
        [ForeignColumn(typeof(CustInfo))]
        public string CUST_CODE { get; set; }
    }

    [Table("rep.rep_addr", DataSource = "BOSS")]
    [Serializable]
    [DisplayName("地址")]
    public class Addr : ObjectBase
    {
        [Column(IsPrimaryKey = true)]
        public string SEGM_ID { get; set; }
        public string SEGM_NAME { get; set; }
    }
}
