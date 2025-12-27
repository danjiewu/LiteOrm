using DAL.Data;
using MyOrm.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Reflection;
using System.ComponentModel;

namespace DAL
{
    #region   源数据表

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "params2.bs_static_data")]
    [Description("参数信息表")]
    public class CloudBossBsStaticData
    {
        [Column("CODE_TYPE")]
        public string CODE_TYPE { get; set; }

        [Column("CODE_VALUE", IsPrimaryKey = true)]
        public string CODE_VALUE { get; set; }

        [Column("CODE_NAME")]
        public string CODE_NAME { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "pzg2.sec_organize")]
    [Description("机构信息表")]
    public class CloudBossSecOrganize
    {
        [Column("ORGANIZE_ID", IsPrimaryKey = true)]
        public Int64? ORGANIZE_ID { get; set; }

        [Column("ORGANIZE_NAME")]
        public string ORGANIZE_NAME { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "params2.sec_operator")]
    [Description("操作员与员工映射信息表")]
    public class CloudBossSecOperator
    {
        [Column("OPERATOR_ID")]
        public Int64? OPERATOR_ID { get; set; }

        [Column("STAFF_ID")]
        public Int64? STAFF_ID { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "files2.cm_customer")]
    [Description("客户基本信息表")]
    public class CloudBossCmCustomer
    {
        [Column("CUST_ID", IsPrimaryKey = true)]
        public Int64? CUST_ID { get; set; }

        [Column("CUST_CODE")]
        public string CUST_CODE { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "files2.cm_account")]
    [Description("账户实例信息表")]
    public class CloudBossCmAccount
    {
        [Column("PARTITION_ID")]
        public string PartitionId { get; set; }

        [Column("ACCT_ID", IsPrimaryKey = true)]
        public string AcctId { get; set; }

        [Column("CUST_ID")]
        public string CustId { get; set; }

        [Column("ACCT_NAME")]
        public string AcctName { get; set; }

        [Column("SCORE_VALUE")]
        public string SourceValue { get; set; }

        [Column("CREDIT_CLASS_ID")]
        public string CreditClassId { get; set; }

        [Column("BASIC_CREDIT_VALUE")]
        public string BasicCreditValue { get; set; }

        [Column("CREDIT_VALUE")]
        public string CreditValue { get; set; }

        [Column("DEBUTY_SUBSCRIBER_INS_ID")]
        public string DebutySubscriberInsId { get; set; }

        [Column("DEBUTY_CODE")]
        public string DebutyCode { get; set; }

        [Column("CONTRACT_NO")]
        public string ContractNo { get; set; }

        [Column("DEPOSIT_PRIOR_RULE_ID")]
        public string DepositPriorRuleId { get; set; }

        [Column("ITEM_PRIOR_RULE_ID")]
        public string ItemPriorRuleId { get; set; }

        [Column("OPEN_DATE")]
        public DateTime? OpenDate { get; set; }

        [Column("REMOVE_TAG")]
        public string RemoveTag { get; set; }

        [Column("REMOVE_DATE")]
        public DateTime? RemoveDate { get; set; }

        [Column("RSRV_STR1")]
        public string RsrvStr1 { get; set; }

        [Column("RSRV_STR2")]
        public string RsrvStr2 { get; set; }

        [Column("RSRV_STR3")]
        public string RsrvStr3 { get; set; }

        [Column("RSRV_STR4")]
        public string RsrvStr4 { get; set; }

        [Column("RSRV_STR5")]
        public string RsrvStr5 { get; set; }

        [Column("RSRV_STR6")]
        public string RsrvStr6 { get; set; }

        [Column("RSRV_STR7")]
        public string RsrvStr7 { get; set; }

        [Column("RSRV_STR8")]
        public string RsrvStr8 { get; set; }

        [Column("RSRV_STR9")]
        public string RsrvStr9 { get; set; }

        [Column("RSRV_STR10")]
        public string RsrvStr10 { get; set; }

        [Column("OWN_ORG_ID")]
        public string OwnOrgId { get; set; }

        [Column("CORP_ORG_ID")]
        public string CorpOrgId { get; set; }

        [Column("OWN_CORP_ORG_ID")]
        public string OwnCorpOrgId { get; set; }

        [Column("PAY_TYPE")]
        public string PayType { get; set; }

        [Column("BILL_POST_MODE")]
        public string BillPostMode { get; set; }

        [Column("BILL_POST_ADDR_ID")]
        public string BillPostAddrId { get; set; }

        [Column("VALID_DATE")]
        public DateTime? ValidDate { get; set; }

        [Column("EXPIRE_DATE")]
        public DateTime? ExpireDate { get; set; }

        [Column("PAY_MODE")]
        public string PayMode { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "files2.um_subscriber")]
    [Description("操作员与员工映射信息表")]
    public class CloudBossUmSubscriber
    {
        [Column("SUBSCRIBER_INS_ID", IsPrimaryKey = true)]
        public Int64? SUBSCRIBER_INS_ID { get; set; }

        [Column("BILL_ID")]
        public string BILL_ID { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "params.am_bill_type")]
    [Description("账单类型参数信息表")]
    public class CloudBossAmBillType
    {
        [Column("BILL_ITEM_ID", IsPrimaryKey = true)]
        public string BillItemId { get; set; }

        [Column("BILL_ITEM_NAME")]
        public string BillItemName { get; set; }

        [Column("BILL_ITEM_DESC")]
        public string BillItemDesc { get; set; }

        [Column("BILL_ITEM_KIND")]
        public string BillItemKind { get; set; }

        [Column("WRITEOFF_PRIORITY")]
        public string WriteOffPriority { get; set; }

        [Column("TAX_RATE")]
        public string TaxRate { get; set; }

        [Column("STATE")]
        public string State { get; set; }

        [Column("OP_DATE")]
        public DateTime? OpDate { get; set; }

        [Column("REGION_ID")]
        public string RegionId { get; set; }

        [Column("CORP_ORG_ID")]
        public string CorpOrgId { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "params.am_item_service")]
    [Description("服务类型参数信息表")]
    public class CloudBossAmItemService
    {
        [Column("AM_ITEM_TYPE_ID", IsPrimaryKey = true)]
        public string AmItemTypeId { get; set; }

        [Column("SERVICE_ID")]
        public string ServiceId { get; set; }

        [Column("SORT")]
        public string Sort { get; set; }

        [Column("STS")]
        public string Sts { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CLOUD_BOSS", TableName = "params.bs_business")]
    [Description("业务表")]
    public class CloudBossBsBusiness
    {
        [Column("BUSINESS_TYPE_ID", IsPrimaryKey = true)]
        public string BUSINESS_TYPE_ID { get; set; }

        [Column("BUSINESS_NAME")]
        public string BUSINESS_NAME { get; set; }


    }

    #endregion

    #region    创建的视图

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "V_OP")]
    [Description("操作员信息视图")]
    public class CloudBossVOp
    {
        [Column("OPERATOR_ID", IsPrimaryKey = true)]
        public Int64? OPERATOR_ID { get; set; }

        [Column("CODE")]
        public string CODE { get; set; }

        [Column("PASSWORD")]
        public string PASSWORD { get; set; }

        [Column("LOCK_FLAG")]
        public string LOCK_FLAG { get; set; }

        [Column("STATE")]
        public Int16? STATE { get; set; }

        [Column("STAFF_NAME")]
        public string STAFF_NAME { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "V_OP_ORG")]
    [Description("操作员与机构映射信息视图")]
    public class CloudBossVOpOrg
    {
        [Column("OPERATOR_ID")]
        public Int64? OPERATOR_ID { get; set; }

        [Column("IS_BASE_STATION")]
        public Int16? IS_BASE_STATION { get; set; }

        [Column("STATION_ID")]
        public Int64? STATION_ID { get; set; }

        [Column("NAME")]
        public string NAME { get; set; }

        [Column("ORGANIZE_ID")]
        public Int64? ORGANIZE_ID { get; set; }

        [Column("ORGANIZE_NAME")]
        public string ORGANIZE_NAME { get; set; }

        [Column("CORP_ORG_ID")]
        public Int64? CORP_ORG_ID { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "V_CUST")]
    [Description("客户基本信息视图")]
    public class CloudBossVCust
    {
        [Column("CUST_ID")]
        public string CUST_ID { get; set; }

        [Column("CUST_CODE")]
        public string CUST_CODE { get; set; }

        [Column("CUST_PASSWD")]
        public string CUST_PASSWD { get; set; }

        [Column("CUST_TYPE")]
        public string CUST_TYPE { get; set; }

        [Column("CUST_LEVEL")]
        public string CUST_LEVEL { get; set; }

        [Column("PARTY_ID")]
        public string PARTY_ID { get; set; }

        [Column("CUST_NAME")]
        public string CUST_NAME { get; set; }

        [Column("CUST_CERT_TYPE")]
        public string CUST_CERT_TYPE { get; set; }

        [Column("CUST_CERT_NO")]
        public string CUST_CERT_NO { get; set; }

        [Column("CUST_PROP")]
        public string CUST_PROP { get; set; }

        [Column("ORGANIZE_NAME")]
        public string ORGANIZE_NAME { get; set; }

        [Column("CUST_STATUS")]
        public string CUST_STATUS { get; set; }

        [Column("CONT_NAME")]
        public string CONT_NAME { get; set; }

        [Column("REMARK")]
        public string REMARK { get; set; }

        [Column("CONT_MOBILE1")]
        public string CONT_MOBILE1 { get; set; }

        [Column("CONT_MOBILE2")]
        public string CONT_MOBILE2 { get; set; }

        [Column("CONT_PHONE1")]
        public string CONT_PHONE1 { get; set; }

        [Column("CONT_PHONE2")]
        public string CONT_PHONE2 { get; set; }

        [Column("VILLAGE_ID")]
        public string VILLAGE_ID { get; set; }

        [Column("STD_ADDR_ID")]
        public string STD_ADDR_ID { get; set; }

        [Column("STD_ADDR_NAME")]
        public string STD_ADDR_NAME { get; set; }

        [Column("REGION_NAME1")]
        public string REGION_NAME1 { get; set; }

        [Column("REGION_NAME2")]
        public string REGION_NAME2 { get; set; }

        [Column("REGION_NAME3")]
        public string REGION_NAME3 { get; set; }

        [Column("LOUDONG")]
        public string LOUDONG { get; set; }

        [Column("DOOR_DESC")]
        public string DOOR_DESC { get; set; }

        [Column("CREATE_ORG_ID")]
        public string CREATE_ORG_ID { get; set; }

        [Column("CREATE_DATE")]
        public DateTime? CREATE_DATE { get; set; }

        [Column("OWN_CORP_ORG_ID")]
        public string OWN_CORP_ORG_ID { get; set; }

        [Column("STAFF_NAME")]
        public string STAFF_NAME { get; set; }

        [Column("OWN_CORP_ORG")]
        public string OWN_CORP_ORG { get; set; }

        [Column("ACCT_ID")]
        public string ACCT_ID { get; set; }

        [Column("PAYMENT_METHOD")]
        public string PAYMENT_METHOD { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "V_SUBSCRIBER")]
    [Description("客户的用户信息视图")]
    public class CloudBossVSubscriber
    {
        [Column("SUBSCRIBER_INS_ID")]
        public Int64? SUBSCRIBER_INS_ID { get; set; }

        [Column("CUST_ID")]
        public Int64? CUST_ID { get; set; }

        [Column("USER_NAME")]
        public string USER_NAME { get; set; }

        [Column("SUBSCRIBER_TYPE")]
        public string SUBSCRIBER_TYPE { get; set; }

        [Column("PROD_LINE_NAME")]
        public string PROD_LINE_NAME { get; set; }

        [Column("BILL_ID")]
        public string BILL_ID { get; set; }

        [Column("SUB_BILL_ID")]
        public string SUB_BILL_ID { get; set; }

        [Column("CREATE_ORG_ID")]
        public Int64? CREATE_ORG_ID { get; set; }

        [Column("CREATE_ORG")]
        public string CREATE_ORG { get; set; }

        [Column("CREATE_DATE")]
        public DateTime? CREATE_DATE { get; set; }

        [Column("OWN_CORP_ORG_ID")]
        public Int64? OWN_CORP_ORG_ID { get; set; }

        [Column("OWN_CORP_ORG")]
        public string OWN_CORP_ORG { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "V_INST_RES")]
    [Description("客户的资源信息视图")]
    public class CloudBossVInstRes
    {
        [Column("SUBSCRIBER_INS_ID")]
        public Int64? SUBSCRIBER_INS_ID { get; set; }

        [Column("PROD_RES_INST_ID")]
        public Int64? PROD_RES_INST_ID { get; set; }

        [Column("CREATE_DATE")]
        public DateTime? CREATE_DATE { get; set; }

        [Column("DONE_DATE")]
        public DateTime? DONE_DATE { get; set; }

        [Column("VALID_DATE")]
        public DateTime? VALID_DATE { get; set; }

        [Column("EXPIRE_DATE")]
        public DateTime? EXPIRE_DATE { get; set; }

        [Column("OP_ID")]
        public Int64? OP_ID { get; set; }

        [Column("ORG_ID")]
        public Int64? ORG_ID { get; set; }

        [Column("CUST_ID")]
        public Int64? CUST_ID { get; set; }

        [Column("RES_NAME")]
        public string RES_NAME { get; set; }

        [Column("RES_EQU_NO")]
        public string RES_EQU_NO { get; set; }
        [Column("RES_EQU_NO")]
        public string RES_EQU_NO2 { get; set; }
        [Column("RES_EQU_NO")]
        public string RES_EQU_NO3 { get; set; }

        [Column("RES_USE_MODE")]
        public int RES_USE_MODE { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "V_CUST_ORD")]
    [Description("未办结订单信息视图")]
    public class CloudBossVCustOrd
    {
        [Column("CUST_ORDER_ID")]
        public Int64? CUST_ORDER_ID { get; set; }

        [Column("CUST_ID")]
        public Int64? CUST_ID { get; set; }

        [Column("CUST_CODE")]
        public string CUST_CODE { get; set; }

        [Column("BUSI_CODE")]
        public string BUSI_CODE { get; set; }

        [Column("BUSI_NAME")]
        public string BUSI_NAME { get; set; }

        [Column("CUST_NAME")]
        public string CUST_NAME { get; set; }

        [Column("ORGANIZE_NAME")]
        public string ORGANIZE_NAME { get; set; }

        [Column("STAFF_NAME")]
        public string STAFF_NAME { get; set; }

        [Column("DEV_NAME")]
        public string DEV_NAME { get; set; }

        [Column("PAY_TYPE")]
        public string PAY_TYPE { get; set; }

        [Column("CREATE_DATE")]
        public DateTime? CREATE_DATE { get; set; }

        [Column("REMARKS")]
        public string REMARKS { get; set; }

        [Column("CANCEL_TAG")]
        public string CANCEL_TAG { get; set; }

        [Column(false)]
        public string ORDER_STATE { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "V_PROD_02")]
    [Description("服务订购信息视图")]
    public class CloudBossVProd02
    {
        [Column("CUST_ID")]
        public Int64? CUST_ID { get; set; }

        [Column("CUST_CODE")]
        public string CUST_CODE { get; set; }

        [Column("SUBSCRIBER_INS_ID")]
        public Int64? SUBSCRIBER_INS_ID { get; set; }

        [Column("OFFER_INS_ID")]
        public Int64? OFFER_INS_ID { get; set; }

        [Column("OFFER_ID")]
        public Int64? OFFER_ID { get; set; }

        [Column("OFFER_NAME")]
        public string OFFER_NAME { get; set; }

        [Column("VALID_DATE")]
        public DateTime? VALID_DATE { get; set; }

        [Column("EXPIRE_DATE")]
        public DateTime? EXPIRE_DATE { get; set; }

        [Column("DONE_DATE")]
        public DateTime? DONE_DATE { get; set; }

        [Column("OP_ID")]
        public Int64? OP_ID { get; set; }

        [Column("ORG_ID")]
        public Int64? ORG_ID { get; set; }

        [Column("CREATE_DATE")]
        public DateTime? CREATE_DATE { get; set; }

        [Column("STAFF_NAME")]
        public string STAFF_NAME { get; set; }

        [Column("ORGANIZE_NAME")]
        public string ORGANIZE_NAME { get; set; }

        [Column("PROD_SPEC_NAME")]
        public string PROD_SPEC_NAME { get; set; }

        [Column("LOGIN_NAME")]
        public string LOGIN_NAME { get; set; }

        [Column("PASSWORD")]
        public string PASSWORD { get; set; }


        [Column("PARENT_OFFER_ID")]
        public string PARENT_OFFER_ID { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "V_PROD_06")]
    [Description("套餐、产品订购信息视图")]
    public class CloudBossVProd06
    {
        [Column("CUST_ID")]
        public Int64? CUST_ID { get; set; }

        [Column("CUST_CODE")]
        public string CUST_CODE { get; set; }

        [Column("SUBSCRIBER_INS_ID")]
        public Int64? SUBSCRIBER_INS_ID { get; set; }

        [Column("OFFER_INS_ID")]
        public Int64? OFFER_INS_ID { get; set; }

        [Column("OFFER_ID")]
        public Int64? OFFER_ID { get; set; }

        [Column("OFFER_NAME")]
        public string OFFER_NAME { get; set; }

        [Column("VALID_DATE")]
        public DateTime? VALID_DATE { get; set; }

        [Column("EXPIRE_DATE")]
        public DateTime? EXPIRE_DATE { get; set; }

        [Column("DONE_DATE")]
        public DateTime? DONE_DATE { get; set; }

        [Column("OP_ID")]
        public Int64? OP_ID { get; set; }

        [Column("ORG_ID")]
        public Int64? ORG_ID { get; set; }

        [Column("CREATE_DATE")]
        public DateTime? CREATE_DATE { get; set; }

        [Column("STAFF_NAME")]
        public string STAFF_NAME { get; set; }

        [Column("ORGANIZE_NAME")]
        public string ORGANIZE_NAME { get; set; }

        [Column("PROD_SPEC_NAME")]
        public string PROD_SPEC_NAME { get; set; }

        [Column("LOGIN_NAME")]
        public string LOGIN_NAME { get; set; }

        [Column("PASSWORD")]
        public string PASSWORD { get; set; }

        [Column("PARENT_OFFER_ID")]
        public string PARENT_OFFER_ID { get; set; }

        [Column("OS_STATUS")]
        public string OS_STATUS { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "V_ORD_PROD")]
    [Description("未办结订单的产品信息视图")]
    public class CloudBossVOrdProd
    {
        [Column("ORDER_ID")]
        public Int64? ORDER_ID { get; set; }

        [Column("BUSI_CODE")]
        public string BUSI_CODE { get; set; }

        [Column("BUSI_NAME")]
        public string BUSI_NAME { get; set; }

        [Column("OFFER_NAME")]
        public string OFFER_NAME { get; set; }

        [Column("RESOURCE_NO")]
        public string RESOURCE_NO { get; set; }

        [Column("CREATE_DATE")]
        public DateTime? CREATE_DATE { get; set; }

        [Column("VALID_DATE")]
        public DateTime? VALID_DATE { get; set; }

        [Column("EXPIRE_DATE")]
        public DateTime? EXPIRE_DATE { get; set; }

        [Column("STATE")]
        public int? STATE { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "V_ORD_RES")]
    [Description("未办结订单的资源信息视图")]
    public class CloudBossVOrdRes
    {
        [Column("RES_CODE")]
        public string RES_CODE { get; set; }

        [Column("RES_EQU_NO")]
        public string RES_EQU_NO { get; set; }

        [Column("STATE")]
        public int? STATE { get; set; }

        [Column("CREATE_DATE")]
        public DateTime? CREATE_DATE { get; set; }

        [Column("DONE_DATE")]
        public DateTime? done_date { get; set; }

        [Column("VALID_DATE")]
        public DateTime? VALID_DATE { get; set; }

        [Column("EXPIRE_DATE")]
        public DateTime? EXPIRE_DATE { get; set; }

        [Column("OP_ID")]
        public Int64? OP_ID { get; set; }

        [Column("ORG_ID")]
        public Int64? ORG_ID { get; set; }

        [Column("REGION_ID")]
        public string REGION_ID { get; set; }

        [Column("CUST_ORDER_ID")]
        public Int64? CUST_ORDER_ID { get; set; }

        [Column("RES_TYPE")]
        public string RES_TYPE { get; set; }

        [Column("RES_NAME")]
        public string RES_NAME { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "V_ORD_FEE")]
    [Description("未办结订单的费用信息视图")]
    public class CloudBossVOrdFee
    {
        [Column("CUST_ORDER_ID")]
        public Int64? CUST_ORDER_ID { get; set; }

        [Column("PRICE_PLAN_NAME")]
        public string PRICE_PLAN_NAME { get; set; }

        [Column("SHOULD_MONEY")]
        public double? SHOULD_MONEY { get; set; }

        [Column("FACT_MONEY")]
        public double? FACT_MONEY { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "V_CUST_CHARGE")]
    [Description("客户充值信息视图")]
    public class CloudBossVCustCharge
    {
        [Column("CUST_ID")]
        public Int64? CUST_ID { get; set; }

        [Column("CUST_CODE")]
        public string CUST_CODE { get; set; }

        [Column("AMOUNT")]
        public double? AMOUNT { get; set; }

        [Column("PAYMENT_DATE")]
        public DateTime? PAYMENT_DATE { get; set; }

        [Column("PAYMENT_METHOD")]
        public Int32? PAYMENT_METHOD { get; set; }

        [Column("OPERATION_TYPE")]
        public string OPERATION_TYPE { get; set; }

        [Column("ORGANIZATION_NAME")]
        public string ORGANIZATION_NAME { get; set; }

        [Column("BANK")]
        public string BANK { get; set; }

        [Column("BANK_NAME")]
        public string BANK_NAME { get; set; }

        [Column("PAYMENT_ID")]
        public Int64? PAYMENT_ID { get; set; }

        [Column("CERTIFIED_TYPE")]
        public string CERTIFIED_TYPE { get; set; }

        [Column("REMARK")]
        public string REMARK { get; set; }

        [Column("STAFF_NAME")]
        public string STAFF_NAME { get; set; }
    }

    #endregion

    #region   分表...

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "jour2.om_order_{0}")]
    [Description("历史订单基本信息表")]
    public class CloudBossOmOrderHistory
    {
        [Column("ORDER_ID", IsPrimaryKey = true)]
        [ForeignType(typeof(CloudBossOmCustomerHis))]
        public Int64? ORDER_ID { get; set; }

        [Column("BUSI_CODE")]
        [ForeignType(typeof(CloudBossBsStaticData), FilterExpression = "CODE_TYPE = 'BUSI_CODE'")]
        public string BUSI_CODE { get; set; }

        [Column("PARTY_NAME")]
        public string PARTY_NAME { get; set; }

        [Column("ORG_ID")]
        [ForeignType(typeof(CloudBossSecOrganize))]
        public string ORG_ID { get; set; }

        [Column("OP_ID")]
        [ForeignType(typeof(CloudBossVOp), Alias = "OP")]
        public string OP_ID { get; set; }

        [Column("DEV_ID")]
        [ForeignType(typeof(CloudBossVOp), Alias = "OP_DEV")]
        public string DEV_ID { get; set; }

        [Column("PAYMENT_MODE")]
        public string PAY_TYPE { get; set; }

        [Column("CREATE_DATE")]
        public DateTime? CREATE_DATE { get; set; }

        [Column("REMARKS")]
        public string REMARKS { get; set; }

        [Column("CANCEL_TAG")]
        public string CANCEL_TAG { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "jour2.om_customer_{0}")]
    [Description("历史订单客户信息表")]
    public class CloudBossOmCustomerHis
    {
        [Column("ORDER_ID", IsPrimaryKey = true)]
        public Int64? ORDER_ID { get; set; }

        [Column("CUST_ID")]
        public Int64? CUST_ID { get; set; }

        [Column("CUST_CODE")]
        public string CUST_CODE { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "jour2.om_offer_{0}")]
    [Description("历史订单套餐订购信息表")]
    public class CloudBossOmOfferHis
    {
        [Column("ORDER_ID", IsPrimaryKey = true)]
        [ForeignType(typeof(CloudBossOmOrderHistory))]
        public Int64? ORDER_ID { get; set; }

        [Column("SUBSCRIBER_INS_ID")]
        [ForeignType(typeof(CloudBossUmSubscriber))]
        public Int64? SUBSCRIBER_INS_ID { get; set; }

        [Column("CREATE_DATE")]
        public DateTime? CREATE_DATE { get; set; }

        [Column("VALID_DATE")]
        public DateTime? VALID_DATE { get; set; }

        [Column("EXPIRE_DATE")]
        public DateTime? EXPIRE_DATE { get; set; }

        [Column("ACTION")]
        public Int16? ACTION { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "jour2.om_res_{0}")]
    [Description("历史订单资源信息表")]
    public class CloudBossOmResHis
    {
        [Column("ORDER_ID")]
        [ForeignType(typeof(CloudBossOmOrderHistory))]
        public Int64? CUST_ORDER_ID { get; set; }

        [Column("RES_CODE")]
        public string RES_CODE { get; set; }

        [Column("RES_EQU_NO")]
        public string RES_EQU_NO { get; set; }

        [Column("ACTION")]
        public string ACTION { get; set; }

        [Column("CREATE_DATE")]
        public DateTime? CREATE_DATE { get; set; }

        [Column("DONE_DATE")]
        public DateTime? DONE_DATE { get; set; }

        [Column("VALID_DATE")]
        public DateTime? VALID_DATE { get; set; }

        [Column("EXPIRE_DATE")]
        public DateTime? EXPIRE_DATE { get; set; }

        [Column("OP_ID")]
        public Int64? OP_ID { get; set; }

        [Column("ORG_ID")]
        public Int64? ORG_ID { get; set; }

        [Column("REGION_ID")]
        public string REGION_ID { get; set; }

        [Column("RES_SUB_TYPE_ID")]
        public string RES_TYPE { get; set; }

        [Column("RES_SUB_TYPE_NAME")]
        public string RES_NAME { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "jour2.om_fee_{0}")]
    [Description("历史订单费用信息表")]
    public class CloudBossOmFeeHis
    {
        [Column("ORDER_ID")]
        [ForeignType(typeof(CloudBossOmOrderHistory))]
        public Int64? CUST_ORDER_ID { get; set; }

        [Column("FEE_TYPE_CODE_NAME")]
        public string PRICE_PLAN_NAME { get; set; }

        [Column("OLDFEE")]
        public double? OLD_FEE { get; set; }

        [Column("FEE")]
        public double? FEE { get; set; }
    }

    [Serializable]
    [Table(DataSource = "CloudBoss", TableName = "ac2.am_bill_ar_{0}")]
    [Description("历史订单费用信息表")]
    public class CloudBossAmBillArHis
    {
        [Column("BUSINESS_ID")]
        public string BUSINESS_ID { get; set; }

        [Column("PARTITION_ID")]
        public string PARTITION_ID { get; set; }

        [Column("ACCT_ID")]
        public Int64? ACCT_ID { get; set; }

        [Column("CUST_ID", IsPrimaryKey = true)]
        [ForeignType(typeof(CloudBossCmCustomer))]
        public string CUST_ID { get; set; }

        [Column("SUBSCRIBER_ID")]
        public Int64? SUBSCRIBER_ID { get; set; }

        [Column("ACCESS_NO")]
        public string AccessNo { get; set; }

        [Column("CHANNEL_ID")]
        public string ChannelId { get; set; }

        [Column("BUSINESS_TYPE")]
        public string BusinessType { get; set; }

        [Column("BUSINESS_TYPE_ID")]
        [ForeignType(typeof(CloudBossBsBusiness))]
        public string BUSINESS_TYPE_ID { get; set; }

        [Column("PAYMENT_MODE_ID")]
        public string PAYMENT_MODE_ID { get; set; }

        [Column("SPE_PAYMENT_FLAG")]
        public string SPE_PAYMENT_FLAG { get; set; }

        [Column("SPE_PAYMENT_ID")]
        public string SPE_PAYMENT_ID { get; set; }

        [Column("ASSET_ITEM_ID")]
        public string ASSET_ITEM_ID { get; set; }

        [Column("AMOUNT")]
        public string AMOUNT { get; set; }

        [Column("VALID_FLAG")]
        public string VALID_FLAG { get; set; }

        [Column("RELA_BUSINESS_ID")]
        public string RELA_BUSINESS_ID { get; set; }

        [Column("RELA_ACCT_ID")]
        public string RELA_ACCT_ID { get; set; }

        [Column("SPAN_REGION_FLAG")]
        public string SPAN_REGION_FLAG { get; set; }

        [Column("PEER_OPER_DATE")]
        public DateTime? PEER_OPER_DATE { get; set; }

        [Column("INPUT_MODE")]
        public string INPUT_MODE { get; set; }

        [Column("INPUT_NO")]
        public string INPUT_NO { get; set; }

        [Column("OFFER_ID")]
        public string OFFER_ID { get; set; }

        [Column("OFFER_INS_ID")]
        public string OFFER_INS_ID { get; set; }

        [Column("PRICE_PLAN_CODE")]
        public string PRICE_PLAN_CODE { get; set; }

        [Column("PRICE_INS_ID")]
        public string PRICE_INS_ID { get; set; }

        [Column("REMARK")]
        public string REMARK { get; set; }

        [Column("CREATE_DATE")]
        public DateTime? CREATE_DATE { get; set; }

        [Column("TRADE_DATE")]
        public DateTime? TRADE_DATE { get; set; }

        [Column("TRADE_OP_ID")]
        public string TRADE_OP_ID { get; set; }

        [Column("TRADE_ORG_ID")]
        public string TRADE_ORG_ID { get; set; }

        [Column("TRADE_COUNTY_CODE")]
        public string TRADE_COUNTY_CODE { get; set; }

        [Column("TRADE_REGION_ID")]
        public string TRADE_REGION_ID { get; set; }

        [Column("CANCEL_FLAG")]
        public string CANCEL_FLAG { get; set; }

        [Column("CANCEL_DATE")]
        public DateTime? CANCEL_DATE { get; set; }

        [Column("CANCEL_OP_ID")]
        public string CANCEL_OP_ID { get; set; }

        [Column("CANCEL_ORG_ID")]
        public string CANCEL_ORG_ID { get; set; }

        [Column("CANCEL_COUNTY_CODE")]
        public string CANCEL_COUNTY_CODE { get; set; }

        [Column("CANCEL_REGION_ID")]
        public string CANCEL_REGION_ID { get; set; }

        [Column("CANCEL_BUSINESS_ID")]
        public string CANCEL_BUSINESS_ID { get; set; }

        [Column("WRITEOFF_MODE")]
        public string WRITEOFF_MODE { get; set; }

        [Column("SPE_BILL_MONTH")]
        public string SPE_BILL_MONTH { get; set; }

        [Column("RECOVER_FLAG")]
        public string RECOVER_FLAG { get; set; }

        [Column("OLD_BALANCE")]
        public string OLD_BALANCE { get; set; }

        [Column("NEW_BALANCE")]
        public string NEW_BALANCE { get; set; }

        [Column("REGION_ID")]
        public string REGION_ID { get; set; }

        [Column("CORP_ORG_ID")]
        public string CORP_ORG_ID { get; set; }

        [Column("TRADE_CORP_ORG_ID")]
        public string TRADE_CORP_ORG_ID { get; set; }

        [Column("CANCEL_CORP_ORG_ID")]
        public string CANCEL_CORP_ORG_ID { get; set; }
    }

    #endregion

    #region   构建视图类


    [Serializable]
    [Description("历史订单信息视图")]
    public class CloudBossVCustOrdHis : CloudBossOmOrderHistory
    {
        [ForeignColumn(typeof(CloudBossOmCustomerHis), Property = "CUST_ID")]
        public Int64? CUST_ID { get; set; }

        [ForeignColumn(typeof(CloudBossOmCustomerHis), Property = "CUST_CODE")]
        public string CUST_CODE { get; set; }

        [ForeignColumn(typeof(CloudBossBsStaticData), Property = "CODE_NAME")]
        public string BUSI_NAME { get; set; }

        [ForeignColumn(typeof(CloudBossSecOrganize), Property = "ORGANIZE_NAME")]
        public string ORGANIZE_NAME { get; set; }

        [ForeignColumn("OP", Property = "STAFF_NAME")]
        public string STAFF_NAME { get; set; }

        [ForeignColumn("OP_DEV", Property = "STAFF_NAME")]
        public string DEV_NAME { get; set; }

        [Column(false)]
        public string ORDER_STATE { get; set; }
    }

    [Serializable]
    [Description("历史订单的产品信息视图")]
    public class CloudBossVOrdProdHis : CloudBossOmOfferHis
    {
        [ForeignColumn(typeof(CloudBossOmOrderHistory), Property = "BUSI_CODE")]
        public string BUSI_CODE { get; set; }

        [ForeignColumn(typeof(CloudBossBsStaticData), Property = "CODE_NAME")]
        public string BUSI_NAME { get; set; }

        [ForeignColumn(typeof(CloudBossUmSubscriber), Property = "BILL_ID")]
        public string RESOURCE_NO { get; set; }
    }

    [Serializable]
    [Description("历史订单的资源信息视图")]
    public class CloudBossVOrdResHis : CloudBossOmResHis
    {
    }

    [Serializable]
    [Description("历史订单的费用信息视图")]
    public class CloudBossVOrdFeeHis : CloudBossOmFeeHis
    {
    }


    [Serializable]
    [Description("消费账单信息视图")]
    public class CloudBossVCustBillHis : CloudBossAmBillArHis
    {
        [ForeignColumn(typeof(CloudBossCmCustomer), Property = "CUST_CODE")]
        public Int64? CUST_CODE { get; set; }


        [ForeignColumn(typeof(CloudBossBsBusiness))]
        public string BUSINESS_NAME { get; set; }
    }

    #endregion
}
