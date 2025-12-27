using MyOrm.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MyOrm.Test
{
    [Table("sz.UP_Product_Catagory", DataSource = "CloudBoss")]
    [Serializable]
    [DisplayName("产品信息")]
    public class BossProduct : EntityBase
    {

        [Column("PRODUCT_ITEM_ID", IsPrimaryKey = true), DisplayName("产品ID")]
        public new Int64 ID { get; set; }
        [DisplayName("产品名称")]
        public string Name { get; set; }
        [DisplayName("参数1")]
        public string PARAM1 { get; set; }
        [DisplayName("参数2")]
        public string PARAM2 { get; set; }
        [DisplayName("参数3")]
        public string PARAM3 { get; set; }
        [DisplayName("是否真实产品")]
        [Browsable(false)]
        public bool? IS_PROD { get; set; }
        [DisplayName("是否主产品")]
        [Browsable(false)]
        public bool? IS_MAIN { get; set; }
        [DisplayName("是否宽带产品")]
        [Browsable(false)]
        public bool? IS_KD { get; set; }
        [DisplayName("互动类别")]
        public HD_TYPE_ENUM? IS_HD { get; set; }
        [DisplayName("创建时间")]
        [Browsable(false)]
        public DateTime? Create_Date { get; set; }
        [DisplayName("价格")]
        public float? PRICE { get; set; }
        [DisplayName("提成金额")]
        [Browsable(false)]
        public float? Bonus { get; set; }
        [DisplayName("宽带类型")]
        public KD_TYPE_ENUM? KD_Type { get; set; }
        [DisplayName("月份数")]
        [Browsable(false)]
        public int? EXP_Month { get; set; }
        [DisplayName("时长类型")]
        [Browsable(false)]
        public EXP_ENUM? IS_Exp { get; set; }
        //CATAGORY    VARCHAR2(40)    产品类别，固定SERVICE_PRICE
        //BONUS   NUMBER 提成金额
        //PARAM1 VARCHAR2(255)   预留参数1
        //PARAM2  VARCHAR2(255)   预留参数2
        //PARAM3  VARCHAR2(255)   预留参数3
        //IS_PROD INTEGER 是否实际产品
        //IS_KD INTEGER 是否宽带产品
        //IS_HD   INTEGER 是否互动产品
        //IS_MAIN INTEGER 是否主产品
        //IS_EXP  INTEGER 是否体验产品，0:非体验产品;1:体验到期转订购；2:包年或其他一次性出账产品；3:体验到期退订
        //EXP_MONTH   INTEGER 体验或一次性出账的月份数
        //KD_TYPE INTEGER 宽带类型，0:非宽带；1:有线宽带；2:互动宽带；3:移动宽带
        //IS_FREE INTEGER 是否免费产品
        //PRICE NUMBER  产品价格（分阶段不同价格的以最高的单价为准）
        //IS_FF INTEGER 是否付费产品
        //PROD_SERVICE_ID INTEGER 业务ID
        //EXP_TYPE NUMBER  体验或包年截止日期的方式，1:按实际天计算；2:截止到月底
        //CREATE_DATE DATE
        //IS_CF   INTEGER 是否需要催费

    }

    public enum KD_TYPE_ENUM
    {
        非宽带 = 0,
        有线宽带 = 1,
        互动宽带 = 2,
        移动宽带 = 3
    }
    public enum EXP_ENUM
    {
        非体验产品 = 0,
        体验到期转订购 = 1,
        包年或一次性出账产品 = 2,
        体验到期退订 = 3
    }

    public enum HD_TYPE_ENUM
    {
        非互动 = 0,
        互动基本 = 1,
        互动增值 = 2
    }
}
